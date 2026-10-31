using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Threading;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class TooltipCoverageTests(HeadlessFixture headless)
{
    [Fact]
    public Task SharedTooltipsUseLabelsAndPreserveExplicitHelp() => headless.Run(() =>
    {
        var action = new ActionButtonView { Caption = "Add Row", ActionLabel = "Add" };
        var button = new Button { Content = "Save" };
        var icon = new Button(); AutomationProperties.SetName(icon, "Search customer");
        var field = new TextBox { PlaceholderText = "Email", Text = "private@example.com" };
        var password = new TextBox { PlaceholderText = "Password", PasswordChar = '*', Text = "secret" };
        var choice = new ComboBox { PlaceholderText = "Choose product" };
        var toggle = new CheckBox { Content = "Active price" };
        var explicitHelp = new Button { Content = "Delete", IsEnabled = false }; ToolTip.SetTip(explicitHelp, "Delete this product permanently");
        var number = new NumericUpDown(); AutomationProperties.SetName(number, "Quantity");
        var panel = new StackPanel { Children = { action, button, icon, field, password, choice, toggle, explicitHelp, number } };
        var window = new Window { Content = panel, Width = 600, Height = 800 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal("Add Row", ToolTip.GetTip(action));
            Assert.Equal("Save", ToolTip.GetTip(button));
            Assert.Equal("Search customer", ToolTip.GetTip(icon));
            Assert.Equal("Email", ToolTip.GetTip(field));
            Assert.Equal("Password", ToolTip.GetTip(password));
            Assert.Equal("Choose product", ToolTip.GetTip(choice));
            Assert.Equal("Active price", ToolTip.GetTip(toggle));
            Assert.Equal("Quantity", ToolTip.GetTip(number));
            Assert.Equal("Delete this product permanently", ToolTip.GetTip(explicitHelp));
            button.Content = "Cancel"; Assert.Equal("Cancel", ToolTip.GetTip(button));
            action.Caption = "Create Customer"; Assert.Equal("Create Customer", ToolTip.GetTip(action));
            action.Tooltip = "Create a new customer record";
            Assert.Equal("Create a new customer record", ToolTip.GetTip(action));
        }
        finally { window.Close(); }
    });
}
