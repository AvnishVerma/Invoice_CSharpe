using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class CompanySettingsViewModel : ObservableObject, IDisposable
{
    private readonly MainWindowViewModel model;
    public FormField LogoValue { get; }
    public FormField LogoPosition { get; }
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
    public MasterCodeSettingsViewModel CodeSettings { get; }
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

    public CompanySettingsViewModel(MainWindowViewModel model)
    {
        this.model = model;
        CodeSettings = model.CreateMasterCodeSettings();
        var sections = model.Settings["Company Info"];
        LogoValue = sections[0].Fields[0]; LogoPosition = sections[0].Fields[1];
        FormField Field(string label) => sections.SelectMany(section => section.Fields).Single(field => field.Label == label);
        CompanyName = Field("Company Name"); Gstin = Field("GSTIN"); Pan = Field("PAN"); FssaiCode = Field("FSSAI Code");
        Country = Field("Country"); Phone = Field("Phone"); Email = Field("Email"); Website = Field("Website"); Address = Field("Address");
        BusinessType = sections[2].Fields[0]; ShowQr = sections[3].Fields[0]; ShowBank = sections[3].Fields[1];
        selectedLanguage = model.Language; selectedTheme = model.ThemeMode;
        LogoImage = Views.Ui.LoadLogo(LogoValue.Value);
    }

    partial void OnSelectedLanguageChanged(string value) => model.SetLanguage(value);
    partial void OnSelectedThemeChanged(string value) => model.SetThemeMode(value);
    partial void OnLogoImageChanged(Bitmap? value) => OnPropertyChanged(nameof(HasLogo));

    public bool SetLogo(byte[] bytes)
    {
        if (bytes.Length > 2 * 1024 * 1024) { model.Status = "Logo must be 2 MB or smaller."; return false; }
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
    [RelayCommand] private void Save() => model.SaveSettings("Company Info");
    public void Dispose() => LogoImage?.Dispose();
}
