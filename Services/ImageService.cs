using ImageMagick;
using Microsoft.AspNetCore.Http;

namespace PharmacyAPI.Services;

public sealed class ImageService
{
    private const int MaxImageDimension = 1600;
    private const int WebpQuality = 82;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".jfif"
        };

    private readonly string _uploadPath;
    private readonly string _uploadUrlPrefix;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IConfiguration configuration,
        ILogger<ImageService> logger)
    {
        _logger = logger;

        _uploadPath =
            configuration["FileStorage:UploadPath"]
            ?? "/var/www/uploads/LILLY";

        _uploadUrlPrefix =
            configuration["FileStorage:UploadUrlPrefix"]
            ?? "/uploads/LILLY";
    }

    // =========================================================
    // SAVE IMAGE
    // =========================================================

    public async Task<string> SaveImageAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length <= 0)
            throw new ArgumentException("Image file is empty.", nameof(file));

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                $"Unsupported image format: {extension}");
        }

        var safeFolder = SanitizeFolder(folder);

        var directory =
            Path.Combine(_uploadPath, safeFolder);

        Directory.CreateDirectory(directory);

        var fileName =
            $"{Guid.NewGuid():N}.webp";

        var physicalPath =
            Path.Combine(directory, fileName);

        try
        {
            await using var inputStream =
                new MemoryStream((int)Math.Min(file.Length, int.MaxValue));

            await file.CopyToAsync(
                inputStream,
                cancellationToken);

            inputStream.Position = 0;

            using var image = new MagickImage();

            await image.ReadAsync(
                inputStream,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Apply EXIF orientation before checking dimensions.
            image.AutoOrient();

            // Resize only when necessary.
            if (image.Width > MaxImageDimension ||
                image.Height > MaxImageDimension)
            {
                image.FilterType = FilterType.Lanczos;

                image.Resize(
                    MaxImageDimension,
                    MaxImageDimension);
            }

            // Remove EXIF/metadata that is unnecessary for ecommerce images.
            image.Strip();

            // WebP gives the frontend significantly smaller files.
            image.Format = MagickFormat.WebP;
            image.Quality = WebpQuality;

            await image.WriteAsync(
                physicalPath,
                cancellationToken);

            return GetRelativePath(
                safeFolder,
                fileName);
        }
        catch
        {
            TryDeletePhysicalFile(physicalPath);
            throw;
        }
    }

    // =========================================================
    // DELETE ONE IMAGE
    // =========================================================

    public Task DeleteImageAsync(
        string? imageUrl,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fileName =
                Path.GetFileName(
                    imageUrl.Split(
                        '?',
                        StringSplitOptions.RemoveEmptyEntries)[0]);

            if (string.IsNullOrWhiteSpace(fileName))
                return Task.CompletedTask;

            var safeFolder = SanitizeFolder(folder);

            var physicalPath =
                Path.Combine(
                    _uploadPath,
                    safeFolder,
                    fileName);

            var fullUploadPath =
                Path.GetFullPath(_uploadPath);

            var fullFilePath =
                Path.GetFullPath(physicalPath);

            // Security check.
            if (!fullFilePath.StartsWith(
                    fullUploadPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Blocked image deletion outside upload directory: {Path}",
                    fullFilePath);

                return Task.CompletedTask;
            }

            TryDeletePhysicalFile(fullFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete image: {ImageUrl}",
                imageUrl);
        }

        return Task.CompletedTask;
    }

    // =========================================================
    // DELETE MULTIPLE IMAGES
    // =========================================================

    public async Task DeleteImagesAsync(
        IEnumerable<string>? imageUrls,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (imageUrls is null)
            return;

        var urls = imageUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (urls.Length == 0)
            return;

        // File deletion is I/O-bound, so doing the independent
        // deletions together is faster than sequential deletion.
        await Task.WhenAll(
            urls.Select(url =>
                DeleteImageAsync(
                    url,
                    folder,
                    cancellationToken)));
    }

    // =========================================================
    // RELATIVE URL
    // =========================================================

    private string GetRelativePath(
        string folder,
        string fileName)
    {
        return
            $"{_uploadUrlPrefix.TrimEnd('/')}/" +
            $"{folder}/" +
            $"{fileName}";
    }

    // =========================================================
    // FOLDER SANITIZATION
    // =========================================================

    private static string SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException(
                "Image folder cannot be empty.",
                nameof(folder));

        var sanitized =
            folder
                .Trim()
                .Replace('\\', '/')
                .Trim('/');

        if (sanitized.Length == 0 ||
            sanitized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Invalid image folder.",
                nameof(folder));
        }

        var segments =
            sanitized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                throw new ArgumentException(
                    "Invalid image folder.",
                    nameof(folder));
            }

            foreach (var character in Path.GetInvalidFileNameChars())
            {
                if (segment.Contains(character))
                {
                    throw new ArgumentException(
                        "Invalid image folder.",
                        nameof(folder));
                }
            }
        }

        return Path.Combine(segments);
    }

    // =========================================================
    // PHYSICAL DELETE
    // =========================================================

    private void TryDeletePhysicalFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete image file: {Path}",
                path);
        }
    }
}