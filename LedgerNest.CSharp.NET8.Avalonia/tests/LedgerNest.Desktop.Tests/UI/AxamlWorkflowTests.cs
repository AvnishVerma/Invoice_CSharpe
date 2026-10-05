using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class AxamlWorkflowTests(HeadlessFixture headless)
{
    [Fact]
    public Task VoidedInvoice_ActualPaymentDialog_ShowsHistoryWithoutNewPaymentForm() => headless.Run(() =>
    {
        var view = new PaymentDialogView(new PaymentDialogModel { IsVoided = true });
        var window = new Window { Content = view, Width = 800, Height = 600 };
        try
        {
            window.Show(); Settle(window);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == "Invoice voided — payment history is read-only");
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == "New Payment");
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == "Invoice fully paid");
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(960, 600)]
    [InlineData(1440, 900)]
    public Task PermissionMatrix_ActualAxaml_ResourcesRenderOnceBelowHeader(double width, double height) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture();
        var permissions = fixture.CreateModel().PermissionManagement;
        var view = new PermissionManagementView { DataContext = permissions };
        var window = new Window { Content = view, Width = width, Height = height };
        try
        {
            window.Show();
            Settle(window);
            Assert.Single(view.GetVisualDescendants().OfType<PageHeaderView>());
            var rows = view.GetVisualDescendants().OfType<ResourcePermissionRowView>().ToArray();
            Assert.Equal(permissions.ResourcePermissions.Count, rows.Length);
            Assert.Equal(rows.Length, rows.Select(row => ((PermissionResourceRowViewModel)row.DataContext!).Resource).Distinct().Count());
            var header = view.GetVisualDescendants().OfType<PageHeaderView>().Single();
            Assert.All(rows, row => Assert.True(row.TranslatePoint(default, view)!.Value.Y >= header.Bounds.Bottom));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task PermissionToggle_ActualAxaml_ViewOffDisablesActionsAndViewOnRestoresEditing() => headless.Run(() =>
    {
        var row = new PermissionResourceRowViewModel
        {
            Resource = "Invoice", View = new PermissionRowViewModel { Action = "View" },
            Add = new PermissionRowViewModel { Action = "Add" }, Update = new PermissionRowViewModel { Action = "Update" },
            Delete = new PermissionRowViewModel { Action = "Delete" }
        };
        row.ConfigureViewDependency();
        var view = new ResourcePermissionRowView { DataContext = row };
        var window = new Window { Content = view, Width = 800, Height = 120 };
        try
        {
            window.Show(); Settle(window);
            var toggles = view.GetVisualDescendants().OfType<ToggleSwitch>().ToArray();
            Assert.Equal(4, toggles.Length);
            Assert.True(toggles[0].IsEnabled);
            Assert.All(toggles.Skip(1), toggle => Assert.False(toggle.IsEnabled));
            toggles[0].IsChecked = true;
            Settle(window);
            Assert.True(row.View.IsAllowed);
            Assert.All(toggles, toggle => Assert.True(toggle.IsEnabled));
            toggles[1].IsChecked = true;
            Assert.True(row.Add.IsAllowed);
            Assert.False(row.Update.IsAllowed);
            toggles[0].IsChecked = false;
            Assert.False(row.Add.IsAllowed);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task Accessibility_ActualAxaml_ShortcutsPresentAndInvoiceLayoutAbsent() => headless.Run(() =>
    {
        var view = new AccessibilitySettingsView(() => { });
        var window = new Window { Content = view, Width = 1200, Height = 800 };
        try
        {
            window.Show(); Settle(window);
            var labels = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? "").ToArray();
            foreach (var shortcut in new[] { "Ctrl + Q", "Ctrl + S", "Ctrl + F", "Ctrl + M", "Ctrl + O", "Ctrl + P" }) Assert.Contains(shortcut, labels);
            Assert.DoesNotContain(labels, text => text.Contains("Create Invoice Layout", StringComparison.OrdinalIgnoreCase));
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData("Company Information")]
    [InlineData("Backup Management")]
    [InlineData("User Management")]
    [InlineData("User Permissions")]
    [InlineData("PDF Settings")]
    [InlineData("Invoice Settings")]
    [InlineData("Product Details")]
    [InlineData("Accessibility")]
    [InlineData("License & Activation")]
    [InlineData("Software Information")]
    public Task Header_ActualSharedAxaml_FixedHeightTypographyAndNoSubtitle(string title) => headless.Run(() =>
    {
        var view = new PageHeaderView { Title = title };
        var window = new Window { Content = view, Width = 1200, Height = 100 };
        try
        {
            window.Show(); Settle(window);
            var border = view.GetVisualDescendants().OfType<Border>().Single(item => item.Classes.Contains("page-header"));
            var height = (double)Avalonia.Application.Current!.FindResource("PageHeaderHeight")!;
            Assert.Equal(height, border.Height);
            Assert.Equal(Math.Round(height * window.RenderScaling) / window.RenderScaling, border.Bounds.Height, 2);
            var heading = view.FindControl<TextBlock>("Heading")!;
            Assert.Equal(title, heading.Text);
            Assert.True(heading.FontWeight >= Avalonia.Media.FontWeight.SemiBold);
            Assert.Single(view.GetVisualDescendants().OfType<TextBlock>());
        }
        finally { window.Close(); }
    });

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
