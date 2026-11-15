using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Jezda.Common.Extensions;
using Xunit;

namespace Jezda.Common.Data.Tests;

public class HttpResponseDataExtensionTests
{
    private sealed record Item(int Id, string Name);

    [Theory]
    [InlineData("utf-7")] // .NET knows the name but has disabled it
    [InlineData("windows-1252")] // unknown without the CodePages provider
    [InlineData("utf-8")]
    public async Task ReadApiResponseDataAsync_ReadsJsonAsUtf8WhateverTheDeclaredCharset(string charset)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"id":1,"name":"Šećer"}"""));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = charset };
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        var item = await response.ReadApiResponseDataAsync<Item>();

        Assert.Equal(new Item(1, "Šećer"), item);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ReadApiResponseDataAsync_FailedResponse_ThrowsWithTheStatus(HttpStatusCode status)
    {
        using var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };

        var ex = await Assert.ThrowsAnyAsync<HttpRequestException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponseForAValueType_ThrowsInsteadOfReturningZero()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("0") };

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => response.ReadApiResponseDataAsync<int>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_JsonNull_ReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") };

        Assert.Null(await response.ReadApiResponseDataAsync<Item>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_KeepsTheBodyOnTheException()
    {
        const string problem = """{"type":"about:blank","status":409,"code":"duplicate_name"}""";
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(problem) };

        var ex = await Assert.ThrowsAsync<ApiResponseException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal(problem, ex.ResponseBody);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_StillCatchableAsHttpRequestException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => response.ReadApiResponseDataAsync<Item>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_BodyIsCapped()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(new string('x', 100_000)) };

        var ex = await Assert.ThrowsAsync<ApiResponseException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Equal(ApiResponseException.MaxBodyLength, ex.ResponseBody!.Length);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponseWithoutBody_HasNullBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway);

        var ex = await Assert.ThrowsAsync<ApiResponseException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Null(ex.ResponseBody);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_NoContent_ReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        Assert.Null(await response.ReadApiResponseDataAsync<Item>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_EmptyOkBody_ReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };

        Assert.Null(await response.ReadApiResponseDataAsync<Item>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_EmptyOkBody_ForANullableValueType_ReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };

        Assert.Null(await response.ReadApiResponseDataAsync<int?>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_EmptyOkBody_ForAValueType_ThrowsInsteadOfReturningZero()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };

        await Assert.ThrowsAsync<JsonException>(() => response.ReadApiResponseDataAsync<int>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_NoContent_ForAValueType_Throws()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        await Assert.ThrowsAsync<JsonException>(() => response.ReadApiResponseDataAsync<int>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_NullLiteral_ForAValueType_Throws()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") };

        await Assert.ThrowsAsync<JsonException>(() => response.ReadApiResponseDataAsync<int>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_CutBodyEndsOnWholeCharacters()
    {
        // 😀 is a surrogate pair: an odd cap would split it
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("a" + string.Concat(Enumerable.Repeat("😀", 5000))) };

        var ex = await Assert.ThrowsAsync<ApiResponseException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.False(char.IsHighSurrogate(ex.ResponseBody![^1]));
        Assert.DoesNotContain('\uFFFD', ex.ResponseBody);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_BomIsNotKept()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("{}")).ToArray();
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new ByteArrayContent(bytes) };

        var ex = await Assert.ThrowsAsync<ApiResponseException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Equal("{}", ex.ResponseBody);
    }
}
