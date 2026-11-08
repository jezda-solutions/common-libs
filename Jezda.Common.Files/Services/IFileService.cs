using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;

namespace Jezda.Common.Files.Services;

/// <summary>
/// Defines a high-level contract for file operations including upload with validation,
/// retrieval, and deletion.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Uploads a file after performing validation and applies file name normalization.
    /// </summary>
    /// <param name="content">The file content stream to upload.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="subPath">
    /// Optional sub-path under the storage root that groups the file, e.g. a tenant ID or
    /// <c>work-items/{id}</c>. Passed through to <see cref="IFileStorage.SaveAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file descriptor with metadata about the uploaded file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when file validation fails.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="subPath"/> contains a <c>..</c> segment, or when it or the file name resolves outside the storage root.
    /// </exception>
    Task<FileDescriptor> UploadAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file for reading from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file stream. Caller is responsible for disposing the stream.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath"/> resolves outside the storage root.</exception>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing true if the file was deleted; false if the file was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePath"/> resolves outside the storage root.</exception>
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}