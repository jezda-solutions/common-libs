using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => response.ReadApiResponseDataAsync<Item>());

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponseForAValueType_ThrowsInsteadOfReturningZero()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("0") };

        await Assert.ThrowsAsync<HttpRequestException>(() => response.ReadApiResponseDataAsync<int>());
    }

    [Fact]
    public async Task ReadApiResponseDataAsync_JsonNull_ReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") };

        Assert.Null(await response.ReadApiResponseDataAsync<Item>());
    }
}
