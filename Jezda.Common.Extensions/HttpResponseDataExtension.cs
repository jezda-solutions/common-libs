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
    /// <returns>
    /// The body; null when the body is the JSON literal <c>null</c> and <typeparamref name="T"/> is a
    /// reference type, <c>default</c> for a value type.
    /// </returns>
    /// <exception cref="HttpRequestException">
    /// The status is not a success; <see cref="HttpRequestException.StatusCode"/> carries it.
    /// </exception>
    /// <exception cref="JsonException">The body is empty or is not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T?> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }
}
