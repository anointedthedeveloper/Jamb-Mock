using CbtExam.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CbtExam.Desktop.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel ViewModel => (LoginViewModel)DataContext;
    private bool _isPasswordVisible = false;
    private bool _isDarkTheme = false;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += LoginWindow_Loaded;
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var app = App.Current as App;
        app?.ApplyTitleBarToWindow(this);
        UsernameTextBox?.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && !_isPasswordVisible)
        {
            vm.AccessCode = CodeBox.Password;
        }
    }

    private void VisibleCodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && _isPasswordVisible)
        {
            vm.AccessCode = VisibleCodeBox.Text;
        }
    }

    private void TogglePasswordBtn_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            VisibleCodeBox.Text = CodeBox.Password;
            CodeBox.Visibility = Visibility.Collapsed;
            VisibleCodeBox.Visibility = Visibility.Visible;
            TogglePasswordIcon.Text = "\uECE4"; // Hide icon
        }
        else
        {
            CodeBox.Password = VisibleCodeBox.Text;
            VisibleCodeBox.Visibility = Visibility.Collapsed;
            CodeBox.Visibility = Visibility.Visible;
            TogglePasswordIcon.Text = "\uE7B3"; // Reveal icon
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        if (_isDarkTheme)
        {
            Resources["PaneBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
            Resources["TextMain"] = new SolidColorBrush(Colors.White);
            Resources["TextSub"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
            Resources["FieldBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27272A"));
            Resources["FieldBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
            Resources["FieldFg"] = new SolidColorBrush(Colors.White);

            LightModeBtn.Background = Brushes.Transparent;
            DarkModeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
        }
        else
        {
            Resources["PaneBg"] = new SolidColorBrush(Colors.White);
            Resources["TextMain"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
            Resources["TextSub"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717A"));
            Resources["FieldBg"] = new SolidColorBrush(Colors.White);
            Resources["FieldBorder"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4E7"));
            Resources["FieldFg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));

            LightModeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            DarkModeBtn.Background = Brushes.Transparent;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
        base.OnMouseDown(e);
    }
}
