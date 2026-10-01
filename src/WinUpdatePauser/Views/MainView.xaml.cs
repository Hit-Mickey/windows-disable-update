using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinUpdatePauser.Services;
using WinUpdatePauser.Utils;

namespace WinUpdatePauser.Views
{
    /// <summary>
    /// 主页面：保留原有新版年月日时分输入逻辑，并在同一页面提供互斥的旧版天数配置。
    /// </summary>
    public partial class MainView : UserControl
    {
        private readonly DateTime _minDate = DateTime.Today.AddDays(1);
        private static readonly DateTime MaxDate = new DateTime(2199, 12, 31);
        private readonly Action<bool> _requestModeChange;
        private readonly Action _onStateChanged;
        private bool _useLegacy;
        private string _currentEndTimeDisplay;
        private string _currentLegacyDaysDisplay;
        private bool _ready;

        public MainView(bool useLegacy,
            Action<bool> requestModeChange, Action onStateChanged)
        {
            _useLegacy = useLegacy;
            _requestModeChange = requestModeChange;
            _onStateChanged = onStateChanged;

            InitializeComponent();
            LoadCurrentState();
            SetMode(useLegacy);
            _ready = true;
            Validate();
            ValidateLegacy();
        }

        /// <summary>读取注册表，刷新状态卡片与两种配置的默认输入值。</summary>
        private void LoadCurrentState()
        {
            SystemVersionText.Text = SystemVersionDetector.DisplayVersion;

            bool newInitialized = PauseRegistryService.IsInitialized();

            string raw = PauseRegistryService.ReadRawEndTime();
            DateTime? local = Iso8601Time.ParseToLocal(raw);
            if (local.HasValue)
            {
                _currentEndTimeDisplay = local.Value.ToString("yyyy-MM-dd HH:mm");
                YearBox.Text = local.Value.Year.ToString("0000");
                MonthBox.Text = local.Value.Month.ToString("00");
                DayBox.Text = local.Value.Day.ToString("00");
                HourBox.Text = local.Value.Hour.ToString("00");
                MinuteBox.Text = local.Value.Minute.ToString("00");
            }
            else
            {
                _currentEndTimeDisplay = raw == null ? "—" : "（格式异常：" + raw + "）";
                YearBox.Text = _minDate.Year.ToString("0000");
                MonthBox.Text = _minDate.Month.ToString("00");
                DayBox.Text = _minDate.Day.ToString("00");
                HourBox.Text = "00";
                MinuteBox.Text = "00";
            }

            int? legacyDays = PauseRegistryService.ReadLegacyPauseDays();
            _currentLegacyDaysDisplay = legacyDays.HasValue ? legacyDays.Value + " 天" : "尚未配置";
            LegacyDaysBox.Text = legacyDays.HasValue ? legacyDays.Value.ToString() : "36500";

            if (_useLegacy)
            {
                RegistryStateLabel.Text = "旧版配置状态：";
                RegistryStateText.Text = legacyDays.HasValue ? "✓ 已配置" : "尚未配置";
                RegistryStateText.Foreground = (Brush)FindResource(
                    legacyDays.HasValue ? "SuccessBrush" : "TextSecondaryBrush");
            }
            else
            {
                RegistryStateLabel.Text = "注册表状态：";
                RegistryStateText.Text = newInitialized ? "✓ 已初始化" : "未初始化（新版需先在设置中暂停一次）";
                RegistryStateText.Foreground = (Brush)FindResource(
                    newInitialized ? "SuccessBrush" : "TextSecondaryBrush");
            }

            UpdateStatusSummary();
            LoadGuardState();
        }

        /// <summary>只显示当前选择的配置方式，另一套内容保持折叠。</summary>
        private void SetMode(bool useLegacy)
        {
            _useLegacy = useLegacy;
            NewCalendarPanel.Visibility = useLegacy ? Visibility.Collapsed : Visibility.Visible;
            LegacyPanel.Visibility = useLegacy ? Visibility.Visible : Visibility.Collapsed;
            GuardStatusLabel.Text = useLegacy ? "天数守护：" : "日期守护：";

            ModeSwitchButton.Style = (Style)FindResource("SecondaryButton");
            ModeSwitchButton.Content = useLegacy ? "切换到新版日历配置" : "切换到旧版天数配置";
            UpdateStatusSummary();

            if (useLegacy)
            {
                ValidateLegacy();
            }
            else
            {
                Validate();
            }
        }

