using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace WinUpdatePauser.Services
{
    /// <summary>
    /// Windows Update 暂停设置的注册表操作模块。
    /// 所有注册表读写集中在此类，UI 层不直接接触注册表。
    ///
    /// 新版日历配置使用三个 REG_SZ 值；旧版配置使用 FlightSettingsMaxPauseDays REG_DWORD。
    /// 每一个会改变注册表的公开操作都会先将相关值备份到当前备份目录。
    /// </summary>
    public static class PauseRegistryService
    {
        private const string SettingsKeyPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        private const string BackupLocationFileName = "backup-location.txt";
        private const string DefaultBackupFolderName = "backup";

        public const string PauseFeatureUpdatesEndTime = "PauseFeatureUpdatesEndTime";
        public const string PauseQualityUpdatesEndTime = "PauseQualityUpdatesEndTime";
        public const string PauseUpdatesExpiryTime = "PauseUpdatesExpiryTime";
        public const string FlightSettingsMaxPauseDays = "FlightSettingsMaxPauseDays";

        private static readonly string[] ManagedValueNames =
        {
            PauseFeatureUpdatesEndTime,
            PauseQualityUpdatesEndTime,
            PauseUpdatesExpiryTime,
            FlightSettingsMaxPauseDays
        };

        private static readonly object BackupDirectoryLock = new object();
        private static string _backupDirectory;

        /// <summary>新版暂停值是否已经由 Windows 设置生成。</summary>
        public static bool IsInitialized()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null)
                {
                    return false;
                }

                foreach (string name in new[]
                {
                    PauseFeatureUpdatesEndTime,
                    PauseQualityUpdatesEndTime,
                    PauseUpdatesExpiryTime
                })
                {
                    if (!HasValue(key, name))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>读取新版暂停结束时间（原始 ISO 8601 字符串）。</summary>
        public static string ReadRawEndTime()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null || !HasValue(key, PauseUpdatesExpiryTime))
                {
                    return null;
                }

                return key.GetValue(PauseUpdatesExpiryTime, null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            }
        }

        /// <summary>读取旧版暂停天数；值不存在或类型无法转换时返回 null。</summary>
        public static int? ReadLegacyPauseDays()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null || !HasValue(key, FlightSettingsMaxPauseDays))
                {
                    return null;
                }

                object value = key.GetValue(FlightSettingsMaxPauseDays, null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 将 ISO 8601 UTC 字符串同时写入三个 REG_SZ 值。
        /// 写入前会自动生成“应用新版配置前的配置_时间”备份。
        /// </summary>
        public static BackupListItem WriteEndTime(string isoUtc)
        {
            if (string.IsNullOrWhiteSpace(isoUtc))
            {
                throw new ArgumentException("暂停结束时间不能为空。", nameof(isoUtc));
            }

            BackupListItem backup = CreateBackup("应用新版配置");
            using (RegistryKey key = OpenSettingsKey(true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(
                        "注册表键不存在，请先在 Windows 设置中暂停一次更新。备份已保存：" + backup.DisplayName);
                }

                foreach (string name in new[]
                {
                    PauseFeatureUpdatesEndTime,
                    PauseQualityUpdatesEndTime,
                    PauseUpdatesExpiryTime
                })
                {
                    key.SetValue(name, isoUtc, RegistryValueKind.String);
                }
            }

            return backup;
        }

        /// <summary>
        /// 写入旧版暂停天数。与 reg add 命令一致：键不存在时创建键。
        /// 写入前会自动生成“应用旧版配置前的配置_时间”备份。
        /// </summary>
        public static BackupListItem WriteLegacyPauseDays(int days)
        {
            if (days < 1 || days > 36500)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "暂停天数应为 1 到 36500。 ");
            }

            BackupListItem backup = CreateBackup("应用旧版配置");
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = baseKey.CreateSubKey(SettingsKeyPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开 Windows Update 注册表设置键。备份已保存：" + backup.DisplayName);
                }

                key.SetValue(FlightSettingsMaxPauseDays, days, RegistryValueKind.DWord);
            }

            return backup;
        }

        /// <summary>
        /// 删除本工具管理的暂停值，恢复到系统继续更新状态。
        /// 删除前会自动生成“恢复正常更新前的配置_时间”备份。
        /// </summary>
        public static BackupListItem ResumeNormalUpdates()
        {
            BackupListItem backup = CreateBackup("恢复正常更新");
            using (RegistryKey key = OpenSettingsKey(true))
            {
                if (key == null)
                {
                    return backup;
                }

                foreach (string name in ManagedValueNames)
                {
                    key.DeleteValue(name, false);
                }
            }

            return backup;
        }

        /// <summary>
        /// 将选定备份恢复到注册表。恢复动作前先备份当前状态，避免覆盖当前配置后无法回退。
        /// </summary>
        public static BackupListItem RestoreBackup(string jsonPath)
        {
            string safePath = ValidateBackupPath(jsonPath);
            BackupDocument document = ReadBackupDocument(safePath);
            BackupListItem beforeRestore = CreateBackup("恢复备份");
            ApplyBackupDocument(document);
            return beforeRestore;
        }

        /// <summary>创建当前注册表相关值的备份。operation 会被格式化为“operation前的配置_时间”。</summary>
        public static BackupListItem CreateBackup(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                operation = "注册表修改";
            }

            string directory = GetBackupDirectory();
            Directory.CreateDirectory(directory);

            DateTime now = DateTime.Now;
            string displayName = BuildBackupDisplayName(operation, now);
            string basePath = GetUniqueBasePath(directory, displayName);
            string jsonPath = basePath + ".json";
            string regPath = basePath + ".reg";

            BackupDocument document = CaptureCurrentState(operation, now);
            try
            {
                WriteJsonDocument(document, jsonPath);
                WriteRegDocument(document, regPath);
            }
            catch
            {
                TryDeleteFile(jsonPath);
                TryDeleteFile(regPath);
                throw;
            }

            return new BackupListItem(jsonPath, displayName, now);
        }

        /// <summary>列出当前备份目录下的 JSON 备份，不触碰既有备份内容。</summary>
        public static IList<BackupListItem> GetBackups()
        {
            string directory = GetBackupDirectory();
            var result = new List<BackupListItem>();
            if (!Directory.Exists(directory))
            {
                return result;
            }

            foreach (string jsonPath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    FileInfo info = new FileInfo(jsonPath);
                    result.Add(new BackupListItem(
                        jsonPath,
                        Path.GetFileNameWithoutExtension(jsonPath),
                        info.LastWriteTime));
                }
                catch (IOException)
                {
                    // 文件在枚举过程中消失时忽略该项，避免备份管理页崩溃。
                }
                catch (UnauthorizedAccessException)
                {
                    // 同上，单个无法读取的项不影响其它备份显示。
                }
            }

            result.Sort((left, right) => right.CreatedAt.CompareTo(left.CreatedAt));
            return result;
        }

        /// <summary>在当前备份目录内重命名 JSON 与对应 REG 文件。</summary>
        public static void RenameBackup(string jsonPath, string newName)
        {
            string oldJsonPath = ValidateBackupPath(jsonPath);
            string safeName = SanitizeBackupName(newName);
            string directory = GetBackupDirectory();
            string newJsonPath = Path.Combine(directory, safeName + ".json");
            string oldRegPath = Path.ChangeExtension(oldJsonPath, ".reg");
            string newRegPath = Path.ChangeExtension(newJsonPath, ".reg");

            if (string.Equals(oldJsonPath, newJsonPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(newJsonPath) || File.Exists(newRegPath))
            {
                throw new IOException("备份名称已存在，请换一个名称。 ");
            }

            File.Move(oldJsonPath, newJsonPath);
            try
            {
                if (File.Exists(oldRegPath))
                {
                    File.Move(oldRegPath, newRegPath);
                }
            }
            catch
            {
                TryMoveFile(newJsonPath, oldJsonPath);
                throw;
            }
        }

        /// <summary>删除当前备份目录内选定的 JSON 与对应 REG 文件。</summary>
        public static void DeleteBackup(string jsonPath)
        {
            string safeJsonPath = ValidateBackupPath(jsonPath);
            string regPath = Path.ChangeExtension(safeJsonPath, ".reg");
            File.Delete(safeJsonPath);
            if (File.Exists(regPath))
            {
                File.Delete(regPath);
            }
        }

        /// <summary>当前备份目录；默认是 EXE 同级的相对 backup 目录，可由用户设置绝对路径。</summary>
        public static string GetBackupDirectory()
        {
            lock (BackupDirectoryLock)
            {
                if (!string.IsNullOrEmpty(_backupDirectory))
                {
                    return _backupDirectory;
                }

                string configured = ReadConfiguredBackupDirectory();
                _backupDirectory = string.IsNullOrEmpty(configured)
                    ? GetDefaultBackupDirectory()
                    : configured;
                return _backupDirectory;
            }
        }

        /// <summary>设置并持久化自定义备份目录。</summary>
        public static string SetBackupDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("备份路径不能为空。", nameof(directory));
            }

            string fullPath = Path.GetFullPath(directory.Trim());
            Directory.CreateDirectory(fullPath);
            File.WriteAllText(GetBackupLocationFilePath(), fullPath, new UTF8Encoding(false));
            lock (BackupDirectoryLock)
            {
                _backupDirectory = fullPath;
            }

            return fullPath;
        }

        /// <summary>清除自定义路径，恢复为 EXE 同级的 backup 目录。</summary>
        public static string ResetBackupDirectory()
        {
            string locationFile = GetBackupLocationFilePath();
            if (File.Exists(locationFile))
            {
                File.Delete(locationFile);
            }

            string defaultPath = GetDefaultBackupDirectory();
            lock (BackupDirectoryLock)
            {
                _backupDirectory = defaultPath;
            }

            return defaultPath;
        }

        private static RegistryKey OpenSettingsKey(bool writable)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                return baseKey.OpenSubKey(SettingsKeyPath, writable);
            }
        }

        private static bool HasValue(RegistryKey key, string name)
        {
            foreach (string existingName in key.GetValueNames())
            {
                if (string.Equals(existingName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static BackupDocument CaptureCurrentState(string operation, DateTime createdAt)
        {
            var document = new BackupDocument
            {
                Operation = operation,
                CreatedAt = createdAt,
                RegistryPath = @"HKEY_LOCAL_MACHINE\" + SettingsKeyPath,
                Values = new List<RegistryValueBackup>()
            };

            using (RegistryKey key = OpenSettingsKey(false))
            {
                document.KeyExisted = key != null;
                foreach (string name in ManagedValueNames)
                {
                    var valueBackup = new RegistryValueBackup
                    {
                        Name = name,
                        Exists = key != null && HasValue(key, name)
                    };

                    if (valueBackup.Exists)
                    {
                        RegistryValueKind kind = key.GetValueKind(name);
                        valueBackup.Kind = kind.ToString();
                        object value = key.GetValue(name, null,
                            RegistryValueOptions.DoNotExpandEnvironmentNames);
                        valueBackup.Data = SerializeValue(value, kind);
                    }

                    document.Values.Add(valueBackup);
                }
            }

            return document;
        }

        private static void ApplyBackupDocument(BackupDocument document)
        {
            if (document == null || document.Values == null)
            {
                throw new InvalidDataException("备份文件缺少有效的注册表数据。 ");
            }

            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = document.KeyExisted
                ? baseKey.CreateSubKey(SettingsKeyPath, true)
                : baseKey.OpenSubKey(SettingsKeyPath, true))
            {
                if (key == null)
                {
                    // 原始状态没有设置键，当前也没有设置键：无需创建任何东西。
                    return;
                }

                foreach (RegistryValueBackup valueBackup in document.Values)
                {
                    if (valueBackup == null || !IsManagedValueName(valueBackup.Name))
                    {
                        continue;
                    }

                    if (!valueBackup.Exists)
                    {
                        key.DeleteValue(valueBackup.Name, false);
                        continue;
                    }

                    RegistryValueKind kind;
                    if (!Enum.TryParse(valueBackup.Kind, true, out kind))
                    {
                        throw new InvalidDataException("备份中的注册表类型无法识别：" + valueBackup.Kind);
                    }

                    key.SetValue(valueBackup.Name, DeserializeValue(valueBackup.Data, kind), kind);
                }
            }
        }

        private static bool IsManagedValueName(string name)
        {
            foreach (string managedName in ManagedValueNames)
            {
                if (string.Equals(managedName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SerializeValue(object value, RegistryValueKind kind)
        {
            if (value == null)
            {
                return string.Empty;
            }

            switch (kind)
            {
                case RegistryValueKind.Binary:
                    return Convert.ToBase64String((byte[])value);
                case RegistryValueKind.MultiString:
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("\0", (string[])value)));
                case RegistryValueKind.DWord:
                    return Convert.ToUInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case RegistryValueKind.QWord:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        private static object DeserializeValue(string data, RegistryValueKind kind)
        {
            data = data ?? string.Empty;
            switch (kind)
            {
                case RegistryValueKind.Binary:
                    return Convert.FromBase64String(data);
                case RegistryValueKind.MultiString:
                    return Encoding.UTF8.GetString(Convert.FromBase64String(data)).Split(new[] { '\0' }, StringSplitOptions.None);
                case RegistryValueKind.DWord:
                    return checked((int)uint.Parse(data, CultureInfo.InvariantCulture));
                case RegistryValueKind.QWord:
                    return checked((long)ulong.Parse(data, CultureInfo.InvariantCulture));
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    return data;
                default:
                    return data;
            }
        }

        private static void WriteJsonDocument(BackupDocument document, string path)
        {
            var serializer = new DataContractJsonSerializer(typeof(BackupDocument));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                serializer.WriteObject(stream, document);
            }
        }

        private static BackupDocument ReadBackupDocument(string path)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(BackupDocument));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return (BackupDocument)serializer.ReadObject(stream);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SerializationException || ex is SecurityException)
            {
                throw new InvalidDataException("无法读取备份文件：" + Path.GetFileName(path), ex);
            }
        }

        private static void WriteRegDocument(BackupDocument document, string path)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Windows Registry Editor Version 5.00");
            builder.AppendLine();
            builder.AppendLine("[" + document.RegistryPath + "]");

            foreach (RegistryValueBackup valueBackup in document.Values)
            {
                if (valueBackup == null || !valueBackup.Exists)
                {
                    continue;
                }

                RegistryValueKind kind;
                if (!Enum.TryParse(valueBackup.Kind, true, out kind))
                {
                    continue;
                }

                builder.Append('"').Append(valueBackup.Name).Append("\"=");
                switch (kind)
                {
                    case RegistryValueKind.DWord:
                        builder.Append("dword:").Append(uint.Parse(valueBackup.Data, CultureInfo.InvariantCulture).ToString("x8", CultureInfo.InvariantCulture));
                        break;
                    case RegistryValueKind.Binary:
                        builder.Append("hex:").Append(BytesToHex(Convert.FromBase64String(valueBackup.Data)));
                        break;
                    case RegistryValueKind.MultiString:
                        builder.Append("hex(7):").Append(BytesToHex(Convert.FromBase64String(valueBackup.Data)));
                        break;
                    default:
                        builder.Append('"').Append(EscapeRegString(valueBackup.Data)).Append('"');
                        break;
                }

                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), Encoding.Unicode);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string EscapeRegString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string BuildBackupDisplayName(string operation, DateTime createdAt)
        {
            string cleanOperation = SanitizeBackupName(operation);
            if (!cleanOperation.EndsWith("前的配置", StringComparison.Ordinal))
            {
                cleanOperation += "前的配置";
            }

            return cleanOperation + "_" + createdAt.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        }

        private static string GetUniqueBasePath(string directory, string displayName)
        {
            string basePath = Path.Combine(directory, displayName);
            int suffix = 1;
            while (File.Exists(basePath + ".json") || File.Exists(basePath + ".reg"))
            {
                basePath = Path.Combine(directory, displayName + "_" + suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }

            return basePath;
        }

        private static string SanitizeBackupName(string name)
        {
            string cleanName = (name ?? string.Empty).Trim();
            if (cleanName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = Path.GetFileNameWithoutExtension(cleanName);
            }

            if (string.IsNullOrWhiteSpace(cleanName) || cleanName == "." || cleanName == "..")
            {
                throw new ArgumentException("备份名称不能为空。", nameof(name));
            }

            if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || cleanName.Contains("/")
                || cleanName.Contains("\\"))
            {
                throw new ArgumentException("备份名称包含无效字符。", nameof(name));
            }

            return cleanName;
        }

        private static string ValidateBackupPath(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                throw new ArgumentException("未选择备份文件。", nameof(jsonPath));
            }

            string fullPath = Path.GetFullPath(jsonPath);
            string root = Path.GetFullPath(GetBackupDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("只能操作当前备份目录中的 JSON 备份。 ");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("备份文件不存在。", fullPath);
            }

            return fullPath;
        }

        private static string GetDefaultBackupDirectory()
        {
            return Path.Combine(GetExecutableDirectory(), DefaultBackupFolderName);
        }

        private static string GetBackupLocationFilePath()
        {
            return Path.Combine(GetExecutableDirectory(), BackupLocationFileName);
        }

        private static string GetExecutableDirectory()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(location);
            return string.IsNullOrEmpty(directory) ? AppDomain.CurrentDomain.BaseDirectory : directory;
        }

        private static string ReadConfiguredBackupDirectory()
        {
            string path = GetBackupLocationFilePath();
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string configured = File.ReadAllText(path, Encoding.UTF8).Trim();
                return string.IsNullOrEmpty(configured) ? null : Path.GetFullPath(configured);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return null;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 清理失败不覆盖原始异常。
            }
        }

        private static void TryMoveFile(string source, string destination)
        {
            try
            {
                if (File.Exists(source) && !File.Exists(destination))
                {
                    File.Move(source, destination);
                }
            }
            catch
            {
                // 回滚失败时保留当前文件，调用方仍会看到原始异常。
            }
        }
    }

    [DataContract]
    internal sealed class BackupDocument
    {
        [DataMember(Order = 1)]
        public string Operation { get; set; }

        [DataMember(Order = 2)]
        public DateTime CreatedAt { get; set; }

        [DataMember(Order = 3)]
        public string RegistryPath { get; set; }

        [DataMember(Order = 4)]
        public bool KeyExisted { get; set; }

        [DataMember(Order = 5)]
        public List<RegistryValueBackup> Values { get; set; }
    }

    [DataContract]
    internal sealed class RegistryValueBackup
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public bool Exists { get; set; }

        [DataMember(Order = 3)]
        public string Kind { get; set; }

        [DataMember(Order = 4)]
        public string Data { get; set; }
    }

    /// <summary>供备份管理界面使用的备份项；路径仅由服务生成并校验。</summary>
    public sealed class BackupListItem
    {
        public BackupListItem(string jsonPath, string displayName, DateTime createdAt)
        {
            JsonPath = jsonPath;
            DisplayName = displayName;
            CreatedAt = createdAt;
        }

        public string JsonPath { get; private set; }
        public string DisplayName { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public override string ToString()
        {
            return DisplayName + "  （" + CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") + "）";
        }
    }
}
