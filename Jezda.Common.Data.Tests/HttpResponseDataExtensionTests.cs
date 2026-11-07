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

    [Fact]
    public async Task ReadApiResponseDataAsync_FailedResponse_ReturnsDefault()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };

        Assert.Null(await response.ReadApiResponseDataAsync<Item>());
    }
}
