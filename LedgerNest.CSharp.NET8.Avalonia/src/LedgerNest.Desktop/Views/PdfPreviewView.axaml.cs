using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfPreviewView : UserControl
{
    // Performs the PDF preview view initialization action for this screen or workflow.
    public PdfPreviewView()
    {
        InitializeComponent();
    }

    // Performs the PDF preview view data context assignment action for this screen or workflow.
    public PdfPreviewView(PdfPreviewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides rendered PDF page controls and header text for the AXAML PDF preview dialog.
public sealed class PdfPreviewModel
{
    public string Title { get; init; } = "";
    public string PageCountText { get; init; } = "";
    public double MaxPreviewHeight { get; init; }
    public ObservableCollection<Control> Pages { get; } = [];
}
