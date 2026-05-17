using CbtExam.Desktop.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CbtExam.Desktop.Views;

public partial class QuestionsView : UserControl
{
    public QuestionsView()
    {
        InitializeComponent();
        UploadZoneBorder.DragOver += UploadZoneBorder_DragOver;
        UploadZoneBorder.Drop += UploadZoneBorder_Drop;
    }

    private void UploadZoneBorder_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void UploadZoneBorder_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is QuestionsViewModel vm)
            {
                var file = files[0];
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    await vm.ParseAndImportJsonFileAsync(file);
                }
                else if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    await vm.ParseAndImportCsvAsync(file);
                }
                else
                {
                    vm.Status = "Unsupported file format. Please drop a .csv or .json file.";
                    vm.StatusOk = false;
                }
            }
        }
    }
}
