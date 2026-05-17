using CbtExam.Desktop.Services;
using CbtExam.Shared.DTOs;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Net.Http;
using System.IO;
using System.Windows;

namespace CbtExam.Desktop.ViewModels;

// ─── Dashboard (Overview) ────────────────────────────────────────────────────
public class DashboardViewModel : BaseViewModel, IRefreshable
{
    private readonly ApiClient api;
    private int _totalStudents, _activeCount, _submittedCount, _pausedCount, _registeredDevicesCount, _onlineDevicesCount;
    public int TotalStudents  { get => _totalStudents;  set { if (Set(ref _totalStudents,  value)) NotifySessionRatios(); } }
    public int ActiveCount    { get => _activeCount;    set { if (Set(ref _activeCount,    value)) NotifySessionRatios(); } }
    public int SubmittedCount { get => _submittedCount; set { if (Set(ref _submittedCount, value)) NotifySessionRatios(); } }
    public int PausedCount    { get => _pausedCount;    set => Set(ref _pausedCount,    value); }
    public int RegisteredDevicesCount { get => _registeredDevicesCount; set => Set(ref _registeredDevicesCount, value); }
    public int OnlineDevicesCount     { get => _onlineDevicesCount;     set => Set(ref _onlineDevicesCount,     value); }
    public double SubmittedPercent => TotalStudents == 0 ? 0 : Math.Round(SubmittedCount * 100.0 / TotalStudents, 1);
    public double ActivePercent => TotalStudents == 0 ? 0 : Math.Round(ActiveCount * 100.0 / TotalStudents, 1);

    private string _latency = "--";
    public string Latency { get => _latency; set { if (Set(ref _latency, value)) OnPropertyChanged(nameof(LatencyColor)); } }
    public string LatencyColor => Latency switch { "--" => "#64748B", var s when int.TryParse(s.Replace("ms", ""), out int v) && v < 100 => "#16A34A", _ => "#EF4444" };

    private SessionDto? _activeSession;
    public SessionDto? ActiveSession
    {
        get => _activeSession;
        set { Set(ref _activeSession, value); OnPropertyChanged(nameof(HasActiveSession)); OnPropertyChanged(nameof(NoActiveSession)); }
    }
    public bool HasActiveSession => ActiveSession is not null;
    public bool NoActiveSession  => ActiveSession is null;

    public ObservableCollection<ExamDto> Exams { get; } = [];
    public ObservableCollection<ExamDto> FilteredExams { get; } = [];
    public ObservableCollection<string> SubjectFilters { get; } = ["All subjects"];

    private string _examSearch = string.Empty;
    public string ExamSearch
    {
        get => _examSearch;
        set { Set(ref _examSearch, value); FilterDashboardExams(); }
    }

    private string _selectedSubject = "All subjects";
    public string SelectedSubject
    {
        get => _selectedSubject;
        set { Set(ref _selectedSubject, value); FilterDashboardExams(); }
    }

    private string _title = "", _subject = "";
    private int _duration = 60;
    private bool _shuffleQ = true, _shuffleO = true;
    public string Title    { get => _title;    set => Set(ref _title,    value); }
    public string Subject  { get => _subject;  set => Set(ref _subject,  value); }
    public int    Duration { get => _duration; set => Set(ref _duration, value); }
    public bool ShuffleQuestions { get => _shuffleQ; set => Set(ref _shuffleQ, value); }
    public bool ShuffleOptions   { get => _shuffleO; set => Set(ref _shuffleO, value); }

    private string _createStatus = string.Empty;
    public string CreateStatus { get => _createStatus; set => Set(ref _createStatus, value); }

    private bool _showCreateForm;
    public bool ShowCreateForm { get => _showCreateForm; set => Set(ref _showCreateForm, value); }

    private System.Timers.Timer? _liveTimer;

    public DashboardViewModel(ApiClient api)
    {
        this.api = api;
        _liveTimer = new System.Timers.Timer(5000);
        _liveTimer.Elapsed += async (s, e) => await UpdateLiveStatsAsync();
        _liveTimer.Start();
    }

    public RelayCommand RefreshCommand    => new(async () => await LoadAsync());
    public RelayCommand ToggleFormCommand => new(() => { ShowCreateForm = !ShowCreateForm; CreateStatus = string.Empty; });
    public RelayCommand CreateExamCommand => new(async () => await CreateExamAsync());
    public RelayCommand<ExamDto> DeleteExamCommand => new(async e => await DeleteExamAsync(e));

    private async Task UpdateLiveStatsAsync()
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sessions = await api.GetSessionsAsync();
            sw.Stop();
            
            App.Current.Dispatcher.Invoke(() => Latency = $"{sw.ElapsedMilliseconds}ms");

            var active = sessions?.FirstOrDefault(s => s.IsActive);
            if (active is not null)
            {
                var students = await api.GetStudentsAsync(active.Id);
                App.Current.Dispatcher.Invoke(() => {
                    TotalStudents  = students?.Count ?? 0;
                    SubmittedCount = students?.Count(s => s.IsSubmitted) ?? 0;
                    ActiveCount    = students?.Count(s => !s.IsSubmitted) ?? 0;
                });
            }

            var devices = await api.GetDevicesAsync();
            App.Current.Dispatcher.Invoke(() => {
                RegisteredDevicesCount = devices?.Count ?? 0;
                OnlineDevicesCount = devices?.Count(d => d.IsOnline) ?? 0;
            });
        }
        catch { App.Current.Dispatcher.Invoke(() => Latency = "Error"); }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var sessions = await api.GetSessionsAsync();
            ActiveSession = sessions?.FirstOrDefault(s => s.IsActive);

            if (ActiveSession is not null)
            {
                var students = await api.GetStudentsAsync(ActiveSession.Id);
                TotalStudents  = students?.Count ?? 0;
                SubmittedCount = students?.Count(s => s.IsSubmitted) ?? 0;
                ActiveCount    = students?.Count(s => !s.IsSubmitted) ?? 0;
                PausedCount    = 0;
            }
            else
            {
                TotalStudents = ActiveCount = SubmittedCount = PausedCount = 0;
            }

            var exams = await api.GetExamsAsync();
            Exams.Clear();
            exams?.ForEach(Exams.Add);
            RebuildSubjectFilters();
            FilterDashboardExams();
        }
        finally { IsBusy = false; }
    }

    private async Task CreateExamAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { CreateStatus = "Title is required."; return; }
        var resp = await api.CreateExamAsync(new ExamCreateDto(Title, Subject, Duration, ShuffleQuestions, ShuffleOptions));
        if (resp.IsSuccessStatusCode)
        {
            CreateStatus = string.Empty;
            Title = Subject = "";
            Duration = 60;
            ShowCreateForm = false;
            await LoadAsync();
        }
        else CreateStatus = "Failed to create exam.";
    }

    private async Task DeleteExamAsync(ExamDto? exam)
    {
        if (exam is null) return;
        await api.DeleteExamAsync(exam.Id);
        await LoadAsync();
    }

    private void NotifySessionRatios()
    {
        OnPropertyChanged(nameof(SubmittedPercent));
        OnPropertyChanged(nameof(ActivePercent));
    }

    private void RebuildSubjectFilters()
    {
        var previous = SelectedSubject;
        SubjectFilters.Clear();
        SubjectFilters.Add("All subjects");

        foreach (var subject in Exams.Select(e => e.Subject)
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s))
        {
            SubjectFilters.Add(subject);
        }

        SelectedSubject = SubjectFilters.Contains(previous) ? previous : "All subjects";
    }

    private void FilterDashboardExams()
    {
        var query = ExamSearch.Trim();
        IEnumerable<ExamDto> result = Exams;

        if (!string.IsNullOrWhiteSpace(query))
        {
            result = result.Where(e =>
                e.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Subject.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedSubject) && SelectedSubject != "All subjects")
            result = result.Where(e => string.Equals(e.Subject, SelectedSubject, StringComparison.OrdinalIgnoreCase));

        FilteredExams.Clear();
        foreach (var exam in result)
            FilteredExams.Add(exam);
    }
}

