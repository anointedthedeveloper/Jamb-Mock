using CbtExam.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CbtExam.Desktop.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel ViewModel => (LoginViewModel)DataContext;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += LoginWindow_Loaded;
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply title bar color
        var app = App.Current as App;
        app?.ApplyTitleBarToWindow(this);

        // Focus on username field
        UsernameTextBox?.Focus();
    }

    private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UsernameBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136));
        UsernameLabel.Foreground = new SolidColorBrush(Color.FromRgb(13, 148, 136));
    }

    private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            UsernameBorder.BorderBrush = Brushes.White;
            UsernameLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }

    private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
    {
        CodeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136));
        CodeLabel.Foreground = new SolidColorBrush(Color.FromRgb(13, 148, 136));
    }

    private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var codeBox = sender as TextBox;
        if (string.IsNullOrWhiteSpace(codeBox?.Text))
        {
            CodeBorder.BorderBrush = Brushes.White;
            CodeLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }

    private void CodeBox_GotFocus(object sender, RoutedEventArgs e)
    {
        CodeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136));
        CodeLabel.Foreground = new SolidColorBrush(Color.FromRgb(13, 148, 136));
    }

    private void CodeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var codeBox = sender as TextBox;
        if (string.IsNullOrWhiteSpace(codeBox?.Text))
        {
            CodeBorder.BorderBrush = Brushes.White;
            CodeLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }

    private void CodeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Access code is handled through binding
    }

    private void ToggleCodeVisibility(object sender, RoutedEventArgs e)
    {
        // This would require a custom control or attached behavior for full implementation
        // For now, just a placeholder
    }

    private void ForgotPassword_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Content = new TextBlock { Text = "Reset access code?", TextDecorations = TextDecorations.Underline };
        }
    }

    private void ForgotPassword_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Content = new TextBlock { Text = "Reset access code?" };
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // Allow dragging the window
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
        base.OnMouseDown(e);
    }
}
