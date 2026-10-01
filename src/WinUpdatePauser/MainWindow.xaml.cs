using System.Windows;
using WinUpdatePauser.Services;
using WinUpdatePauser.Views;

namespace WinUpdatePauser
{
    /// <summary>
    /// 主窗口：仅承载视图切换逻辑。
    /// 恢复用户上次选择的配置方式；新版未初始化时进入引导页。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            RefreshView();
        }

        /// <summary>
        /// 根据上次选择与注册表初始化状态选择显示的视图。
        /// GuideView 的"重新检测"通过回调重新触发本方法。
        /// </summary>
        public void RefreshView()
        {
            bool useLegacy = UserPreferenceService.ReadUseLegacy();
            if (!useLegacy && !PauseRegistryService.IsInitialized())
            {
                ShowGuide();
                return;
            }

            ShowMainView(useLegacy);
        }

        /// <summary>显示指定配置方式；新版尚未初始化时保留首次引导页。</summary>
        private void ShowMainView(bool useLegacy)
        {
            UserPreferenceService.SaveUseLegacy(useLegacy);
            if (!useLegacy && !PauseRegistryService.IsInitialized())
            {
                ShowGuide();
                return;
            }

            RootContent.Content = new MainView(
                useLegacy,
                ShowMainView,
                RefreshView);
        }

        private void ShowGuide()
        {
            RootContent.Content = new GuideView(RefreshView, () => ShowMainView(true));
        }
    }
}
