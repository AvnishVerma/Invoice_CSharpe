using LedgerNest.Desktop;
using LedgerNest.Desktop.Printing;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Settings;

[Trait("Category", "Unit")]
public sealed class PdfSettingsTests
{
    [Fact]
    public void ResetPdfSettings_ModifiedValues_RestoresCatalogDefaults()
    {
        var model = new MainWindowViewModel(licenseService: new TestLicenseService());
        var settings = new PdfSettingsViewModel(model, new StubPrintService());
        settings.ThemeColor.Value = "#047857";
        settings.Template.Value = "Modern";
        Assert.True(settings.HasChanges);
        settings.ResetCommand.Execute(null);
        var expected = FormCatalog.Settings()["PDF Settings"].SelectMany(section => section.Fields).ToArray();
        Assert.Equal(expected.Select(field => field.Value), model.Settings["PDF Settings"].SelectMany(section => section.Fields).Select(field => field.Value));
        Assert.False(settings.HasChanges);
    }

    [Theory]
    [InlineData("A4", "Classic", true)]
    [InlineData("A4", "Thermal", false)]
    [InlineData("A5", "Grid Classic", true)]
    [InlineData("A5", "Modern", false)]
    [InlineData("A6", "Compact", true)]
    [InlineData("Thermal 80mm", "Thermal", true)]
    [InlineData("Thermal 58mm", "Grid Classic", false)]
    public void PdfTemplate_PageSize_OnlyCompatibleTemplatesAvailable(string size, string template, bool visible)
    {
        var model = new MainWindowViewModel(licenseService: new TestLicenseService());
        var settings = new PdfSettingsViewModel(model, new StubPrintService());
        settings.PageSize.Value = size;
        var option = settings.Templates.Single(item => item.Name == template);
        Assert.Equal(visible, option.IsVisible);
        var previous = settings.Template.Value;
        settings.SelectTemplateCommand.Execute(option);
        Assert.Equal(visible ? template : previous, settings.Template.Value);
    }

    [Fact]
    public async Task PrinterRefresh_DuplicateResultsAndVirtualPrinters_Filtered()
    {
        var settings = new PdfSettingsViewModel(new MainWindowViewModel(licenseService: new TestLicenseService()), new StubPrintService());
        await settings.LoadPrintersCommand.ExecuteAsync(null);
        Assert.False(settings.IsLoadingPrinters);
        Assert.DoesNotContain("Microsoft Print to PDF", settings.Printer.Options);
        Assert.Equal(settings.Printer.Options.Length, settings.Printer.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("Office", settings.Printer.Options);
    }

    private sealed class StubPrintService : IPrintService
    {
        public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PrinterInfo>>([new("Office", true), new("office"), new("Microsoft Print to PDF")]);
        public Task<PrinterInfo?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<PrinterInfo?>(new("Office", true));
        public Task PrintPdfAsync(string pdfFilePath, string? printerName = null, PrintOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Settings tests must not submit native print jobs.");
    }
}
