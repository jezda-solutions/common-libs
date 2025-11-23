using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Storage;

/// <summary>
/// Defines a contract for file storage operations including saving, reading, and deleting files.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file stream to storage with optional sub-path organization.
    /// </summary>
    /// <param name="content">The file content stream to save.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="subPath">Optional sub-path for organizing files (e.g., tenant ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file descriptor with metadata about the saved file.</returns>
    Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file for reading from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file stream. Caller is responsible for disposing the stream.</returns>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing true if the file was deleted; false if the file was not found.</returns>
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the public URL for accessing a file, if available.
    /// </summary>
    /// <param name="relativePath">The relative path to the file.</param>
    /// <returns>The public URL if configured; otherwise, null.</returns>
    string? GetPublicUrl(string relativePath);
}