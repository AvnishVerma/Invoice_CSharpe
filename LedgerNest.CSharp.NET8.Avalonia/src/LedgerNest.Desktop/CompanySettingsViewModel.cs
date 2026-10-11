using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class CompanySettingsViewModel : ObservableObject, IDisposable
{
    private readonly MainWindowViewModel model;
    public FormField LogoValue { get; }
    public FormField ShowLogo { get; }
    public FormField CompanyName { get; }
    public FormField Gstin { get; }
    public FormField Pan { get; }
    public FormField FssaiCode { get; }
    public FormField Country { get; }
    public FormField Phone { get; }
    public FormField Email { get; }
    public FormField Website { get; }
    public FormField Address { get; }
    public FormField BusinessType { get; }
    public FormField ShowQr { get; }
    public FormField ShowBank { get; }
    public ObservableCollection<FormField[]> UpiAccounts => model.UpiAccounts;
    public ObservableCollection<FormField[]> BankAccounts => model.BankAccounts;
    public string[] Languages => UiLocalization.Languages;
    public string[] Themes { get; } = ["Light", "Dark", "System"];
    [ObservableProperty] private string selectedLanguage;
    [ObservableProperty] private string selectedTheme;
    [ObservableProperty] private Bitmap? logoImage;
    public bool HasLogo => LogoImage != null;
    public bool IsProduct => BusinessType.Value == "Product";
    public bool IsService => BusinessType.Value == "Service";
    public bool IsBoth => BusinessType.Value == "Both";

    public CompanyFieldModel NameInput { get; }
    public CompanyFieldModel GstinInput { get; }
    public CompanyFieldModel PanInput { get; }
    public CompanyFieldModel FssaiInput { get; }
    public CompanyFieldModel CountryInput { get; }
    public CompanyFieldModel PhoneInput { get; }
    public CompanyFieldModel EmailInput { get; }
    public CompanyFieldModel WebsiteInput { get; }
    public CompanyFieldModel AddressInput { get; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isSaving;
    public bool CanSave => !IsSaving;
    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    public CompanySettingsViewModel(MainWindowViewModel model, Action<FormField>? expand = null)
    {
        this.model = model;
        var sections = model.Settings["Company Info"];
        LogoValue = sections[0].Fields[0]; ShowLogo = sections[0].Fields[1];
        FormField Field(string label) => sections.SelectMany(section => section.Fields).Single(field => field.Label == label);
        CompanyName = Field("Company Name"); Gstin = Field("GSTIN"); Pan = Field("PAN"); FssaiCode = Field("FSSAI Code");
        Country = Field("Country"); Phone = Field("Phone"); Email = Field("Email"); Website = Field("Website"); Address = Field("Address");
        BusinessType = sections[2].Fields[0]; ShowQr = sections[3].Fields[0]; ShowBank = sections[3].Fields[1];
        NameInput = new(CompanyName, "business"); GstinInput = new(Gstin, "receipt");
        PanInput = new(Pan, "credit_card"); FssaiInput = new(FssaiCode, "verified");
        CountryInput = new(Country, "public"); PhoneInput = new(Phone, "phone"); EmailInput = new(Email, "email");
        WebsiteInput = new(Website, "public");
        AddressInput = new(Address, "location_on", expand == null ? null : new RelayCommand(() => expand(Address)));
        selectedLanguage = model.Language; selectedTheme = model.ThemeMode;
        LogoImage = Views.Ui.LoadLogo(LogoValue.Value);
    }

    partial void OnSelectedLanguageChanged(string value) => model.SetLanguage(value);
    partial void OnSelectedThemeChanged(string value) => model.SetThemeMode(value);
    partial void OnLogoImageChanged(Bitmap? value) => OnPropertyChanged(nameof(HasLogo));

    public bool SetLogo(byte[] bytes)
    {
        if (bytes.Length > 2 * 1024 * 1024) { model.Status = "Logo must be 2 MB or smaller."; return false; }
        if (!(bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            && !(bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255))
        { model.Status = "Choose a PNG or JPG logo."; return false; }
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width > 1080 || bitmap.PixelSize.Height > 1080)
            {
                bitmap.Dispose(); model.Status = "Logo must be at most 1080 × 1080 pixels."; return false;
            }
            LogoImage?.Dispose(); LogoImage = bitmap; LogoValue.Value = "base64:" + Convert.ToBase64String(bytes); return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        { model.Status = "The selected image could not be opened."; return false; }
    }

    [RelayCommand] private void SelectBusinessType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        BusinessType.Value = value;
        OnPropertyChanged(nameof(IsProduct)); OnPropertyChanged(nameof(IsService)); OnPropertyChanged(nameof(IsBoth));
    }
    [RelayCommand] private void AddUpi() => model.AddUpiAccount();
    [RelayCommand] private void RemoveUpi(FormField[]? account) { if (account != null) model.RemoveUpiAccount(account); }
    [RelayCommand] private void AddBank() => model.AddBankAccount();
    [RelayCommand] private void RemoveBank(FormField[]? account) { if (account != null) model.RemoveBankAccount(account); }
    [RelayCommand] private void RemoveLogo()
    {
        LogoImage?.Dispose(); LogoImage = null; LogoValue.Value = "";
    }
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (IsSaving) return;
        IsSaving = true;
        try { var previousStatus = model.Status; if (!model.SaveSettings("Company Info") && model.Status == previousStatus) model.Status = "Correct the highlighted company fields before saving."; }
        catch (Exception exception) when (exception is Microsoft.EntityFrameworkCore.DbUpdateException or System.Data.Common.DbException or IOException or UnauthorizedAccessException)
        { model.Status = "Company information could not be saved: " + exception.Message; }
        finally { IsSaving = false; }
    }
    public void Dispose() => LogoImage?.Dispose();
}
