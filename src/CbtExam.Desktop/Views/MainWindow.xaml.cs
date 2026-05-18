using CbtExam.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CbtExam.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += Window_Closing;
    }

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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Block close if a live exam is currently running
        var sessions = vm.Sessions;
        bool liveExamRunning = sessions.IsManagingRoom &&
                               sessions.CurrentRoom is { IsStarted: true };

        if (liveExamRunning)
        {
            e.Cancel = true;
            MessageBox.Show(
                "⚠️  A live exam is currently in progress!\n\n" +
                $"Session: \"{sessions.CurrentRoom!.ExamTitle}\"  |  Code: {sessions.CurrentRoom.SessionCode}\n\n" +
                "You cannot close the admin console while candidates are sitting an active exam.\n" +
                "Please end the session first from the Sessions panel.",
                "Cannot Close — Live Exam in Progress",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
