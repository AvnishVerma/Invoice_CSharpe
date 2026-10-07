using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public sealed record InvoiceBankChoice(InvoiceBankAccountSnapshot? Account)
{
    public static InvoiceBankChoice Default { get; } = new(Account: null);
    public string Caption => Account == null ? "Use company bank accounts" : string.Join(" · ", new[] { Account.Label, Account.BankName, Account.AccountNumber.Length > 0 ? Account.AccountNumber : Account.Iban }.Where(value => value.Length > 0));
}

public partial class MainWindowViewModel
{
    public FormField OrderTime { get; } = new("Order Time", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)) { Help = "24-hour time, for example 14:30 or 14:30:45." };
    private InvoiceBankAccountSnapshot? historicalBankSelection;
    [ObservableProperty] private InvoiceBankChoice? selectedInvoiceBank = InvoiceBankChoice.Default;
    public InvoiceBankChoice[] InvoiceBankChoices
    {
        get
        {
            var choices = BankAccounts.Where(fields => fields[2].Value.Length > 0 || fields[4].Value.Length > 0)
                .Select(fields => new InvoiceBankChoice(new(fields[5].Value, fields[0].Value, fields[1].Value, fields[2].Value, fields[3].Value, fields[4].Value))).ToList();
            if (historicalBankSelection is { } historical && !choices.Any(choice => choice.Account == historical))
                choices.Add(new(historical));
            return [InvoiceBankChoice.Default, .. choices];
        }
    }
    partial void OnSelectedInvoiceBankChanged(InvoiceBankChoice? value) => InvoiceChanged?.Invoke();
    private void RestoreInvoiceBank(InvoiceBankAccountSnapshot? bank)
    {
        historicalBankSelection = bank;
        OnPropertyChanged(nameof(InvoiceBankChoices));
        SelectedInvoiceBank = bank == null ? InvoiceBankChoice.Default : new(bank);
    }
    private void SetOrderTime(DateTime date) => OrderTime.Value = date.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
}

public sealed class InvoiceDocumentDetailsViewModel(MainWindowViewModel workspace)
{
    public MainWindowViewModel Workspace { get; } = workspace;
    public FormFieldsViewModel MainFields { get; } = new(workspace.InvoiceDetails.Take(3).Select(field => new FormFieldViewModel(field)).ToArray(), 2);
    public FormFieldViewModel TimeField { get; } = new(workspace.OrderTime);
    public FormFieldViewModel TitleField { get; } = new(workspace.InvoiceDetails[3]);
    public FormFieldViewModel HideNumberField { get; } = new(workspace.HideInvoiceNumber);
}