        private void UpdateStatusSummary()
        {
            if (CurrentStatusLabel == null || CurrentStatusValue == null)
            {
                return;
            }

            CurrentStatusLabel.Text = _useLegacy ? "旧版暂停天数：" : "当前暂停更新时间：";
            CurrentStatusValue.Text = _useLegacy ? _currentLegacyDaysDisplay : _currentEndTimeDisplay;
        }

        private void LoadGuardState()
        {
            if (GuardStatusText == null) return;

            try
            {
                GuardMode mode = _useLegacy ? GuardMode.Legacy : GuardMode.Modern;
                GuardStatus status = UpdateGuardManager.GetStatus(mode);
                string target;
                if (_useLegacy)
                {
                    target = status.TargetDays.HasValue
                        ? status.TargetDays.Value + " 天"
                        : "未设置";
                }
                else
                {
                    DateTime? targetLocal = Iso8601Time.ParseToLocal(status.TargetUtc);
                    target = targetLocal.HasValue
                        ? targetLocal.Value.ToString("yyyy-MM-dd HH:mm")
                        : "未设置";
                }

                if (status.Enabled && status.Running)
                {
                    GuardStatusText.Text = _useLegacy
                        ? "运行中 · 守护 " + target
                        : "运行中 · 守护至 " + target;
                    GuardStatusText.Foreground = (Brush)FindResource("SuccessBrush");
                }
                else if (status.Installed)
                {
                    GuardStatusText.Text = status.Enabled
                        ? "服务未运行 · 目标 " + target
                        : "已停止 · 目标 " + target;
                    GuardStatusText.Foreground = (Brush)FindResource("TextSecondaryBrush");
                }
                else if (status.Enabled)
                {
                    GuardStatusText.Text = "服务未安装 · 目标 " + target;
                    GuardStatusText.Foreground = (Brush)FindResource("ErrorBrush");
                }
                else
                {
                    GuardStatusText.Text = "未运行";
                    GuardStatusText.Foreground = (Brush)FindResource("TextSecondaryBrush");
                }

                UninstallGuardButton.Visibility = status.Running
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                UninstallGuardButton.Visibility = Visibility.Collapsed;
                GuardStatusText.Text = "无法读取服务状态：" + ex.Message;
                GuardStatusText.Foreground = (Brush)FindResource("ErrorBrush");
            }
        }

