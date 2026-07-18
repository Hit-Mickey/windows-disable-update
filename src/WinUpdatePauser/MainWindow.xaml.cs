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
        public MainWindow()
        {
            InitializeComponent();
            RefreshView();
        }

        /// <summary>
        /// 根据注册表初始化状态选择显示的视图。
        /// GuideView 的"重新检测"通过回调重新触发本方法。
        /// </summary>
        public void RefreshView()
        {
            if (PauseRegistryService.IsInitialized())
            {
                RootContent.Content = new MainView();
            }
            else
            {
                RootContent.Content = new GuideView(RefreshView);
            }
        }
    }
}
