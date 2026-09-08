using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinUpdatePauser.Services;

namespace WinUpdatePauser.Views
{
    /// <summary>备份列表及恢复、重命名、删除和路径管理窗口。</summary>
    public partial class BackupWindow : Window
    {
        private readonly Action _onStateChanged;

        public BackupWindow(Action onStateChanged)
        {
            InitializeComponent();
            _onStateChanged = onStateChanged;
            BackupPathBox.Text = PauseRegistryService.GetBackupDirectory();
            RefreshBackups();
        }

        private void RefreshBackups()
        {
            BackupList.Items.Clear();
            foreach (BackupListItem item in PauseRegistryService.GetBackups())
            {
                BackupList.Items.Add(item);
            }

            RenameBox.Clear();
        }

        private BackupListItem SelectedBackup
        {
            get { return BackupList.SelectedItem as BackupListItem; }
        }

        private void BackupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BackupListItem item = SelectedBackup;
            RenameBox.Text = item == null ? string.Empty : item.DisplayName;
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            BackupListItem item = SelectedBackup;
            if (item == null)
            {
                MessageBox.Show("请先选择一个备份。", "备份管理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "恢复该备份会先保存当前注册表配置，再覆盖本工具管理的值。是否继续？",
                "恢复备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                BackupListItem beforeRestore = PauseRegistryService.RestoreBackup(item.JsonPath);
                if (_onStateChanged != null)
                {
                    _onStateChanged();
                }

                RefreshBackups();
                MessageBox.Show("备份已恢复。\n\n恢复前的当前配置也已备份：" + beforeRestore.DisplayName,
                    "操作完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("恢复备份失败", ex);
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            BackupListItem item = SelectedBackup;
            if (item == null)
            {
                MessageBox.Show("请先选择一个备份。", "备份管理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                PauseRegistryService.RenameBackup(item.JsonPath, RenameBox.Text);
                RefreshBackups();
            }
            catch (Exception ex)
            {
                ShowError("重命名备份失败", ex);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            BackupListItem item = SelectedBackup;
            if (item == null)
            {
                MessageBox.Show("请先选择一个备份。", "备份管理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                "确定删除备份“" + item.DisplayName + "”及其 .reg 文件吗？此操作无法撤销。",
                "删除备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                PauseRegistryService.DeleteBackup(item.JsonPath);
                RefreshBackups();
            }
            catch (Exception ex)
            {
                ShowError("删除备份失败", ex);
            }
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择 Windows 更新暂停备份目录";
                dialog.SelectedPath = BackupPathBox.Text;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    BackupPathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void ApplyPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = PauseRegistryService.SetBackupDirectory(BackupPathBox.Text);
                BackupPathBox.Text = path;
                RefreshBackups();
                MessageBox.Show("备份路径已更新。", "备份管理", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("设置备份路径失败", ex);
            }
        }

        private void ResetPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BackupPathBox.Text = PauseRegistryService.ResetBackupDirectory();
                RefreshBackups();
            }
            catch (Exception ex)
            {
                ShowError("恢复默认路径失败", ex);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = PauseRegistryService.GetBackupDirectory();
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError("无法打开备份文件夹", ex);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static void ShowError(string title, Exception ex)
        {
            MessageBox.Show(title + "：\n\n" + ex.Message,
                "Windows 更新暂停助手", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
