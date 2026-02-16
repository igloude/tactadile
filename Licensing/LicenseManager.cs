using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tactadile.Config;

namespace Tactadile.Licensing;

public sealed class LicenseManager
{
    private static readonly string TokenFilePath = Path.Combine(
        ConfigManager.ConfigDirectory, "license_token.json");
    private static readonly string EncryptedKeyFilePath = Path.Combine(
        ConfigManager.ConfigDirectory, "license_key.bin");
    private static readonly string RefreshTimestampPath = Path.Combine(
        ConfigManager.ConfigDirectory, "license_refresh_ts");

    // TODO: Replace with actual RSA public key once the entitlement service is set up.
    // Generate a 2048-bit RSA key pair; embed the public key here as Base64 (SubjectPublicKeyInfo).
    private const string PublicKeyBase64 = "";

    private const int RefreshIntervalDays = 30;
    private const int StaleWarningDays = 90;

    private static readonly HashSet<ActionType> FreeActions = new()
    {
        ActionType.MoveDrag,
        ActionType.ResizeDrag
    };

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions TokenJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly LicenseClient _client = new();

    public LicenseTier CurrentTier { get; private set; } = LicenseTier.Free;
    public LicenseToken? CurrentToken { get; private set; }
    public DateTime? LastRefreshUtc { get; private set; }
    public bool IsStale => LastRefreshUtc.HasValue &&
        (DateTime.UtcNow - LastRefreshUtc.Value).TotalDays >= StaleWarningDays;

    public event Action<LicenseTier>? TierChanged;

    /// <summary>
    /// Load token from disk, verify signature, set current tier.
    /// Call once at startup (synchronous).
    /// </summary>
    public void Initialize()
    {
        LastRefreshUtc = LoadRefreshTimestamp();

        if (!File.Exists(TokenFilePath))
        {
            CurrentTier = LicenseTier.Free;
            return;
        }

        try
        {
            var json = File.ReadAllText(TokenFilePath);
            var token = JsonSerializer.Deserialize<LicenseToken>(json, TokenJsonOptions);
            if (token == null)
            {
                CurrentTier = LicenseTier.Free;
                return;
            }

            if (!VerifySignature(token))
            {
                CurrentTier = LicenseTier.Free;
                return;
            }

            CurrentToken = token;
            CurrentTier = ResolveTier(token);
        }
        catch
        {
            CurrentTier = LicenseTier.Free;
        }
    }

    public bool IsActionAllowed(ActionType action)
    {
        if (CurrentTier is LicenseTier.Pro or LicenseTier.Lifetime)
            return true;
        return FreeActions.Contains(action);
    }

    public bool IsUpdateEligible()
    {
        if (CurrentTier == LicenseTier.Lifetime) return true;
        if (CurrentTier == LicenseTier.Free) return true; // Free always gets updates (limited features)
        if (CurrentToken?.UpdatesUntilUtc == null) return false;
        return BuildMetadata.BuildDateUtc <= CurrentToken.UpdatesUntilUtc.Value;
    }

    /// <summary>
    /// Activate a license key. Returns (success, errorMessage).
    /// </summary>
    public async Task<(bool Success, string Error)> ActivateAsync(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return (false, "Please enter a license key.");

        var response = await _client.ActivateAsync(licenseKey.Trim());

        if (response == null)
            return (false, "Network error. Check your internet connection and try again.");

        if (response.Error != null)
            return (false, response.Message ?? "Activation failed.");

        if (response.Token == null)
            return (false, "Invalid server response.");

        // Apply the signature from the outer response to the token
        response.Token.Signature = response.Signature;

        if (!VerifySignature(response.Token))
            return (false, "Invalid server response (signature verification failed).");

        // Store token
        try
        {
            Directory.CreateDirectory(ConfigManager.ConfigDirectory);
            var tokenJson = JsonSerializer.Serialize(response.Token, TokenJsonOptions);
            File.WriteAllText(TokenFilePath, tokenJson);
        }
        catch
        {
            return (false, "Failed to save license token.");
        }

        // Store encrypted license key for renewal URL
        StoreEncryptedKey(licenseKey.Trim());

        // Update refresh timestamp
        SaveRefreshTimestamp(DateTime.UtcNow);
        LastRefreshUtc = DateTime.UtcNow;

        // Apply tier
        CurrentToken = response.Token;
        var newTier = ResolveTier(response.Token);
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            TierChanged?.Invoke(CurrentTier);
        }
        else
        {
            CurrentTier = newTier;
        }

