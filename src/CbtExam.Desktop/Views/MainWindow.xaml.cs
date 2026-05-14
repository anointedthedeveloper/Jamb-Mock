using CbtExam.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CbtExam.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply title bar color now that HWND exists
        App? app = App.Current as App;
        app?.ApplyTitleBarToWindow(this);

        try
        {
            if (DataContext is MainViewModel vm)
            {
                // Re-apply title bar whenever theme changes
                vm.ThemeChanged += () => app?.ApplyTitleBarToWindow(this);
                await vm.InitAsync();
            }
        }
        catch (Exception ex)
        {
            App.Log("Window_Loaded error", ex);
            MessageBox.Show(
                $"Failed to start server:\n\n{ex.Message}\n\n{ex.InnerException?.Message}\n\nSee cbt_error.log for details.",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox != null && string.IsNullOrWhiteSpace(SearchBox.Text) && SearchPlaceholder != null)
            SearchPlaceholder.Visibility = Visibility.Visible;
    }
}
