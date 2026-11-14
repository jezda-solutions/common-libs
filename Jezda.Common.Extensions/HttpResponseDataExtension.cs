using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Extensions;

public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Deserializes a successful response's JSON body.
    /// </summary>
    /// <remarks>
    /// The body is read as UTF-8 whatever charset the <c>Content-Type</c> header declares, as RFC 8259
    /// requires of JSON. <c>ReadFromJsonAsync</c> decodes by the declared charset instead, and throws
    /// for one .NET will not decode, such as <c>utf-7</c> or an unknown name.
    /// </remarks>
    /// <returns>The body, or null when the body is the JSON literal <c>null</c>.</returns>
    /// <exception cref="HttpRequestException">
    /// The status is not a success; <see cref="HttpRequestException.StatusCode"/> carries it. Returning
    /// default instead made a 500 read as <c>0</c>, <c>false</c> or an empty result.
    /// </exception>
    public static async Task<T?> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}
