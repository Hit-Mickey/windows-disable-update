using Microsoft.Win32;

namespace WinUpdatePauser.Services
{
    /// <summary>保存当前用户最后选择的配置界面。</summary>
    public static class UserPreferenceService
    {
        private const string KeyPath = @"SOFTWARE\WUPause";
        private const string LastModeValueName = "LastMode";

        public static bool ReadUseLegacy()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                return string.Equals(key?.GetValue(LastModeValueName) as string,
                    "Legacy", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SaveUseLegacy(bool useLegacy)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, true))
            {
                key?.SetValue(LastModeValueName,
                    useLegacy ? "Legacy" : "Modern", RegistryValueKind.String);
            }
        }
    }
}
