using System;
using System.Net;
using System.Net.Http;
using System.Text;
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
    /// for one .NET will not decode, such as <c>utf-7</c> or an unknown name. A successful body is
    /// buffered in memory before it is read, so an empty body can be told from malformed JSON.
    /// </remarks>
    /// <returns>
    /// The body; null when the body is the JSON literal <c>null</c> or empty (a 204, or a 2xx without
    /// content) and <typeparamref name="T"/> is a reference or nullable type. For a non-nullable value type
    /// both an empty body and the literal <c>null</c> are errors, so a body never reads as zero or <c>false</c>.
    /// </returns>
    /// <exception cref="ApiResponseException">
    /// The status is not a success; <see cref="HttpRequestException.StatusCode"/> carries it and
    /// <see cref="ApiResponseException.ResponseBody"/> the start of the error body.
    /// </exception>
    /// <exception cref="JsonException">
    /// The body is not valid JSON for <typeparamref name="T"/>, or is empty or the literal <c>null</c> and
    /// <typeparamref name="T"/> is a non-nullable value type.
    /// </exception>
    public static async Task<T?> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiResponseException(response.StatusCode, await ReadErrorBodyAsync(response, ct));

        // Buffered, so the length is known and an empty body can be told from malformed JSON
        await response.Content.LoadIntoBufferAsync(ct);
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            if (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null)
                return default;

            throw new JsonException("The response body is empty.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    // Best effort: a body that cannot be read must not hide the status.
    private static async Task<string?> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[ApiResponseException.MaxBodyLength * 4];
            var read = 0;
            int n;
            while (read < buffer.Length && (n = await stream.ReadAsync(buffer.AsMemory(read), ct)) > 0)
                read += n;
            if (read == 0)
                return null;

            var text = Encoding.UTF8.GetString(buffer, 0, read).TrimStart('\uFEFF');
            if (text.Length > ApiResponseException.MaxBodyLength)
                text = text[..ApiResponseException.MaxBodyLength];

            // The cut may have split a surrogate pair or a multi-byte character; drop that last half
            return text.Length > 0 && (char.IsHighSurrogate(text[^1]) || (read == buffer.Length && text[^1] == '\uFFFD'))
                ? text[..^1]
                : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
