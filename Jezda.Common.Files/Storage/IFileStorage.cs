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
    /// <param name="fileName">The name of the file. A null or blank name is stored as <c>file</c> (unlike <c>IFileService.UploadAsync</c>, which rejects it). <see cref="LocalFileStorage"/> stores it as <c>&lt;guid&gt;-&lt;name&gt;</c> and shortens the base of the name, keeping the extension, so the stored name fits 255 UTF-8 bytes; <see cref="FileDescriptor.Name"/> keeps the full name.</param>
    /// <param name="subPath">Optional sub-path for organizing files (e.g., tenant ID). <c>\</c> is treated as <c>/</c> on every OS, so a leading <c>\</c> makes it rooted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file descriptor with metadata about the saved file.</returns>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="subPath"/> is rooted, contains a <c>..</c> segment or names no folder (only separators or <c>.</c> segments) or has a segment made only of dots and whitespace (other than a plain <c>.</c>), <paramref name="fileName"/> has a
    /// directory part, or either resolves outside the storage root.
    /// </exception>
    Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file for reading from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the file stream. Caller is responsible for disposing the stream.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="relativePath"/> resolves outside the storage root.</exception>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing true if the file was deleted; false if the file was not found.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="relativePath"/> resolves outside the storage root.</exception>
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the public URL for accessing a file, if available.
    /// </summary>
    /// <param name="relativePath">The relative path to the file.</param>
    /// <returns>The public URL if configured; otherwise, null.</returns>
    string? GetPublicUrl(string relativePath);
}