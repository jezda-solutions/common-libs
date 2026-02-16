namespace Jezda.Common.Integrations.Jira;

public class JiraOptions
{
    public const string SectionName = "Integrations:Jira";

    public string BaseUrl { get; set; } = string.Empty; // e.g., "https://your-domain.atlassian.net/"
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
}
