using System;

namespace WinUpdatePauser.Services
{
    /// <summary>
    /// 根据 Windows 当前版本给出配置方式建议。
    /// Windows 11 24H2 及之后的系统 Build 从 26100 开始，优先使用新版日历配置。
    /// </summary>
    public static class SystemVersionDetector
    {
        public const int NewCalendarBuild = 26100;

        public static Version CurrentVersion
        {
            get { return Environment.OSVersion.Version; }
        }

        public static int CurrentBuild
        {
            get { return CurrentVersion.Build; }
        }

        public static bool RecommendNewCalendar
        {
            get { return CurrentBuild >= NewCalendarBuild; }
        }

        public static string DisplayVersion
        {
            get
            {
                string family = CurrentBuild >= 22000 ? "Windows 11" : "Windows 10";
                return family + " · OS Build " + CurrentBuild;
            }
        }

        public static string RecommendationText
        {
            get { return RecommendNewCalendar ? "推荐使用新版日历配置" : "推荐使用旧版天数配置"; }
        }
    }
}
