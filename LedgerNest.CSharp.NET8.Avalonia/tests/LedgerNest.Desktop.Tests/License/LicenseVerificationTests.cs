using System.Security.Cryptography;
using System.Text.Json;
using LedgerNest.Application;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.License;

[Trait("Category", "Unit")]
public sealed class LicenseVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Paid", 1, 0, LicenseState.Active)]
    [InlineData("Trial", 1, 0, LicenseState.Trial)]
    [InlineData("Paid", -1, -2, LicenseState.Expired)]
    [InlineData("Paid", 0, -2, LicenseState.Expired)]
    [InlineData("Paid", 2, 1, LicenseState.NotYetValid)]
    public void Verify_SignedLicense_TimeBoundariesEnforced(string kind, int expiryDays, int startsDays, LicenseState expected)
    {
        using var rsa = RSA.Create(2048);
        var claims = Claims() with { Kind = kind, NotBeforeUtc = Now.AddDays(startsDays), ExpiresAtUtc = Now.AddDays(expiryDays) };
        Assert.Equal(expected, new LicenseVerifier(rsa.ExportSubjectPublicKeyInfoPem()).Verify(Sign(rsa, claims), "test-device", Now).State);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"Payload\":\"invalid-base64\",\"Signature\":\"invalid\"}")]
    public void Verify_InvalidLicense_RejectedWithoutCrash(string document)
    {
        using var rsa = RSA.Create(2048);
        var status = new LicenseVerifier(rsa.ExportSubjectPublicKeyInfoPem()).Verify(document, "test-device", Now);
        Assert.Equal(document.Length == 0 ? LicenseState.Missing : LicenseState.Invalid, status.State);
        Assert.False(status.Allows(LicenseFeatures.BusinessWrite));
    }

    [Fact]
    public void Verify_LicenseForOtherDevice_Rejected()
    {
        using var rsa = RSA.Create(2048);
        var status = new LicenseVerifier(rsa.ExportSubjectPublicKeyInfoPem()).Verify(Sign(rsa, Claims()), "other-device", Now);
        Assert.Equal(LicenseState.WrongDevice, status.State);
        Assert.False(status.Allows(LicenseFeatures.BusinessWrite));
    }

    [Fact]
    public void Verify_TamperedSignedPayload_Rejected()
    {
        using var rsa = RSA.Create(2048);
        var envelope = JsonSerializer.Deserialize<SignedLicense>(Sign(rsa, Claims()))!;
        var tampered = JsonSerializer.Serialize(envelope with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Claims() with { Customer = "tampered" })) });
        Assert.Equal(LicenseState.Invalid, new LicenseVerifier(rsa.ExportSubjectPublicKeyInfoPem()).Verify(tampered, "test-device", Now).State);
    }

    [Fact]
    public void Verify_PrivateTrustAnchor_NotConfigured()
    {
        using var rsa = RSA.Create(2048);
        Assert.False(new LicenseVerifier(rsa.ExportPkcs8PrivateKeyPem()).IsConfigured);
    }

    private static LicenseClaims Claims() => new()
    {
        LicenseId = "8fe62a18-2221-44e1-a5bc-966f882f53cb", Customer = "Test Company", DeviceId = "test-device",
        IssuedAtUtc = Now.AddDays(-2), NotBeforeUtc = Now.AddDays(-2), ExpiresAtUtc = Now.AddDays(1), Features = [LicenseFeatures.BusinessWrite]
    };
    private static string Sign(RSA rsa, LicenseClaims claims)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(claims);
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload), Convert.ToBase64String(signature)));
    }
}
