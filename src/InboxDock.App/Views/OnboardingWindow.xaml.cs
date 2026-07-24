using System.Windows;
using InboxDock.App.ViewModels;

namespace InboxDock.App.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += _ =>
        {
            DialogResult = true;
            Close();
            return Task.CompletedTask;
        };
        viewModel.Cancelled += () =>
        {
            DialogResult = false;
            Close();
        };
    }

    private void BrowseVault_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择 Obsidian Vault 文件夹",
        };
        if (dialog.ShowDialog() == true)
        {
            if (DataContext is OnboardingViewModel vm)
            {
                _ = vm.PickVaultAsync(dialog.FolderName);
            }
        }
    }
}
