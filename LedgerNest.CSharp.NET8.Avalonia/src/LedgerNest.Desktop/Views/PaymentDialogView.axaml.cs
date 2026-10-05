using System.Collections.ObjectModel;
using Avalonia.Controls;

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

// Provides payment summary, history, and optional form content for the AXAML payment dialog.
public sealed class PaymentDialogModel
{
    public string InvoiceLine { get; init; } = "";
    public string TotalText { get; init; } = "";
    public string PaidText { get; init; } = "";
    public string OutstandingText { get; init; } = "";
    public ObservableCollection<PaymentHistoryRowModel> Payments { get; } = [];
    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => Payments.Count == 0;
    public bool IsFullyPaid { get; init; }
    public bool IsVoided { get; init; }
    public bool HasUpdatePermission { get; init; } = true;
    public bool CanRecordPayment => !IsFullyPaid && !IsVoided && HasUpdatePermission;
    public FormFieldsViewModel? AmountAndDate { get; init; }
    public FormFieldsViewModel? MethodAndTax { get; init; }
    public FormFieldViewModel? Note { get; init; }
    public string AmountHint { get; init; } = "";
}

// Describes one payment history row rendered in the AXAML payment dialog.
public sealed record PaymentHistoryRowModel(string Receipt, string Date, string Amount, string TaxCovered, string Method);
