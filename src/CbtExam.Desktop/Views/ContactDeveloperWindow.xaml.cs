using System.Windows;
using System.Diagnostics;

namespace CbtExam.Desktop.Views;

public partial class ContactDeveloperWindow : Window
{
    public ContactDeveloperWindow()
    {
        InitializeComponent();
        Loaded += ContactDeveloperWindow_Loaded;
    }

    private void ContactDeveloperWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var app = App.Current as App;
        app?.ApplyTitleBarToWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void WhatsApp1_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://wa.me/2348101209470",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void WhatsApp2_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://wa.me/2349016471351",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:anointedthedeveloper@gmail.com",
                UseShellExecute = true
            });
        }
        catch { }
    }

    protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
        base.OnMouseDown(e);
    }
}
