using System;
using System.Globalization;

namespace WinUpdatePauser.Utils
{
    /// <summary>
    /// 注册表 ISO 8601 UTC 时间格式与本地时间的双向转换模块。
    ///
    /// 注册表格式示例：2077-01-01T00:00:00Z（UTC，"Z" 表示零时区）。
    /// 界面统一使用本地时间：用户选择的时间即 Windows 设置页最终显示的时间，
    /// 写入时本地 → UTC，读取时 UTC → 本地。
    /// </summary>
    public static class Iso8601Time
    {
        /// <summary>注册表要求的固定格式（不使用 "K" 等占位符，保证恒定输出 "Z" 后缀）。</summary>
        private const string RegistryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>
        /// 本地时间 → 注册表 UTC 字符串。
        /// 例（东八区）：本地 2077-01-01 00:00 → "2076-12-31T16:00:00Z"。
        /// </summary>
        public static string ToRegistryFormat(DateTime local)
        {
            // 显式声明为本地时间后再转 UTC，避免 Kind 为 Unspecified 时的歧义
            DateTime utc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
            return utc.ToString(RegistryFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 注册表字符串 → 本地时间。
        /// 容错解析：格式异常时返回 null（由界面显示提示，而不是崩溃）。
        /// </summary>
        public static DateTime? ParseToLocal(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            DateTimeOffset result;
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out result))
            {
                return result.LocalDateTime;
            }

            return null;
        }
    }
}
