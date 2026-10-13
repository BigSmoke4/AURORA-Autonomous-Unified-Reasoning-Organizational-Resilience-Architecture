using System.Net.Http.Headers;
using System.Text;

namespace Aurora.Web.Integration.Feeds;

/// <summary>
/// Fetches one payload from a live feed endpoint. The body is read up to a size limit, so a misbehaving source cannot
/// exhaust memory. Error messages name the feed and the status code, but never the API key or the response body.
/// </summary>
internal static class HttpFeedClient
{
    public const int MaxPayloadBytes = 5 * 1024 * 1024;
    private const int ReadBufferBytes = 8192;

    public static async Task<string> GetPayloadAsync(HttpClient client, FeedEndpointOptions endpoint, string sourceKey, CancellationToken ct)
    {
        if (!endpoint.IsConfigured)
        {
            throw new InvalidOperationException($"Feed '{sourceKey}' has no BaseUrl configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Path.TrimStart('/'));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(endpoint.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Feed '{sourceKey}' returned HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadBufferBytes];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxPayloadBytes)
            {
                throw new InvalidDataException($"Feed '{sourceKey}' sent more than {MaxPayloadBytes} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
