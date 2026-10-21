using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class InvoiceReferenceUiTests(HeadlessFixture headless)
{
    [Fact]
    public Task AdjustmentCountBadgeIsCircular() => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var options = new InvoiceOptionsViewModel(model);
        options.AddCostCommand.Execute(null);
        options.Attach();
        var view = new InvoiceOptionsPanelView { DataContext = options };
        var window = new Window { Content = view, Width = 450, Height = 700 };
        try
        {
            window.Show(); Settle(window);
            var label = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "1");
            var badge = Assert.IsType<Border>(label.Parent);
            Assert.Equal(20, badge.Bounds.Width);
            Assert.Equal(badge.Bounds.Width, badge.Bounds.Height);
            Assert.Equal(10, badge.CornerRadius.TopLeft);
        }
        finally { window.Close(); options.Detach(); }
    });

    [Theory] [InlineData(1366)] [InlineData(1000)] [InlineData(800)]
    public Task ExistingInvoiceEditor_RendersResponsiveReferenceSections(int width) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel(false);
        using (var db = fixture.CreateDbContext()) { db.Users.Single(user => user.Username == "admin").PasswordChanged = true; db.SaveChanges(); }
        Assert.True(workspace.SignIn("admin", "admin")); workspace.StartDocument("Invoice");
        var window = new MainWindow { DataContext = workspace, Width = width, Height = 768 };
        try
        {
            window.Show(); Settle(window);
            var shell = Assert.Single(window.GetVisualDescendants().OfType<InvoiceEditorShellView>());
            var page = Assert.IsType<InvoiceEditorPageModel>(shell.DataContext);
            Assert.Equal(width < 1100, page.IsNarrow);
            Assert.Single(shell.GetVisualDescendants().OfType<InvoiceCustomerPanelView>());
            var tabs = shell.GetVisualDescendants().OfType<TabControl>().Single(control => control.Name == "InvoiceDetailTabs");
            Assert.Equal(5, tabs.ItemCount);
            tabs.SelectedIndex = 2; Settle(window);
            Assert.Single(shell.GetVisualDescendants().OfType<InvoiceOptionsPanelView>());
            tabs.SelectedIndex = 1; Settle(window);
            Assert.Single(shell.GetVisualDescendants().OfType<InvoiceDocumentDetailsPanelView>());
            Assert.Contains(shell.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "No items added yet");
            var search = shell.GetVisualDescendants().OfType<TextBox>().Single(box => box.PlaceholderText == "Search & add a product or service (Ctrl+F)");
            Assert.True(search.IsEffectivelyVisible);
            var divider = shell.GetVisualDescendants().OfType<GridSplitter>().ToArray();
            Assert.Equal(page.IsNarrow ? 0 : 1, divider.Length);
            Settle(window); Capture(window, "invoice-details-" + width);
            var details = shell.GetVisualDescendants().OfType<InvoiceDocumentDetailsPanelView>().Single();
            tabs.SelectedIndex = 0; Settle(window); Capture(window, "invoice-customer-" + width);
            Assert.False(workspace.HasUnsavedInvoiceChanges);
            Assert.NotNull(page.Options.NotesField.ExpandCommand);
            page.Options.NotesField.ExpandCommand!.Execute(null); Settle(window);
            var overlay = Assert.IsType<OverlayDialogViewModel>(workspace.CurrentOverlay);
            var note = Assert.IsType<ExpandedTextViewModel>(overlay.Body); Assert.Equal(1000, note.MaximumLength);
            note.Text = "Notes draft"; Assert.Equal("11/1000", note.CharacterCount);
            Capture(window, "notes-" + width);
            Assert.Empty(workspace.InvoiceOptions[2].Value);
            Assert.IsType<DialogActions>(overlay.Footer).Items.Single(action => action.Label == UiLocalization.TranslateKey("actionSave")).Command!.Execute(null);
            Assert.Equal("Notes draft", workspace.InvoiceOptions[2].Value); Assert.True(workspace.HasUnsavedInvoiceChanges);
            workspace.NavigateCommand.Execute("Dashboard"); Settle(window); Capture(window, "unsaved-" + width);
            Assert.Equal("New Invoice", workspace.Title);
            workspace.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing); workspace.CurrentOverlay = null;
            foreach (var button in shell.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible))
            {
                var point = button.TranslatePoint(default, shell);
                if (point != null) Assert.True(point.Value.X >= -1 && point.Value.X + button.Bounds.Width <= shell.Bounds.Width + 1, $"Button overflow at width {width}: {point}/{shell.Bounds}");
            }
        }
        finally { workspace.ResolveInvoiceLeave(InvoiceLeaveChoice.KeepEditing); workspace.MarkInvoiceFormClean(); window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "LedgerNest.CSharp.sln"))) root = root.Parent;
        Assert.NotNull(root); var output = Path.Combine(root!.FullName, "artifacts", "invoice-reference-ui"); Directory.CreateDirectory(output);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame!.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
