using CbtExam.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
        {
            vm.AccessCode = pb.Password;
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
