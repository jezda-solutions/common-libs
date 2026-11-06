using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Models;

public class ExternalDtoTests
{
    [Fact]
    public void ExternalProjectDto_InitializesCorrectly()
    {
        var dto = new ExternalProjectDto
        {
            Id = "proj-1",
            Path = "owner/proj",
            Name = "Test Project",
            Description = "A description",
            Url = "https://example.com/proj-1",
            Provider = ExternalProvider.GitHub
        };

        Assert.Equal("proj-1", dto.Id);
        Assert.Equal("owner/proj", dto.Path);
        Assert.Equal("Test Project", dto.Name);
        Assert.Equal("A description", dto.Description);
        Assert.Equal("https://example.com/proj-1", dto.Url);
        Assert.Equal(ExternalProvider.GitHub, dto.Provider);
    }

    [Fact]
    public void ExternalProjectDto_NullableFieldsCanBeNull()
    {
        var dto = new ExternalProjectDto
        {
            Id = "proj-1",
            Path = "proj-1",
            Name = "Test",
            Provider = ExternalProvider.Jira
        };

        Assert.Null(dto.Description);
        Assert.Null(dto.Url);
        Assert.Null(dto.CreatedAt);
        Assert.Null(dto.UpdatedAt);
        Assert.Null(dto.IsArchived);
        Assert.Null(dto.IsPrivate);
    }

    [Fact]
    public void ExternalTaskDto_InitializesCorrectly()
    {
        var dto = new ExternalTaskDto
        {
            Id = "task-1",
            Title = "Fix bug",
            Status = "Open",
            Url = "https://example.com/task-1",
            ProjectId = "proj-1",
            ProjectPath = "owner/proj",
            Provider = ExternalProvider.AzureDevOps
        };

        Assert.Equal("task-1", dto.Id);
        Assert.Equal("Fix bug", dto.Title);
        Assert.Equal("Open", dto.Status);
        Assert.Equal("https://example.com/task-1", dto.Url);
        Assert.Equal("proj-1", dto.ProjectId);
        Assert.Equal("owner/proj", dto.ProjectPath);
        Assert.Equal(ExternalProvider.AzureDevOps, dto.Provider);
    }

    [Theory]
    [InlineData(ExternalProvider.GitHub, 1)]
    [InlineData(ExternalProvider.Jira, 2)]
    [InlineData(ExternalProvider.AzureDevOps, 3)]
    public void ExternalProvider_HasCorrectValues(ExternalProvider provider, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)provider);
    }
}
