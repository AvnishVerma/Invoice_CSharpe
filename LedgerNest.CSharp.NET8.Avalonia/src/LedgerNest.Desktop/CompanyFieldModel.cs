using CommunityToolkit.Mvvm.Input;
namespace LedgerNest.Desktop;
public sealed record CompanyFieldModel(FormField Field, string Icon, IRelayCommand? ExpandCommand = null)
{
    public string ExpandIcon => "open_in_full";
    public bool IsChoice => Field.Kind == "choice";
    public bool IsMultiline => Field.Kind == "multiline";
    public bool CanExpand => ExpandCommand != null;
}
