using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace DevAtlas.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel viewModel) await viewModel.InitializeAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel) viewModel.Close();
        base.OnClosed(e);
    }

    private async void AddRoot_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.IsBusy) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose a workspace root"
            });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
                await viewModel.AddRootAsync(path);
        }
        catch (Exception)
        {
            viewModel.ReportError("Could not open the folder picker. Check access permissions and try again.");
        }
    }
}
