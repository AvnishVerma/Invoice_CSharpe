using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceEditorShellView : UserControl
{
    // Performs the invoice editor shell view initialization action for this screen or workflow.
    public InvoiceEditorShellView()
    {
        InitializeComponent();
    }

    // Performs the invoice editor shell view content assignment action for this screen or workflow.
    public InvoiceEditorShellView(InvoiceEditorShellModel model, Control workspace, Control actions, Control create)
    {
        InitializeComponent();
        DataContext = model;
        WorkspaceHost.Content = workspace;
        ActionsHost.Content = actions;
        CreateHost.Content = create;
        SizeChanged += (_, e) => FooterFrame.Padding = new Thickness(e.NewSize.Width < 700 ? 8 : 64, 10);
    }
}

public sealed class InvoiceEditorShellModel : INotifyPropertyChanged
{
    private string title = "";
    private string dateText = "";
    private string numberText = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title
    {
        get => title;
        set => SetField(ref title, value);
    }

    public string DateText
    {
        get => dateText;
        set => SetField(ref dateText, value);
    }

    public string NumberText
    {
        get => numberText;
        set => SetField(ref numberText, value);
    }

    // Performs the invoice editor shell field update action for this screen or workflow.
    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
