using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;

namespace LedgerNest.Desktop.Views;

public sealed partial class PaymentDialogView : UserControl
{
    // Performs the payment dialog view initialization action for this screen or workflow.
    public PaymentDialogView()
    {
        InitializeComponent();
    }

    // Performs the payment dialog view data context assignment action for this screen or workflow.
    public PaymentDialogView(PaymentDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

public sealed class PaymentDialogModel
{
    public string InvoiceLine { get; init; } = "";
    public string TotalText { get; init; } = "";
    public string PaidText { get; init; } = "";
    public string OutstandingText { get; init; } = "";
    public IBrush OutstandingBrush { get; init; } = Brushes.Gray;
    public IBrush OutstandingBorder { get; init; } = Brushes.LightGray;
    public IBrush OutstandingBackground { get; init; } = Brushes.White;
    public ObservableCollection<PaymentHistoryRowModel> Payments { get; } = [];
    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => Payments.Count == 0;
    public bool IsFullyPaid { get; init; }
    public bool CanRecordPayment => !IsFullyPaid;
    public Control? PaymentForm { get; init; }
}

public sealed record PaymentHistoryRowModel(string Receipt, string Date, string Amount, string TaxCovered, string Method);
