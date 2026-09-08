using System.Windows;
using WinUpdatePauser.Services;
using WinUpdatePauser.Views;

namespace WinUpdatePauser
{
    /// <summary>
    /// 主窗口：仅承载视图切换逻辑。
    /// 未初始化（注册表三值不全）→ 引导页 GuideView；已初始化 → 主页 MainView。
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly bool _recommendedLegacy;

        public MainWindow()
        {
            InitializeComponent();
            _recommendedLegacy = !SystemVersionDetector.RecommendNewCalendar;
            RefreshView();
        }

        /// <summary>
        /// 根据注册表初始化状态选择显示的视图。
        /// GuideView 的"重新检测"通过回调重新触发本方法。
        /// </summary>
        public void RefreshView()
        {
            bool initialized = PauseRegistryService.IsInitialized();
            if (initialized || _recommendedLegacy)
            {
                ShowMainView(_recommendedLegacy);
            }
            else
            {
                RootContent.Content = new GuideView(RefreshView, ShowLegacyView);
            }
        }

        /// <summary>显示指定配置方式；新版尚未初始化时保留首次引导页。</summary>
        private void ShowMainView(bool useLegacy)
        {
            if (!useLegacy && !PauseRegistryService.IsInitialized())
            {
                RootContent.Content = new GuideView(RefreshView, ShowLegacyView);
                return;
            }

            RootContent.Content = new MainView(
                useLegacy,
                RequestModeChange,
                RefreshView);
        }

        private void ShowLegacyView()
        {
            ShowMainView(true);
        }

        private void RequestModeChange(bool useLegacy)
        {
            ShowMainView(useLegacy);
        }
    }
}
