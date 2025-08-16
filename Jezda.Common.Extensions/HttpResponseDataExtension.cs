using Jezda.Common.Abstractions.Responses;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Extensions;

public static class HttpResponseMessageExtensions
{
    public static async Task<T> ReadApiResponseDataAsync<T>(
        this HttpResponseMessage response, 
        CancellationToken ct = default)
    {
        if (!response.IsSuccessStatusCode)
            return default!;

        var wrapper = await response.Content.ReadFromJsonAsync<T>(
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, 
            cancellationToken: ct
        );

        return wrapper is null
            ? default!
            : wrapper;
    }
}