// ─── Create Exam (dedicated page) ────────────────────────────────────────────
public class CreateExamViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    // Step 1 — exam details
    private string _title = "", _subject = "";
    private int _duration = 60;
    private bool _shuffleQ = true, _shuffleO = true;
    public string Title    { get => _title;    set => Set(ref _title,    value); }
    public string Subject  { get => _subject;  set => Set(ref _subject,  value); }
    public int    Duration { get => _duration; set => Set(ref _duration, value); }
    public bool ShuffleQuestions { get => _shuffleQ; set => Set(ref _shuffleQ, value); }
    public bool ShuffleOptions   { get => _shuffleO; set => Set(ref _shuffleO, value); }

    // Step 2 — add questions after exam created
    private int? _createdExamId;
    private string _createdExamTitle = string.Empty;
    public int?   CreatedExamId    { get => _createdExamId;    set { Set(ref _createdExamId, value); OnPropertyChanged(nameof(ExamCreated)); } }
    public string CreatedExamTitle { get => _createdExamTitle; set => Set(ref _createdExamTitle, value); }
    public bool ExamCreated => CreatedExamId.HasValue;

    // Question form
    private string _qText = "", _opt1 = "", _opt2 = "", _opt3 = "", _opt4 = "", _correct = "";
    public string QText   { get => _qText;   set => Set(ref _qText,   value); }
    public string Opt1    { get => _opt1;    set => Set(ref _opt1,    value); }
    public string Opt2    { get => _opt2;    set => Set(ref _opt2,    value); }
    public string Opt3    { get => _opt3;    set => Set(ref _opt3,    value); }
    public string Opt4    { get => _opt4;    set => Set(ref _opt4,    value); }
    public string Correct { get => _correct; set => Set(ref _correct, value); }

    // JSON bulk import
    private string _jsonImport = string.Empty;
    public string JsonImport { get => _jsonImport; set => Set(ref _jsonImport, value); }

    public ObservableCollection<QuestionCreateDto> Questions { get; } = [];

    private string _status = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }

    private bool _isSuccess;
    public bool IsSuccess { get => _isSuccess; set => Set(ref _isSuccess, value); }

    public RelayCommand SaveExamCommand    => new(async () => await SaveExamAsync());
    public RelayCommand AddQuestionCommand => new(async () => await AddQuestionAsync());
    public RelayCommand ImportJsonCommand  => new(async () => await ImportJsonAsync());
    public RelayCommand ResetCommand       => new(Reset);

    public Task LoadAsync() => Task.CompletedTask;

    private async Task SaveExamAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) { Status = "Title is required."; IsSuccess = false; return; }
        var resp = await api.CreateExamAsync(new ExamCreateDto(Title, Subject, Duration, ShuffleQuestions, ShuffleOptions));
        if (!resp.IsSuccessStatusCode) { Status = "Failed to create exam."; IsSuccess = false; return; }
        var id = await resp.Content.ReadAsStringAsync();
        CreatedExamId    = int.Parse(id.Trim());
        CreatedExamTitle = Title;
        Status    = $"Exam \"{Title}\" created! Now add questions below.";
        IsSuccess = true;
    }

    private async Task AddQuestionAsync()
    {
        if (!CreatedExamId.HasValue) return;
        if (string.IsNullOrWhiteSpace(QText) || string.IsNullOrWhiteSpace(Opt1) ||
            string.IsNullOrWhiteSpace(Opt2)  || string.IsNullOrWhiteSpace(Correct))
        {
            Status = "Question text, at least 2 options, and correct answer are required.";
            IsSuccess = false;
            return;
        }

        var options = new List<string> { Opt1, Opt2 };
        if (!string.IsNullOrWhiteSpace(Opt3)) options.Add(Opt3);
        if (!string.IsNullOrWhiteSpace(Opt4)) options.Add(Opt4);

        if (!options.Contains(Correct, StringComparer.OrdinalIgnoreCase))
        {
            Status = "Correct answer must match one of the options exactly.";
            IsSuccess = false;
            return;
        }

        var dto = new QuestionCreateDto(Questions.Count + 1, QText, options, Correct);
        var resp = await api.AddQuestionAsync(CreatedExamId.Value, dto);
        if (!resp.IsSuccessStatusCode) { Status = "Failed to add question."; IsSuccess = false; return; }

        Questions.Add(dto);
        QText = Opt1 = Opt2 = Opt3 = Opt4 = Correct = "";
        Status = $"Question {Questions.Count} added.";
        IsSuccess = true;
    }

    private async Task ImportJsonAsync()
    {
        if (!CreatedExamId.HasValue || string.IsNullOrWhiteSpace(JsonImport)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<QuestionCreateDto>>(JsonImport,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list is null || list.Count == 0) { Status = "No questions found in JSON."; IsSuccess = false; return; }
            var duplicateNumbers = list.GroupBy(x => x.QuestionNumber).Where(g => g.Key > 0 && g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateNumbers.Count > 0)
            {
                Status = $"Invalid format: duplicate questionNumber ({string.Join(", ", duplicateNumbers)}).";
                IsSuccess = false;
                return;
            }
            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item.Text) || item.Options is null || item.Options.Count < 2 ||
                    item.Options.Any(o => string.IsNullOrWhiteSpace(o)) ||
                    !item.Options.Contains(item.CorrectAnswer, StringComparer.OrdinalIgnoreCase))
                {
                    Status = "Invalid format: each row must have text, >=2 options, and correctAnswer matching one option.";
                    IsSuccess = false;
                    return;
                }
            }
            var resp = await api.ImportQuestionsAsync(CreatedExamId.Value, list);
            if (!resp.IsSuccessStatusCode) { Status = "Import failed."; IsSuccess = false; return; }
            Questions.Clear();
            foreach (var q in list)
                Questions.Add(q);
            JsonImport = string.Empty;
            Status = $"{list.Count} question(s) processed.";
            IsSuccess = true;
        }
        catch
        {
            Status = "Invalid JSON format.";
            IsSuccess = false;
        }
    }

    private void Reset()
    {
        Title = Subject = "";
        Duration = 60;
        ShuffleQuestions = ShuffleOptions = true;
        CreatedExamId = null;
        CreatedExamTitle = "";
        QText = Opt1 = Opt2 = Opt3 = Opt4 = Correct = JsonImport = "";
        Questions.Clear();
        Status = string.Empty;
    }
}

// ─── Subject configuration row for exam template builder ──────────────────────
public class ExamSubjectConfigVM : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly Action<ExamSubjectConfigVM> _onRemove;
    private readonly Action _onChanged;
    private List<QuestionBankDto> _bankQuestions = [];

    public ExamSubjectConfigVM(ApiClient api, ObservableCollection<string> availableSubjects, Action<ExamSubjectConfigVM> onRemove, Action onChanged)
    {
        _api = api;
        _onRemove = onRemove;
        _onChanged = onChanged;
        AvailableSubjects = availableSubjects;
    }

    public ObservableCollection<string> AvailableSubjects { get; }
    public ObservableCollection<YearToggle> AvailableYears { get; } = [];

    private string _selectedSubject = string.Empty;
    public string SelectedSubject
    {
        get => _selectedSubject;
        set
        {
            if (Set(ref _selectedSubject, value))
                _ = LoadYearsAsync();
        }
    }

    private int _questionCount = 40;
    public int QuestionCount
    {
        get => _questionCount;
        set
        {
            if (Set(ref _questionCount, value))
            {
                OnPropertyChanged(nameof(HasPoolWarning));
                OnPropertyChanged(nameof(PoolWarningText));
                _onChanged();
            }
        }
    }

    private int _poolSize;
    public int PoolSize
    {
        get => _poolSize;
        set
        {
            Set(ref _poolSize, value);
            OnPropertyChanged(nameof(HasPoolWarning));
            OnPropertyChanged(nameof(PoolWarningText));
        }
    }

    public bool HasPoolWarning => PoolSize > 0 && QuestionCount > PoolSize;
    public string PoolWarningText => HasPoolWarning ? $"Only {PoolSize} questions available in bank!" : string.Empty;

    public List<int> GetSelectedYears() => AvailableYears.Where(y => y.IsSelected).Select(y => y.Year).ToList();

    public RelayCommand RemoveCommand => new(() => _onRemove(this));

    public RelayCommand<YearToggle> ToggleYearCommand => new(y =>
    {
        if (y is null) return;
        y.IsSelected = !y.IsSelected;
        RecalcPool();
    });

    public RelayCommand SelectAllYearsCommand => new(() =>
    {
        foreach (var y in AvailableYears) y.IsSelected = true;
        RecalcPool();
    });

    public RelayCommand DeselectAllYearsCommand => new(() =>
    {
        foreach (var y in AvailableYears) y.IsSelected = false;
        RecalcPool();
    });

    private async Task LoadYearsAsync()
    {
        AvailableYears.Clear();
        _bankQuestions = [];
        PoolSize = 0;
        if (string.IsNullOrWhiteSpace(SelectedSubject)) return;

        // Load all questions for this subject to get accurate counts
        var questions = await _api.GetQuestionBankAsync(SelectedSubject);
        _bankQuestions = questions ?? [];

        var yearGroups = _bankQuestions.GroupBy(q => q.Year).OrderByDescending(g => g.Key);
        foreach (var g in yearGroups)
            AvailableYears.Add(new YearToggle(g.Key, true, g.Count()));
        RecalcPool();
    }

    private void RecalcPool()
    {
        var selectedYears = GetSelectedYears();
        PoolSize = _bankQuestions.Count(q => selectedYears.Contains(q.Year));
        _onChanged();
    }
}

public class YearToggle : BaseViewModel
{
    public int Year { get; }
    public int QuestionCount { get; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public string Label => QuestionCount > 0 ? $"{Year} ({QuestionCount})" : $"{Year}";
    public YearToggle(int year, bool selected, int questionCount = 0) { Year = year; _isSelected = selected; QuestionCount = questionCount; }
}

// ─── Exams list (Template builder) ────────────────────────────────────────────
public class ExamsViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<ExamDto> Exams { get; } = [];
    private List<ExamDto> _all = [];

    private string _search = string.Empty;
    public string Search
    {
        get => _search;
        set { Set(ref _search, value); FilterExams(); }
    }

    private ObservableCollection<ExamDto> _filtered = [];
    public ObservableCollection<ExamDto> Filtered { get => _filtered; set => Set(ref _filtered, value); }

