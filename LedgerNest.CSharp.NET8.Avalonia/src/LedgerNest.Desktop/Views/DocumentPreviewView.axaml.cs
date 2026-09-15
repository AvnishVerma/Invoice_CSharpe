using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class DocumentPreviewView : UserControl
{
    // Performs the document preview view initialization action for this screen or workflow.
    public DocumentPreviewView()
    {
        InitializeComponent();
    }

    // Performs the document preview view data context assignment action for this screen or workflow.
    public DocumentPreviewView(DocumentPreviewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

public sealed class DocumentPreviewModel
{
    public string Title { get; init; } = "";
    public string CustomerText { get; init; } = "";
    public string DateText { get; init; } = "";
    public string SubtotalText { get; init; } = "";
    public string TaxLabelText { get; init; } = "";
    public string TaxText { get; init; } = "";
    public string TotalText { get; init; } = "";
    public ObservableCollection<DocumentPreviewItemModel> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public bool HasNoItems => Items.Count == 0;
}

public sealed record DocumentPreviewItemModel(string Description, string Amount);
