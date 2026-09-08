using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly Action _onLegacyRequested;

        public GuideView(Action onInitialized, Action onLegacyRequested)
        {
            InitializeComponent();
            _onInitialized = onInitialized;
            _onLegacyRequested = onLegacyRequested;
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

        /// <summary>新版尚未初始化时，允许用户直接切换到旧版天数配置。</summary>
        private void UseLegacy_Click(object sender, RoutedEventArgs e)
        {
            _onLegacyRequested();
        }

        private void GuideImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Image image = sender as Image;
            if (image == null || image.Source == null)
            {
                return;
            }

            var preview = new Window
            {
                Owner = Window.GetWindow(this),
                Title = "引导图原图",
                Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/app-icon.ico")),
                Width = 900,
                Height = 650,
                MinWidth = 480,
                MinHeight = 320,
                ResizeMode = ResizeMode.CanResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Brush)FindResource("WindowBackgroundBrush"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Content = new ScrollViewer
                {
                    Padding = new Thickness(16),
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new Border
                    {
                        Background = (Brush)FindResource("CardBackgroundBrush"),
                        BorderBrush = (Brush)FindResource("CardBorderBrush"),
                        BorderThickness = new Thickness(1),
                        Child = new Image
                        {
                            Source = image.Source,
                            Stretch = Stretch.None,
                            SnapsToDevicePixels = true,
                            UseLayoutRounding = true
                        }
                    }
                }
            };

            RenderOptions.SetBitmapScalingMode((Image)((Border)((ScrollViewer)preview.Content).Content).Child,
                BitmapScalingMode.HighQuality);
            preview.ShowDialog();
            e.Handled = true;
        }
    }
}
