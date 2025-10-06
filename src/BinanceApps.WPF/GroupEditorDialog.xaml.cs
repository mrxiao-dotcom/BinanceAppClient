using System;
using System.Windows;
using BinanceApps.Core.Services;

namespace BinanceApps.WPF
{
    public partial class GroupEditorDialog : Window
    {
        private readonly PortfolioGroupService _groupService;
        
        public string GroupName { get; private set; } = string.Empty;
        
        public GroupEditorDialog(PortfolioGroupService groupService)
        {
            InitializeComponent();
            _groupService = groupService;
        }
        
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                var name = txtGroupName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("请输入分组名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtGroupName.Focus();
                    return;
                }
                
                // 检查名称长度
                if (name.Length > 10)
                {
                    var result = MessageBox.Show(
                        "分组名称过长（超过10个字符），建议缩短以便更好地显示。\n\n确定要继续吗？",
                        "提示",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    
                    if (result != MessageBoxResult.Yes)
                        return;
                }
                
                var description = txtDescription.Text.Trim();
                
                // 禁用按钮防止重复点击
                btnSave.IsEnabled = false;
                btnSave.Content = "保存中...";
                
                // 创建分组
                await _groupService.CreateGroupAsync(name, description);
                
                GroupName = name;
                
                // 关闭对话框
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSave.IsEnabled = true;
                btnSave.Content = "保存";
            }
        }
    }
} 