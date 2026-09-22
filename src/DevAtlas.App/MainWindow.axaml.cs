using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DevAtlas.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void AddRoot_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose a workspace root"
        });
        if (folders.Count > 0 && DataContext is MainWindowViewModel viewModel)
            await viewModel.AddRootAsync(folders[0].Path.LocalPath);
    }
}
