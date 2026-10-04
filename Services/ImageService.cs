
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace PharmacyAPI.Services;

public sealed class ImageService
{
    private readonly string _uploadPath;
    private readonly string _uploadUrlPrefix;

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

    public ImageService(IConfiguration configuration)
    {
        _uploadPath =
            Path.GetFullPath(
                configuration["FileStorage:UploadPath"]
                ?? "/var/www/uploads/LILLY");

        _uploadUrlPrefix =
            (configuration["FileStorage:UploadUrlPrefix"]
             ?? "/uploads/LILLY/").TrimEnd('/');
    }

    // ============================================================
    // SAVE, RESIZE AND COMPRESS IMAGE
    // ============================================================

    public async Task<string> SaveImageAsync(
        IFormFile image,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
            throw new ArgumentException("الصورة مطلوبة");

        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("مجلد الصورة مطلوب");

        var extension =
            Path.GetExtension(image.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                "نوع الصورة غير متوافر");
        }

        folder = SanitizeFolder(folder);

        var folderPath =
            Path.GetFullPath(
                Path.Combine(_uploadPath, folder));

        // Ensure the destination stays inside upload root.
        if (!folderPath.StartsWith(
                _uploadPath + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "مجلد الصورة غير صالح");
        }

        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}.webp";

        var filePath =
            Path.Combine(folderPath, fileName);

        try
        {
            // Decode uploaded image.
            await using var inputStream =
                image.OpenReadStream();

            using var decodedImage =
                await Image.LoadAsync(
                    inputStream,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Correct orientation from EXIF metadata.
            decodedImage.Mutate(x => x.AutoOrient());

            // Resize only if either dimension exceeds the limit.
            if (decodedImage.Width > MaxImageDimension ||
                decodedImage.Height > MaxImageDimension)
            {
                decodedImage.Mutate(x => x.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(
                            MaxImageDimension,
                            MaxImageDimension),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));
            }

            // Save compressed WebP.
            var encoder = new WebpEncoder
            {
                Quality = WebpQuality
            };

            await using var outputStream =
                new FileStream(
                    filePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        BufferSize = 128 * 1024,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });

            await decodedImage.SaveAsWebpAsync(
                outputStream,
                encoder,
                cancellationToken);

            await outputStream.FlushAsync(
                cancellationToken);

            return $"{_uploadUrlPrefix}/{folder}/{fileName}";
        }
        catch
        {
            // Remove incomplete output if processing fails.
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Do not hide the original exception.
            }

            throw;
        }
    }

    // ============================================================
    // DELETE IMAGE
    // ============================================================

    public Task DeleteImageAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        try
        {
            var relativePath = GetRelativePath(imageUrl);

            if (relativePath is null)
                return Task.CompletedTask;

            var filePath = Path.GetFullPath(
                Path.Combine(_uploadPath, relativePath));

            // Prevent deleting files outside the upload root.
            if (!filePath.StartsWith(
                    _uploadPath + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Image cleanup must not break other operations.
        }

        return Task.CompletedTask;
    }

    // ============================================================
    // GET RELATIVE PATH
    // ============================================================

    private string? GetRelativePath(string imageUrl)
    {
        var value = imageUrl.Trim();

        var prefix = _uploadUrlPrefix + "/";

        if (!value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = value[prefix.Length..];

        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        relativePath = relativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(relativePath) ||
            relativePath.Split(
                Path.DirectorySeparatorChar)
                .Any(segment =>
                    segment is "." or ".."))
        {
            return null;
        }

        return relativePath;
    }

    // ============================================================
    // SANITIZE FOLDER
    // ============================================================

    private static string SanitizeFolder(string folder)
    {
        folder = folder.Trim();

        if (Path.IsPathRooted(folder) ||
            folder.Contains(':'))
        {
            throw new ArgumentException(
                "مجلد الصورة غير صالح");
        }

        folder = folder.Replace('\\', '/').Trim('/');

        var segments = folder.Split('/');

        if (segments.Length == 0 ||
            segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." ||
                segment.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException(
                "مجلد الصورة غير صالح");
        }

        return Path.Combine(segments);
    }
}