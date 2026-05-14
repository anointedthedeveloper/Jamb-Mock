using System.Collections.ObjectModel;
using System.Windows.Input;
using CbtExam.Desktop.Services;
using CbtExam.Desktop.Models;
using CbtExam.Desktop.Views;

namespace CbtExam.Desktop.ViewModels;

public class OmnibarViewModel : BaseViewModel
{
    private readonly SearchCatalogService _catalogService;
    private readonly ICommand _closeCommand;
    private readonly ICommand _navigateCommand;
    private readonly ICommand _executeActionCommand;
    private string _query = string.Empty;
    private ObservableCollection<SearchItemRecord> _results = new();
    private bool _isOpen = false;

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value))
            {
                PerformSearch();
            }
        }
    }

    public ObservableCollection<SearchItemRecord> Results
    {
        get => _results;
        set => Set(ref _results, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    public ICommand CloseCommand => _closeCommand ??= new RelayCommand(() => IsOpen = false);
    public ICommand NavigateCommand => _navigateCommand ??= new RelayCommand<string>(NavigateToPage);
    public ICommand ExecuteActionCommand => _executeActionCommand ??= new RelayCommand<SearchItemRecord>(ExecuteAction);

    public OmnibarViewModel()
    {
        _catalogService = new SearchCatalogService(new ApiClient());
        // Load catalog on startup
        LoadCatalog();
    }

    private async void LoadCatalog()
    {
        // In a real app, you'd load from API. For now, we'll just use the service methods.
        // We'll keep it simple: the service methods are synchronous for now.
    }

    private void PerformSearch()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            Results.Clear();
            return;
        }

        // Simple fuzzy search: case-insensitive contains
        var allItems = new List<SearchItemRecord>();
        allItems.AddRange(_catalogService.GetQuickActions());
        allItems.AddRange(_catalogService.GetPages().Select(p => new SearchItemRecord
        {
            Id = p.Key,
            Title = p.Title,
            Description = $"{p.Title} page",
            Icon = p.Icon,
            Category = "Pages"
        }));

        var filtered = allItems.Where(item =>
            item.Title.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains(Query, StringComparison.OrdinalIgnoreCase)).ToList();

        Results = new ObservableCollection<SearchItemRecord>(filtered);
    }

    private void NavigateToPage(string pageKey)
    {
        // Find the main view model from MainWindow
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow &&
            mainWindow.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.NavigateCommand.Execute(pageKey);
        }
        IsOpen = false;
    }

    private void ExecuteAction(SearchItemRecord action)
    {
        if (action.Action == "Theme")
        {
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.ToggleThemeCommand.Execute(null);
            }
        }
        else if (action.Action == "ServerToggle")
        {
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.ToggleServerCommand.Execute(null);
            }
        }
        else if (action.Action == "ErrorGuide")
        {
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.NavigateCommand.Execute("ErrorGuide");
            }
        }
        // Add more actions as needed

        IsOpen = false;
    }
}