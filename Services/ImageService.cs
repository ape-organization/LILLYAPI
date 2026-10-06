using Microsoft.AspNetCore.Http;
using SkiaSharp;
using System.Diagnostics;

namespace PharmacyAPI.Services;

public sealed class ImageService
{
    private readonly string _uploadPath;
    private readonly string _uploadUrlPrefix;

    private const int MaxImageDimension = 1600;
    private const int WebpQuality = 82;
    private static readonly int MaxConcurrentImageProcessing =
        Math.Clamp(Environment.ProcessorCount, 2, 3);

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
            ?? "/uploads/LILLY";
    }

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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // --------------------------------------------------------
            // FAST PATH: already-valid WebP
            // --------------------------------------------------------
            // If the source is WebP, already within our dimensions,
            // and has no EXIF orientation transform, do not decode or
            // re-encode it. Re-encoding was the largest image cost in
            // the previous implementation.
            // --------------------------------------------------------

            var extension = Path.GetExtension(file.FileName);

            if (string.Equals(
                    extension,
                    ".webp",
                    StringComparison.OrdinalIgnoreCase))
            {
                using var probeStream = file.OpenReadStream();
                using var codec = SKCodec.Create(probeStream);

                if (codec is null)
                {
                    throw new InvalidOperationException(
                        "The uploaded file could not be decoded as an image.");
                }

                var width = codec.Info.Width;
                var height = codec.Info.Height;
                var orientation = codec.EncodedOrigin;

                if (width <= MaxImageDimension &&
                    height <= MaxImageDimension &&
                    orientation == SKEncodedOrigin.TopLeft)
                {
                    await using var inputStream = file.OpenReadStream();

                    await using var outputStream =
                        new FileStream(
                            physicalPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            64 * 1024,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);

                    await inputStream.CopyToAsync(
                        outputStream,
                        64 * 1024,
                        cancellationToken);

                    return
                        $"{_uploadUrlPrefix.TrimEnd('/')}/" +
                        $"{folder}/" +
                        fileName;
                }
            }

            // --------------------------------------------------------
            // NORMAL IMAGE PIPELINE
            // --------------------------------------------------------

            using var sourceStream = file.OpenReadStream();
            using var sourceCodec = SKCodec.Create(sourceStream);

            if (sourceCodec is null)
            {
                throw new InvalidOperationException(
                    "The uploaded file could not be decoded as an image.");
            }

            var originalWidth = sourceCodec.Info.Width;
            var originalHeight = sourceCodec.Info.Height;
            var orientationValue = sourceCodec.EncodedOrigin;

            var bitmapInfo = new SKImageInfo(
                originalWidth,
                originalHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            using var decodedBitmap = new SKBitmap(bitmapInfo);

            var decodeResult = sourceCodec.GetPixels(
                bitmapInfo,
                decodedBitmap.GetPixels());

            if (decodeResult != SKCodecResult.Success &&
                decodeResult != SKCodecResult.IncompleteInput)
            {
                throw new InvalidOperationException(
                    $"Image decoding failed with result: {decodeResult}");
            }

            SKBitmap? orientationBitmap = null;
            SKBitmap? resizedBitmap = null;

            try
            {
                var processingBitmap = decodedBitmap;

                if (orientationValue != SKEncodedOrigin.TopLeft)
                {
                    orientationBitmap = ApplyOrientation(
                        decodedBitmap,
                        orientationValue);

                    processingBitmap = orientationBitmap;
                }

                var finalBitmap = processingBitmap;

                if (processingBitmap.Width > MaxImageDimension ||
                    processingBitmap.Height > MaxImageDimension)
                {
                    resizedBitmap = ResizeBitmap(
                        processingBitmap,
                        MaxImageDimension);

                    finalBitmap = resizedBitmap;
                }

                using var image = SKImage.FromBitmap(finalBitmap);

                using var encodedData = image.Encode(
                    SKEncodedImageFormat.Webp,
                    WebpQuality);

                if (encodedData is null || encodedData.Size <= 0)
                {
                    throw new InvalidOperationException(
                        "WebP encoding failed.");
                }

                await using var outputStream =
                    new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                encodedData.SaveTo(outputStream);

                await outputStream.FlushAsync(cancellationToken);
            }
            finally
            {
                resizedBitmap?.Dispose();
                orientationBitmap?.Dispose();
            }

            return
                $"{_uploadUrlPrefix.TrimEnd('/')}/" +
                $"{folder}/" +
                fileName;
        }
        catch
        {
            TryDeletePhysicalFile(physicalPath);
            throw;
        }
    }

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

        var batchStopwatch =
            Stopwatch.StartNew();

        Console.WriteLine(
            $"IMAGE BATCH START | " +
            $"Count={fileList.Count} | " +
            $"MaxConcurrency={MaxConcurrentImageProcessing}");

        var results =
            new string[fileList.Count];

        using var semaphore =
            new SemaphoreSlim(
                MaxConcurrentImageProcessing);

        var tasks =
            fileList
                .Select(
                    (file, index) =>
                        ProcessImageAsync(
                            file,
                            folder,
                            index,
                            results,
                            semaphore,
                            cancellationToken))
                .ToArray();

        await Task.WhenAll(tasks);

        batchStopwatch.Stop();

        Console.WriteLine(
            $"IMAGE BATCH COMPLETE | " +
            $"Count={fileList.Count} | " +
            $"Total={batchStopwatch.ElapsedMilliseconds} ms");

        return results.ToList();
    }

    private async Task ProcessImageAsync(
        IFormFile file,
        string folder,
        int index,
        string[] results,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(
            cancellationToken);

        try
        {
            results[index] =
                await SaveImageAsync(
                    file,
                    folder,
                    cancellationToken);
        }
        finally
        {
            semaphore.Release();
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
                (int)Math.Round(
                    source.Width * scale));

        var newHeight =
            Math.Max(
                1,
                (int)Math.Round(
                    source.Height * scale));

        var resized =
            new SKBitmap(
                new SKImageInfo(
                    newWidth,
                    newHeight,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));

        using var canvas =
            new SKCanvas(resized);

        canvas.Clear(
            SKColors.Transparent);

        using var paint =
            new SKPaint
            {
                IsAntialias = true
            };

        var sampling =
            new SKSamplingOptions(
                SKFilterMode.Linear,
                SKMipmapMode.Linear);

        canvas.DrawBitmap(
            source,
            new SKRect(
                0,
                0,
                newWidth,
                newHeight),
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
                Transform(
                    source,
                    0,
                    flipHorizontal: true),

            SKEncodedOrigin.BottomRight =>
                Transform(
                    source,
                    180),

            SKEncodedOrigin.BottomLeft =>
                Transform(
                    source,
                    0,
                    flipVertical: true),

            SKEncodedOrigin.LeftTop =>
                Transform(
                    source,
                    90,
                    flipHorizontal: true),

            SKEncodedOrigin.RightTop =>
                Transform(
                    source,
                    90),

            SKEncodedOrigin.RightBottom =>
                Transform(
                    source,
                    270,
                    flipHorizontal: true),

            SKEncodedOrigin.LeftBottom =>
                Transform(
                    source,
                    270),

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

        using var canvas =
            new SKCanvas(destination);

        canvas.Clear(
            SKColors.Transparent);

        canvas.Save();

        canvas.Translate(
            width / 2f,
            height / 2f);

        canvas.RotateDegrees(
            rotateDegrees);

        canvas.Scale(
            flipHorizontal ? -1 : 1,
            flipVertical ? -1 : 1);

        canvas.Translate(
            -source.Width / 2f,
            -source.Height / 2f);

        using var paint =
            new SKPaint
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
            throw new ArgumentNullException(
                nameof(file));
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException(
                "The uploaded image is empty.");
        }

        var extension =
            Path.GetExtension(
                file.FileName);

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

    public Task DeleteImageAsync(
        string? imageUrl,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();

        var physicalPath =
            GetPhysicalPath(
                imageUrl,
                folder);

        TryDeletePhysicalFile(
            physicalPath);

        return Task.CompletedTask;
    }

    public async Task DeleteImagesAsync(
        IEnumerable<string?> imageUrls,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var urls =
            imageUrls
                .Where(
                    x => !string.IsNullOrWhiteSpace(x))
                .ToList();

        if (urls.Count == 0)
            return;

        var tasks =
            urls.Select(
                url =>
                    DeleteImageAsync(
                        url,
                        folder,
                        cancellationToken));

        await Task.WhenAll(tasks);
    }

    // =============================================================
    // PHYSICAL IMAGE PATH
    // =============================================================

    private string GetPhysicalPath(
        string imageUrl,
        string folder)
    {
        var relative =
            imageUrl;

        if (Uri.TryCreate(
                imageUrl,
                UriKind.Absolute,
                out var absoluteUri))
        {
            relative =
                absoluteUri.AbsolutePath;
        }

        relative =
            Uri.UnescapeDataString(
                relative)
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

        var safeFolder =
            SanitizeFolder(folder);

        var expectedFolderPrefix =
            safeFolder
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/')
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
            Path.GetFullPath(
                _uploadPath)
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
                File.Delete(
                    physicalPath);
            }
        }
        catch
        {
            // Do not hide the original exception.
        }
    }

    // =============================================================
    // FORMAT BYTES
    // =============================================================

    private static string FormatBytes(
        long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024 * 1024)
        {
            return
                $"{bytes / 1024.0:F1} KB";
        }

        return
            $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}