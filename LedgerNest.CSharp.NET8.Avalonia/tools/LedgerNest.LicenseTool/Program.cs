using System.Globalization;
using System.Security.Cryptography;
using LedgerNest.LicensePublisher;
using LedgerNest.Application;
using LedgerNest.Domain;

// Publisher utility: never package this executable or signing keys with the desktop app.
try
{
    if (args.Length == 0 || args[0] is "--help" or "help")
    {
        Console.WriteLine("""
            LedgerNest publisher license tool
              keygen --private issuer.private.pem --public public-key.pem
              issue --private issuer.private.pem --customer "Customer" --device DEVICE_ID --out customer.ledgerlicense [--days 365 | --perpetual] [--trial]
              verify --public public-key.pem --device DEVICE_ID --file customer.ledgerlicense
            Trial licenses default to 30 days; paid licenses default to 365 days.
            Keys are password-encrypted. Password is prompted securely, or read from LEDGERNEST_SIGNING_PASSWORD for automation.
            Output files are never overwritten. Keep the private key and password outside customer installations.
            """);
        return 0;
    }
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Expected a named --option.");
        var key = args[i][2..];
        if (!options.TryAdd(key, key is "trial" or "perpetual" ? "true" : ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value.")))
            throw new ArgumentException("Duplicate option: " + key);
    }
    string Required(string key) => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing --" + key);
    void Allowed(params string[] names)
    {
        if (options.Keys.Any(key => !names.Contains(key))) throw new ArgumentException("Unknown option.");
    }
    switch (args[0])
    {
        case "keygen":
            Allowed("private", "public");
            var privatePath = Required("private"); var publicPath = Required("public");
            LicenseIssuer.SaveKeys(privatePath, publicPath, LicenseIssuer.GenerateKeys(ReadPassword()));
            Console.WriteLine("Created encrypted publisher key and public verification key. Back up the encrypted private key securely.");
            break;
        case "issue":
            Allowed("private", "customer", "device", "out", "days", "perpetual", "trial");
            var customer = Required("customer"); var device = Required("device").ToUpperInvariant();
            if (customer.Length > 200 || device.Length != 64 || device.Any(character => !char.IsAsciiHexDigit(character))) throw new ArgumentException("Use a customer name up to 200 characters and the 64-character Device ID from the app.");
            var trial = options.ContainsKey("trial"); var perpetual = options.ContainsKey("perpetual");
            if (perpetual && (trial || options.ContainsKey("days"))) throw new ArgumentException("--perpetual cannot be combined with --trial or --days.");
            var days = options.TryGetValue("days", out var daysText) ? int.Parse(daysText, CultureInfo.InvariantCulture) : trial ? 30 : 365;
            if (days < 1 || days > (trial ? 30 : 36500)) throw new ArgumentException("License duration is outside the supported range.");
            var output = Required("out");
            if (File.Exists(output)) throw new ArgumentException("Output file already exists.");
            var issued = LicenseIssuer.Issue(File.ReadAllText(Required("private")), ReadPassword(), customer, device, trial, perpetual, days);
            LicenseIssuer.WriteNew(output, issued.Document);
            var claims = issued.Claims;
            Console.WriteLine($"Issued {claims.Kind} license {claims.LicenseId}. Expiry: {claims.ExpiresAtUtc?.ToString("u") ?? "Perpetual"}.");
            break;
        case "verify":
            Allowed("public", "device", "file");
            var result = new LicenseVerifier(File.ReadAllText(Required("public"))).Verify(File.ReadAllText(Required("file")), Required("device").ToUpperInvariant(), DateTimeOffset.UtcNow);
            Console.WriteLine(result.Message);
            return result.State is LicenseState.Active or LicenseState.Trial ? 0 : 1;
        default: throw new ArgumentException("Unknown command. Use --help.");
    }
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or CryptographicException or FormatException or OverflowException)
{
    Console.Error.WriteLine("License operation failed: " + ex.Message);
    return 1;
}

static string ReadPassword()
{
    var configured = Environment.GetEnvironmentVariable("LEDGERNEST_SIGNING_PASSWORD");
    if (!string.IsNullOrEmpty(configured)) return configured;
    if (Console.IsInputRedirected) throw new ArgumentException("Set LEDGERNEST_SIGNING_PASSWORD for noninteractive use.");
    Console.Write("Signing password: ");
    var password = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && password.Length > 0) password.Length--;
        else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
    }
    Console.WriteLine();
    return password.ToString();
}
