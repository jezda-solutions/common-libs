namespace Jezda.Common.Integrations.AzureDevOps.Configuration;

public class AzureDevOpsOptions
{
    public const string SectionName = "Integrations:AzureDevOps";
    
    public string BaseUrl { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "7.1";
}
