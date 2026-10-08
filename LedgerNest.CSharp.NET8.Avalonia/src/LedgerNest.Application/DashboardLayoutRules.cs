namespace LedgerNest.Application;

public static class DashboardLayoutRules
{
    public const string SettingKey = "dashboard_layout";
    public static string? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "default" => "default", "classic" => "classic", "bento" => "bento",
        "simple" or "simple feed" => "simple", _ => null
    };
    public static string DisplayName(string? value) => Parse(value) switch
    {
        "classic" => "Classic", "bento" => "Bento", "simple" => "Simple", _ => "Default"
    };
}
