using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace WinUpdatePauser.Services
{
    /// <summary>
    /// 管理员权限检测与提权重启模块。
    ///
    /// 正常情况下 app.manifest 的 requireAdministrator 已保证程序以管理员启动（UAC 弹窗）。
    /// 此类为防御层：若程序以非管理员运行（如 manifest 被剥离或特殊启动方式），
    /// 由 App.OnStartup 提示用户并调用 RestartAsAdministrator 提权重启。
    /// </summary>
    public static class AdminHelper
    {
        /// <summary>当前进程是否具有管理员权限。</summary>
        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// 以管理员身份重新启动自身（verb = "runas" 触发 UAC）。
        /// 用户在 UAC 中取消时静默返回，由调用方负责退出当前实例。
        /// </summary>
        public static void RestartAsAdministrator()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                // 用户在 UAC 弹窗中点击了"否"（Win32Exception）—— 不再重启，直接返回
            }
        }
    }
}
