using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace LedgerNest.Desktop.Views;

/// <summary>AXAML owns both responsive layouts and the resizable invoice divider.</summary>
public sealed partial class InvoiceWorkspace : UserControl
{
    public InvoiceWorkspace()
    {
        InitializeComponent();
        SizeChanged += (_, args) => { if (DataContext is InvoiceEditorPageModel model) model.IsNarrow = args.NewSize.Width < 880; };
        AttachedToVisualTree += (_, _) => { if (DataContext is InvoiceEditorPageModel model) model.FocusSearchRequested += FocusSearch; };
        DetachedFromVisualTree += (_, _) => { if (DataContext is InvoiceEditorPageModel model) model.FocusSearchRequested -= FocusSearch; };
    }
    private void FocusSearch()
    {
        var search = this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.Name == "ProductSearch");
        search?.Focus(); search?.SelectAll();
    }
    private void SearchKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not InvoiceEditorPageModel model) return;
        if (args.Key == Key.Escape) { model.ShowSuggestions = false; args.Handled = true; }
        if (args.Key == Key.Enter) { model.AcceptSearch(); args.Handled = true; }
    }
}