        private DateTime? GetInputLocalTime()
        {
            int year, month, day, hour, minute;
            if (!int.TryParse(YearBox.Text, out year)
                || !int.TryParse(MonthBox.Text, out month)
                || !int.TryParse(DayBox.Text, out day)
                || !int.TryParse(HourBox.Text, out hour)
                || !int.TryParse(MinuteBox.Text, out minute))
            {
                return null;
            }

            try
            {
                return new DateTime(year, month, day, hour, minute, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private void Input_DigitsOnly(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (c < '0' || c > '9')
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void Input_Changed(object sender, TextChangedEventArgs e)
        {
            if (_ready)
            {
                Validate();
            }
        }

        private void LegacyInput_Changed(object sender, TextChangedEventArgs e)
        {
            if (_ready)
            {
                ValidateLegacy();
            }
        }

        private void Validate()
        {
            if (!_ready && ApplyButton == null)
            {
                return;
            }

            DateTime? input = GetInputLocalTime();
            if (!input.HasValue)
            {
                ApplyButton.IsEnabled = false;
                ShowValidation("请输入有效的日期和时间（注意月份天数）", true);
                return;
            }

            if (input.Value.Date < _minDate || input.Value.Date > MaxDate)
            {
                ApplyButton.IsEnabled = false;
                ShowValidation("日期超出可填范围（明天 ~ 2199-12-31），请重新输入", true);
                return;
            }

            ApplyButton.IsEnabled = true;
            ShowValidation("可填范围：明天 ~ 2199-12-31", false);
        }

        private void ValidateLegacy()
        {
            if (LegacyDaysBox == null || ApplyLegacyButton == null)
            {
                return;
            }

            int days;
            if (!int.TryParse(LegacyDaysBox.Text, out days) || days < 1 || days > 36500)
            {
                ApplyLegacyButton.IsEnabled = false;
                LegacyValidationText.Text = "请输入 1 ~ 36500 之间的暂停天数";
                LegacyValidationText.Foreground = (Brush)FindResource("ErrorBrush");
                return;
            }

            ApplyLegacyButton.IsEnabled = true;
            LegacyValidationText.Text = "可填范围：1 ~ 36500 天";
            LegacyValidationText.Foreground = (Brush)FindResource("TextSecondaryBrush");
        }

        private void ShowValidation(string message, bool isError)
        {
            ValidationText.Text = message;
            ValidationText.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "TextSecondaryBrush");
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DateTime? input = GetInputLocalTime();
            if (!input.HasValue)
            {
                return;
            }

            string isoUtc = Iso8601Time.ToRegistryFormat(input.Value);
            try
            {
                PauseRegistryService.WriteEndTime(isoUtc);
                UpdateGuardManager.InstallAndEnableModern(isoUtc);
                LoadCurrentState();
                MessageBox.Show(
                    "已将 Windows 更新暂停至：" + input.Value.ToString("yyyy-MM-dd HH:mm")
                    + "\n\n日期守护服务已启用，可打开「设置 → Windows 更新」查看效果。",
                    "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("应用暂停日期失败：", ex);
            }
        }

        private void UninstallGuard_Click(object sender, RoutedEventArgs e)
        {
            string guardName = _useLegacy ? "天数守护" : "日期守护";
            MessageBoxResult result = MessageBox.Show(
                "将停止并删除" + guardName + "服务及其配置，已设置的暂停值不会改变。是否继续？",
                "卸载" + guardName, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                UpdateGuardManager.Uninstall(_useLegacy ? GuardMode.Legacy : GuardMode.Modern);
                LoadGuardState();
                MessageBox.Show(guardName + "服务已卸载。",
                    "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("卸载日期守护失败：", ex);
                LoadGuardState();
            }
        }

        private void ApplyLegacy_Click(object sender, RoutedEventArgs e)
        {
            int days;
            if (!int.TryParse(LegacyDaysBox.Text, out days) || days < 1 || days > 36500)
            {
                ValidateLegacy();
                return;
            }

            try
            {
                PauseRegistryService.WriteLegacyPauseDays(days);
                UpdateGuardManager.InstallAndEnableLegacy(days);
                LoadCurrentState();
                MessageBox.Show(
                    "已将旧版 Windows 更新暂停天数设置为：" + days
                    + " 天\n\n天数守护服务已启用。",
                    "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("应用旧版暂停天数失败：", ex);
            }
        }

        private void Resume_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "将停止日期守护，并删除本工具管理的新版和旧版暂停值。是否继续？",
                "恢复正常更新", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                UpdateGuardManager.DisableAll();
                PauseRegistryService.ResumeNormalUpdates();
                MessageBox.Show("已恢复正常更新。",
                    "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
                if (_onStateChanged != null)
                {
                    _onStateChanged();
                }
                else
                {
                    LoadCurrentState();
                }
            }
            catch (Exception ex)
            {
                ShowError("恢复正常更新失败：", ex);
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("ms-settings:windowsupdate");
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开 Windows 设置：" + ex.Message,
                    "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ModeSwitch_Click(object sender, RoutedEventArgs e)
        {
            bool targetLegacy = !_useLegacy;
            if (_requestModeChange != null)
            {
                _requestModeChange(targetLegacy);
            }
            else
            {
                SetMode(targetLegacy);
            }
        }

        private static void ShowError(string message, Exception ex)
        {
            MessageBox.Show(message + "\n\n" + ex.Message,
                "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
