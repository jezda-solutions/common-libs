namespace Jezda.Common.Integrations.GitHub;

public class GitHubOptions
{
    public const string SectionName = "Integrations:GitHub";

    public string BaseUrl { get; set; } = "https://api.github.com/";
    public string AccessToken { get; set; } = string.Empty;
    public string UserAgent { get; set; } = "Jezda-Common-Integration-Client";
}
