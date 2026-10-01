using System;
using System.Globalization;
using Microsoft.Win32;

namespace WinUpdatePauser.Services
{
    /// <summary>
    /// Windows Update 暂停设置的注册表操作模块。
    /// 所有注册表读写集中在此类，UI 层不直接接触注册表。
    /// 新版日期配置使用三个 REG_SZ 值；旧版配置使用 FlightSettingsMaxPauseDays REG_DWORD。
    /// </summary>
    public static class PauseRegistryService
    {
        private const string SettingsKeyPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

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

        private static readonly string[] NewCalendarValueNames =
        {
            PauseFeatureUpdatesEndTime,
            PauseQualityUpdatesEndTime,
            PauseUpdatesExpiryTime
        };

        /// <summary>新版暂停值是否已经由 Windows 设置生成。</summary>
        public static bool IsInitialized()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null)
                {
                    return false;
                }

                foreach (string name in NewCalendarValueNames)
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

        /// <summary>将 ISO 8601 UTC 字符串同时写入三个 REG_SZ 值。</summary>
        public static void WriteEndTime(string isoUtc)
        {
            if (string.IsNullOrWhiteSpace(isoUtc))
            {
                throw new ArgumentException("暂停结束时间不能为空。", nameof(isoUtc));
            }

            using (RegistryKey key = OpenSettingsKey(true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(
                        "注册表键不存在，请先在 Windows 设置中暂停一次更新。");
                }

                foreach (string name in NewCalendarValueNames)
                {
                    key.SetValue(name, isoUtc, RegistryValueKind.String);
                }
            }
        }

        /// <summary>写入旧版暂停天数。与 reg add 命令一致：键不存在时创建键。</summary>
        public static void WriteLegacyPauseDays(int days)
        {
            if (days < 1 || days > 36500)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "暂停天数应为 1 到 36500。 ");
            }

            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = baseKey.CreateSubKey(SettingsKeyPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开 Windows Update 注册表设置键。");
                }

                key.SetValue(FlightSettingsMaxPauseDays, days, RegistryValueKind.DWord);
            }
        }

        /// <summary>删除本工具管理的暂停值，恢复到系统继续更新状态。</summary>
        public static void ResumeNormalUpdates()
        {
            using (RegistryKey key = OpenSettingsKey(true))
            {
                if (key == null)
                {
                    return;
                }

                foreach (string name in ManagedValueNames)
                {
                    key.DeleteValue(name, false);
                }
            }
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
    }
}
