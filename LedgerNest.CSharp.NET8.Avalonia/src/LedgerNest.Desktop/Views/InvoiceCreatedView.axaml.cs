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
        Action preview,
        Func<Task> download,
        Func<Task> print,
        Action createNew,
        Action applyPayment)
    {
        InitializeComponent();
        DateText.Text = DateTime.Today.ToString("dd/MM/yyyy");
        InvoiceNumberText.Text = $"Invoice Number : #[{invoiceNumber}] ⓘ";
        InvoiceIdText.Text = $"Invoice ID: {invoiceId}";
        ToolTip.SetTip(ViewButton, "View invoice summary");
        ToolTip.SetTip(PreviewButton, "Preview PDF and download file");
        ToolTip.SetTip(DownloadButton, "Download PDF");
        ToolTip.SetTip(PrintButton, "Print PDF directly");
        ToolTip.SetTip(CreateNewButton, "Create new invoice");
        ToolTip.SetTip(ApplyPaymentButton, "Accept payment");
        ViewButton.Click += (_, _) => view();
        PreviewButton.Click += (_, _) => preview();
        DownloadButton.Click += async (_, _) => await download();
        PrintButton.Click += async (_, _) => await print();
        CreateNewButton.Click += (_, _) => createNew();
        PaymentActionPanel.IsVisible = canApplyPayment;
        ApplyPaymentButton.Click += (_, _) => applyPayment();
    }
}
