using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Tactadile.Licensing;

public sealed class LicenseClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BaseUrl = "https://api.tactadile.app";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Activate a license key. Returns the response on HTTP success, null on network/parse failure.
    /// </summary>
    public async Task<LicenseResponse?> ActivateAsync(string licenseKey)
    {
        return await PostAsync("/api/license/activate", new { license_key = licenseKey });
    }

    /// <summary>
    /// Refresh an existing license. Returns the response on HTTP success, null on network/parse failure.
    /// </summary>
    public async Task<LicenseResponse?> RefreshAsync(string licenseKey)
    {
        return await PostAsync("/api/license/refresh", new { license_key = licenseKey });
    }

    private async Task<LicenseResponse?> PostAsync(string path, object body)
    {
        try
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(BaseUrl + path, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<LicenseResponse>(responseBody, JsonOptions);
                return result;
            }

            // Try to parse error response for structured error messages
            try
            {
                var errorResponse = JsonSerializer.Deserialize<LicenseResponse>(responseBody, JsonOptions);
                return errorResponse;
            }
            catch
            {
                return new LicenseResponse
                {
                    Error = "server_error",
                    Message = $"Server returned {(int)response.StatusCode}."
                };
            }
        }
        catch (TaskCanceledException)
        {
            return null; // Timeout
        }
        catch (HttpRequestException)
        {
            return null; // Network error
        }
        catch
        {
            return null; // Unexpected
        }
    }
}
