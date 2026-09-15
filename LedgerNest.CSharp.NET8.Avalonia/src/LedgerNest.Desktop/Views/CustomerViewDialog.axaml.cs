using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class CustomerViewDialog : UserControl
{
    // Performs the customer view dialog initialization action for this screen or workflow.
    public CustomerViewDialog()
    {
        InitializeComponent();
    }

    // Performs the customer view dialog data context assignment action for this screen or workflow.
    public CustomerViewDialog(CustomerViewDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides read-only customer field rows for the AXAML customer view dialog.
public sealed class CustomerViewDialogModel
{
    public ObservableCollection<CustomerViewFieldModel> Fields { get; } = [];
}

// Describes one read-only customer field row rendered by the AXAML customer view dialog.
public sealed record CustomerViewFieldModel(string Label, string Icon, string Value, string Counter)
{
    public bool HasCounter => !string.IsNullOrWhiteSpace(Counter);
}
