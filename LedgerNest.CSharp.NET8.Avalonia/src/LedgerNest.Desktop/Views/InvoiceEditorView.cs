using Avalonia.Controls;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Composes the existing AXAML invoice editor; all visual structure belongs to the view.
    private Control InvoiceEditor()
    {
        invoiceCompletionVisible = false;
        InvoiceEditorPageModel? editor = null;
        editor = new InvoiceEditorPageModel(Model, SelectCustomer,
            product => ShowProductItem(product, () => editor!.RequestSearchFocus()), ShowCustomItem, ShowInvoiceSuccess,
            () => Confirm("Void Invoice", $"Void {Model.EditorDocumentNumber}? This action cannot be undone.", () => Model.VoidCurrentInvoice()),
            () => { if (Model.LastSavedDocument is { } document) ShowDocumentPreview(document); },
            async () => { if (Model.LastSavedDocument is { } document) await ShowPdfPreviewAsync(document); },
            async () => { if (Model.LastSavedDocument is { } document) await DownloadDocumentPdf(document); },
            async () => { if (Model.LastSavedDocument is { } document) await PrintDocumentAsync(document); }, ExpandInvoiceField, CreateInvoiceCustomer);
        return new InvoiceEditorShellView(editor);
    }

    private void ExpandInvoiceField(FormField field)
    {
        var editor = new ExpandedTextViewModel(field.Value, field.Label == "Notes" ? 1000 : field.MaxLength, field.IsReadOnly);
        ShowOverlay(field.Label, editor, new DialogActions([
            new(UiLocalization.Translate("Cancel"), new CommunityToolkit.Mvvm.Input.RelayCommand(CloseOverlay)),
            new(UiLocalization.TranslateKey("actionSave"), new CommunityToolkit.Mvvm.Input.RelayCommand(() => { if (!field.IsReadOnly) field.Value = editor.Text; CloseOverlay(); }), true)
        ]), width: 528, prominentHeader: true);
    }
    // Performs the show product item action for this screen or workflow.
    private void ShowProductItem(UiRecord product, Action focusSearch)
    {
        var dialog = new ProductItemDialogViewModel(product, _ => { }, () =>
        {
            CloseOverlay();
            focusSearch();
        }, Model.TryAddInvoiceLine, Model.InvoiceSetting("Allow Fractional Quantity").IsChecked, Model.ProductFieldVisible,
            Model.ActiveUnitCodes(product["Unit"]), Model.SellingUnitChoices(product));
        Model.CurrentOverlay = dialog;
    }
    private void CreateInvoiceCustomer()
    {
        if (!Model.CanPerformAction("Customer", "Add")) { Model.Status = "Your role cannot create customers."; return; }
        var fields = FormCatalog.Customer();
        var save = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            if (!Model.SaveRecord("Customer", fields)) return;
            // SaveRecord appends the exact newly persisted record synchronously.
            var customer = Model.Customers.Last();
            if (Model.SelectInvoiceCustomer(customer)) CloseOverlay();
        });
        ShowOverlay("Create Customer",
            new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 1),
            new DialogActions([
                new(UiLocalization.Translate("Cancel"), new CommunityToolkit.Mvvm.Input.RelayCommand(CloseOverlay)),
                new(UiLocalization.Translate("Create Customer"), save, true)]), width: 520);
    }

    // Performs the select customer action for this screen or workflow.
    private void SelectCustomer()
    {
        void Apply(UiRecord customer)
        {
            if (Model.SelectInvoiceCustomer(customer)) CloseOverlay();
        }
        ShowOverlay("Choose a customer", new CustomerChooserView(Model.Customers, Apply), width: 420);
    }
    // Performs the show invoice success action for this screen or workflow.
    private void ShowInvoiceSuccess()
    {
        invoiceCompletionVisible = true;
        var document = Model.LastSavedDocument!;
        var outstanding = decimal.TryParse(document["Outstanding"], out var balance) ? balance : decimal.TryParse(document["Total"], out var total) ? total : 0m;
        page.Content = InvoiceCreatedScreen(document, outstanding > 0.005m);
    }

    // Performs the invoice created screen action for this screen or workflow.
    private Control InvoiceCreatedScreen(UiRecord document, bool canApplyPayment)
    {
        var invoiceId = document.Name.TrimStart('#').Replace("[", "").Replace("]", "");
        return new InvoiceCreatedView(
            invoiceId,
            invoiceId,
            canApplyPayment,
            () => ShowDocumentPreview(document),
            () => ShowPdfPreviewAsync(document),
            () => PrintDocumentAsync(document),
            () => { Model.StartDocument("Invoice"); if (!Model.HasPendingInvoiceNavigation) page.Content = InvoiceEditor(); },
            () => ShowPayment(document),
            channel =>
            {
                try
                {
                    LedgerNest.Domain.DeliveryChannel[] channels = channel is "Both" or "Resend"
                        ? [LedgerNest.Domain.DeliveryChannel.Email, LedgerNest.Domain.DeliveryChannel.WhatsApp]
                        : [Enum.Parse<LedgerNest.Domain.DeliveryChannel>(channel)];
                    var count = Model.QueueCustomerDelivery(document, channels, resend: channel == "Resend");
                    Model.Status = count > 0 ? $"Queued {count} invoice delivery requests." : "Nothing queued. Check enabled integrations, customer consent and existing delivery history.";
                }
                catch (InvalidOperationException e) { Model.Status = e.Message; }
                catch (UnauthorizedAccessException e) { Model.Status = e.Message; }
                catch { Model.Status = "Could not queue invoice delivery. Review Communication settings."; }
            });
    }
}
