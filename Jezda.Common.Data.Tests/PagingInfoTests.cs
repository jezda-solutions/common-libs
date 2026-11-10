using System.Net;
using System.Text.Json;
using Jezda.Common.Domain.Paged;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jezda.Common.Data.Tests;

public class PagingInfoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    public void TryGetSearchTerm_AbsentOrEmpty_ReadsAsEmptyMap(string? json)
    {
        var paging = new PagingInfo { SearchTermJson = json };

        Assert.True(paging.TryGetSearchTerm(out var searchTerm));
        Assert.Empty(searchTerm);
        Assert.Empty(paging.SearchTerm);
    }

    [Fact]
    public void TryGetSearchTerm_JsonObjectOfStrings_ReadsColumns()
    {
        var paging = new PagingInfo { SearchTermJson = "{\"name\":\"alpha\",\"code\":\"7\"}" };

        Assert.True(paging.TryGetSearchTerm(out var searchTerm));
        Assert.Equal("alpha", searchTerm["name"]);
        Assert.Equal("7", searchTerm["code"]);
        Assert.Equal(searchTerm, paging.SearchTerm);
    }

    [Theory]
    [InlineData("alpha")]
    [InlineData("")]
    [InlineData("[\"name\"]")]
    [InlineData("{\"name\":1}")]
    [InlineData("{\"name\":")]
    public void UnreadableSearchTerm_FailsTryGetAndThrowsTypedFromGetter(string json)
    {
        var paging = new PagingInfo { SearchTermJson = json };

        Assert.False(paging.TryGetSearchTerm(out var searchTerm));
        Assert.Empty(searchTerm);

        var exception = Assert.Throws<InvalidPagingParameterException>(() => paging.SearchTerm);
        Assert.Equal(PagingParameter.SearchTerm, exception.Parameter);
        Assert.Null(exception.Column);
        Assert.IsType<JsonException>(exception.InnerException, exactMatch: false);
    }

    [Fact]
    public void InvalidPagingParameterException_IsAnArgumentException()
    {
        // Consumers that already catch ArgumentException keep working.
        Assert.IsAssignableFrom<ArgumentException>(
            new InvalidPagingParameterException(PagingParameter.SortColumn, "x", "message"));
    }

    [Theory]
    [InlineData("alpha", HttpStatusCode.BadRequest)]
    [InlineData("{\"name\":\"alpha\"}", HttpStatusCode.OK)]
    public async Task Mvc_DoesNotReadSearchTermWhileBindingOrValidating(string searchTerm, HttpStatusCode expected)
    {
        // MVC binds into a read-only collection property and validates it, both through the getter.
        // Without [BindNever]/[ValidateNever] an unreadable value threw before the action ran.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(PagingProbeController).Assembly);

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        var response = await app.GetTestClient()
            .GetAsync($"/paging-probe?current_page=2&search_term={Uri.EscapeDataString(searchTerm)}");

        Assert.Equal(expected, response.StatusCode);
    }
}

[ApiController]
[Route("paging-probe")]
public sealed class PagingProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] PagingInfo paging) =>
        paging.CurrentPage == 2 && paging.TryGetSearchTerm(out _)
            ? Ok()
            : StatusCode(StatusCodes.Status400BadRequest);
}
