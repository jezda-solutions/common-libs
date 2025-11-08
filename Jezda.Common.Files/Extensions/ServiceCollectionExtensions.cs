using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Files.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileValidation(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddOptions<FileValidationOptions>();

        if (configuration is not null)
        {
            var section = configuration.GetSection("FileValidation");
            if (section.Exists())
            {
                services.Configure<FileValidationOptions>(section);
            }
        }

        services.AddSingleton<IMimeTypeDetector, MagicNumberMimeTypeDetector>();
        services.AddSingleton<IFileScanner, NoopFileScanner>();
        services.AddSingleton<IFileValidator, FileValidator>();
        return services;
    }
}