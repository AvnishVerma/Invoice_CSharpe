using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class MasterCodeSettingsUiTests(HeadlessFixture headless)
{
    [Theory] [InlineData(360)] [InlineData(900)]
    public Task ActualAxaml_PreviewsAndPersistsBothConfigurations(int width) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel(); var state = workspace.CreateMasterCodeSettings();
        var view = new MasterCodeSettingsView { DataContext = state }; var window = new Window { Content = view, Width = width, Height = 900 };
        try
        {
            window.Show(); Settle(window);
            Assert.Equal(2, view.GetVisualDescendants().OfType<ToggleSwitch>().Count());
            var customer = state.Configurations.Single(item => item.Entity == "Customer");
            var prefix = view.GetVisualDescendants().OfType<TextBox>().Single(item => Avalonia.Automation.AutomationProperties.GetName(item) == "Customer Code Prefix");
            prefix.Text = "CUS-"; customer.NextNumber = 25; customer.LeadingZeros = 5; Settle(window);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), item => item.Text == "Preview: CUS-00025");
            Assert.Equal(1, new AutoCodeGenerator(fixture).Load("Customer").NextNumber); // Preview does not consume a number.
            var product = state.Configurations.Single(item => item.Entity == "Product"); product.Prefix = "PROD"; product.NextNumber = 100; product.LeadingZeros = 6;
            state.SaveCommand.Execute(null); Assert.Empty(state.Error);
            var reload = fixture.CreateModel().CreateMasterCodeSettings(); Assert.Equal("Preview: CUS-00025", reload.Configurations[0].Preview); Assert.Equal("Preview: PROD000100", reload.Configurations[1].Preview);
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "LedgerNest.CSharp.sln"))) root = root.Parent;
            Assert.NotNull(root);
            var output = Path.Combine(root!.FullName, "artifacts", "master-code-ui"); Directory.CreateDirectory(output);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            using (var frame = window.CaptureRenderedFrame()) { Assert.NotNull(frame); frame!.Save(Path.Combine(output, $"code-settings-{width}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); }
            customer.NextNumber = 0; state.SaveCommand.Execute(null); Assert.NotEmpty(state.Error);
            Assert.Equal(25, new AutoCodeGenerator(fixture).Load("Customer").NextNumber);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