    // ── Wizard state ──
    private bool _showCreateForm;
    public bool ShowCreateForm { get => _showCreateForm; set => Set(ref _showCreateForm, value); }

    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            Set(ref _currentStep, value);
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(NextButtonText));
        }
    }

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < 3;
    public string NextButtonText => CurrentStep == 3 ? (IsEditing ? "Update Template" : "Create Template") : "Next";

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set { Set(ref _isEditing, value); OnPropertyChanged(nameof(FormTitle)); OnPropertyChanged(nameof(NextButtonText)); }
    }

    private ExamDto? _selectedExam;
    public ExamDto? SelectedExam { get => _selectedExam; set => Set(ref _selectedExam, value); }

    public string FormTitle => IsEditing ? "Edit Exam Template" : "New Exam Template";

    // ── Step 1: General Info ──
    private string _title = string.Empty;
    public string Title { get => _title; set => Set(ref _title, value); }

    private string _category = string.Empty;
    public string Category { get => _category; set => Set(ref _category, value); }

    private int _duration = 60;
    public int Duration { get => _duration; set => Set(ref _duration, value); }

    private string _accessPassword = string.Empty;
    public string AccessPassword { get => _accessPassword; set => Set(ref _accessPassword, value); }

    // Duration presets
    public RelayCommand SetDuration30 => new(() => Duration = 30);
    public RelayCommand SetDuration45 => new(() => Duration = 45);
    public RelayCommand SetDuration60 => new(() => Duration = 60);
    public RelayCommand SetDuration90 => new(() => Duration = 90);
    public RelayCommand SetDuration120 => new(() => Duration = 120);

    // ── Step 2: Subjects ──
    public ObservableCollection<string> AvailableSubjects { get; } = [];
    public ObservableCollection<ExamSubjectConfigVM> SubjectConfigs { get; } = [];

    public bool CanAddSubject => SubjectConfigs.Count < 4;

    // ── Step 3: Settings & Review ──
    private bool _shuffleQ = true, _shuffleO = true;
    public bool ShuffleQuestions { get => _shuffleQ; set => Set(ref _shuffleQ, value); }
    public bool ShuffleOptions { get => _shuffleO; set => Set(ref _shuffleO, value); }

    private string _createStatus = string.Empty;
    public string CreateStatus { get => _createStatus; set => Set(ref _createStatus, value); }

    private bool _statusOk;
    public bool StatusOk { get => _statusOk; set => Set(ref _statusOk, value); }

    // Dynamic totals — updated via callback from each ExamSubjectConfigVM
    public int TotalQuestions => SubjectConfigs.Sum(s => s.QuestionCount);
    public int TotalSubjects => SubjectConfigs.Count;
    public int TotalPoolSize => SubjectConfigs.Sum(s => s.PoolSize);
    public bool HasAnyPoolWarning => SubjectConfigs.Any(s => s.HasPoolWarning);

    // ── Commands ──
    public RelayCommand RefreshCommand => new(async () => await LoadAsync());
    public RelayCommand ToggleCreateCommand => new(() =>
    {
        IsEditing = false;
        ShowCreateForm = !ShowCreateForm;
        CreateStatus = string.Empty;
        if (ShowCreateForm) ClearForm();
    });
    public RelayCommand SaveCommand => new(async () => await SaveTemplateAsync());
    public RelayCommand NextStepCommand => new(() =>
    {
        if (CurrentStep == 3) { _ = SaveTemplateAsync(); return; }
        // Validate before advancing
        if (CurrentStep == 1 && string.IsNullOrWhiteSpace(Title))
        { CreateStatus = "Exam name is required."; StatusOk = false; return; }
        if (CurrentStep == 2 && SubjectConfigs.Count == 0)
        { CreateStatus = "Add at least one subject."; StatusOk = false; return; }
        CreateStatus = string.Empty;
        CurrentStep++;
    });
    public RelayCommand PrevStepCommand => new(() => { if (CurrentStep > 1) CurrentStep--; });
    public RelayCommand<object> BatchAddSubjectsCommand => new(items =>
    {
        if (items is string single)
        {
            if (SubjectConfigs.Count < 4 && !SubjectConfigs.Any(s => s.SelectedSubject == single))
            {
                var row = new ExamSubjectConfigVM(api, AvailableSubjects, RemoveSubjectRow, NotifySummary);
                row.SelectedSubject = single;
                SubjectConfigs.Add(row);
                NotifySummary();
            }
        }
        else if (items is System.Collections.IList list)
        {
            foreach (var item in list)
            {
                if (item is string subject && SubjectConfigs.Count < 4)
                {
                    if (SubjectConfigs.Any(s => s.SelectedSubject == subject)) continue;
                    var row = new ExamSubjectConfigVM(api, AvailableSubjects, RemoveSubjectRow, NotifySummary);
                    row.SelectedSubject = subject;
                    SubjectConfigs.Add(row);
                }
            }
            NotifySummary();
        }
    });

    public RelayCommand AddSubjectCommand => new(() => AddSubjectRow());
    public RelayCommand<ExamDto> EditCommand => new(e => StartEdit(e));
    public RelayCommand<ExamDto> DeleteCommand => new(async e => await DeleteAsync(e));

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            // Load subjects from question bank
            var subjects = await api.GetQuestionBankSubjectsAsync();
            AvailableSubjects.Clear();
            subjects?.ForEach(AvailableSubjects.Add);

            // Load exams
            var list = await api.GetExamsAsync();
            _all = list ?? [];
            Exams.Clear();
            _all.ForEach(Exams.Add);
            FilterExams();
        }
        catch (Exception ex)
        {
            App.Log("Error loading exam templates", ex);
        }
        finally { IsBusy = false; }
    }

    private void AddSubjectRow()
    {
        if (SubjectConfigs.Count >= 4) return;
        SubjectConfigs.Add(new ExamSubjectConfigVM(api, AvailableSubjects, RemoveSubjectRow, NotifySummary));
        NotifySummary();
    }

    private void RemoveSubjectRow(ExamSubjectConfigVM row)
    {
        SubjectConfigs.Remove(row);
        NotifySummary();
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(CanAddSubject));
        OnPropertyChanged(nameof(TotalSubjects));
        OnPropertyChanged(nameof(TotalQuestions));
        OnPropertyChanged(nameof(TotalPoolSize));
        OnPropertyChanged(nameof(HasAnyPoolWarning));
    }

    private async Task SaveTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            CreateStatus = "Exam name is required."; StatusOk = false; CurrentStep = 1; return;
        }
        if (SubjectConfigs.Count == 0)
        {
            CreateStatus = "Add at least one subject."; StatusOk = false; CurrentStep = 2; return;
        }

        // Validate each subject config
        foreach (var cfg in SubjectConfigs)
        {
            if (string.IsNullOrWhiteSpace(cfg.SelectedSubject))
            {
                CreateStatus = "All subjects must be selected."; StatusOk = false; CurrentStep = 2; return;
            }
            if (cfg.GetSelectedYears().Count == 0)
            {
                CreateStatus = $"Select at least one year for {cfg.SelectedSubject}."; StatusOk = false; CurrentStep = 2; return;
            }
            if (cfg.QuestionCount <= 0)
            {
                CreateStatus = $"Question count must be > 0 for {cfg.SelectedSubject}."; StatusOk = false; CurrentStep = 2; return;
            }
            if (cfg.HasPoolWarning)
            {
                CreateStatus = $"'{cfg.SelectedSubject}' requests {cfg.QuestionCount} but only {cfg.PoolSize} available.";
                StatusOk = false; CurrentStep = 2; return;
            }
        }

        IsBusy = true;
        BusyMessage = IsEditing ? "Updating template..." : "Creating template...";
        try
        {
            if (IsEditing && SelectedExam != null)
            {
                await api.DeleteExamAsync(SelectedExam.Id);
            }

            // Build the title with category prefix if provided
            var fullTitle = !string.IsNullOrWhiteSpace(Category) ? $"[{Category}] {Title}" : Title;

            var subjectDtos = SubjectConfigs.Select(s => new QuestionBankSubjectYearDto(
                s.SelectedSubject,
                s.GetSelectedYears(),
                s.QuestionCount
            )).ToList();

            var dto = new ExamGenerateDto(fullTitle, Duration, ShuffleQuestions, ShuffleOptions, AccessPassword, subjectDtos);
            var resp = await api.GenerateExamFromBankAsync(dto);

            if (resp.IsSuccessStatusCode)
            {
                CreateStatus = IsEditing ? "Template updated!" : "Template created!";
                StatusOk = true;
                ClearForm();
                ShowCreateForm = false;
                IsEditing = false;
                await LoadAsync();
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                CreateStatus = $"Failed: {body}"; StatusOk = false;
            }
        }
        catch (Exception ex)
        {
            CreateStatus = $"Error: {ex.Message}"; StatusOk = false;
        }
        finally { IsBusy = false; }
    }

    private void StartEdit(ExamDto? e)
    {
        if (e is null) return;
        SelectedExam = e;

        // Parse category from title if formatted as [Category] Title
        var title = e.Title;
        if (title.StartsWith('[') && title.Contains(']'))
        {
            var idx = title.IndexOf(']');
            Category = title[1..idx].Trim();
            Title = title[(idx + 1)..].Trim();
        }
        else
        {
            Category = string.Empty;
            Title = title;
        }

        Duration = e.DurationMinutes;
        ShuffleQuestions = e.ShuffleQuestions;
        ShuffleOptions = e.ShuffleOptions;
        AccessPassword = e.AccessPassword;

        SubjectConfigs.Clear();
        var subjects = e.Subject.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var sub in subjects)
        {
            var row = new ExamSubjectConfigVM(api, AvailableSubjects, RemoveSubjectRow, NotifySummary);
            row.SelectedSubject = sub;
            SubjectConfigs.Add(row);
        }

        IsEditing = true;
        ShowCreateForm = true;
        CurrentStep = 1;
        NotifySummary();
    }

    private void ClearForm()
    {
        Title = string.Empty;
        Category = string.Empty;
        Duration = 60;
        ShuffleQuestions = true;
        ShuffleOptions = true;
        AccessPassword = string.Empty;
        SelectedExam = null;
        SubjectConfigs.Clear();
        CreateStatus = string.Empty;
        CurrentStep = 1;
        NotifySummary();
    }

    private void FilterExams()
    {
        var q = Search.Trim();
        var list = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(e => e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Subject.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Filtered.Clear();
        foreach (var e in list) Filtered.Add(e);
    }

    private async Task DeleteAsync(ExamDto? exam)
    {
        if (exam is null) return;
        var res = MessageBox.Show($"Are you sure you want to delete '{exam.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res == MessageBoxResult.Yes)
        {
            await api.DeleteExamAsync(exam.Id);
            await LoadAsync();
        }
    }
}

// ─── Monitor ─────────────────────────────────────────────────────────────────
public class SessionViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<ExamDto> Exams { get; } = [];
    public ObservableCollection<SessionDto> Sessions { get; } = [];
    public ObservableCollection<SessionDto> ActiveSessions { get; } = [];

    private ExamDto? _selectedExam;
    public ExamDto? SelectedExam { get => _selectedExam; set => Set(ref _selectedExam, value); }

    public bool HasActiveSessions => ActiveSessions.Count > 0;

    public string GetJoinUrl(SessionDto session) => $"{api.BaseUrl}?code={session.SessionCode}";

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());
    public RelayCommand StartCommand => new(async () => await StartAsync());
    public RelayCommand<SessionDto> StopSessionCommand => new(async s => await StopSessionAsync(s));
    public RelayCommand EndAllCommand => new(async () => await EndAllAsync());
    public RelayCommand<SessionDto> CopyJoinUrlCommand => new(s =>
    {
        if (s is not null)
            System.Windows.Clipboard.SetText(GetJoinUrl(s));
    });

    public async Task LoadAsync()
    {
        var exams = await api.GetExamsAsync();
        Exams.Clear();
        exams?.ForEach(Exams.Add);
        SelectedExam ??= Exams.FirstOrDefault();

        var sessions = await api.GetSessionsAsync();
        Sessions.Clear();
        sessions?.ForEach(Sessions.Add);
        
        ActiveSessions.Clear();
        foreach (var s in Sessions.Where(s => s.IsActive))
            ActiveSessions.Add(s);
        OnPropertyChanged(nameof(HasActiveSessions));
    }

    private async Task StartAsync()
    {
        if (SelectedExam is null) return;
        await api.StartSessionAsync(SelectedExam.Id);
        await LoadAsync();
    }

    private async Task StopSessionAsync(SessionDto? session)
    {
        if (session is null) return;
        var res = MessageBox.Show(
            $"End session '{session.ExamTitle}' (Code: {session.SessionCode})?\n\nAll unsubmitted students will be auto-submitted.",
            "End Session", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res == MessageBoxResult.Yes)
        {
            await api.StopSessionAsync(session.Id);
            await LoadAsync();
        }
    }

    private async Task EndAllAsync()
    {
        if (ActiveSessions.Count == 0) return;
        var res = MessageBox.Show(
            $"End ALL {ActiveSessions.Count} active session(s)?\n\nAll unsubmitted students will be auto-submitted.",
            "End All Sessions", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res == MessageBoxResult.Yes)
        {
            await api.EndAllSessionsAsync();
            await LoadAsync();
        }
    }
}

