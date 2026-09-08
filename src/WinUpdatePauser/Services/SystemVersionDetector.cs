using System;

namespace WinUpdatePauser.Services
{
    /// <summary>仅提供系统版本信息显示，不参与新版或旧版配置路由。</summary>
    public static class SystemVersionDetector
    {
        public static Version CurrentVersion
        {
            get { return Environment.OSVersion.Version; }
        }

        public static int CurrentBuild
        {
            get { return CurrentVersion.Build; }
        }

        public static string DisplayVersion
        {
            get
            {
                string family = CurrentBuild >= 22000 ? "Windows 11" : "Windows 10";
                return family + " · OS Build " + CurrentBuild;
            }
        }

    }
}
