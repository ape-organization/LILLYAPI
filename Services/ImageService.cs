using Microsoft.AspNetCore.Http;

namespace PharmacyAPI.Services;

public class ImageService
{
    private readonly string _uploadPath;
    private readonly string _requestPath;
    
    public ImageService(IConfiguration configuration)
    {
        _uploadPath = configuration["FileStorage:UploadPath"]
            ?? "/var/www/uploads/Shop";
    
        _requestPath = configuration["FileStorage:RequestPath"]
            ?? "/uploads/Shop";
    }

    public async Task<string> SaveImageAsync(
        IFormFile image,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (image == null || image.Length == 0)
            throw new ArgumentException("الصوره مطلوبه");

        var extension = Path.GetExtension(image.FileName)
            .ToLowerInvariant();

        var allowedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".jfif"
        };

        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException("نوع الصوره ليس متوافر");

        var folderPath = Path.Combine(
            _uploadPath,
            folder);

        Directory.CreateDirectory(folderPath);
        if (extension == ".jfif")
        {
            extension = ".jpg";
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";

        var filePath = Path.Combine(
            folderPath,
            fileName);

        await using var stream = new FileStream(
            filePath,
            FileMode.Create);

        await image.CopyToAsync(
            stream,
            cancellationToken);

        return $"{_requestPath}/{folder}/{fileName}";
    }
    public async  void DeleteImage(string imageUrl)
    {
        try
        {
            var relativePath = imageUrl
                .Replace(
                    _requestPath,
                    "",
                    StringComparison.OrdinalIgnoreCase)
                .TrimStart('/');
            
            var filePath = Path.Combine(
                _uploadPath,
                relativePath);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"خطا في مسح الصوره '{imageUrl}': {ex.Message}");
        }
    }

}
