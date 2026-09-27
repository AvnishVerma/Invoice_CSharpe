using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop.Views;

/// <summary>Renders editable invoice lines through an XAML table template.</summary>
public sealed partial class InvoiceLineItemsView : UserControl
{
    public static readonly StyledProperty<bool> HasLinesProperty = AvaloniaProperty.Register<InvoiceLineItemsView, bool>(nameof(HasLines));
    public static readonly StyledProperty<bool> HasNoLinesProperty = AvaloniaProperty.Register<InvoiceLineItemsView, bool>(nameof(HasNoLines), true);
    public ObservableCollection<InvoiceLineItemModel> Lines { get; } = [];
    public bool HasLines { get => GetValue(HasLinesProperty); private set => SetValue(HasLinesProperty, value); }
    public bool HasNoLines { get => GetValue(HasNoLinesProperty); private set => SetValue(HasNoLinesProperty, value); }

    public InvoiceLineItemsView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void SetLines(IEnumerable<InvoiceLineViewModel> lines, bool showProductType, Action<InvoiceLineViewModel> remove)
    {
        Lines.Clear();
        foreach (var line in lines)
            Lines.Add(new InvoiceLineItemModel(line, showProductType, new RelayCommand(() => remove(line))));
        HasLines = Lines.Count > 0;
        HasNoLines = !HasLines;
    }

}

public sealed record InvoiceLineItemModel(InvoiceLineViewModel Line, bool ShowProductType, IRelayCommand RemoveCommand)
{
    public string UnitText => Line.Unit == "None" ? string.Empty : Line.Unit;
    public string ProductTypeText => Line.ProductType;
}
