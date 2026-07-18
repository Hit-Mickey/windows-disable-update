using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinUpdatePauser.Services;

namespace WinUpdatePauser.Views
{
    /// <summary>
    /// 引导页：指引用户先在系统设置中手动暂停一次更新，让系统生成注册表值。
    /// 本页面绝不主动创建注册表项。
    /// </summary>
    public partial class GuideView : UserControl
    {
        /// <summary>检测通过后的回调（由 MainWindow 传入，用于切换到主视图）。</summary>
        private readonly Action _onInitialized;

        public GuideView(Action onInitialized)
        {
            InitializeComponent();
            _onInitialized = onInitialized;
        }

        /// <summary>打开系统「Windows 更新」设置页。</summary>
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ms-settings 协议由 shell 处理，直接跳转到 Windows 更新页面
                Process.Start("ms-settings:windowsupdate");
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开 Windows 设置：" + ex.Message,
                    "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>重新检测注册表：已初始化则通知主窗口切换视图，否则提示继续操作。</summary>
        private void Recheck_Click(object sender, RoutedEventArgs e)
        {
            if (PauseRegistryService.IsInitialized())
            {
                _onInitialized();
            }
            else
            {
                MessageBox.Show(
                    "仍未检测到暂停设置。\n\n请确认已在「设置 → Windows 更新」中点击「暂停更新」并选择了一个日期。",
                    "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
