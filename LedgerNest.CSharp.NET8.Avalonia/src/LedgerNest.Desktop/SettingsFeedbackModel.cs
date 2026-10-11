using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop;

/// <summary>Presentation-only feedback supplied by a page's existing workflow.</summary>
public sealed partial class SettingsFeedbackModel : ObservableObject
{
    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private bool isSuccess;
    [ObservableProperty] private bool isFailure;
    [ObservableProperty] private bool hasUnsavedChanges;
}
