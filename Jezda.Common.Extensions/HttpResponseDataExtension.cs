using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Extensions;

public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Deserializes a successful response's JSON body, or returns default for a failed one.
    /// </summary>
    /// <remarks>
    /// The body is read as UTF-8 whatever charset the <c>Content-Type</c> header declares, as RFC 8259
    /// requires of JSON. <c>ReadFromJsonAsync</c> decodes by the declared charset instead, and throws
    /// for one .NET will not decode, such as <c>utf-7</c> or an unknown name.
    /// </remarks>
    public static async Task<T> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        if (!response.IsSuccessStatusCode)
            return default!;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var wrapper = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);

        return wrapper is null
            ? default!
            : wrapper;
    }
}
