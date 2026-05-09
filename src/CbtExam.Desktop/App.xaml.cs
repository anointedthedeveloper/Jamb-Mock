using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.IO;

namespace CbtExam.Desktop;

public partial class App : Application
{
    private string ThemeFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch any unhandled exception on the UI thread — show message instead of silent crash
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Catch unhandled exceptions from background Task threads
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Catch anything else
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        LoadTheme();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.InnerException?.Message}",
            "CBT Exam — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // prevent app from closing
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved(); // prevent process termination
        Dispatcher.Invoke(() =>
            MessageBox.Show(
                $"Background error:\n\n{e.Exception.InnerException?.Message ?? e.Exception.Message}",
                "CBT Exam — Error", MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"Fatal error:\n\n{ex?.Message}",
            "CBT Exam — Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ApplyTheme(string theme, string accent)
    {
        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);
        SetBrush("BgBrush", dark ? "#0F172A" : "#F8FAFC");
        SetBrush("CardBrush", dark ? "#111827" : "#FFFFFF");
        SetBrush("TextPrimaryBrush", dark ? "#E5E7EB" : "#0F2A1E");
        SetBrush("TextSecondaryBrush", dark ? "#94A3B8" : "#4B7A62");
        SetBrush("BorderBrush", dark ? "#334155" : "#E5E7EB");

        var accentHex = accent switch
        {
            "Blue" => "#2563EB",
            "Purple" => "#7C3AED",
            "Emerald" => "#059669",
            _ => "#0D9488"
        };
        SetBrush("AccentBrush", accentHex);
        SaveTheme(theme, accent);
    }

    private void SetBrush(string key, string colorHex)
    {
        if (Resources[key] is SolidColorBrush brush)
            brush.Color = (Color)ColorConverter.ConvertFromString(colorHex);
    }

    private void SaveTheme(string theme, string accent)
    {
        File.WriteAllText(ThemeFile, $"{theme}|{accent}");
    }

    private void LoadTheme()
    {
        if (!File.Exists(ThemeFile)) return;
        var parts = File.ReadAllText(ThemeFile).Split('|');
        if (parts.Length == 2) ApplyTheme(parts[0], parts[1]);
    }
}
