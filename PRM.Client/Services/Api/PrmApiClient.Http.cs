using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PRM.Client.Services.Api;

namespace PRM.Client.Services;

public sealed partial class PrmApiClient
{
    private HttpRequestMessage CreateAuthorized(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (session.IsAuthenticated)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return request;
    }

    private async Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Get, url);
        return await SendAsync<T>(request, ct);
    }

    private async Task<ApiResult<T>> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Post, url);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendAsync<T>(request, ct);
    }

    private async Task<ApiResult<T>> PutAsync<T>(string url, object body, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Put, url);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendAsync<T>(request, ct);
    }

    private async Task<ApiResult<T>> PatchAsync<T>(string url, object body, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Patch, url);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendAsync<T>(request, ct);
    }

    private async Task<ApiResult<T>> DeleteAsync<T>(string url, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Delete, url);
        return await SendAsync<T>(request, ct);
    }

    private async Task<ApiResult> PutVoidAsync(string url, object body, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Put, url);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendVoidAsync(request, ct);
    }

    private async Task<ApiResult> DeleteVoidAsync(string url, CancellationToken ct)
    {
        using var request = CreateAuthorized(HttpMethod.Delete, url);
        return await SendVoidAsync(request, ct);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(body))
                    return ApiResult<T>.Fail("API returned an empty response.");

                try
                {
                    var data = JsonSerializer.Deserialize<T>(body, JsonOptions);
                    return ApiResult<T>.Ok(data!);
                }
                catch (JsonException)
                {
                    return ApiResult<T>.Fail("API returned an invalid response.");
                }
            }

            return ApiResult<T>.Fail(await ReadErrorAsync(response, ct));
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"Cannot reach API server: {ex.Message}");
        }
    }

    private async Task<ApiResult> SendVoidAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return ApiResult.Ok();

            return ApiResult.Fail(await ReadErrorAsync(response, ct));
        }
        catch (HttpRequestException ex)
        {
            return ApiResult.Fail($"Cannot reach API server: {ex.Message}");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
        }
        catch
        {
            // fall through
        }

        return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase})";
    }

    private sealed record ErrorResponse(string? Message);
}
