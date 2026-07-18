using System.Windows;
using WinUpdatePauser.Services;

namespace WinUpdatePauser
{
    /// <summary>
    /// 应用程序入口。
    /// 启动流程：管理员权限检测（防御层）→ 创建主窗口（主窗口内部再做初始化检测）。
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 权限防御检测：正常情况下 app.manifest 的 requireAdministrator 已保证以管理员启动。
            // 若以非管理员运行（例如 manifest 被剥离或特殊启动方式），提示并提供提权重启。
            if (!AdminHelper.IsAdministrator())
            {
                MessageBoxResult result = MessageBox.Show(
                    "需要管理员权限修改 Windows Update 设置。\n\n是否以管理员身份重新启动？",
                    "Windows 更新暂停助手",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    AdminHelper.RestartAsAdministrator();
                }

                Shutdown();
                return;
            }

            var window = new MainWindow();
            window.Show();
        }
    }
}