public class MonitorViewModel(ApiClient api, MonitorRealtimeService realtime) : BaseViewModel, IRefreshable
{
    public ObservableCollection<StudentStatusDto> Students { get; } = [];

    private string _sessionInfo = "No active session";
    public string SessionInfo { get => _sessionInfo; set => Set(ref _sessionInfo, value); }

    private int _sessionId;
    private System.Timers.Timer? _autoRefresh;
    private string _sessionCode = string.Empty;

    public bool AutoRefresh { get; set; } = true;

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        var sessions = await api.GetSessionsAsync();
        var active = sessions?.FirstOrDefault(s => s.IsActive);
        if (active is null)
        {
            Students.Clear();
            SessionInfo = "No active session";
            StopAutoRefresh();
            await realtime.DisconnectAsync();
            return;
        }
        _sessionId  = active.Id;
        _sessionCode = active.SessionCode;
        SessionInfo = $"{active.ExamTitle}  ·  Code: {active.SessionCode}";
        var list = await api.GetStudentsAsync(_sessionId);
        Students.Clear();
        list?.ForEach(Students.Add);
        await ConnectRealtimeAsync();
        StartAutoRefresh();
    }

    private async Task ConnectRealtimeAsync()
    {
        realtime.StudentUpdated -= RealtimeOnStudentUpdated;
        realtime.StudentUpdated += RealtimeOnStudentUpdated;
        await realtime.ConnectAsync(api.BaseUrl, _sessionCode);
    }

    private void RealtimeOnStudentUpdated(IReadOnlyList<StudentStatusDto> payload)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Students.Clear();
            foreach (var row in payload)
                Students.Add(row);
        });
    }

    private void StartAutoRefresh()
    {
        if (_autoRefresh is not null) return;
        _autoRefresh = new System.Timers.Timer(5000);
        _autoRefresh.Elapsed += async (_, _) =>
        {
            if (!AutoRefresh) return;
            var list = await api.GetStudentsAsync(_sessionId);
            App.Current.Dispatcher.Invoke(() =>
            {
                Students.Clear();
                list?.ForEach(Students.Add);
            });
        };
        _autoRefresh.Start();
    }

    private void StopAutoRefresh()
    {
        _autoRefresh?.Stop();
        _autoRefresh?.Dispose();
        _autoRefresh = null;
    }
}

// ─── Devices ─────────────────────────────────────────────────────────────────
public class DevicesViewModel : BaseViewModel, IRefreshable
{
    private readonly ApiClient api;
    private System.Timers.Timer? _refreshTimer;

    public ObservableCollection<DeviceRow> Devices { get; } = [];

    private string _sessionInfo = string.Empty;
    public string SessionInfo { get => _sessionInfo; set => Set(ref _sessionInfo, value); }

    private int _total, _online;
    public int Total  { get => _total;  set => Set(ref _total,  value); }
    public int Online { get => _online; set => Set(ref _online, value); }

    public DevicesViewModel(ApiClient api)
    {
        this.api = api;
        _refreshTimer = new System.Timers.Timer(5000);
        _refreshTimer.Elapsed += async (_, _) => await LoadAsync();
        _refreshTimer.Start();
    }

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        try
        {
            var sessions = await api.GetSessionsAsync();
            var active = sessions?.FirstOrDefault(s => s.IsActive);
            if (active is null) { 
                App.Current.Dispatcher.Invoke(() => { Devices.Clear(); SessionInfo = "No active session"; Total = Online = 0; });
                return; 
            }

            App.Current.Dispatcher.Invoke(() => SessionInfo = $"{active.ExamTitle}  ·  Code: {active.SessionCode}");
            var students = await api.GetStudentsAsync(active.Id);
            if (students is null) return;

            App.Current.Dispatcher.Invoke(() => {
                Devices.Clear();
                foreach (var s in students)
                {
                    Devices.Add(new DeviceRow(
                        s.FullName, s.StudentId,
                        s.JoinedAt.ToLocalTime().ToString("HH:mm:ss"),
                        s.IsSubmitted ? "Submitted" : s.ConnectionState,
                        s.TabSwitchCount,
                        s.BatteryLevel, s.IsOnline, s.DeviceName));
                }
                Total = students.Count;
                Online = students.Count(s => !s.IsSubmitted && s.IsOnline);
            });
        }
        catch { /* ignore */ }
    }
}

public record ExamSubjectConfig(string Subject, List<int> Years, int QuestionCount);

public record DeviceRow(string Name, string StudentId, string JoinedAt, string Status, int TabSwitches, int Battery, bool Online, string DeviceName = "");

public record QuestionBankRow(int Serial, int Id, string Subject, int Year, int QuestionNumber, string Preview);

public record StudentRow(int Serial, int Id, string FullName, string StudentId, bool IsActive);

