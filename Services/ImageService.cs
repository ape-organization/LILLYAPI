using Microsoft.AspNetCore.Http;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace PharmacyAPI.Services;

public sealed class ImageService
{
    private readonly string _uploadPath;
    private readonly string _uploadUrlPrefix;
    private readonly ILogger<ImageService> _logger;

    private const int MaxImageDimension = 1600;
    private const int WebpQuality = 82;

    // Rejects "decompression bombs": small files that claim huge dimensions.
    // 50 MP is above any normal phone camera photo.
    private const long MaxSourcePixels = 50_000_000;

    // WebP files above this size are re-encoded even when their
    // dimensions are already fine, to keep served images small.
    private const long WebpFastPathMaxBytes = 500 * 1024;

    private const int FileBufferSize = 64 * 1024;

    private static readonly int MaxConcurrentImageProcessing =
        Math.Clamp(Environment.ProcessorCount, 2, 3);

    // ONE gate for the whole server (not per request), so concurrent
    // uploads from several admins cannot exhaust memory.
    private static readonly SemaphoreSlim ImageGate =
        new(MaxConcurrentImageProcessing, MaxConcurrentImageProcessing);

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".jfif"
        };

    public ImageService(
        IConfiguration configuration,
        ILogger<ImageService> logger)
    {
        _uploadPath =
            configuration["FileStorage:UploadPath"]
            ?? "/var/www/uploads/LILLY";

        _uploadUrlPrefix =
            configuration["FileStorage:UploadUrlPrefix"]
            ?? "/uploads/LILLY";

        _logger = logger;
    }

    // =============================================================
    // SAVE SINGLE IMAGE
    // =============================================================

    public async Task<string> SaveImageAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file);
        folder = SanitizeFolder(folder);

        var directory = Path.Combine(_uploadPath, folder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}.webp";
        var physicalPath = Path.Combine(directory, fileName);

        // Global concurrency limit. Applies to single uploads and batches.
        await ImageGate.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WriteImageAsync(
                file,
                physicalPath,
                cancellationToken);

            return BuildUrl(folder, fileName);
        }
        catch
        {
            TryDeletePhysicalFile(physicalPath);
            throw;
        }
        finally
        {
            ImageGate.Release();
        }
    }

    // =============================================================
    // SAVE MULTIPLE IMAGES
    // =============================================================

    public async Task<List<string>> SaveImagesAsync(
        IEnumerable<IFormFile> files,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var fileList =
            files
                .Where(x => x is not null)
                .ToList();

        if (fileList.Count == 0)
            return [];

        var batchStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Image batch start | Count={Count} | MaxConcurrency={MaxConcurrency}",
            fileList.Count,
            MaxConcurrentImageProcessing);

        var results = new string?[fileList.Count];

        // If one image fails, the others stop instead of finishing for nothing.
        using var batchCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        var tasks =
            fileList
                .Select(
                    (file, index) =>
                        ProcessImageAsync(
                            file,
                            folder,
                            index,
                            results,
                            batchCts))
                .ToArray();

        var whenAll = Task.WhenAll(tasks);

        try
        {
            await whenAll;
        }
        catch
        {
            // Delete the images that DID succeed, otherwise they stay on
            // disk forever: the caller never receives their URLs.
            await DeleteImagesAsync(
                results.Where(x => x is not null),
                folder,
                CancellationToken.None);

            // Throw the real error, not the cancellation of a sibling task.
            var realError =
                whenAll.Exception?
                    .InnerExceptions
                    .FirstOrDefault(e => e is not OperationCanceledException);

            if (realError is not null)
            {
                ExceptionDispatchInfo.Capture(realError).Throw();
            }

            throw;
        }

        batchStopwatch.Stop();

        _logger.LogInformation(
            "Image batch complete | Count={Count} | Total={ElapsedMs} ms",
            fileList.Count,
            batchStopwatch.ElapsedMilliseconds);

        return results
            .Select(x => x!)
            .ToList();
    }

    private async Task ProcessImageAsync(
        IFormFile file,
        string folder,
        int index,
        string?[] results,
        CancellationTokenSource batchCts)
    {
        try
        {
            results[index] =
                await SaveImageAsync(
                    file,
                    folder,
                    batchCts.Token);
        }
        catch
        {
            batchCts.Cancel();
            throw;
        }
    }

    // =============================================================
    // IMAGE PIPELINE
    // =============================================================

    private static async Task WriteImageAsync(
        IFormFile file,
        string physicalPath,
        CancellationToken cancellationToken)
    {
        if (await TryCopyWebpAsIsAsync(
                file,
                physicalPath,
                cancellationToken))
        {
            return;
        }

        using var sourceStream = file.OpenReadStream();

        using var codec =
            SKCodec.Create(sourceStream)
            ?? throw new InvalidOperationException(
                "The uploaded file could not be decoded as an image.");

        EnsureDimensionsAllowed(
            codec.Info.Width,
            codec.Info.Height);

        var origin = codec.EncodedOrigin;

        // Decode directly at a reduced size when possible
        // (JPEG supports 1/2, 1/4, 1/8 natively). This avoids
        // allocating the full-resolution bitmap.
        using var decodedBitmap = DecodeScaled(codec);

        SKBitmap? resizedBitmap = null;
        SKBitmap? orientedBitmap = null;

        try
        {
            var current = decodedBitmap;

            // Resize FIRST, then rotate: rotating a small bitmap is
            // much cheaper. The limit is the same for width and height,
            // so the final result is identical.
            if (current.Width > MaxImageDimension ||
                current.Height > MaxImageDimension)
            {
                resizedBitmap = ResizeBitmap(
                    current,
                    MaxImageDimension);

                current = resizedBitmap;
            }

            if (origin != SKEncodedOrigin.TopLeft)
            {
                orientedBitmap = ApplyOrientation(
                    current,
                    origin);

                current = orientedBitmap;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var image = SKImage.FromBitmap(current);

            using var encodedData = image.Encode(
                SKEncodedImageFormat.Webp,
                WebpQuality);

            if (encodedData is null || encodedData.Size <= 0)
            {
                throw new InvalidOperationException(
                    "WebP encoding failed.");
            }

            await using var outputStream = CreateOutputFile(physicalPath);
            using var dataStream = encodedData.AsStream();

            await dataStream.CopyToAsync(
                outputStream,
                FileBufferSize,
                cancellationToken);
        }
        finally
        {
            orientedBitmap?.Dispose();
            resizedBitmap?.Dispose();
        }
    }

    // -------------------------------------------------------------
    // FAST PATH: small, already-valid WebP
    // -------------------------------------------------------------
    // Copied as-is when it is WebP, small in bytes, within our
    // dimensions and has no EXIF orientation transform.
    // -------------------------------------------------------------

    private static async Task<bool> TryCopyWebpAsIsAsync(
        IFormFile file,
        string physicalPath,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(
                extension,
                ".webp",
                StringComparison.OrdinalIgnoreCase) ||
            file.Length > WebpFastPathMaxBytes)
        {
            return false;
        }

        using (var probeStream = file.OpenReadStream())
        using (var codec = SKCodec.Create(probeStream))
        {
            if (codec is null)
            {
                throw new InvalidOperationException(
                    "The uploaded file could not be decoded as an image.");
            }

            if (codec.Info.Width > MaxImageDimension ||
                codec.Info.Height > MaxImageDimension ||
                codec.EncodedOrigin != SKEncodedOrigin.TopLeft)
            {
                return false;
            }
        }

        await using var inputStream = file.OpenReadStream();
        await using var outputStream = CreateOutputFile(physicalPath);

        await inputStream.CopyToAsync(
            outputStream,
            FileBufferSize,
            cancellationToken);

        return true;
    }

    // =============================================================
    // DECODE
    // =============================================================

    private static SKBitmap DecodeScaled(
        SKCodec codec)
    {
        var info = codec.Info;

        var scale =
            Math.Min(
                1f,
                (float)MaxImageDimension /
                Math.Max(info.Width, info.Height));

        if (scale < 1f)
        {
            // Returns the closest size the decoder supports,
            // at or slightly above the requested size.
            var scaledSize = codec.GetScaledDimensions(scale);

            if (scaledSize.Width > 0 &&
                scaledSize.Height > 0 &&
                (scaledSize.Width < info.Width ||
                 scaledSize.Height < info.Height))
            {
                var scaled = TryDecode(
                    codec,
                    scaledSize.Width,
                    scaledSize.Height);

                if (scaled is not null)
                    return scaled;
            }
        }

        // Format does not support scaled decoding: decode full size.
        return TryDecode(
                   codec,
                   info.Width,
                   info.Height)
               ?? throw new InvalidOperationException(
                   "Image decoding failed.");
    }

    private static SKBitmap? TryDecode(
        SKCodec codec,
        int width,
        int height)
    {
        var bitmapInfo = new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        var bitmap = new SKBitmap(bitmapInfo);

        var result = codec.GetPixels(
            bitmapInfo,
            bitmap.GetPixels());

        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            return bitmap;

        bitmap.Dispose();

        if (result == SKCodecResult.InvalidScale)
            return null;

        throw new InvalidOperationException(
            $"Image decoding failed with result: {result}");
    }

    private static void EnsureDimensionsAllowed(
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException(
                "The uploaded image has invalid dimensions.");
        }

        if ((long)width * height > MaxSourcePixels)
        {
            throw new InvalidOperationException(
                "The uploaded image dimensions are too large.");
        }
    }

    // =============================================================
    // RESIZE
    // =============================================================

    private static SKBitmap ResizeBitmap(
        SKBitmap source,
        int maxDimension)
    {
        var scale =
            Math.Min(
                (double)maxDimension / source.Width,
                (double)maxDimension / source.Height);

        var newWidth =
            Math.Max(
                1,
                (int)Math.Round(source.Width * scale));

        var newHeight =
            Math.Max(
                1,
                (int)Math.Round(source.Height * scale));

        var resized =
            new SKBitmap(
                new SKImageInfo(
                    newWidth,
                    newHeight,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));

        using var canvas = new SKCanvas(resized);

        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        // After scaled decoding the remaining ratio is small (< 2x),
        // where a cubic filter gives sharper results than linear and
        // no mipmaps have to be built.
        var sampling =
            new SKSamplingOptions(
                SKCubicResampler.Mitchell);

        canvas.DrawBitmap(
            source,
            new SKRect(0, 0, newWidth, newHeight),
            sampling,
            paint);

        canvas.Flush();

        return resized;
    }

    // =============================================================
    // EXIF ORIENTATION
    // =============================================================

    private static SKBitmap ApplyOrientation(
        SKBitmap source,
        SKEncodedOrigin origin)
    {
        return origin switch
        {
            SKEncodedOrigin.TopLeft =>
                source.Copy(),

            SKEncodedOrigin.TopRight =>
                Transform(source, 0, flipHorizontal: true),

            SKEncodedOrigin.BottomRight =>
                Transform(source, 180),

            SKEncodedOrigin.BottomLeft =>
                Transform(source, 0, flipVertical: true),

            SKEncodedOrigin.LeftTop =>
                Transform(source, 90, flipHorizontal: true),

            SKEncodedOrigin.RightTop =>
                Transform(source, 90),

            SKEncodedOrigin.RightBottom =>
                Transform(source, 270, flipHorizontal: true),

            SKEncodedOrigin.LeftBottom =>
                Transform(source, 270),

            _ =>
                source.Copy()
        };
    }

    private static SKBitmap Transform(
        SKBitmap source,
        int rotateDegrees,
        bool flipHorizontal = false,
        bool flipVertical = false)
    {
        var swapDimensions =
            rotateDegrees == 90 ||
            rotateDegrees == 270;

        var width =
            swapDimensions
                ? source.Height
                : source.Width;

        var height =
            swapDimensions
                ? source.Width
                : source.Height;

        var destination =
            new SKBitmap(
                new SKImageInfo(
                    width,
                    height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));

        using var canvas = new SKCanvas(destination);

        canvas.Clear(SKColors.Transparent);

        canvas.Save();

        canvas.Translate(width / 2f, height / 2f);

        canvas.RotateDegrees(rotateDegrees);

        canvas.Scale(
            flipHorizontal ? -1 : 1,
            flipVertical ? -1 : 1);

        canvas.Translate(
            -source.Width / 2f,
            -source.Height / 2f);

        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var sampling =
            new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.None);

        canvas.DrawBitmap(
            source,
            0,
            0,
            sampling,
            paint);

        canvas.Restore();

        canvas.Flush();

        return destination;
    }

    // =============================================================
    // VALIDATION
    // =============================================================

    private static void ValidateFile(
        IFormFile file)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException(
                "The uploaded image is empty.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"Unsupported image format: {extension}");
        }
    }

    // =============================================================
    // FOLDER SANITIZATION
    // =============================================================

    private static string SanitizeFolder(
        string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return "products";

        folder =
            folder
                .Trim()
                .Replace('\\', '/')
                .Trim('/');

        if (folder.Contains("..") ||
            Path.IsPathRooted(folder))
        {
            throw new InvalidOperationException(
                "Invalid upload folder.");
        }

        var segments =
            folder.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == "." ||
                segment == "..")
            {
                throw new InvalidOperationException(
                    "Invalid upload folder.");
            }
        }

        return string.Join(
            Path.DirectorySeparatorChar,
            segments);
    }

    // =============================================================
    // DELETE IMAGE
    // =============================================================
    //
    // Deleting never throws for a bad or unknown URL: it is logged
    // instead. A failed cleanup must not turn a successful save into
    // an error, or hide the original exception in a rollback.
    //

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
            var physicalPath =
                GetPhysicalPath(
                    imageUrl,
                    folder);

            TryDeletePhysicalFile(physicalPath);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(
                e,
                "Could not delete image {ImageUrl}",
                imageUrl);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteImagesAsync(
        IEnumerable<string?> imageUrls,
        string folder,
        CancellationToken cancellationToken = default)
    {
        foreach (var url in imageUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
                continue;

            await DeleteImageAsync(
                url,
                folder,
                cancellationToken);
        }
    }

    // =============================================================
    // URL / FILE HELPERS
    // =============================================================

    private string BuildUrl(
        string folder,
        string fileName)
    {
        return
            $"{_uploadUrlPrefix.TrimEnd('/')}/" +
            $"{folder.Replace('\\', '/')}/" +
            fileName;
    }

    private static FileStream CreateOutputFile(
        string physicalPath)
    {
        return new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            FileBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    // =============================================================
    // PHYSICAL IMAGE PATH
    // =============================================================

    private string GetPhysicalPath(
        string imageUrl,
        string folder)
    {
        var relative = imageUrl;

        if (Uri.TryCreate(
                imageUrl,
                UriKind.Absolute,
                out var absoluteUri))
        {
            relative = absoluteUri.AbsolutePath;
        }

        relative =
            Uri.UnescapeDataString(relative)
                .Replace('\\', '/');

        var prefix =
            _uploadUrlPrefix
                .TrimEnd('/')
                .Replace('\\', '/');

        if (!relative.StartsWith(
                prefix + "/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The image path is outside the configured upload directory.");
        }

        var relativePart =
            relative[prefix.Length..]
                .TrimStart('/');

        var safeFolder = SanitizeFolder(folder);

        var expectedFolderPrefix =
            safeFolder
                .Replace(Path.DirectorySeparatorChar, '/')
                .Trim('/');

        if (!relativePart.StartsWith(
                expectedFolderPrefix + "/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The image path does not belong to the specified folder.");
        }

        var combined =
            Path.GetFullPath(
                Path.Combine(
                    _uploadPath,
                    relativePart));

        var root =
            Path.GetFullPath(_uploadPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid image path.");
        }

        return combined;
    }

    // =============================================================
    // DELETE SAFELY
    // =============================================================

    private static void TryDeletePhysicalFile(
        string physicalPath)
    {
        try
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch
        {
            // Do not hide the original exception.
        }
    }
}