        return (true, "");
    }

    /// <summary>
    /// Best-effort periodic refresh. Returns true if a refresh was performed (success or downgrade).
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        if (CurrentToken == null)
            return false;

        // Skip if refreshed recently
        if (LastRefreshUtc.HasValue &&
            (DateTime.UtcNow - LastRefreshUtc.Value).TotalDays < RefreshIntervalDays)
            return false;

        // Need the raw license key for refresh
        var licenseKey = LoadEncryptedKey();
        if (licenseKey == null)
            return false;

        var response = await _client.RefreshAsync(licenseKey);

        if (response == null)
            return false; // Network error — silently continue

        // Check for revocation/refund
        if (response.Error is "revoked" or "refunded" or "not_found")
        {
            CurrentToken = null;
            var oldTier = CurrentTier;
            CurrentTier = LicenseTier.Free;
            if (oldTier != LicenseTier.Free)
                TierChanged?.Invoke(LicenseTier.Free);

            // Delete token file but keep config
            try { File.Delete(TokenFilePath); } catch { }
            try { File.Delete(EncryptedKeyFilePath); } catch { }

            return true;
        }

        if (response.Token == null)
            return false;

        response.Token.Signature = response.Signature;

        if (!VerifySignature(response.Token))
            return false;

        // Update token on disk
        try
        {
            var tokenJson = JsonSerializer.Serialize(response.Token, TokenJsonOptions);
            File.WriteAllText(TokenFilePath, tokenJson);
        }
        catch
        {
            return false;
        }

        SaveRefreshTimestamp(DateTime.UtcNow);
        LastRefreshUtc = DateTime.UtcNow;

        CurrentToken = response.Token;
        var newTier = ResolveTier(response.Token);
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            TierChanged?.Invoke(CurrentTier);
        }

        return true;
    }

    /// <summary>
    /// Returns the Gumroad renewal checkout URL with the license key parameter, or null if key is unavailable.
    /// </summary>
    public string? GetRenewalUrl()
    {
        var key = LoadEncryptedKey();
        if (key == null) return null;
        return $"https://store.tactadile.app/renew?license={Uri.EscapeDataString(key)}";
    }

    /// <summary>
    /// Returns the Gumroad Lifetime upgrade URL, or null if key is unavailable.
    /// </summary>
    public string? GetUpgradeUrl()
    {
        var key = LoadEncryptedKey();
        if (key == null) return null;
        return $"https://store.tactadile.app/upgrade?license={Uri.EscapeDataString(key)}";
    }

    private bool VerifySignature(LicenseToken token)
    {
        if (string.IsNullOrEmpty(PublicKeyBase64))
            return true; // No key configured yet — allow all tokens during development

        try
        {
            var payload = BuildCanonicalPayload(token);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(token.Signature);

            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);

            return rsa.VerifyData(payloadBytes, signatureBytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildCanonicalPayload(LicenseToken token)
    {
        // Alphabetical key order: issued_at_utc, license_key_hash, tier, token_version, updates_until_utc
        var payload = new SortedDictionary<string, object?>
        {
            ["issued_at_utc"] = token.IssuedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["license_key_hash"] = token.LicenseKeyHash,
            ["tier"] = token.Tier,
            ["token_version"] = token.TokenVersion,
            ["updates_until_utc"] = token.UpdatesUntilUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
        };

        return JsonSerializer.Serialize(payload, CanonicalJsonOptions);
    }

    private static LicenseTier ResolveTier(LicenseToken token)
    {
        return token.Tier.ToLowerInvariant() switch
        {
            "pro" => LicenseTier.Pro,
            "lifetime" => LicenseTier.Lifetime,
            _ => LicenseTier.Free
        };
    }

    private void StoreEncryptedKey(string licenseKey)
    {
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(licenseKey);
            var encryptedBytes = System.Security.Cryptography.ProtectedData.Protect(
                plainBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(ConfigManager.ConfigDirectory);
            File.WriteAllBytes(EncryptedKeyFilePath, encryptedBytes);
        }
        catch
        {
            // Best effort — failure means renewal URL won't include the key
        }
    }

    private string? LoadEncryptedKey()
    {
        if (!File.Exists(EncryptedKeyFilePath)) return null;
        try
        {
            var encryptedBytes = File.ReadAllBytes(EncryptedKeyFilePath);
            var plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                encryptedBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveRefreshTimestamp(DateTime utc)
    {
        try
        {
            Directory.CreateDirectory(ConfigManager.ConfigDirectory);
            File.WriteAllText(RefreshTimestampPath,
                utc.ToString("o", CultureInfo.InvariantCulture));
        }
        catch { }
    }

    private static DateTime? LoadRefreshTimestamp()
    {
        if (!File.Exists(RefreshTimestampPath)) return null;
        try
        {
            var text = File.ReadAllText(RefreshTimestampPath).Trim();
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                return dt;
        }
        catch { }
        return null;
    }
}
