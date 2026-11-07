using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.Http;

/// <summary>
/// Reads response bodies as UTF-8 whatever charset the <c>Content-Type</c> header declares.
/// </summary>
/// <remarks>
/// <see cref="HttpContent.ReadAsStringAsync()"/> and <c>ReadFromJsonAsync</c> decode by the declared
/// charset, and throw for one .NET will not decode: <see cref="NotSupportedException"/> for
/// <c>utf-7</c>, which .NET knows by name but has disabled, and <see cref="InvalidOperationException"/>
/// for a name it does not know, such as <c>windows-1252</c> without the CodePages provider. On a
/// failed response that exception replaces the <see cref="HttpRequestException"/> carrying the
/// status, which is what callers branch on. Integration APIs answer JSON, which RFC 8259 requires to
/// be UTF-8, so these methods do not look at the charset at all, and none of those errors can occur.
/// </remarks>
public static class HttpContentUtf8Extensions
{
    /// <summary>
    /// Reads the body as UTF-8 text. Invalid bytes become U+FFFD instead of failing, and a leading byte
    /// order mark is dropped.
    /// </summary>
    public static async Task<string> ReadAsUtf8StringAsync(this HttpContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
        var text = Encoding.UTF8.GetString(bytes);

        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    /// <summary>
    /// Deserializes the body as UTF-8 JSON. A leading byte order mark is skipped.
    /// </summary>
    /// <exception cref="JsonException">The body is not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T?> ReadFromUtf8JsonAsync<T>(
        this HttpContent content,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken);
    }

    /// <summary>
    /// Sends a GET and deserializes the body as UTF-8 JSON: <c>GetFromJsonAsync</c> without its
    /// charset lookup.
    /// </summary>
    /// <exception cref="HttpRequestException">The response is not a success; it carries the status.</exception>
    /// <exception cref="JsonException">The body is not valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T?> GetFromUtf8JsonAsync<T>(
        this HttpClient client,
        string requestUri,
        JsonSerializerOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Headers only, as GetFromJsonAsync does: the body is streamed into the deserializer, not buffered.
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromUtf8JsonAsync<T>(options, cancellationToken);
    }
}
