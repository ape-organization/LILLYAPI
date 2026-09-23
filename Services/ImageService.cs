using Microsoft.AspNetCore.Http;

namespace PharmacyAPI.Services;

public sealed class ImageService
{
    private readonly string _uploadPath;
    private readonly string _uploadUrlPrefix;

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
            configuration["FileStorage:UploadPath"]
            ?? "/var/www/uploads/LILLY";

        _uploadUrlPrefix =
            configuration["FileStorage:UploadUrlPrefix"]
            ?? "/uploads/LILLY/";
    }

    // ============================================================
    // SAVE IMAGE
    // ============================================================

    public async Task<string> SaveImageAsync(
        IFormFile image,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
            throw new ArgumentException("الصورة مطلوبة");

        var extension =
            Path.GetExtension(image.FileName)
                .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException("نوع الصورة غير متوافر");

        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("مجلد الصورة مطلوب");

        folder = SanitizeFolder(folder);

        if (extension == ".jfif")
            extension = ".jpg";

        var folderPath =
            Path.Combine(_uploadPath, folder);

        Directory.CreateDirectory(folderPath);

        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var filePath =
            Path.Combine(folderPath, fileName);

        await using var stream = new FileStream(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 128 * 1024,
                Options = FileOptions.Asynchronous |
                          FileOptions.SequentialScan
            });

        await image.CopyToAsync(
            stream,
            cancellationToken);

        return $"{_uploadUrlPrefix.TrimEnd('/')}/{folder}/{fileName}";
    }

    // ============================================================
    // DELETE IMAGE
    // ============================================================

    public Task DeleteImageAsync(
        string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        try
        {
            var relativePath =
                GetRelativePath(imageUrl);

            if (relativePath is null)
                return Task.CompletedTask;

            var filePath =
                Path.Combine(
                    _uploadPath,
                    relativePath);

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Never allow image cleanup failure
            // to break product operations.
        }

        return Task.CompletedTask;
    }

    // ============================================================
    // GET RELATIVE PATH
    // ============================================================

    private string? GetRelativePath(
        string imageUrl)
    {
        var value =
            imageUrl.Trim();

        var prefix =
            _uploadUrlPrefix.TrimEnd('/') + "/";

        if (!value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath =
            value[prefix.Length..];

        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (
            relativePath.Contains(
                "..",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            return null;
        }

        return relativePath.Replace(
            '/',
            Path.DirectorySeparatorChar);
    }

    // ============================================================
    // SANITIZE FOLDER
    // ============================================================

    private static string SanitizeFolder(
        string folder)
    {
        folder =
            folder.Trim()
                  .Trim(
                      Path.DirectorySeparatorChar,
                      Path.AltDirectorySeparatorChar);

        if (
            folder.Contains(
                "..",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(folder))
        {
            throw new ArgumentException(
                "مجلد الصورة غير صالح");
        }

        return folder;
    }
}