// ─── Questions Management ────────────────────────────────────────────────────
public class QuestionsViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<QuestionBankRow> Questions { get; } = [];
    private List<QuestionBankDto> _all = [];

    private QuestionBankDto? _selected;
    public QuestionBankDto? Selected
    {
        get => _selected;
        set { Set(ref _selected, value); OnPropertyChanged(nameof(IsEditing)); }
    }
    public bool IsEditing => Selected is not null;

    private string _search = string.Empty;
    public string Search { get => _search; set { Set(ref _search, value); ApplyFilter(); } }

    public ObservableCollection<string> SubjectFilters { get; } = ["All subjects"];
    private string _selectedSubject = "All subjects";
    public string SelectedSubject 
    { 
        get => _selectedSubject; 
        set 
        { 
            if (Set(ref _selectedSubject, value))
            {
                ApplyFilter();
                // Auto-fill the subject field in the form if it's a new question and a subject is selected
                if (!IsEditing && value != "All subjects")
                {
                    Subject = value;
                }
            }
        } 
    }

    public ObservableCollection<string> YearFilters { get; } = ["All years"];
    private string _selectedYear = "All years";
    public string SelectedYear { get => _selectedYear; set { Set(ref _selectedYear, value); ApplyFilter(); } }

    private string _questionText = string.Empty;
    public string QuestionText { get => _questionText; set => Set(ref _questionText, value); }

    private string _optionA = string.Empty;
    public string OptionA { get => _optionA; set => Set(ref _optionA, value); }

    private string _optionB = string.Empty;
    public string OptionB { get => _optionB; set => Set(ref _optionB, value); }

    private string _optionC = string.Empty;
    public string OptionC { get => _optionC; set => Set(ref _optionC, value); }

    private string _optionD = string.Empty;
    public string OptionD { get => _optionD; set => Set(ref _optionD, value); }

    private string _subject = string.Empty;
    public string Subject { get => _subject; set => Set(ref _subject, value); }

    private int _year = DateTime.Now.Year;
    public int Year { get => _year; set => Set(ref _year, value); }

    private string _correctAnswer = "A";
    public string CorrectAnswer { get => _correctAnswer; set => Set(ref _correctAnswer, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }

    private bool _statusOk;
    public bool StatusOk { get => _statusOk; set => Set(ref _statusOk, value); }

    private string _jsonImport = string.Empty;
    public string JsonImport { get => _jsonImport; set => Set(ref _jsonImport, value); }

    public string BulkJsonTemplate => @"[
  {
    ""subject"": ""Mathematics"",
    ""year"": 2024,
    ""questionNumber"": 1,
    ""text"": ""Solve for x: 2x + 5 = 15"",
    ""options"": [""5"", ""10"", ""7"", ""15""],
    ""correctAnswer"": ""5""
  }
]";

    private readonly HashSet<string> _localSubjects = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<string> Subjects { get; } = [];

    private bool _isAddingSubject;
    public bool IsAddingSubject { get => _isAddingSubject; set => Set(ref _isAddingSubject, value); }

    private string _newSubjectName = string.Empty;
    public string NewSubjectName { get => _newSubjectName; set => Set(ref _newSubjectName, value); }

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());
    public RelayCommand SaveCommand => new(async () => await SaveAsync());
    public RelayCommand DeleteCommand => new(async () => await DeleteAsync());
    public RelayCommand ClearCommand => new(Clear);
    public RelayCommand<QuestionBankRow> PickCommand => new(q => { if (q != null) Pick(_all.FirstOrDefault(x => x.Id == q.Id)); });
    public RelayCommand ImportJsonCommand => new(async () => await ImportJsonAsync());
    public RelayCommand CopyTemplateCommand => new(() => { Clipboard.SetText(BulkJsonTemplate); Status = "Template copied to clipboard!"; StatusOk = true; });
    public RelayCommand AddSubjectCommand => new(() => { IsAddingSubject = true; NewSubjectName = string.Empty; });
    
    public RelayCommand ConfirmAddSubjectCommand => new(() => {
        if (!string.IsNullOrWhiteSpace(NewSubjectName)) {
            var name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(NewSubjectName.Trim().ToLower());
            _localSubjects.Add(name);
            if (!Subjects.Contains(name)) Subjects.Add(name);
            if (!SubjectFilters.Contains(name)) SubjectFilters.Add(name);
            Subject = name;
        }
        IsAddingSubject = false;
    });

    public RelayCommand CancelAddSubjectCommand => new(() => IsAddingSubject = false);

    public RelayCommand BrowseFileCommand => new(async () => {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Question Files (*.csv, *.json)|*.csv;*.json|CSV UTF-8 (*.csv)|*.csv|JSON File (*.json)|*.json"
        };
        if (ofd.ShowDialog() == true)
        {
            if (ofd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                await ParseAndImportJsonFileAsync(ofd.FileName);
            else
                await ParseAndImportCsvAsync(ofd.FileName);
        }
    });

    public RelayCommand DownloadTemplateCommand => new(() => {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV UTF-8 (Comma delimited) (*.csv)|*.csv|JSON File (*.json)|*.json",
            FileName = "jamb_questions_template",
            DefaultExt = ".csv"
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                if (sfd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(sfd.FileName, BulkJsonTemplate);
                }
                else
                {
                    var csvContent = "Subject,Year,QuestionNumber,QuestionText,OptionA,OptionB,OptionC,OptionD,CorrectAnswer\n" +
                                     "Mathematics,2024,1,\"Solve for x: 2x + 5 = 15\",5,10,7,15,5\n" +
                                     "English Language,2024,2,\"Choose the option opposite in meaning to 'generous'.\",Miserly,Kind,Happy,Sad,Miserly";
                    File.WriteAllText(sfd.FileName, csvContent, System.Text.Encoding.UTF8);
                }
                Status = "Template downloaded successfully!";
                StatusOk = true;
            }
            catch (Exception ex)
            {
                Status = $"Error saving template: {ex.Message}";
                StatusOk = false;
            }
        }
    });

    public async Task ParseAndImportCsvAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);
            if (lines.Length <= 1)
            {
                Status = "CSV file is empty or only contains headers.";
                StatusOk = false;
                return;
            }

            var questionsList = new List<QuestionBankCreateDto>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = ParseCsvLine(line);
                if (parts.Count < 9) continue;

                var subject = parts[0].Trim();
                int.TryParse(parts[1], out int year);
                int.TryParse(parts[2], out int qNum);
                var text = parts[3].Trim();
                var optA = parts[4].Trim();
                var optB = parts[5].Trim();
                var optC = parts[6].Trim();
                var optD = parts[7].Trim();
                var correct = parts[8].Trim();

                if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(subject)) continue;

                var options = new List<string> { optA, optB, optC, optD };
                questionsList.Add(new QuestionBankCreateDto(subject, year, qNum, text, options, correct));
            }

            if (questionsList.Count == 0)
            {
                Status = "No valid questions found in CSV file.";
                StatusOk = false;
                return;
            }

            var standardizedList = questionsList.Select(q => q with { 
                Subject = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.Subject?.Trim().ToLower() ?? "Unknown") 
            }).ToList();

            var resp = await api.ImportQuestionBankAsync(standardizedList);
            if (resp.IsSuccessStatusCode)
            {
                Status = $"{standardizedList.Count} questions imported successfully from CSV.";
                StatusOk = true;
                await LoadAsync();
            }
            else
            {
                Status = "Failed to import questions from CSV.";
                StatusOk = false;
            }
        }
        catch (Exception ex)
        {
            Status = $"Error parsing CSV: {ex.Message}";
            StatusOk = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    public async Task ParseAndImportJsonFileAsync(string filePath)
    {
        IsBusy = true;
        try
        {
            var content = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<QuestionBankCreateDto>>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (list == null || list.Count == 0)
            {
                Status = "No questions found in JSON."; StatusOk = false; return;
            }

            var standardizedList = list.Select(q => q with { 
                Subject = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.Subject?.Trim().ToLower() ?? "Unknown") 
            }).ToList();

            var resp = await api.ImportQuestionBankAsync(standardizedList);
            if (resp.IsSuccessStatusCode)
            {
                Status = $"{list.Count} questions imported successfully from JSON.";
                StatusOk = true;
                await LoadAsync();
            }
            else
            {
                Status = "Failed to import questions from JSON."; StatusOk = false;
            }
        }
        catch (Exception ex)
        {
            Status = $"Error parsing JSON: {ex.Message}"; StatusOk = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var list = await api.GetQuestionBankAsync();
            _all = list ?? [];
            
            var dbSubjects = await api.GetQuestionBankSubjectsAsync() ?? [];
            var mergedSubjects = dbSubjects
                .Concat(_localSubjects)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            Subjects.Clear();
            mergedSubjects.ForEach(Subjects.Add);

            RebuildFilters();
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    private void RebuildFilters()
    {
        var prevSub = SelectedSubject;
        var prevYear = SelectedYear;

        SubjectFilters.Clear();
        SubjectFilters.Add("All subjects");
        
        var distinctDbSubjects = _all.Select(x => x.Subject);
        var mergedSubjects = distinctDbSubjects
            .Concat(_localSubjects)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var s in mergedSubjects)
            SubjectFilters.Add(s);

        YearFilters.Clear();
        YearFilters.Add("All years");
        foreach (var y in _all.Select(x => x.Year).Distinct().OrderByDescending(x => x))
            YearFilters.Add(y.ToString());

        SelectedSubject = SubjectFilters.Contains(prevSub) ? prevSub : "All subjects";
        SelectedYear = YearFilters.Contains(prevYear) ? prevYear : "All years";
    }

    private void ApplyFilter()
    {
        var searchQuery = Search.Trim();
        var result = _all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            result = result.Where(qb => qb.Text.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                      qb.Subject.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedSubject != "All subjects")
        {
            result = result.Where(qb => qb.Subject.Equals(SelectedSubject, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedYear != "All years" && int.TryParse(SelectedYear, out int yearValue))
        {
            result = result.Where(qb => qb.Year == yearValue);
        }

        Questions.Clear();
        int i = 1;
        foreach (var question in result.OrderBy(qb => qb.Subject).ThenBy(qb => qb.Year).ThenBy(qb => qb.QuestionNumber))
            Questions.Add(new QuestionBankRow(i++, question.Id, question.Subject, question.Year, question.QuestionNumber, question.Text.Substring(0, Math.Min(100, question.Text.Length)) + "..."));
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionText)) { Status = "Question text is required."; StatusOk = false; return; }
        if (string.IsNullOrWhiteSpace(Subject)) { Status = "Subject is required."; StatusOk = false; return; }
        if (string.IsNullOrWhiteSpace(OptionA) || string.IsNullOrWhiteSpace(OptionB) || 
            string.IsNullOrWhiteSpace(OptionC) || string.IsNullOrWhiteSpace(OptionD))
        { Status = "All four options are required."; StatusOk = false; return; }

        IsBusy = true;
        try
        {
            // Standardize subject name to Title Case
            var rawSubject = (Subject ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawSubject))
            {
                Status = "Subject is required."; StatusOk = false; return;
            }
            var standardizedSubject = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawSubject.ToLower());
            Subject = standardizedSubject;

            var options = new List<string> { OptionA, OptionB, OptionC, OptionD };
            var dto = new QuestionBankCreateDto(standardizedSubject, Year, 0, QuestionText, options, CorrectAnswer);

            HttpResponseMessage resp;
            if (Selected is not null)
                resp = await api.UpdateQuestionBankAsync(Selected.Id, dto);
            else
                resp = await api.AddQuestionBankAsync(dto);

            if (resp.IsSuccessStatusCode)
            {
                Status = "Question saved successfully."; StatusOk = true;
                Clear();
                await LoadAsync();
            }
            else
            {
                Status = "Failed to save question."; StatusOk = false;
            }
        }
        finally { IsBusy = false; }
    }

    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        var resp = await api.DeleteQuestionBankAsync(Selected.Id);
        if (resp.IsSuccessStatusCode)
        {
            Status = "Question deleted successfully."; StatusOk = true;
            Clear();
            await LoadAsync();
        }
        else
        {
            Status = "Failed to delete question."; StatusOk = false;
        }
    }

    private void Pick(QuestionBankDto? q)
    {
        if (q is null) return;
        Selected = q;
        QuestionText = q.Text;
        Subject = q.Subject;
        Year = q.Year;
        
        try
        {
            var options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson);
            if (options != null && options.Count >= 4)
            {
                OptionA = options[0];
                OptionB = options[1];
                OptionC = options[2];
                OptionD = options[3];
            }
        }
        catch { /* Fallback */ }
        
        CorrectAnswer = q.CorrectAnswer;
    }

    private async Task ImportJsonAsync()
    {
        if (string.IsNullOrWhiteSpace(JsonImport)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<QuestionBankCreateDto>>(JsonImport, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (list == null || list.Count == 0)
            {
                Status = "No questions found in JSON."; StatusOk = false; return;
            }

            // Standardize subject names on import
            foreach (var q in list)
            {
                if (!string.IsNullOrWhiteSpace(q.Subject))
                {
                    // Using a private field or logic to not trigger property changes repeatedly if many
                    var std = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.Subject.Trim().ToLower());
                    // Since record, we can't easily modify properties if they are init-only, 
                    // but QuestionBankCreateDto is likely a record with positional properties.
                    // Let's assume we can't modify it directly if it's a record, so we'll map to new objects if needed.
                }
            }
            
            // Re-map to ensure standardized subjects
            var standardizedList = list.Select(q => q with { 
                Subject = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(q.Subject?.Trim().ToLower() ?? "Unknown") 
            }).ToList();

            var resp = await api.ImportQuestionBankAsync(standardizedList);
            if (resp.IsSuccessStatusCode)
            {
                Status = $"{list.Count} questions imported successfully."; StatusOk = true;
                JsonImport = string.Empty;
                await LoadAsync();
            }
            else
            {
                Status = "Failed to import questions."; StatusOk = false;
            }
        }
        catch
        {
            Status = "Invalid JSON format."; StatusOk = false;
        }
    }

    private void Clear()
    {
        Selected = null;
        QuestionText = OptionA = OptionB = OptionC = OptionD = Subject = string.Empty;
        Year = DateTime.Now.Year;
        CorrectAnswer = "A";
        Status = string.Empty;
    }
}

