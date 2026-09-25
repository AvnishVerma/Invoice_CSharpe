using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfPreviewView : UserControl
{
    public PdfPreviewView() => InitializeComponent();
    public PdfPreviewView(PdfPreviewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

public sealed class PdfPreviewModel
{
    public string Title { get; init; } = "PDF Preview";
    public string PageCountText { get; init; } = "";
    public ObservableCollection<IImage> Pages { get; } = [];
}
