using System;
using Microsoft.Win32;

namespace WinUpdatePauser.Services
{
    /// <summary>
    /// Windows Update 暂停设置的注册表操作模块。
    /// 所有注册表读写集中在此类，UI 层不直接接触注册表。
    ///
    /// 原理：Windows 在用户于「设置 → Windows 更新 → 暂停更新」中选择日期后，
    /// 会在 HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings 下生成三个 REG_SZ 值
    /// （ISO 8601 UTC 格式，如 2077-01-01T00:00:00Z）。将三值改为任意未来时间
    /// 即可延长暂停期，三值必须保持完全一致。
    /// </summary>
    public static class PauseRegistryService
    {
        /// <summary>设置键路径（相对 HKEY_LOCAL_MACHINE）。</summary>
        private const string SettingsKeyPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

        // 三个必须保持一致的值名
        public const string PauseFeatureUpdatesEndTime = "PauseFeatureUpdatesEndTime";
        public const string PauseQualityUpdatesEndTime = "PauseQualityUpdatesEndTime";
        public const string PauseUpdatesExpiryTime = "PauseUpdatesExpiryTime";

        private static readonly string[] AllValueNames =
        {
            PauseFeatureUpdatesEndTime,
            PauseQualityUpdatesEndTime,
            PauseUpdatesExpiryTime
        };

        /// <summary>
        /// 打开设置键。强制使用 64 位注册表视图，避免进程以 32 位方式运行时
        /// 被 WOW64 重定向到 SOFTWARE\WOW6432Node 导致读写错位置。
        /// 键不存在时返回 null —— 本模块绝不创建键（未初始化时必须由系统生成）。
        /// </summary>
        private static RegistryKey OpenSettingsKey(bool writable)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                return baseKey.OpenSubKey(SettingsKeyPath, writable);
            }
        }

        /// <summary>
        /// 是否已初始化：三个值全部存在才算已初始化（严格模式）。
        /// 任一缺失说明用户尚未在系统设置中暂停过更新（或状态异常），应走引导流程。
        /// </summary>
        public static bool IsInitialized()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null)
                {
                    return false;
                }

                foreach (string name in AllValueNames)
                {
                    if (key.GetValue(name) == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 读取当前暂停结束时间的原始字符串。
        /// 以 PauseUpdatesExpiryTime 为准（写入时三值统一，读取取其一即可）。
        /// 键或值不存在时返回 null。
        /// </summary>
        public static string ReadRawEndTime()
        {
            using (RegistryKey key = OpenSettingsKey(false))
            {
                if (key == null)
                {
                    return null;
                }

                return key.GetValue(PauseUpdatesExpiryTime) as string;
            }
        }

        /// <summary>
        /// 将 ISO 8601 UTC 字符串同时写入三个值（REG_SZ），保证三值完全一致。
        /// 调用前应保证 IsInitialized() 为 true —— 本方法不创建键。
        /// </summary>
        /// <exception cref="InvalidOperationException">注册表键不存在（未初始化）。</exception>
        public static void WriteEndTime(string isoUtc)
        {
            using (RegistryKey key = OpenSettingsKey(true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(
                        "注册表键不存在，请先在 Windows 设置中暂停一次更新。");
                }

                foreach (string name in AllValueNames)
                {
                    key.SetValue(name, isoUtc, RegistryValueKind.String);
                }
            }
        }
    }
}
