using System;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Validation;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Files.Extensions;

/// <summary>
/// Provides extension methods for registering file services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers file validation services including MIME type detector, file scanner, and validator.
    /// Optionally configures validation options from the "FileValidation" configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration to load FileValidationOptions from.</param>
    /// <returns>The service collection for chaining.</returns>
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

    /// <summary>
    /// Registers local file storage implementation with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure file storage options (RootPath, PublicBaseUrl, UseDateFolders).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalFileStorage(this IServiceCollection services, Action<FileStorageOptions> configure)
    {
        services.AddOptions<FileStorageOptions>();
        services.Configure(configure);
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }

    /// <summary>
    /// Registers the high-level file service that coordinates validation and storage.
    /// Requires file validation and storage services to be registered first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        return services;
    }
}