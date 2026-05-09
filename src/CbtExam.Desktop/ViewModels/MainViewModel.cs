using CbtExam.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace CbtExam.Desktop.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly EmbeddedServerService _server;
    private readonly MonitorRealtimeService _monitorRealtime;
    public ApiClient Api { get; }

    private BaseViewModel _currentPage;
    public BaseViewModel CurrentPage { get => _currentPage; set => Set(ref _currentPage, value); }

    private string _currentPageKey = "Dashboard";
    public string CurrentPageKey { get => _currentPageKey; set => Set(ref _currentPageKey, value); }

    private bool _serverRunning;
    public bool ServerRunning
    {
        get => _serverRunning;
        set { Set(ref _serverRunning, value); OnPropertyChanged(nameof(ServerStatusText)); }
    }

    private string _serverUrl = string.Empty;
    public string ServerUrl { get => _serverUrl; set => Set(ref _serverUrl, value); }

    private string _startupStatus = "Starting server…";
    public string StartupStatus { get => _startupStatus; set => Set(ref _startupStatus, value); }

    public string ServerStatusText => ServerRunning ? "● Server Running" : "○ Server Stopped";

    private bool _sidebarOpen = true;
    public bool SidebarOpen { get => _sidebarOpen; set { Set(ref _sidebarOpen, value); OnPropertyChanged(nameof(SidebarWidth)); } }
    public GridLength SidebarWidth => SidebarOpen ? new GridLength(240) : new GridLength(64);

    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleServerCommand { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand OpenNotificationsCommand { get; }

    private string _globalSearch = string.Empty;
    public string GlobalSearch { get => _globalSearch; set => Set(ref _globalSearch, value); }
    public int NotificationCount => Notifications.UnreadCount;

    // Pages
    public DashboardViewModel  Dashboard    { get; }
    public CreateExamViewModel CreateExam   { get; }
    public ExamsViewModel      Exams        { get; }
    public SessionViewModel    Sessions     { get; }
    public MonitorViewModel    Monitor      { get; }
    public StudentsViewModel   Students     { get; }
    public DevicesViewModel    Devices      { get; }
    public ResultsViewModel    Results      { get; }
    public ReportsViewModel    Reports      { get; }
    public NotificationsViewModel Notifications { get; }
    public SettingsViewModel   Settings     { get; }

    public MainViewModel()
    {
        _server = new EmbeddedServerService();
        _monitorRealtime = new MonitorRealtimeService();
        Api = new ApiClient();

        Dashboard  = new DashboardViewModel(Api);
        CreateExam = new CreateExamViewModel(Api);
        Exams      = new ExamsViewModel(Api);
        Sessions   = new SessionViewModel(Api);
        Monitor    = new MonitorViewModel(Api, _monitorRealtime);
        Students   = new StudentsViewModel(Api);
        Devices    = new DevicesViewModel(Api);
        Results    = new ResultsViewModel(Api);
        Reports    = new ReportsViewModel(Api);
        Notifications = new NotificationsViewModel();
        Settings   = new SettingsViewModel(_server);

        _currentPage = Dashboard;

        ToggleSidebarCommand = new RelayCommand(() => SidebarOpen = !SidebarOpen);
        ToggleServerCommand = new RelayCommand(async () => await ToggleServerAsync());
        NavigateCommand     = new RelayCommand<string>(Navigate);
        SearchCommand = new RelayCommand(ApplyGlobalSearch);
        ToggleThemeCommand = new RelayCommand(() =>
        {
            Settings.SelectedTheme = Settings.SelectedTheme == "Dark" ? "Light" : "Dark";
            Settings.ApplyThemeCommand.Execute(null);
        });
        OpenNotificationsCommand = new RelayCommand(() => Navigate("Notifications"));

        _monitorRealtime.StudentUpdated += payload =>
        {
            Notifications.Add(new NotificationItem("Student activity", $"{payload.Count} student updates received.", DateTime.Now, "info"));
            OnPropertyChanged(nameof(NotificationCount));
        };
    }

    // Called from MainWindow.Loaded — auto-start on launch
    public async Task InitAsync()
    {
        await StartServerAsync();
        // Only load dashboard data if server actually started
        if (ServerRunning)
        {
            await Dashboard.LoadAsync();
            await Students.LoadAsync();
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            StartupStatus = "Starting server…";

            // Use the EXE's own directory — works for both dev and single-file publish
            var exeDir   = Path.GetDirectoryName(Environment.ProcessPath)
                           ?? AppDomain.CurrentDomain.BaseDirectory;
            var dbPath   = Path.Combine(exeDir, "cbt_exam.db");
            var wwwroot  = Path.Combine(exeDir, "wwwroot");

            // Fallback: if wwwroot doesn't exist next to EXE, try BaseDirectory (dev run)
            if (!Directory.Exists(wwwroot))
                wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            await _server.StartAsync(dbPath, wwwroot, Settings.Port);
            Api.SetBaseUrl(_server.ServerUrl);
            ServerUrl     = _server.ServerUrl;
            ServerRunning = true;
            StartupStatus = string.Empty;
            Settings.NotifyServerStarted(_server.ServerUrl);
        }
        catch (Exception ex)
        {
            StartupStatus = ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                ? "Setup required: local database schema is being initialized."
                : $"Server error: {ex.Message}";
            ServerRunning = false;
        }
    }

    private async Task ToggleServerAsync()
    {
        if (!ServerRunning)
            await StartServerAsync();
        else
        {
            await _server.StopAsync();
            ServerRunning = false;
            ServerUrl     = string.Empty;
            Settings.NotifyServerStopped();
        }
    }

    private void Navigate(string? page)
    {
        CurrentPageKey = page ?? "Dashboard";
        CurrentPage = page switch
        {
            "Dashboard"  => Dashboard,
            "CreateExam" => CreateExam,
            "Exams"      => Exams,
            "Students"   => Students,
            "Sessions"   => Sessions,
            "Monitor"    => Monitor,
            "Devices"    => Devices,
            "Results"    => Results,
            "Reports"    => Reports,
            "Notifications" => Notifications,
            "Settings"   => Settings,
            _            => Dashboard
        };
        if (CurrentPage is IRefreshable r) _ = r.LoadAsync();
    }

    private void ApplyGlobalSearch()
    {
        Exams.Search = GlobalSearch;
        Students.Search = GlobalSearch;
        Navigate("Exams");
    }
}

public interface IRefreshable { Task LoadAsync(); }