// ─── Results ─────────────────────────────────────────────────────────────────
public class ResultsViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<SessionDto> Sessions { get; } = [];
    public ObservableCollection<ResultDto>  Results  { get; } = [];

    private SessionDto? _selectedSession;
    public SessionDto? SelectedSession
    {
        get => _selectedSession;
        set { Set(ref _selectedSession, value); _ = LoadResultsAsync(); }
    }

    private double _avgScore;
    private int _passCount, _failCount;
    public double AvgScore   { get => _avgScore;   set => Set(ref _avgScore,   value); }
    public int    PassCount  { get => _passCount;  set => Set(ref _passCount,  value); }
    public int    FailCount  { get => _failCount;  set => Set(ref _failCount,  value); }

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        var sessions = await api.GetSessionsAsync();
        Sessions.Clear();
        sessions?.ForEach(Sessions.Add);
    }

    private async Task LoadResultsAsync()
    {
        if (SelectedSession is null) return;
        var list = await api.GetResultsAsync(SelectedSession.Id);
        Results.Clear();
        list?.ForEach(Results.Add);
        AvgScore  = Results.Count == 0 ? 0 : Math.Round(Results.Average(r => r.Percentage), 1);
        PassCount = Results.Count(r => r.Percentage >= 50);
        FailCount = Results.Count(r => r.Percentage < 50);
    }
}

// ─── Reports ─────────────────────────────────────────────────────────────────
public class ReportsViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<ReportRow> Rows { get; } = [];

    private int _totalExams, _totalSessions, _totalStudents;
    public int TotalExams    { get => _totalExams;    set => Set(ref _totalExams,    value); }
    public int TotalSessions { get => _totalSessions; set => Set(ref _totalSessions, value); }
    public int TotalStudents { get => _totalStudents; set => Set(ref _totalStudents, value); }

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        var exams    = await api.GetExamsAsync();
        var sessions = await api.GetSessionsAsync();
        TotalExams    = exams?.Count ?? 0;
        TotalSessions = sessions?.Count ?? 0;

        Rows.Clear();
        if (sessions is null) return;

        int grandTotal = 0;
        foreach (var s in sessions)
        {
            var results = await api.GetResultsAsync(s.Id);
            if (results is null || results.Count == 0) continue;
            grandTotal += results.Count;
            Rows.Add(new ReportRow(
                s.ExamTitle, s.SessionCode,
                s.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                results.Count,
                Math.Round(results.Average(r => r.Percentage), 1),
                results.Count(r => r.Percentage >= 50),
                results.Count(r => r.Percentage < 50)));
        }
        TotalStudents = grandTotal;
    }
}

public record ReportRow(string ExamTitle, string SessionCode, string Date,
    int Students, double AvgPct, int Passed, int Failed);

// ─── Search Results ─────────────────────────────────────────────────────────────
public class SearchResultsViewModel : BaseViewModel
{
    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set => Set(ref _searchQuery, value); }

    private ObservableCollection<SearchSuggestion> _suggestions = [];
    public ObservableCollection<SearchSuggestion> Suggestions { get => _suggestions; set => Set(ref _suggestions, value); }

    private ObservableCollection<SearchResult> _searchResults = [];
    public ObservableCollection<SearchResult> SearchResults { get => _searchResults; set => Set(ref _searchResults, value); }

    public bool HasSuggestions => Suggestions.Count > 0;
    public bool HasNoResults => SearchResults.Count == 0 && !string.IsNullOrWhiteSpace(SearchQuery);

    public RelayCommand CloseCommand => new(() => { 
        // Navigate back to previous page or dashboard
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute("Dashboard");
        }
    });
    
    public RelayCommand NavigateToExamSettingsCommand => new(() => { 
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute("CreateExam");
        }
    });
    
    public RelayCommand NavigateToThemeSettingsCommand => new(() => { 
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute("Settings");
        }
    });
    
    public RelayCommand NavigateToErrorGuideCommand => new(() => { 
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute("ErrorGuide");
        }
    });
    
    public RelayCommand NavigateToSettingsCommand => new(() => { 
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute("Settings");
        }
    });
    
    public RelayCommand<SearchSuggestion> NavigateToSuggestionCommand => new(suggestion => { 
        // Handle suggestion navigation based on title
        if (suggestion?.Title == null) return;
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            switch (suggestion.Title.ToLower())
            {
                case "exam settings":
                case "create new exam":
                    mainViewModel.NavigateCommand.Execute("CreateExam");
                    break;
                case "theme settings":
                case "dark mode":
                    mainViewModel.NavigateCommand.Execute("Settings");
                    break;
                case "error guide":
                case "troubleshooting":
                    mainViewModel.NavigateCommand.Execute("ErrorGuide");
                    break;
                case "settings":
                case "general settings":
                    mainViewModel.NavigateCommand.Execute("Settings");
                    break;
                case "exam history":
                case "exam list":
                    mainViewModel.NavigateCommand.Execute("Exams");
                    break;
                case "student management":
                    mainViewModel.NavigateCommand.Execute("Students");
                    break;
                case "student reports":
                    mainViewModel.NavigateCommand.Execute("Results");
                    break;
                case "question bank":
                case "search questions":
                    mainViewModel.NavigateCommand.Execute("Questions");
                    break;
            }
        }
    });
    
    public RelayCommand<SearchResult> NavigateToResultCommand => new(result => { 
        // Handle result navigation based on title
        if (result?.Title == null) return;
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
        {
            switch (result.Title.ToLower())
            {
                case "student management":
                    mainViewModel.NavigateCommand.Execute("Students");
                    break;
                case "student reports":
                    mainViewModel.NavigateCommand.Execute("Results");
                    break;
                case "create exam":
                    mainViewModel.NavigateCommand.Execute("CreateExam");
                    break;
                case "exam list":
                    mainViewModel.NavigateCommand.Execute("Exams");
                    break;
                case "question bank":
                case "search questions":
                    mainViewModel.NavigateCommand.Execute("Questions");
                    break;
                case "general settings":
                case "theme settings":
                    mainViewModel.NavigateCommand.Execute("Settings");
                    break;
            }
        }
    });

    public SearchResultsViewModel(string query)
    {
        SearchQuery = query;
        LoadSuggestions();
        LoadResults();
    }

    private void LoadSuggestions()
    {
        Suggestions.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Suggestions.Add(new SearchSuggestion("&#xE7FD;", "Exam Settings", "Configure exam parameters"));
            Suggestions.Add(new SearchSuggestion("&#xE706;", "Theme Settings", "Customize appearance"));
            Suggestions.Add(new SearchSuggestion("&#xE783;", "Error Guide", "Troubleshooting help"));
            Suggestions.Add(new SearchSuggestion("&#xE713;", "Settings", "General settings"));
        }
        else
        {
            // Dynamic suggestions based on query
            if (SearchQuery.Contains("exam", StringComparison.OrdinalIgnoreCase))
            {
                Suggestions.Add(new SearchSuggestion("&#xE7FD;", "Create New Exam", "Start creating an exam"));
                Suggestions.Add(new SearchSuggestion("&#xE8A5;", "Exam History", "View past exams"));
            }
            if (SearchQuery.Contains("theme", StringComparison.OrdinalIgnoreCase))
            {
                Suggestions.Add(new SearchSuggestion("&#xE706;", "Dark Mode", "Switch to dark theme"));
                Suggestions.Add(new SearchSuggestion("&#xE790;", "Color Settings", "Customize colors"));
            }
            if (SearchQuery.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                Suggestions.Add(new SearchSuggestion("&#xE783;", "Troubleshooting", "Common issues and fixes"));
                Suggestions.Add(new SearchSuggestion("&#xE946;", "System Status", "Check system health"));
            }
        }
    }

    private void LoadResults()
    {
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        // Mock search results - in real implementation, this would search actual data
        if (SearchQuery.Contains("student", StringComparison.OrdinalIgnoreCase))
        {
            SearchResults.Add(new SearchResult("&#xE77B;", "Student Management", "Manage student accounts and enrollment"));
            SearchResults.Add(new SearchResult("&#xE8A5;", "Student Reports", "View student performance reports"));
        }
        if (SearchQuery.Contains("exam", StringComparison.OrdinalIgnoreCase))
        {
            SearchResults.Add(new SearchResult("&#xE7FD;", "Create Exam", "Create a new examination"));
            SearchResults.Add(new SearchResult("&#xE8A5;", "Exam List", "View all available exams"));
        }
        if (SearchQuery.Contains("question", StringComparison.OrdinalIgnoreCase))
        {
            SearchResults.Add(new SearchResult("&#xE8F7;", "Question Bank", "Manage question database"));
            SearchResults.Add(new SearchResult("&#xE721;", "Search Questions", "Find specific questions"));
        }
        if (SearchQuery.Contains("setting", StringComparison.OrdinalIgnoreCase))
        {
            SearchResults.Add(new SearchResult("&#xE713;", "General Settings", "Application configuration"));
            SearchResults.Add(new SearchResult("&#xE706;", "Theme Settings", "Customize appearance"));
        }
    }
}

