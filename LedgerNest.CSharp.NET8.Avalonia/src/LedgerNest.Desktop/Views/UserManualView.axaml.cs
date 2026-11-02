using System.ComponentModel;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class UserManualView : UserControl
{
    private UserManualViewModel? model;
    public UserManualView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (model != null) model.PropertyChanged -= OnModelChanged;
            model = DataContext as UserManualViewModel;
            if (model != null) model.PropertyChanged += OnModelChanged;
        };
        DetachedFromVisualTree += (_, _) => { if (model != null) model.PropertyChanged -= OnModelChanged; };
        AttachedToVisualTree += (_, _) =>
        {
            if (model == null) return;
            model.PropertyChanged -= OnModelChanged;
            model.PropertyChanged += OnModelChanged;
        };
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UserManualViewModel.SelectedTopic)) ArticleScroll.Offset = default;
    }
}
