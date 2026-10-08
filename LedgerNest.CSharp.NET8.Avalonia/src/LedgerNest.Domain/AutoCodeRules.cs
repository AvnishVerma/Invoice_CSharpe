using System.Globalization;

namespace LedgerNest.Domain;

public sealed record AutoCodeSettings(string Prefix, long NextNumber = 1, int LeadingZeros = 5, bool AutoGenerate = true);

public static class AutoCodeRules
{
    public static void Validate(AutoCodeSettings settings)
    {
        if (settings.Prefix == null || settings.Prefix.Length > 50 || settings.Prefix.Any(char.IsControl))
            throw new ArgumentException("Prefix must contain at most 50 printable characters.");
        if (settings.NextNumber < 1 || settings.NextNumber == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(settings.NextNumber), "Next number must be positive and leave room for the next code.");
        if (settings.LeadingZeros is < 0 or > 18)
            throw new ArgumentOutOfRangeException(nameof(settings.LeadingZeros), "Leading zeros must be between 0 and 18.");
    }

    public static string Format(string prefix, long number, int leadingZeros)
    {
        Validate(new AutoCodeSettings(prefix, number, leadingZeros));
        return prefix.Trim() + number.ToString(leadingZeros == 0 ? "0" : "D" + leadingZeros, CultureInfo.InvariantCulture);
    }

    public static long NextCompatible(AutoCodeSettings settings, IEnumerable<string?> codes)
    {
        Validate(settings);
        var next = settings.NextNumber;
        var prefix = settings.Prefix.Trim();
        foreach (var code in codes)
        {
            if (code == null || !code.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var suffix = code.AsSpan(prefix.Length);
            if (suffix.Length == 0 || suffix.ContainsAnyExceptInRange('0', '9')) continue;
            if (long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= next)
            {
                if (number >= long.MaxValue - 1) throw new InvalidOperationException("Compatible code numbers are exhausted. Choose another prefix.");
                next = number + 1;
            }
        }
        Validate(settings with { NextNumber = next });
        return next;
    }
}
