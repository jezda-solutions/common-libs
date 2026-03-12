namespace Jezda.Common.Integrations.Tests.Helpers;

public sealed class MockHttpClientFactory(MockHttpMessageHandler handler, string expectedName, Uri? baseAddress = null) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name != expectedName)
        {
            throw new InvalidOperationException($"Unexpected HttpClient name: '{name}'. Expected: '{expectedName}'.");
        }

        return new HttpClient(handler) { BaseAddress = baseAddress };
    }
}