public record SearchSuggestion(string Icon, string Title, string Description);
public record SearchResult(string Icon, string Title, string Description);

// ─── Error Guide ─────────────────────────────────────────────────────────
public class ErrorGuideViewModel : BaseViewModel
{
    private string _serverStatus = "Unknown";
    public string ServerStatus { get => _serverStatus; set => Set(ref _serverStatus, value); }
    public string ServerStatusColor => ServerStatus == "Running" ? "#16A34A" : "#EF4444";
    public string ServerStatusText => ServerStatus == "Running" ? "Server is running normally" : "Server is not responding";

    private string _databaseStatus = "Unknown";
    public string DatabaseStatus { get => _databaseStatus; set => Set(ref _databaseStatus, value); }
    public string DatabaseStatusColor => DatabaseStatus == "Healthy" ? "#16A34A" : "#EF4444";
    public string DatabaseStatusText => DatabaseStatus == "Healthy" ? "Database is accessible" : "Database connection issues";

    public ObservableCollection<CommonIssue> CommonIssues { get; } = [
        new CommonIssue("Server Not Starting", "The CBT server fails to start when launching the application.", 
            "1. Check if port 5000 is available\n2. Restart the application\n3. Check Windows Firewall settings\n4. Run as Administrator", 
            "&#xE946;"),
        new CommonIssue("Database Locked", "The database file is locked by another process.", 
            "1. Close all other instances of CBT Exam System\n2. Check Task Manager for CbtExam.exe processes\n3. Restart the computer", 
            "&#xE747;"),
        new CommonIssue("Connection Refused", "Cannot connect to the server from student devices.", 
            "1. Verify server is running in admin panel\n2. Check network connectivity\n3. Verify correct IP address and port\n4. Check Windows Firewall rules", 
            "&#xE7C4;"),
        new CommonIssue("Students Not Loading", "Student list appears empty or fails to load.", 
            "1. Ensure server is running\n2. Check database file integrity\n3. Verify student data exists\n4. Refresh the page manually", 
            "&#xE77B;"),
        new CommonIssue("Exam Creation Fails", "Unable to create new exams or save questions.", 
            "1. Check server connection status\n2. Verify database write permissions\n3. Clear browser cache\n4. Try creating a simpler exam first", 
            "&#xE7FD;"),
        new CommonIssue("Questions Not Displaying", "Questions don't appear in exam or question bank.", 
            "1. Refresh the question bank\n2. Check question import format\n3. Verify exam has questions\n4. Check browser console for errors", 
            "&#xE8F7;"),
        new CommonIssue("Results Not Generating", "Exam results are not being calculated or displayed.", 
            "1. Ensure all students have submitted exams\n2. Check session is properly closed\n3. Verify result calculation settings\n4. Regenerate reports manually", 
            "&#xE8A5;")
    ];

    public RelayCommand CloseCommand => new(() => { 
        // Navigate back to dashboard
        var mainViewModel = System.Windows.Application.Current.MainWindow.DataContext as MainViewModel;
        mainViewModel?.NavigateCommand.Execute("Dashboard");
    });
    
    public RelayCommand CheckServerStatusCommand => new(CheckServerStatus);
    public RelayCommand ViewLogsCommand => new(ViewLogs);
    public RelayCommand ResetDatabaseCommand => new(ResetDatabase);
    public RelayCommand ContactSupportCommand => new(() => ContactSupport());
    public RelayCommand<CommonIssue> FixIssueCommand => new(issue => FixIssue(issue));

    private void CheckServerStatus()
    {
        // Mock server status check - in real implementation, this would ping the server
        ServerStatus = "Running"; // or "Not Running"
    }

    private void ViewLogs()
    {
        // Mock log viewing - in real implementation, this would open log files
        System.Windows.MessageBox.Show("Log files would open here", "Error Logs", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetDatabase()
    {
        // Mock database reset - in real implementation, this would reset the database
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset the database? This will delete all data and cannot be undone.", 
            "Reset Database", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            DatabaseStatus = "Healthy";
            System.Windows.MessageBox.Show("Database has been reset successfully.", 
                "Database Reset", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private void ContactSupport()
    {
        // Mock support contact - in real implementation, this would open support email or chat
        System.Windows.MessageBox.Show("Technical Support:\n\nEmail: support@cbtexam.com\nPhone: +1-800-CBT-HELP\n\nPlease include:\n- Application version\n- Error details\n- Steps to reproduce", 
            "Contact Technical Support", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void FixIssue(CommonIssue? issue)
    {
        // Mock issue fixing - in real implementation, this would provide specific fixes
        if (issue != null)
        {
            System.Windows.MessageBox.Show($"Applying fix for: {issue.Title}\n\n{issue.Solution}", 
                "Fix Issue", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}

public record CommonIssue(string Title, string Description, string Solution, string Icon);
public record ContactSupportCommand(string Title, string Email, string Phone);

// ─── Settings ────────────────────────────────────────────────────────────────
public class SettingsViewModel : BaseViewModel, IRefreshable
{
    private readonly ApiClient api;
    private readonly EmbeddedServerService _server;

    private int _port = 5000;
    public int Port { get => _port; set { Set(ref _port, value); SaveSettings(); } }

    public string LocalIp { get; } = EmbeddedServerService.GetLocalIp();

    private string _serverUrl = string.Empty;
    public string ServerUrl { get => _serverUrl; set => Set(ref _serverUrl, value); }

    private string _copyStatus = string.Empty;
    public string CopyStatus { get => _copyStatus; set => Set(ref _copyStatus, value); }

    private string _repoUrl = string.Empty;
    public string RepoUrl { get => _repoUrl; set { Set(ref _repoUrl, value); SaveSettings(); } }

    public ObservableCollection<string> ThemeOptions { get; } = ["Light", "Dark"];
    public ObservableCollection<string> AccentOptions { get; } = ["Teal", "Blue", "Purple", "Emerald"];

    private string _selectedTheme = "Light";
    public string SelectedTheme { get => _selectedTheme; set { if (Set(ref _selectedTheme, value)) { SaveSettings(); ApplyTheme(); ThemeApplied?.Invoke(); } } }

    private string _selectedAccent = "Teal";
    public string SelectedAccent { get => _selectedAccent; set { if (Set(ref _selectedAccent, value)) { SaveSettings(); ApplyTheme(); ThemeApplied?.Invoke(); } } }

    // New properties for enhanced settings
    private string _systemName = "CBT Exam System";
    public string SystemName { get => _systemName; set { Set(ref _systemName, value); SaveSettings(); } }

    private string _adminEmail = "admin@example.com";
    public string AdminEmail { get => _adminEmail; set { Set(ref _adminEmail, value); SaveSettings(); } }

    private string? _schoolLogoPath = null;
    public string? SchoolLogoPath
    {
        get => _schoolLogoPath;
        set
        {
            if (Set(ref _schoolLogoPath, value))
            {
                UpdateLogoImage();
                SaveSettings();
            }
        }
    }

    private System.Windows.Media.Imaging.BitmapImage? _schoolLogoImage;
    public System.Windows.Media.Imaging.BitmapImage? SchoolLogoImage
    {
        get => _schoolLogoImage;
        private set => Set(ref _schoolLogoImage, value);
    }

    private void UpdateLogoImage()
    {
        if (string.IsNullOrEmpty(_schoolLogoPath) || !File.Exists(_schoolLogoPath))
        {
            SchoolLogoImage = null;
            return;
        }

        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(_schoolLogoPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // Allow crossing threads
            SchoolLogoImage = bitmap;
        }
        catch (Exception ex)
        {
            App.Log("Failed to load school logo image", ex);
            SchoolLogoImage = null;
        }
    }

    public RelayCommand ApplyThemeCommand => new(() =>
    {
        ApplyTheme();
        CopyStatus = $"Theme applied: {SelectedTheme} / {SelectedAccent}";
        Task.Delay(1800).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => CopyStatus = string.Empty));
        ThemeApplied?.Invoke();
    });

    private void ApplyTheme()
    {
        if (App.Current is CbtExam.Desktop.App app)
        {
            app.ApplyTheme(SelectedTheme, SelectedAccent);
        }
    }

    public RelayCommand CopyUrlCommand => new(() =>
    {
        if (!string.IsNullOrEmpty(ServerUrl))
        {
            System.Windows.Clipboard.SetText(ServerUrl);
            CopyStatus = "Copied!";
            Task.Delay(2000).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => CopyStatus = string.Empty));
        }
    });

    public RelayCommand DownloadRepoCommand => new(async () => await DownloadQuestionsAsync());

    private async Task DownloadQuestionsAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoUrl))
        {
            CopyStatus = "Please enter a valid URL.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Downloading questions from repository...";
        try
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync(RepoUrl);
            var questions = JsonSerializer.Deserialize<List<QuestionBankCreateDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (questions == null || questions.Count == 0)
            {
                CopyStatus = "No questions found in repository.";
                return;
            }

            var resp = await api.ImportQuestionBankAsync(questions);
            if (resp.IsSuccessStatusCode)
            {
                CopyStatus = $"Successfully imported {questions.Count} questions!";
            }
            else
            {
                CopyStatus = "Failed to import questions to server.";
            }
        }
        catch (Exception ex)
        {
            App.Log("Failed to download questions", ex);
            CopyStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _ = Task.Delay(5000).ContinueWith(_ => App.Current?.Dispatcher.Invoke(() => CopyStatus = string.Empty));
        }
    }

    public RelayCommand OpenFirewallCommand => new(() =>
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"CBT Exam\" dir=in action=allow protocol=TCP localport={Port}",
                Verb = "runas",
                UseShellExecute = true
            });
        }
        catch { /* user cancelled UAC */ }
    });

    public RelayCommand UploadLogoCommand => new(() =>
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select School Logo",
            Filter = "Image Files|*.png;*.jpg;*.jpeg|PNG Files|*.png|JPEG Files|*.jpg;*.jpeg",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var sourceFile = openFileDialog.FileName;
                var destFile = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    "school_logo.png");

                // Copy and overwrite if needed
                File.Copy(sourceFile, destFile, true);
                
                // Force an update even if the path string is the same
                _schoolLogoPath = destFile;
                UpdateLogoImage();
                SaveSettings();
                OnPropertyChanged(nameof(SchoolLogoPath));
                CopyStatus = "Logo uploaded successfully!";
                Task.Delay(2000).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => CopyStatus = string.Empty));
            }
            catch (Exception ex)
            {
                App.Log("Failed to upload logo", ex);
                CopyStatus = "Failed to upload logo";
                Task.Delay(2000).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => CopyStatus = string.Empty));
            }
        }
    });

    public SettingsViewModel(ApiClient api, EmbeddedServerService server)
    {
        this.api = api;
        _server = server;
        LoadSettings();
    }

    public event Action? ThemeApplied;

    public void NotifyServerStarted(string url) => ServerUrl = url;
    public void NotifyServerStopped()           => ServerUrl = string.Empty;

    public Task LoadAsync() 
    { 
        LoadSettings(); 
        return Task.CompletedTask; 
    }

    private void LoadSettings()
    {
        try
        {
            var settingsFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "settings.json");

            if (File.Exists(settingsFile))
            {
                var json = File.ReadAllText(settingsFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        Port = data.Port ?? 5000;
                        SystemName = data.SystemName ?? "CBT Exam System";
                        AdminEmail = data.AdminEmail ?? "admin@example.com";
                        SelectedTheme = data.Theme ?? "Light";
                        SelectedAccent = data.Accent ?? "Teal";
                        SchoolLogoPath = data.SchoolLogoPath;
                        RepoUrl = data.RepoUrl ?? string.Empty;
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            App.Log("JSON parsing error in settings", ex);
            // Try to delete corrupted file
            try
            {
                var settingsFile = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    "settings.json");
                if (File.Exists(settingsFile))
                {
                    File.Delete(settingsFile);
                }
            }
            catch { /* Ignore deletion errors */ }
        }
        catch (Exception ex)
        {
            App.Log("Failed to load settings", ex);
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settingsFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "settings.json");

            var data = new SettingsData
            {
                Port = Port,
                SystemName = SystemName,
                AdminEmail = AdminEmail,
                Theme = SelectedTheme,
                Accent = SelectedAccent,
                SchoolLogoPath = SchoolLogoPath,
                RepoUrl = RepoUrl
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }
        catch (Exception ex)
        {
            App.Log("Failed to save settings", ex);
        }
    }
}

