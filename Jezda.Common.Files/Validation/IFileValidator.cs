using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Jezda.Common.Files.Validation;

public interface IFileValidator
{
    Task<FileValidationResult> ValidateAsync(
        IFormFile file,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);

    Task<FileValidationResult> ValidateAsync(
        Stream fileStream,
        string? fileName = null,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);
}