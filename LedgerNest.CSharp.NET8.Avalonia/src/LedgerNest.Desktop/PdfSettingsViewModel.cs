using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Printing;

namespace LedgerNest.Desktop;

public sealed partial class PdfTemplateOptionViewModel(string name, string description) : ObservableObject
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool IsDefault => Name == "Classic";
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isVisible = true;
}

public sealed partial class PdfSettingsViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly IPrintService printService;
    private readonly FormField[] trackedFields;
    private string[] savedValues;
    public FormField PageSize { get; }
    public FormField Orientation { get; }
    public FormField Template { get; }
    public FormField ShowTotalQuantity { get; }
    public FormField ItemLayout { get; }
    public FormField CompanyNameSize { get; }
    public FormField ThemeColor { get; }
    public FormField Printer { get; }
    public FormField ShowPrinterDialog { get; }
    public FormField FontSize { get; }
    public FormField CompanyFontSize { get; }
    public FormField DocumentTitleFontSize { get; }
    public FormField TableHeaderFontSize { get; }
    public FormField TableItemsFontSize { get; }
    public FormField TotalsFontSize { get; }
    public bool SupportsFontSizes => Template.Value != "Thermal" && !PageSize.Value.StartsWith("Thermal", StringComparison.Ordinal);
    public ObservableCollection<PdfTemplateOptionViewModel> Templates { get; } = [];
    public string[] ThemeColors { get; } = ["#002E78", "#2563EB", "#047857", "#7C2D12", "#6D28D9"];
    public bool SupportsQuantity => Template.Value is "Compact" or "Grid Classic" or "Thermal";
    public bool IsPortrait => Orientation.Value == "Portrait";
    public bool IsLandscape => !IsPortrait;
    [ObservableProperty] private bool hasChanges;
    [ObservableProperty] private bool isLoadingPrinters;

    public PdfSettingsViewModel(MainWindowViewModel model, IPrintService printService)
    {
        this.model = model;
        this.printService = printService;
        var sections = model.Settings["PDF Settings"];
        FormField Field(string label) => sections.SelectMany(section => section.Fields).Single(field => field.Label == label);
        PageSize = Field("Page Size"); Orientation = Field("Orientation"); Template = Field("Template");
        ShowTotalQuantity = Field("Show Total Quantity"); ItemLayout = Field("Item Layout"); CompanyNameSize = Field("Company Name Size");
        ThemeColor = Field("Theme Color"); Printer = Field("Printer"); ShowPrinterDialog = Field("Show Printer Selection Dialog");
        FontSize = Field("PDF Font Size"); CompanyFontSize = Field("Company Font Size"); DocumentTitleFontSize = Field("Document Title Font Size");
        TableHeaderFontSize = Field("Table Header Font Size"); TableItemsFontSize = Field("Table Items Font Size"); TotalsFontSize = Field("Totals Font Size");
        trackedFields = sections.SelectMany(section => section.Fields).ToArray();
        savedValues = trackedFields.Select(field => field.Value).ToArray();
        foreach (var name in Template.Options.OrderBy(value => value == "Grid Classic" ? 0 : 1))
            Templates.Add(new PdfTemplateOptionViewModel(name, Description(name)));
        foreach (var field in trackedFields) field.PropertyChanged += FieldChanged;
        RefreshDerivedState();
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FormField.Value) && args.PropertyName != nameof(FormField.IsChecked)) return;
        HasChanges = !trackedFields.Select(field => field.Value).SequenceEqual(savedValues);
        RefreshDerivedState();
    }

    partial void OnHasChangesChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private void RefreshDerivedState()
    {
        foreach (var item in Templates)
        {
            item.IsSelected = item.Name == Template.Value;
            item.IsVisible = PageSize.Value switch
            {
                "A5" => item.Name == "Grid Classic",
                "A6" => item.Name is "Compact" or "Grid Classic",
                "Thermal 80mm" or "Thermal 58mm" => item.Name == "Thermal",
                _ => item.Name is not ("Compact" or "Thermal")
            };
        }
        OnPropertyChanged(nameof(SupportsQuantity));
        OnPropertyChanged(nameof(SupportsFontSizes));
        OnPropertyChanged(nameof(IsPortrait));
        OnPropertyChanged(nameof(IsLandscape));
        OnPropertyChanged(nameof(Template));
        OnPropertyChanged(nameof(ThemeColor));
    }

    [RelayCommand]
    private void SelectTemplate(PdfTemplateOptionViewModel? item)
    {
        if (item is null || !item.IsVisible) return;
        Template.Value = item.Name;
    }

    [RelayCommand]
    private void SetOrientation(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) Orientation.Value = value;
    }

    [RelayCommand]
    private void SetThemeColor(string? value) => ThemeColor.Value = string.IsNullOrWhiteSpace(value) ? "#002E78" : value;

    [RelayCommand(CanExecute = nameof(HasChanges))]
    private void Save()
    {
        if (!model.SaveSettings("PDF Settings")) return;
        savedValues = trackedFields.Select(field => field.Value).ToArray();
        HasChanges = false;
    }

    [RelayCommand]
    private void Reset()
    {
        var defaults = FormCatalog.Settings()["PDF Settings"].SelectMany(section => section.Fields).ToArray();
        for (var index = 0; index < trackedFields.Length; index++)
        {
            trackedFields[index].Value = defaults[index].Value;
            trackedFields[index].IsChecked = defaults[index].IsChecked;
        }
        RefreshDerivedState();
    }

    [RelayCommand]
    private async Task LoadPrintersAsync()
    {
        if (IsLoadingPrinters) return;
        IsLoadingPrinters = true;
        try
        {
            var discovered = await printService.GetPrintersAsync();
            Printer.Options = new[] { PlatformPrinterService.DefaultPrinter }
                .Concat(discovered.Where(item => !PrinterClassifier.IsFilePrinter(item.Name)).Select(item => item.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            OnPropertyChanged(nameof(Printer));
            if (!Printer.Options.Contains(Printer.Value, StringComparer.OrdinalIgnoreCase))
                Printer.Value = discovered.FirstOrDefault(item => item.IsDefault && !PrinterClassifier.IsFilePrinter(item.Name))?.Name ?? Printer.Options[0];
        }
        catch (Exception exception)
        {
            AppErrorLog.Write(exception, "Refreshing printer choices");
            model.Status = "Printers could not be refreshed. Check the printing service configuration.";
        }
        finally { IsLoadingPrinters = false; }
    }

    public static string Description(string template) => template switch
    {
        "Modern" => "Bold header with contemporary styling", "Minimal" => "Simple and distraction-free",
        "Executive" => "Premium business layout with structured billing blocks", "Compact" => "Space-efficient receipt layout, ideal for A6 printing",
        "Thermal" => "Narrow receipt layout for thermal printers", "Grid Classic" => "Old-style bordered tabular bill, for A4, A5 and A6",
        _ => "Traditional layout with clean structure"
    };
}
