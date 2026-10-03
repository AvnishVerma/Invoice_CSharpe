using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfSettingsView : UserControl
{
    public PdfSettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PdfSettingsViewModel model) model.LoadPrintersCommand.Execute(null);
        };
    }
}
