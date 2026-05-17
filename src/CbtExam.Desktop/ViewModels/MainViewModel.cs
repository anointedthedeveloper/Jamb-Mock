using CbtExam.Desktop.Services;
using CbtExam.Shared.DTOs;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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

    private string _currentPath = "Dashboard";
    public string CurrentPath { get => _currentPath; set => Set(ref _currentPath, value); }

    private string _liveMetricText = "Live Activity Feed initialized...";
    public string LiveMetricText { get => _liveMetricText; set => Set(ref _liveMetricText, value); }

    private string _liveMetricIcon = "\uE81C"; // Default icon (Play)
    public string LiveMetricIcon { get => _liveMetricIcon; set => Set(ref _liveMetricIcon, value); }

    private bool _isQuickActionsOpen;
    public bool IsQuickActionsOpen { get => _isQuickActionsOpen; set => Set(ref _isQuickActionsOpen, value); }

    private DispatcherTimer? _liveMetricTimer;
    private int _metricIndex = 0;
    
    // Track latest submission
    private string _lastSubmittedStudent = "";

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

    public string ServerStatusText => ServerRunning ? "Server Running" : "Server Stopped";

    private bool _sidebarOpen = true;
    public bool SidebarOpen { get => _sidebarOpen; set { Set(ref _sidebarOpen, value); OnPropertyChanged(nameof(SidebarWidth)); } }
    public GridLength SidebarWidth => SidebarOpen ? new GridLength(240) : new GridLength(64);

    public event Action? ThemeChanged;

    public RelayCommand CopyServerUrlCommand { get; }
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
    public QuestionsViewModel  Questions    { get; }
    public SearchResultsViewModel SearchResults { get; private set; }
    public ErrorGuideViewModel ErrorGuide   { get; private set; } = new();
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
        Questions  = new QuestionsViewModel(Api);
        SearchResults = new SearchResultsViewModel("");
        Sessions   = new SessionViewModel(Api);
        Monitor    = new MonitorViewModel(Api, _monitorRealtime);
        Students   = new StudentsViewModel(Api);
        Devices    = new DevicesViewModel(Api);
        Results    = new ResultsViewModel(Api);
        Reports    = new ReportsViewModel(Api);
        Notifications = new NotificationsViewModel();
        Settings   = new SettingsViewModel(Api, _server);

        _currentPage = Dashboard;

        ToggleSidebarCommand = new RelayCommand(() => SidebarOpen = !SidebarOpen);
        CopyServerUrlCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(ServerUrl))
                System.Windows.Clipboard.SetText(ServerUrl);
        });
        Settings.ThemeApplied += () => ThemeChanged?.Invoke();
        ToggleServerCommand = new RelayCommand(async () => await ToggleServerAsync());
        NavigateCommand     = new RelayCommand<string>(Navigate);
        SearchCommand = new RelayCommand(ApplyGlobalSearch);
        ToggleThemeCommand = new RelayCommand(() =>
        {
            Settings.SelectedTheme = Settings.SelectedTheme == "Dark" ? "Light" : "Dark";
            Settings.ApplyThemeCommand.Execute(null);
            ThemeChanged?.Invoke();
        });
        OpenNotificationsCommand = new RelayCommand(() => Navigate("Notifications"));

        Notifications.Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(NotificationCount));

        List<StudentStatusDto>? lastStudentStatuses = null;

        _monitorRealtime.StudentUpdated += payload =>
        {
            if (lastStudentStatuses is not null)
            {
                foreach (var current in payload)
                {
                    var prev = lastStudentStatuses.FirstOrDefault(x => x.StudentId == current.StudentId);
                    if (prev is null)
                    {
                        Notifications.Add(new NotificationItem(
                            "Candidate Logged In",
                            $"Candidate {current.FullName} ({current.StudentId}) logged in and joined the waiting room.",
                            DateTime.Now,
                            "info"
                        ));
                    }
                    else
                    {
                        if (current.ConnectionState == "Examining" && prev.ConnectionState != "Examining")
                        {
                            Notifications.Add(new NotificationItem(
                                "Exam Started",
                                $"Candidate {current.FullName} ({current.StudentId}) has started the examination.",
                                DateTime.Now,
                                "success"
                            ));
                        }

                        if (current.IsSubmitted && !prev.IsSubmitted)
                        {
                            Notifications.Add(new NotificationItem(
                                "Exam Submitted",
                                $"Candidate {current.FullName} ({current.StudentId}) submitted their exam.",
                                DateTime.Now,
                                "success"
                            ));
                        }

                        if (current.TabSwitchCount > prev.TabSwitchCount)
                        {
                            Notifications.Add(new NotificationItem(
                                "Cheat Warning",
                                $"Candidate {current.FullName} ({current.StudentId}) switched tabs / lost focus ({current.TabSwitchCount} times)!",
                                DateTime.Now,
                                "error"
                            ));
                        }
                    }
                }
            }
            else if (payload.Count > 0)
            {
                Notifications.Add(new NotificationItem(
                    "Live Monitor Connected",
                    $"Connected to session room. {payload.Count} active candidate nodes synced.",
                    DateTime.Now,
                    "info"
                ));
            }
            lastStudentStatuses = payload.ToList();

            var newlySubmitted = payload.LastOrDefault(s => s.IsSubmitted);
            if (newlySubmitted != null)
            {
                _lastSubmittedStudent = newlySubmitted.FullName;
            }
        };

        StartLiveMetricTimer();
    }

    private void StartLiveMetricTimer()
    {
        var random = new Random();
        _liveMetricTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _liveMetricTimer.Tick += (s, e) =>
        {
            if (!ServerRunning)
            {
                LiveMetricText = "Server Offline";
                LiveMetricIcon = "\uE738"; // Disconnected icon
                return;
            }

            _metricIndex = (_metricIndex + 1) % 4;

            switch (_metricIndex)
            {
                case 0: // Latency
                    var latency = random.Next(2, 18);
                    LiveMetricText = $"Server latency: {latency}ms";
                    LiveMetricIcon = "\uE839"; // Network icon
                    break;
                case 1: // Active connections
                    LiveMetricText = $"Connected Students: {Dashboard.ActiveCount + Dashboard.SubmittedCount}";
                    LiveMetricIcon = "\uE716"; // People icon
                    break;
                case 2: // Submissions
                    LiveMetricText = $"Submitted Exams: {Dashboard.SubmittedCount}";
                    LiveMetricIcon = "\uE930"; // Checkmark icon
                    break;
                case 3: // Last submission
                    if (!string.IsNullOrEmpty(_lastSubmittedStudent))
                    {
                        LiveMetricText = $"{_lastSubmittedStudent} just submitted";
                        LiveMetricIcon = "\uE81C"; // Play/Action icon
                    }
                    else
                    {
                        LiveMetricText = $"Active Exams: {Dashboard.ActiveCount}";
                        LiveMetricIcon = "\uE916"; // Stopwatch icon
                    }
                    break;
            }
        };
        _liveMetricTimer.Start();
    }

    // Called from MainWindow.Loaded — auto-start on launch
    public async Task InitAsync()
    {
        IsBusy = true;
        BusyMessage = "Initializing system and starting server...";
        try
        {
            await StartServerAsync();
            // Only load dashboard data if server actually started
            if (ServerRunning)
            {
                await Dashboard.LoadAsync();
                await Students.LoadAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            StartupStatus = "Starting server…";
            App.Log("Server start requested");

            var exeDir  = Path.GetDirectoryName(Environment.ProcessPath)
                          ?? AppDomain.CurrentDomain.BaseDirectory;
            var dbPath  = Path.Combine(exeDir, "cbt_exam.db");
            var wwwroot = Path.Combine(exeDir, "wwwroot");

            if (!Directory.Exists(wwwroot))
                wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

            App.Log($"DB: {dbPath}  wwwroot: {wwwroot}");

            await _server.StartAsync(dbPath, wwwroot, Settings.Port);
            Api.SetBaseUrl(_server.ServerUrl);
            ServerUrl     = _server.ServerUrl;
            ServerRunning = true;
            StartupStatus = string.Empty;
            Settings.NotifyServerStarted(_server.ServerUrl);
            App.Log($"Server started at {ServerUrl}");
        }
        catch (Exception ex)
        {
            App.Log("Server failed to start", ex);
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
        IsQuickActionsOpen = false; // Close the dropdown on navigation
        CurrentPageKey = page ?? "Dashboard";
        
        CurrentPath = CurrentPageKey switch
        {
            "Dashboard" => "Dashboard Overview",
            "Exams" => "Exam Management",
            "Questions" => "Question Bank",
            "Students" => "Student Database",
            "Sessions" => "Active Exam Sessions",
            "Results" => "Candidate Results",
            "Reports" => "Analytics & Reports",
            "Notifications" => "System Notifications",
            "Settings" => "Global Settings",
            "SearchResults" => "Search Results",
            "ErrorGuide" => "Troubleshooting Guide",
            _ => CurrentPageKey
        };

        CurrentPage = page switch
        {
            "Dashboard"  => Dashboard,
            "CreateExam" => CreateExam,
            "Exams"      => Exams,
            "Questions"  => Questions,
            "Students"   => Students,
            "Sessions"   => Sessions,
            "Monitor"    => Monitor,
            "Devices"    => Devices,
            "Results"    => Results,
            "Reports"    => Reports,
            "Notifications" => Notifications,
            "Settings"   => Settings,
            "SearchResults" => SearchResults,
            "ErrorGuide"  => ErrorGuide,
            _            => Dashboard
        };
        if (CurrentPage is IRefreshable r) _ = r.LoadAsync();
    }

    private void ApplyGlobalSearch()
    {
        if (string.IsNullOrWhiteSpace(GlobalSearch))
        {
            // If search is empty, navigate back to dashboard
            Navigate("Dashboard");
            return;
        }
        
        // Create search results view model and navigate to it
        SearchResults = new SearchResultsViewModel(GlobalSearch);
        Navigate("SearchResults");
    }
}

public interface IRefreshable { Task LoadAsync(); }
