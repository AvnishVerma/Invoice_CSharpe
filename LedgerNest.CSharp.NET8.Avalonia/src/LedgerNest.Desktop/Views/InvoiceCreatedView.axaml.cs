using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class InvoiceCreatedView : UserControl
{
    public InvoiceCreatedView()
    {
        InitializeComponent();
    }

    public InvoiceCreatedView(
        string invoiceId,
        string invoiceNumber,
        bool canApplyPayment,
        Action view,
        Func<Task> preview,
        Func<Task> print,
        Action createNew,
        Action applyPayment,
        Action<string>? delivery = null)
    {
        InitializeComponent();
        DateText.Text = DateTime.Today.ToString("dd/MM/yyyy");
        InvoiceNumberText.Text = $"Invoice Number : #[{invoiceNumber}]";
        InvoiceIdText.Text = $"Invoice ID: {invoiceId}";
        ToolTip.SetTip(ViewButton, "View invoice summary");
        ToolTip.SetTip(PreviewButton, "Preview PDF");
        ToolTip.SetTip(PrintButton, "Print invoice directly");
        ToolTip.SetTip(CreateNewButton, "Create new invoice");
        ToolTip.SetTip(ApplyPaymentButton, "Accept payment");
        ViewButton.Click += (_, _) => view();
        PreviewButton.Click += async (_, _) => await preview();
        PrintButton.Click += async (_, _) => await print();
        CreateNewButton.Click += (_, _) => createNew();
        PaymentActionPanel.IsVisible = canApplyPayment;
        ApplyPaymentButton.Click += (_, _) => applyPayment();
        DeliveryActions.IsVisible = delivery != null;
        SendEmailButton.Click += (_, _) => delivery?.Invoke("Email");
        SendWhatsAppButton.Click += (_, _) => delivery?.Invoke("WhatsApp");
        SendBothButton.Click += (_, _) => delivery?.Invoke("Both");
        ResendInvoiceButton.Click += (_, _) => delivery?.Invoke("Resend");
    }
}
