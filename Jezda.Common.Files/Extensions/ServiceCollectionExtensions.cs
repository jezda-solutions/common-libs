using System;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Validation;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Services;
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

    public static IServiceCollection AddLocalFileStorage(this IServiceCollection services, Action<FileStorageOptions> configure)
    {
        services.AddOptions<FileStorageOptions>();
        services.Configure(configure);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }

    public static IServiceCollection AddFileServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        return services;
    }
}