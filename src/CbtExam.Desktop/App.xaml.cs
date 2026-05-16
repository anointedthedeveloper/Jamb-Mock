using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

namespace CbtExam.Desktop;

public partial class App : Application
{
    private string ThemeFile => Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
        "theme.json");

    private static readonly string LogFile = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
        "cbt_error.log");

    // Current theme state — read by MainWindow to apply DWM on load
    public static string CurrentTheme  { get; private set; } = "Light";
    public static string CurrentAccent { get; private set; } = "Teal";

    // ── Logging ────────────────────────────────────────────────────────────
    public static void Log(string message, Exception? ex = null)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex is not null)
                line += $"\n  Exception: {ex.ToString()}";
            File.AppendAllText(LogFile, line + "\n");
        }
        catch { }
    }

    // ── Shell Integration ─────────────────────────────────────────────────
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    // ── Startup ────────────────────────────────────────────────────────────
    protected override void OnStartup(StartupEventArgs e)
    {
        // Fix taskbar grouping and icons - v3 forces a fresh identity
        try { SetCurrentProcessExplicitAppUserModelID("Anobyte.CbtExam.Admin.v3"); } catch { }

        base.OnStartup(e);

        // Ensure app doesn't close when switching between windows
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        // Force icon to be set at the OS level as soon as window handle exists
        EventManager.RegisterClassHandler(typeof(Window), Window.SourceInitializedEvent, new RoutedEventHandler((s, e) =>
        {
            if (s is Window window)
            {
                try {
                    window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/CbtExam;component/Resources/jamb.png"));
                } catch { }
            }
        }));

        DispatcherUnhandledException          += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        
        LoadTheme();

        var login = new Views.LoginWindow();
        login.Show();
    }

    // ── Exception handlers ─────────────────────────────────────────────────
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log("UI thread exception", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.InnerException?.Message}\n\nSee cbt_error.log for details.",
            "CBT Exam — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Log("Unobserved task exception", e.Exception);
        Dispatcher.Invoke(() =>
            MessageBox.Show(
                $"Background error:\n\n{e.Exception.InnerException?.Message ?? e.Exception.Message}\n\nSee cbt_error.log for details.",
                "CBT Exam — Error", MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log("Fatal unhandled exception", ex);
        MessageBox.Show(
            $"Fatal error:\n\n{ex?.Message}\n\nSee cbt_error.log for details.",
            "CBT Exam — Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // ── Theme engine ───────────────────────────────────────────────────────
    public void ApplyTheme(string theme, string accent)
    {
        CurrentTheme  = theme;
        CurrentAccent = accent;

        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);

        // Accent palette
        var (accentHex, accentDarkHex, accentTintHex, accentMutedHex, accentFgHex) = accent switch
        {
            "Blue"    => ("#2563EB", "#1D4ED8", "#EFF6FF", "#BFDBFE", "#1E40AF"),
            "Purple"  => ("#7C3AED", "#6D28D9", "#F5F3FF", "#DDD6FE", "#4C1D95"),
            "Emerald" => ("#059669", "#047857", "#ECFDF5", "#A7F3D0", "#065F46"),
            "Rose"    => ("#E11D48", "#BE123C", "#FFF1F2", "#FECDD3", "#9F1239"),
            _         => ("#0D9488", "#0F766E", "#F0FDFA", "#99F6E4", "#134E4A"), // Teal
        };

        if (dark)
        {
            // ── Dark surfaces ──
            Set("BgBrush",            "#0F172A");  // deep navy
            Set("CardBrush",          "#1E293B");  // slate-800
            Set("SurfaceBrush",       "#1E293B");
            Set("InputBgBrush",       "#0F172A");
            Set("InputBorderBrush",   "#334155");
            Set("TextPrimaryBrush",   "#F1F5F9");
            Set("TextSecondaryBrush", "#94A3B8");
            Set("BorderBrush",        "#334155");
            Set("NavHoverBrush",      "#1E3A2F");
            Set("NavHoverFgBrush",    accentTintHex);
            Set("NavActiveBrush",     "#1E3A2F");
            Set("NavActiveFgBrush",   accentHex);
            Set("RowHoverBrush",      "#1E293B");
            Set("RowSelectedBrush",   "#1E3A2F");
            Set("HeaderBgBrush",      "#0F172A");
        }
        else
        {
            // ── Light surfaces ──
            Set("BgBrush",            "#F8FAFC");
            Set("CardBrush",          "#FFFFFF");
            Set("SurfaceBrush",       "#FFFFFF");
            Set("InputBgBrush",       "#FFFFFF");
            Set("InputBorderBrush",   accentMutedHex);
            Set("TextPrimaryBrush",   "#0F172A");
            Set("TextSecondaryBrush", "#64748B");
            Set("BorderBrush",        "#E5E7EB");
            Set("NavHoverBrush",      accentTintHex);
            Set("NavHoverFgBrush",    accentFgHex);
            Set("NavActiveBrush",     accentTintHex);
            Set("NavActiveFgBrush",   accentHex);
            Set("RowHoverBrush",      "#F9FAFB");
            Set("RowSelectedBrush",   accentTintHex);
            Set("HeaderBgBrush",      "#F8FAFC");
        }

        // Accent brushes (same for both modes)
        Set("AccentBrush",     accentHex);
        Set("AccentDarkBrush", accentDarkHex);

        SaveTheme(theme, accent);

        // Apply DWM title bar color to all open windows
        foreach (Window w in Current.Windows)
            ApplyTitleBar(w, dark, accentHex);
    }

    // Called from MainWindow after it's loaded so the HWND exists
    public void ApplyTitleBarToWindow(Window w)
    {
        var dark = string.Equals(CurrentTheme, "Dark", StringComparison.OrdinalIgnoreCase);
        var (accentHex, _, _, _, _) = CurrentAccent switch
        {
            "Blue"    => ("#2563EB", "", "", "", ""),
            "Purple"  => ("#7C3AED", "", "", "", ""),
            "Emerald" => ("#059669", "", "", "", ""),
            "Rose"    => ("#E11D48", "", "", "", ""),
            _         => ("#0D9488", "", "", "", ""),
        };
        ApplyTitleBar(w, dark, accentHex);
    }

    // ── DWM title bar ──────────────────────────────────────────────────────
    private static void ApplyTitleBar(Window w, bool dark, string accentHex)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Dark/light caption text (DWMWA_USE_IMMERSIVE_DARK_MODE = 20)
            int darkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

            // Title bar background color (DWMWA_CAPTION_COLOR = 35) — Win11 only, safe to fail
            var color = ParseBgr(dark ? "#1E293B" : accentHex);
            DwmSetWindowAttribute(hwnd, 35, ref color, sizeof(int));
        }
        catch { /* older Windows — silently skip */ }
    }

    // Convert #RRGGBB → COLORREF (0x00BBGGRR)
    private static int ParseBgr(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex[..2], 16);
        int g = Convert.ToInt32(hex[2..4], 16);
        int b = Convert.ToInt32(hex[4..6], 16);
        return r | (g << 8) | (b << 16);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── Brush helper ───────────────────────────────────────────────────────
    private void Set(string key, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        Resources[key] = new SolidColorBrush(color);
    }

    // ── Persistence ────────────────────────────────────────────────────────
    private void SaveTheme(string theme, string accent) =>
        File.WriteAllText(ThemeFile, $"{theme}|{accent}");

    private void LoadTheme()
    {
        if (!File.Exists(ThemeFile)) { ApplyTheme("Light", "Teal"); return; }
        var parts = File.ReadAllText(ThemeFile).Split('|');
        ApplyTheme(parts.Length >= 1 ? parts[0] : "Light",
                   parts.Length >= 2 ? parts[1] : "Teal");
    }
}
