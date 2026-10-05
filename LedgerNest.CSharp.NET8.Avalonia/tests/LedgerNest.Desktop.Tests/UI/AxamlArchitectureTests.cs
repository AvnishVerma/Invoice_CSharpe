using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LedgerNest.Desktop.Tests.UI;

[Trait("Category", "Architecture")]
public sealed class AxamlArchitectureTests
{
    [Theory]
    [InlineData("Views/PageHeaderView.axaml")]
    [InlineData("Views/ActionButtonView.axaml")]
    [InlineData("Views/ScreenScaffoldView.axaml")]
    [InlineData("Views/SettingsPageView.axaml")]
    [InlineData("Views/ResourcePermissionRowView.axaml")]
    [InlineData("Views/PermissionToggleView.axaml")]
    [InlineData("Views/ProductEditorFormView.axaml")]
    [InlineData("MainWindow.axaml")]
    public void CommonVisuals_AxamlSource_ContainsDeclarativeStructure(string relative)
    {
        var document = XDocument.Load(Path.Combine(Root(), "src/LedgerNest.Desktop", relative));
        Assert.NotNull(document.Root);
        Assert.NotEmpty(document.Root!.Elements());
    }

    [Fact]
    public void SettingsShell_DeclarativeRows_HeaderBeforeNavigationAndContent()
    {
        var document = XDocument.Load(Path.Combine(Root(), "src/LedgerNest.Desktop/Views/SettingsPageView.axaml"));
        var grid = document.Descendants().First(element => element.Name.LocalName == "Grid");
        Assert.Equal("Auto,Auto,*", grid.Attribute("RowDefinitions")!.Value);
        Assert.Contains("CurrentHeader", grid.Elements().First().Attribute("Content")!.Value);
        Assert.Equal("1", grid.Elements().ElementAt(1).Attribute("Grid.Row")!.Value);
        Assert.Equal("2", grid.Elements().ElementAt(2).Attribute("Grid.Row")!.Value);
    }

    [Fact]
    public void ProductionUi_StandardControls_NotConstructedInCSharp()
    {
        var directory = Path.Combine(Root(), "src");
        // Explicit constructors, collection mutation, and target-typed visual factories are audited.
        // Named AXAML view initialization and ScottPlot data-series APIs are not visual builders.
        const string visuals = @"(?:Button|TextBlock|Grid|StackPanel|WrapPanel|Border|TextBox|ComboBox|CheckBox|ToggleSwitch|ListBox|DataGrid|TabControl|TabItem|UserControl|MenuItem|ContentControl|ScrollViewer|ContextMenu|Flyout|Popup|GridSplitter)";
        var pattern = new Regex($@"\bnew\s+(?:Avalonia\.Controls\.)?{visuals}\b|\.(?:Children|Controls)\.Add\s*\(|\b{visuals}\s+\w+\([^\r\n]*=>\s*new\s*\(|\b{visuals}\s+\w+\s*=\s*new\s*\(", RegexOptions.Compiled);
        var matches = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "artifacts"))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (line, index))
                .Where(item => pattern.IsMatch(item.line))
                .Select(item => $"{Path.GetRelativePath(directory, path)}:{item.index + 1}: {item.line.Trim()}"))
            .ToArray();
        Assert.True(matches.Length == 0, "Remaining code-generated UI (migration is incomplete):" + Environment.NewLine + string.Join(Environment.NewLine, matches));
    }

    private static string Root()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "LedgerNest.CSharp.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run architecture tests from the repository checkout.");
    }
}
