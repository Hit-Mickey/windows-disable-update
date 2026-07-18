using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinUpdatePauser.Services;
using WinUpdatePauser.Utils;

namespace WinUpdatePauser.Views
{
    /// <summary>
    /// 主页：显示当前暂停结束时间，提供 年/月/日/时/分 手动输入并一键写入注册表。
    /// 界面统一使用本地时间，写入时由 Iso8601Time 转为 UTC。
    /// </summary>
    public partial class MainView : UserControl
    {
        /// <summary>可选下限：明天（禁止填过去时间，避免暂停立即失效）。</summary>
        private readonly DateTime _minDate = DateTime.Today.AddDays(1);

        /// <summary>可选上限：2099-12-31。</summary>
        private static readonly DateTime MaxDate = new DateTime(2099, 12, 31);

        /// <summary>控件初始填充完成前不做校验（避免半初始化状态误报）。</summary>
        private bool _ready;

        public MainView()
        {
            InitializeComponent();
            LoadCurrentState();
            _ready = true;
            Validate();
        }

        /// <summary>
        /// 读取注册表 → 刷新状态卡片，并把当前值填入输入框作为默认值。
        /// </summary>
        private void LoadCurrentState()
        {
            string raw = PauseRegistryService.ReadRawEndTime();
            DateTime? local = Iso8601Time.ParseToLocal(raw);

            if (local.HasValue)
            {
                CurrentEndTimeText.Text = local.Value.ToString("yyyy-MM-dd HH:mm");

                // 默认填入注册表中的当前日期（即使已过期也照实显示，应用时再校验范围）
                YearBox.Text = local.Value.Year.ToString("0000");
                MonthBox.Text = local.Value.Month.ToString("00");
                DayBox.Text = local.Value.Day.ToString("00");
                HourBox.Text = local.Value.Hour.ToString("00");
                MinuteBox.Text = local.Value.Minute.ToString("00");
            }
            else
            {
                // 值缺失或格式异常：状态区如实提示，输入框给出最小可用默认值
                CurrentEndTimeText.Text = raw == null ? "—" : "（格式异常：" + raw + "）";
                YearBox.Text = _minDate.Year.ToString("0000");
                MonthBox.Text = _minDate.Month.ToString("00");
                DayBox.Text = _minDate.Day.ToString("00");
                HourBox.Text = "00";
                MinuteBox.Text = "00";
            }
        }

        /// <summary>
        /// 解析五个输入框得到本地时间。
        /// 任一框为空/非数字/组合不是有效日期（如 2 月 30 日）时返回 null。
        /// </summary>
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
                // DateTime 构造函数自动校验月份天数（大小月/闰年），非法组合抛异常
                return new DateTime(year, month, day, hour, minute, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>输入框只允许输入数字。</summary>
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

        /// <summary>任一输入框内容变化时重新校验。</summary>
        private void Input_Changed(object sender, TextChangedEventArgs e)
        {
            if (_ready)
            {
                Validate();
            }
        }

        /// <summary>
        /// 校验输入时间是否有效且在 明天 ~ 2099-12-31 范围内：
        /// 不合法 → 禁用应用按钮并以红色提示原因；合法 → 恢复默认提示。
        /// </summary>
        private void Validate()
        {
            DateTime? input = GetInputLocalTime();

            if (!input.HasValue)
            {
                ApplyButton.IsEnabled = false;
                ShowValidation("请输入有效的日期和时间（注意月份天数）", true);
                return;
            }

            if (input.Value.Hour > 23 || input.Value.Minute > 59)
            {
                // DateTime 构造已保证时分合法，此分支纯防御
                ApplyButton.IsEnabled = false;
                ShowValidation("时间无效（时 0-23，分 0-59）", true);
                return;
            }

            if (input.Value.Date < _minDate || input.Value.Date > MaxDate)
            {
                ApplyButton.IsEnabled = false;
                ShowValidation("日期超出可填范围（明天 ~ 2099-12-31），请重新输入", true);
                return;
            }

            ApplyButton.IsEnabled = true;
            ShowValidation("可填范围：明天 ~ 2099-12-31", false);
        }

        private void ShowValidation(string message, bool isError)
        {
            ValidationText.Text = message;
            ValidationText.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "TextSecondaryBrush");
        }

        /// <summary>
        /// 应用按钮：本地时间 → ISO 8601 UTC → 同时写入三个注册表值 → 刷新状态。
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DateTime? input = GetInputLocalTime();
            if (!input.HasValue)
            {
                return; // 按钮禁用时不会到达，纯防御
            }

            string isoUtc = Iso8601Time.ToRegistryFormat(input.Value);

            try
            {
                PauseRegistryService.WriteEndTime(isoUtc);
            }
            catch (Exception ex)
            {
                MessageBox.Show("写入注册表失败：\n\n" + ex.Message,
                    "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadCurrentState(); // 刷新状态卡片（读回写入结果）

            MessageBox.Show(
                "已将 Windows 更新暂停至：" + input.Value.ToString("yyyy-MM-dd HH:mm")
                + "\n\n写入的注册表值：" + isoUtc
                + "\n\n可打开「设置 → Windows 更新」查看效果。",
                "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
