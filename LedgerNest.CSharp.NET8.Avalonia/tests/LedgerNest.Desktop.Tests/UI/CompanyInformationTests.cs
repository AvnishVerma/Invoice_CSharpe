using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class CompanyInformationTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData("Product")]
    [InlineData("Service")]
    [InlineData("Both")]
    public Task ExistingFieldsAndPaymentPreferencesPersist(string businessType) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        using var page = new CompanySettingsViewModel(fixture.CreateModel());
        page.CompanyName.Value = "Example Company"; page.Address.Value = "123 Street";
        page.Phone.Value = "9876543210"; page.Email.Value = "info@example.com";
        page.Website.Value = "example.com"; page.Pan.Value = "PAN123"; page.FssaiCode.Value = "FSSAI123";
        page.Gstin.Value = "GST123"; page.Country.Value = "India"; page.ShowLogo.IsChecked = false;
        page.ShowQr.IsChecked = true; page.ShowBank.IsChecked = true;
        page.AddUpiCommand.Execute(null); page.UpiAccounts[0][0].Value="Primary"; page.UpiAccounts[0][1].Value="merchant@upi";
        page.AddBankCommand.Execute(null); page.BankAccounts[0][0].Value="Main"; page.BankAccounts[0][1].Value="Example Bank";
        page.BankAccounts[0][2].Value="123456789"; page.BankAccounts[0][3].Value="TEST0001";
        page.SelectBusinessTypeCommand.Execute(businessType); page.SaveCommand.Execute(null);
        using var reload = new CompanySettingsViewModel(fixture.CreateModel());
        foreach (var pair in new[] { (page.CompanyName,reload.CompanyName),(page.Address,reload.Address),(page.Phone,reload.Phone),(page.Email,reload.Email),(page.Website,reload.Website),(page.Pan,reload.Pan),(page.FssaiCode,reload.FssaiCode),(page.Gstin,reload.Gstin),(page.Country,reload.Country) }) Assert.Equal(pair.Item1.Value,pair.Item2.Value);
        Assert.Equal(businessType,reload.BusinessType.Value); Assert.False(reload.ShowLogo.IsChecked);
        Assert.True(reload.ShowQr.IsChecked); Assert.True(reload.ShowBank.IsChecked);
        Assert.Equal("merchant@upi",reload.UpiAccounts[0][1].Value); Assert.Equal("123456789",reload.BankAccounts[0][2].Value);
        Assert.Same(reload.CompanyName,reload.NameInput.Field);
    });

    [Fact]
    public Task LogoReplaceRemoveAndValidationPreserveState() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); using var page = new CompanySettingsViewModel(fixture.CreateModel());
        page.CompanyName.Value = "Logo company";
        Assert.True(page.SetLogo(Png(8))); var original = page.LogoValue.Value;
        Assert.False(page.SetLogo([0,1,2])); Assert.Equal(original,page.LogoValue.Value);
        Assert.False(page.SetLogo(new byte[2*1024*1024+1])); Assert.Equal(original,page.LogoValue.Value);
        Assert.False(page.SetLogo(Png(1081))); Assert.Equal(original,page.LogoValue.Value);
        Assert.True(page.SetLogo(Png(16))); Assert.NotEqual(original,page.LogoValue.Value);
        page.SaveCommand.Execute(null);
        using (var reload = new CompanySettingsViewModel(fixture.CreateModel())) { Assert.True(reload.HasLogo); Assert.Equal(page.LogoValue.Value,reload.LogoValue.Value); }
        page.RemoveLogoCommand.Execute(null); Assert.False(page.HasLogo); Assert.Empty(page.LogoValue.Value);
        page.SaveCommand.Execute(null);
        using var removed = new CompanySettingsViewModel(fixture.CreateModel()); Assert.False(removed.HasLogo);
    });

    [Fact]
    public Task FailedSaveKeepsEnteredDataAndAllowsRetry() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        using var page = new CompanySettingsViewModel(workspace); page.CompanyName.Value = "Before"; page.SaveCommand.Execute(null);
        using var db = fixture.CreateDbContext();
        db.Database.ExecuteSqlRaw("CREATE TRIGGER fail_company_save BEFORE UPDATE ON settings BEGIN SELECT RAISE(ABORT, 'Injected save failure'); END");
        page.CompanyName.Value = "Keep this edit"; page.SaveCommand.Execute(null);
        Assert.Equal("Keep this edit",page.CompanyName.Value); Assert.Contains("could not be saved",workspace.Status);
        Assert.True(page.CanSave); Assert.False(page.IsSaving);
        db.Database.ExecuteSqlRaw("DROP TRIGGER fail_company_save"); page.SaveCommand.Execute(null);
        using var reload = new CompanySettingsViewModel(fixture.CreateModel()); Assert.Equal("Keep this edit",reload.CompanyName.Value);
    });

    [Theory]
    [InlineData(1200,false)] [InlineData(1200,true)] [InlineData(440,false)] [InlineData(440,true)]
    public Task ReferenceLayoutFitsDesktopAndNarrowThemes(int width,bool dark) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); using var model = new CompanySettingsViewModel(fixture.CreateModel(), _ => { });
        model.CompanyName.Value = "Your Company Name"; model.Phone.Value = "9876543210"; model.Email.Value = "info@yourcompany.com"; model.Address.Value = "123 Street\nCity, State 12345";
        var view = new CompanySettingsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var panel = view.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "LogoPanel");
            Assert.Equal(width>720 ? 240 : width,panel.Bounds.Width,0);
            var form = view.GetVisualDescendants().OfType<ScrollViewer>().Single(item => item.Name == "CompanyForm");
            var point = form.TranslatePoint(default,view)!.Value;
            if(width>720) Assert.Equal(240,point.X,0); else { Assert.Equal(0,point.X,0); Assert.True(point.Y>=300); }
            Assert.InRange(form.Bounds.Width+point.X,0,width+1);
            Assert.Equal(width>480?2:1,view.GetVisualDescendants().OfType<UniformGrid>().First(grid => grid.Classes.Contains("details-pair")).Columns);
            var nameView = view.GetVisualDescendants().OfType<CompanyFieldView>().First();
            var caption = nameView.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("field-caption"));
            var input = nameView.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.False(caption.IsVisible);
            input.Focus(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(caption.IsVisible);
            Assert.True(caption.TranslatePoint(default,nameView)!.Value.Y >= -0.5);
            view.GetVisualDescendants().OfType<ComboBox>().First().Focus(); Dispatcher.UIThread.RunJobs();
            Assert.False(caption.IsVisible);
            Assert.True(model.AddressInput.CanExpand);
            var save = view.GetVisualDescendants().OfType<ActionButtonView>().Single(button => button.ActionLabel=="Save");
            Assert.True(save.Bounds.Height>0); Assert.True(save.IsEnabled);
            var output=Path.Combine(Directory.GetCurrentDirectory(),"artifacts","company-reference"); Directory.CreateDirectory(output);
            using var frame=window.CaptureRenderedFrame(); Assert.NotNull(frame); frame!.Save(Path.Combine(output,$"company-{width}-{(dark?"dark":"light")}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally { window.Close(); }
    });
    private static byte[] Png(int width)
    {
        using var bitmap=new SKBitmap(width,1); bitmap.Erase(SKColors.Blue);
        using var image=SKImage.FromBitmap(bitmap); using var data=image.Encode(SKEncodedImageFormat.Png,100); return data.ToArray();
    }
}
