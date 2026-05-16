using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CbtExam.Desktop.Views;
using CbtExam.Data;

namespace CbtExam.Desktop.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private string _username = string.Empty;
    private string _accessCode = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _rememberMe = false;
    private string? _schoolLogoPath = null;

    public string Username
    {
        get => _username;
        set => Set(ref _username, value);
    }

    public string AccessCode
    {
        get => _accessCode;
        set => Set(ref _accessCode, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool RememberMe
    {
        get => _rememberMe;
        set => Set(ref _rememberMe, value);
    }

    public string? SchoolLogoPath
    {
        get => _schoolLogoPath;
        set
        {
            if (Set(ref _schoolLogoPath, value))
            {
                UpdateLogoImage();
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

    private bool _isLoggingIn = false;
    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        set => Set(ref _isLoggingIn, value);
    }

    public ICommand LoginCommand => new RelayCommand(async () => await Login());

    public LoginViewModel()
    {
        LoadSavedCredentials();
        LoadSchoolLogo();
        // Default username to admin
        Username = "admin";
    }

    private void LoadSavedCredentials()
    {
        try
        {
            var credentialsFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "login.json");

            if (File.Exists(credentialsFile))
            {
                var json = File.ReadAllText(credentialsFile);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<LoginData>(json);
                    if (data != null)
                    {
                        Username = data.Username ?? string.Empty;
                        RememberMe = data.RememberMe;
                        if (RememberMe && !string.IsNullOrEmpty(data.Password))
                        {
                            AccessCode = data.Password;
                        }
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            App.Log("JSON parsing error in login credentials", ex);
            // Try to delete corrupted file
            try
            {
                var credentialsFile = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    "login.json");
                if (File.Exists(credentialsFile))
                {
                    File.Delete(credentialsFile);
                }
            }
            catch { /* Ignore deletion errors */ }
        }
        catch (Exception ex)
        {
            App.Log("Failed to load saved credentials", ex);
        }
    }

    private void LoadSchoolLogo()
    {
        try
        {
            var logoFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "school_logo.png");

            if (File.Exists(logoFile))
            {
                SchoolLogoPath = logoFile;
            }
        }
        catch (Exception ex)
        {
            App.Log("Failed to load school logo", ex);
        }
    }

    private async Task Login()
    {
        // Clear previous errors
        ErrorMessage = string.Empty;

        // Validate input
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required";
            return;
        }

        if (string.IsNullOrWhiteSpace(AccessCode))
        {
            ErrorMessage = "Access code is required";
            return;
        }

        // Admin authentication - username must be 'admin' and code must be valid
        if (!Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Invalid username. Admin access requires username 'admin'.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AccessCode))
        {
            ErrorMessage = "Admin access code is required.";
            return;
        }

        // Show loading state
        IsLoggingIn = true;
        await Task.Delay(100); // Brief loading simulation

        // Validate against database stored credentials
        if (ValidateAdminCredentials(AccessCode))
        {
            // Successful admin login
            if (RememberMe)
            {
                SaveCredentials();
            }
            else
            {
                ClearSavedCredentials();
            }

            // Open main window first so there is always at least one window open
            var mainWindow = new MainWindow();
            if (Application.Current != null)
            {
                Application.Current.MainWindow = mainWindow;
            }
            mainWindow.Show();

            // Now close the login window(s)
            foreach (Window window in Application.Current.Windows)
            {
                if (window is LoginWindow)
                {
                    window.Close();
                }
            }
        }
        else
        {
            ErrorMessage = "Incorrect password. Please try again.";
            App.Log($"Failed login attempt for username '{Username}': Incorrect password.", null);
        }

        // Hide loading state
        IsLoggingIn = false;
    }

    private bool ValidateAdminCredentials(string code)
    {
        try
        {
            // For now, use default admin code validation
            // In a full implementation, this would validate against the API
            return code == "ADMIN123";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Validation error: {ex.Message}";
            return false;
        }
    }

    private void SaveCredentials()
    {
        try
        {
            var credentialsFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "login.json");

            var data = new LoginData
            {
                Username = Username,
                Password = AccessCode, // Store the password
                RememberMe = RememberMe
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data);
            File.WriteAllText(credentialsFile, json);
        }
        catch (Exception ex)
        {
            App.Log("Failed to save credentials", ex);
        }
    }

    private void ClearSavedCredentials()
    {
        try
        {
            var credentialsFile = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                "login.json");

            if (File.Exists(credentialsFile))
            {
                File.Delete(credentialsFile);
            }
        }
        catch (Exception ex)
        {
            App.Log("Failed to clear saved credentials", ex);
        }
    }
}

public class LoginData
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool RememberMe { get; set; }
}