public class SettingsData
{
    public int? Port { get; set; }
    public string SystemName { get; set; } = "CBT Exam System";
    public string AdminEmail { get; set; } = "admin@example.com";
    public string Theme { get; set; } = "Light";
    public string Accent { get; set; } = "Teal";
    public string? SchoolLogoPath { get; set; }
    public string? RepoUrl { get; set; }
}

public class StudentsViewModel(ApiClient api) : BaseViewModel, IRefreshable
{
    public ObservableCollection<StudentAdminDto> Students { get; } = [];
    private List<StudentAdminDto> _all = [];

    private StudentAdminDto? _selected;
    public StudentAdminDto? Selected { get => _selected; set => Set(ref _selected, value); }

    private string _search = string.Empty;
    public string Search
    {
        get => _search;
        set { Set(ref _search, value); Filter(); }
    }

    private string _fullName = string.Empty;
    public string FullName { get => _fullName; set => Set(ref _fullName, value); }

    private string _studentId = string.Empty;
    public string StudentId { get => _studentId; set => Set(ref _studentId, value); }

    private string _newPassword = string.Empty;
    public string NewPassword { get => _newPassword; set => Set(ref _newPassword, value); }

    private bool _isActive = true;
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }

    public RelayCommand RefreshCommand => new(async () => await LoadAsync());
    public RelayCommand SaveCommand => new(async () => await SaveAsync());
    public RelayCommand DeleteCommand => new(async () => await DeleteAsync());
    public RelayCommand PasswordCommand => new(async () => await UpdatePasswordAsync());
    public RelayCommand BulkImportCommand => new(async () => await BulkImportAsync());
    public RelayCommand UploadCsvCommand => new(async () => await UploadCsvAsync());
    public RelayCommand PrintCommand => new(PrintStudents);
    public RelayCommand<StudentAdminDto> PickCommand => new(s => Pick(s));
    public RelayCommand ClearCommand => new(Clear);

    public bool IsEditing => Selected != null;
    public string BulkCsv { get; set; } = string.Empty;
    public string BulkStatus { get; set; } = string.Empty;
    public bool BulkSuccess { get; set; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _all = await api.GetStudentRosterAsync() ?? [];
            Filter();
        }
        finally { IsBusy = false; }
    }

    private void Filter()
    {
        var q = Search.Trim();
        var list = string.IsNullOrWhiteSpace(q)
            ? _all
            : _all.Where(s => s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) || s.StudentId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Students.Clear();
        foreach (var s in list) Students.Add(s);
    }

    private async Task SaveAsync()
    {
        var password = NewPassword;
        if (!IsEditing && string.IsNullOrWhiteSpace(password))
        {
            password = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
        }

        var resp = await api.UpsertStudentAsync(new StudentUpsertDto(Selected?.Id, FullName, StudentId, IsActive, password));
        if (resp.IsSuccessStatusCode)
        {
            Status = IsEditing ? "Student updated." : $"Student added. Pass: {password}";
            Clear();
            await LoadAsync();
        }
        else Status = "Could not save student.";
    }

    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        var resp = await api.DeleteStudentAsync(Selected.Id);
        Status = resp.IsSuccessStatusCode ? "Student deleted." : "Could not delete student.";
        await LoadAsync();
    }

    private async Task UpdatePasswordAsync()
    {
        if (Selected is null || string.IsNullOrWhiteSpace(NewPassword)) return;
        var resp = await api.UpdateStudentPasswordAsync(new StudentPasswordUpdateDto(Selected.Id, NewPassword));
        Status = resp.IsSuccessStatusCode ? "Password updated." : "Could not update password.";
        NewPassword = string.Empty;
    }

    private void Pick(StudentAdminDto? s)
    {
        if (s is null) return;
        Selected = s;
        FullName = s.FullName;
        StudentId = s.StudentId;
        IsActive = s.IsActive;
        NewPassword = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    private void Clear()
    {
        Selected = null;
        FullName = string.Empty;
        StudentId = string.Empty;
        NewPassword = string.Empty;
        IsActive = true;
        Status = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    private async Task UploadCsvAsync()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*" };
        if (ofd.ShowDialog() == true)
        {
            try
            {
                var content = await File.ReadAllTextAsync(ofd.FileName);
                BulkCsv = content;
                OnPropertyChanged("BulkCsv");
                Status = "File loaded. Click 'Import Students' to proceed.";
            }
            catch (Exception ex) { Status = $"Error loading file: {ex.Message}"; }
        }
    }

    private async Task BulkImportAsync()
    {
        if (string.IsNullOrWhiteSpace(BulkCsv)) { BulkStatus = "No data to import."; BulkSuccess = false; OnPropertyChanged("BulkStatus"); return; }
        
        IsBusy = true;
        BulkStatus = "Processing...";
        BulkSuccess = true;
        OnPropertyChanged("BulkStatus");

        try
        {
            var lines = BulkCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var name = parts[0].Trim();
                var id = parts[1].Trim();
                var pass = parts.Length > 2 ? parts[2].Trim() : Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

                await api.UpsertStudentAsync(new StudentUpsertDto(null, name, id, true, pass));
                count++;
            }

            BulkStatus = $"Successfully imported {count} students.";
            BulkSuccess = true;
            BulkCsv = string.Empty;
            OnPropertyChanged("BulkCsv");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            BulkStatus = $"Error: {ex.Message}";
            BulkSuccess = false;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged("BulkStatus");
            OnPropertyChanged("BulkSuccess");
        }
    }

    private void PrintStudents()
    {
        var doc = new System.Windows.Documents.FlowDocument();
        doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Student List")));
        foreach (var s in Students)
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{s.FullName}  —  ID: {s.StudentId}  —  Pass: {s.Password}")));
        var pd = new System.Windows.Controls.PrintDialog();
        if (pd.ShowDialog() == true)
            pd.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, "Student List");
    }
}

public record NotificationItem(string Title, string Message, DateTime CreatedAt, string Level);

public class NotificationsViewModel : BaseViewModel, IRefreshable
{
    public ObservableCollection<NotificationItem> Items { get; } = [];
    public int UnreadCount => Items.Count;
    public Task LoadAsync() => Task.CompletedTask;

    public void Add(NotificationItem item)
    {
        Items.Insert(0, item);
        if (Items.Count > 100)
            Items.RemoveAt(Items.Count - 1);
        OnPropertyChanged(nameof(UnreadCount));
    }
}
