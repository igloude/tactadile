using System.Text.Json.Serialization;

namespace Tactadile.Licensing;

public sealed class LicenseToken
{
    [JsonPropertyName("token_version")]
    public int TokenVersion { get; set; } = 1;

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "Free";

    [JsonPropertyName("updates_until_utc")]
    public DateTime? UpdatesUntilUtc { get; set; }

    [JsonPropertyName("issued_at_utc")]
    public DateTime IssuedAtUtc { get; set; }

    [JsonPropertyName("license_key_hash")]
    public string LicenseKeyHash { get; set; } = "";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}

/// <summary>
/// Wire format returned by the entitlement service.
/// </summary>
public sealed class LicenseResponse
{
    [JsonPropertyName("token")]
    public LicenseToken? Token { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
