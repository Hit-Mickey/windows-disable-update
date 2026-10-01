using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WinUpdatePauser.Services
{
    public enum GuardMode
    {
        Modern,
        Legacy
    }

    /// <summary>安装、配置和控制新版日期与旧版天数守护服务。</summary>
    public static class UpdateGuardManager
    {
        private const string ModernServiceName = "WUPauseDateGuard";
        private const string LegacyServiceName = "WUPauseDaysGuard";
        private const string ObsoleteServiceName = "WUPauseGuard";
        private const string ConfigRootPath = @"SOFTWARE\WUPause\Guard";
        private const string EnabledValueName = "Enabled";
        private const string TargetUtcValueName = "TargetUtc";
        private const string TargetDaysValueName = "TargetDays";

        private static string InstallDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WUPause");

        private static string InstalledExePath => Path.Combine(InstallDirectory, "WUPauseGuard.exe");

        internal static string GetServiceName(GuardMode mode)
        {
            return mode == GuardMode.Legacy ? LegacyServiceName : ModernServiceName;
        }

        public static GuardStatus GetStatus(GuardMode mode)
        {
            GuardConfiguration config = ReadConfiguration(mode);
            string serviceName = GetServiceName(mode);
            bool installed = IsServiceInstalled(serviceName);
            if (mode == GuardMode.Modern && !installed && IsServiceInstalled(ObsoleteServiceName))
            {
                serviceName = ObsoleteServiceName;
                installed = true;
                config = ReadObsoleteConfiguration();
            }
            bool running = installed && GetServiceStatus(serviceName) == ServiceControllerStatus.Running;
            return new GuardStatus(installed, running, config.Enabled,
                config.TargetUtc, config.TargetDays);
        }

        public static void InstallAndEnableModern(string targetUtc)
        {
            if (string.IsNullOrWhiteSpace(targetUtc))
            {
                throw new ArgumentException("守护日期不能为空。", nameof(targetUtc));
            }

            DeployExecutable();
            StopServiceIfInstalled(ModernServiceName);
            WriteConfiguration(GuardMode.Modern, true, targetUtc, null);
            EnsureServiceDefinition(GuardMode.Modern);
            StartEnabledServices();
        }

        public static void InstallAndEnableLegacy(int targetDays)
        {
            if (targetDays < 1 || targetDays > 36500)
            {
                throw new ArgumentOutOfRangeException(nameof(targetDays),
                    "守护天数应为 1 到 36500。");
            }

            DeployExecutable();
            StopServiceIfInstalled(LegacyServiceName);
            WriteConfiguration(GuardMode.Legacy, true, null, targetDays);
            EnsureServiceDefinition(GuardMode.Legacy);
            StartEnabledServices();
        }

        public static void DisableAll()
        {
            foreach (GuardMode mode in new[] { GuardMode.Modern, GuardMode.Legacy })
            {
                GuardConfiguration config = ReadConfiguration(mode);
                if (config.Enabled)
                {
                    WriteConfiguration(mode, false, config.TargetUtc, config.TargetDays);
                }
                string serviceName = GetServiceName(mode);
                if (IsServiceInstalled(serviceName))
                {
                    RunSc("config", serviceName, "start=", "demand");
                    StopServiceIfInstalled(serviceName);
                }
            }

            RemoveObsoleteService(false);
        }

        public static void Uninstall(GuardMode mode)
        {
            string serviceName = GetServiceName(mode);
            StopServiceIfInstalled(serviceName);
            if (IsServiceInstalled(serviceName))
            {
                RunSc("delete", serviceName);
                WaitUntilServiceDeleted(serviceName);
            }

            DeleteConfiguration(mode);
            if (mode == GuardMode.Modern)
            {
                RemoveObsoleteService(false);
            }
            if (!IsServiceInstalled(ModernServiceName)
                && !IsServiceInstalled(LegacyServiceName)
                && !IsServiceInstalled(ObsoleteServiceName))
            {
                DeleteInstalledExecutable();
            }
        }

        internal static GuardConfiguration ReadConfiguration(GuardMode mode)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetConfigPath(mode)))
            {
                bool enabled = ReadEnabled(key);
                string targetUtc = key?.GetValue(TargetUtcValueName, null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                int? targetDays = ReadInt32(key?.GetValue(TargetDaysValueName));
                return new GuardConfiguration(enabled, targetUtc, targetDays);
            }
        }

        private static GuardConfiguration ReadObsoleteConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(ConfigRootPath))
            {
                return new GuardConfiguration(
                    ReadEnabled(key),
                    key?.GetValue(TargetUtcValueName, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as string,
                    null);
            }
        }

        private static void DeployExecutable()
        {
            Directory.CreateDirectory(InstallDirectory);
            bool migratedModern = RemoveObsoleteService(true);

            string source = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(InstalledExePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                StopServiceIfInstalled(ModernServiceName);
                StopServiceIfInstalled(LegacyServiceName);
                File.Copy(source, InstalledExePath, true);
            }

            if (migratedModern)
            {
                EnsureServiceDefinition(GuardMode.Modern);
            }
        }

        private static void EnsureServiceDefinition(GuardMode mode)
        {
            string serviceName = GetServiceName(mode);
            string displayName = mode == GuardMode.Legacy
                ? "WUPause 天数守护"
                : "WUPause 日期守护";
            string description = mode == GuardMode.Legacy
                ? "监视并恢复 WUPause 设置的 Windows 更新暂停天数"
                : "监视并恢复 WUPause 设置的 Windows 更新暂停日期";
            string serviceArgument = mode == GuardMode.Legacy ? "legacy" : "modern";
            string binaryCommand = "\"" + InstalledExePath + "\" --service " + serviceArgument;

            if (IsServiceInstalled(serviceName))
            {
                RunSc("config", serviceName, "binPath=", binaryCommand,
                    "start=", "delayed-auto", "DisplayName=", displayName);
            }
            else
            {
                RunSc("create", serviceName, "binPath=", binaryCommand,
                    "start=", "delayed-auto", "DisplayName=", displayName);
            }

            RunSc("description", serviceName, description);
            RunSc("failure", serviceName, "reset=", "86400",
                "actions=", "restart/5000/restart/15000/restart/60000");
        }

        private static void StartEnabledServices()
        {
            foreach (GuardMode mode in new[] { GuardMode.Modern, GuardMode.Legacy })
            {
                string serviceName = GetServiceName(mode);
                if (ReadConfiguration(mode).Enabled && IsServiceInstalled(serviceName))
                {
                    StartService(serviceName);
                }
            }
        }

        private static bool RemoveObsoleteService(bool migrateModern)
        {
            GuardConfiguration obsolete = ReadObsoleteConfiguration();
            GuardConfiguration modern = ReadConfiguration(GuardMode.Modern);
            bool shouldMigrate = migrateModern
                                 && obsolete.Enabled
                                 && !string.IsNullOrWhiteSpace(obsolete.TargetUtc)
                                 && !modern.Enabled;

            StopServiceIfInstalled(ObsoleteServiceName);
            if (IsServiceInstalled(ObsoleteServiceName))
            {
                RunSc("delete", ObsoleteServiceName);
                WaitUntilServiceDeleted(ObsoleteServiceName);
            }

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(ConfigRootPath, true))
            {
                key?.DeleteValue(EnabledValueName, false);
                key?.DeleteValue(TargetUtcValueName, false);
            }

            if (shouldMigrate)
            {
                WriteConfiguration(GuardMode.Modern, true, obsolete.TargetUtc, null);
            }

            return shouldMigrate;
        }

        private static void WriteConfiguration(GuardMode mode, bool enabled,
            string targetUtc, int? targetDays)
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(GetConfigPath(mode), true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法写入守护服务配置。");
                }

                key.SetValue(EnabledValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
                if (targetUtc != null)
                {
                    key.SetValue(TargetUtcValueName, targetUtc, RegistryValueKind.String);
                }
                if (targetDays.HasValue)
                {
                    key.SetValue(TargetDaysValueName, targetDays.Value, RegistryValueKind.DWord);
                }
            }
        }

        private static void DeleteConfiguration(GuardMode mode)
        {
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(ConfigRootPath, true))
            {
                root?.DeleteSubKeyTree(GetConfigSubKey(mode), false);
            }
        }

        private static string GetConfigPath(GuardMode mode)
        {
            return ConfigRootPath + "\\" + GetConfigSubKey(mode);
        }

        private static string GetConfigSubKey(GuardMode mode)
        {
            return mode == GuardMode.Legacy ? "Legacy" : "Modern";
        }

        private static bool ReadEnabled(RegistryKey key)
        {
            int? value = ReadInt32(key?.GetValue(EnabledValueName));
            return value.HasValue && value.Value == 1;
        }

        private static int? ReadInt32(object value)
        {
            if (value == null) return null;
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException
                                        || ex is OverflowException)
            {
                return null;
            }
        }

        private static bool IsServiceInstalled(string serviceName)
        {
            ServiceController[] services = ServiceController.GetServices();
            try
            {
                return services.Any(service => string.Equals(service.ServiceName, serviceName,
                    StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                foreach (ServiceController service in services) service.Dispose();
            }
        }

        private static ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            using (var service = new ServiceController(serviceName)) return service.Status;
        }

        private static void StartService(string serviceName)
        {
            using (var service = new ServiceController(serviceName))
            {
                service.Refresh();
                if (service.Status == ServiceControllerStatus.Running) return;
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            }
        }

        private static void StopServiceIfInstalled(string serviceName)
        {
            if (!IsServiceInstalled(serviceName)) return;
            using (var service = new ServiceController(serviceName))
            {
                service.Refresh();
                if (service.Status == ServiceControllerStatus.Stopped) return;
                if (!service.CanStop)
                {
                    throw new InvalidOperationException("守护服务当前无法停止。");
                }
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }

        private static void WaitUntilServiceDeleted(string serviceName)
        {
            var timeout = Stopwatch.StartNew();
            while (IsServiceInstalled(serviceName) && timeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(250);
            }
        }

        private static void DeleteInstalledExecutable()
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            if (File.Exists(InstalledExePath))
            {
                if (string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(InstalledExePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!MoveFileEx(InstalledExePath, null, MoveFileDelayUntilReboot))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(),
                            "无法安排删除守护程序。");
                    }
                }
                else
                {
                    File.Delete(InstalledExePath);
                }
            }

            if (Directory.Exists(InstallDirectory)
                && !Directory.EnumerateFileSystemEntries(InstallDirectory).Any())
            {
                Directory.Delete(InstallDirectory);
            }
        }

        private static void RunSc(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("sc.exe",
                string.Join(" ", arguments.Select(QuoteCommandLineArgument)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "服务操作失败：" + (string.IsNullOrWhiteSpace(error) ? output : error));
                }
            }
        }

        private static string QuoteCommandLineArgument(string value)
        {
            if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;

            var result = new StringBuilder("\"");
            int slashCount = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    slashCount++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', slashCount * 2 + 1);
                    result.Append('"');
                }
                else
                {
                    result.Append('\\', slashCount);
                    result.Append(character);
                }
                slashCount = 0;
            }

            result.Append('\\', slashCount * 2);
            result.Append('"');
            return result.ToString();
        }

        private const int MoveFileDelayUntilReboot = 0x00000004;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }

    public sealed class GuardStatus
    {
        public GuardStatus(bool installed, bool running, bool enabled,
            string targetUtc, int? targetDays)
        {
            Installed = installed;
            Running = running;
            Enabled = enabled;
            TargetUtc = targetUtc;
            TargetDays = targetDays;
        }

        public bool Installed { get; }
        public bool Running { get; }
        public bool Enabled { get; }
        public string TargetUtc { get; }
        public int? TargetDays { get; }
    }

    internal sealed class GuardConfiguration
    {
        public GuardConfiguration(bool enabled, string targetUtc, int? targetDays)
        {
            Enabled = enabled;
            TargetUtc = targetUtc;
            TargetDays = targetDays;
        }

        public bool Enabled { get; }
        public string TargetUtc { get; }
        public int? TargetDays { get; }
    }
}
