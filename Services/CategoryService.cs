using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services;

public interface ICategoryService
{
    Task<List<Category>> GetCategories(CancellationToken cancellationToken = default);
    Task<Category?> GetCategory(int id, CancellationToken cancellationToken = default);
    Task<Category> CreateCategory(CreateCategoryRequest dto, CancellationToken cancellationToken = default);
    Task<List<Category>> GetCategoriesForMenu(CancellationToken cancellationToken = default);
    Task UpdateCategory(int id, CreateCategoryRequest dto, CancellationToken cancellationToken = default);
    Task DeleteCategory(int id, CancellationToken cancellationToken = default);
}

public sealed class CategoryService : ICategoryService
{
    private readonly ShoesDbContext _context;
    private readonly ImageService _imageService;

    public CategoryService(ImageService imageService, ShoesDbContext context)
    {
        _imageService = imageService;
        _context = context;
    }

    public Task<List<Category>> GetCategoriesForMenu(
        CancellationToken cancellationToken = default)
    {
        return _context.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Category>> GetCategories(
        CancellationToken cancellationToken = default)
    {
        return _context.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<Category?> GetCategory(
        int id,
        CancellationToken cancellationToken = default)
    {
        return _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id && !c.IsDeleted,
                cancellationToken);
    }

    public async Task<Category> CreateCategory(
        CreateCategoryRequest dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(dto);

            var nameEn = dto.NameEn?.Trim();
            var nameAr = dto.NameAr?.Trim();

            if (string.IsNullOrWhiteSpace(nameEn))
                throw new ArgumentException("Category English name is required.");

            if (string.IsNullOrWhiteSpace(nameAr))
                throw new ArgumentException("Category Arabic name is required.");

            var exists = await _context.Categories
                .AsNoTracking()
                .AnyAsync(
                    c =>
                        !c.IsDeleted &&
                        (c.NameEn == nameEn || c.NameAr == nameAr),
                    cancellationToken);

            if (exists)
                throw new InvalidOperationException("الفئة موجودة بالفعل.");

            var category = new Category
            {
                NameEn = nameEn,
                NameAr = nameAr,
                IsDeleted = false
            };

            if (dto.Image is not null)
            {
                category.ImageUrl = await _imageService.SaveImageAsync(
                    dto.Image,
                    "categories",
                    cancellationToken);
            }

            _context.Categories.Add(category);

                await _context.SaveChangesAsync(cancellationToken);
           

            return category;
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred while creating the category: {e.Message}");
            return null;
        }
    }



    public async Task UpdateCategory(
        int id,
        CreateCategoryRequest dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var nameEn = dto.NameEn?.Trim();
        var nameAr = dto.NameAr?.Trim();

        if (string.IsNullOrWhiteSpace(nameEn))
            throw new ArgumentException("Category English name is required.");

        if (string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("Category Arabic name is required.");

        var category = await _context.Categories
            .FirstOrDefaultAsync(
                c => c.Id == id && !c.IsDeleted,
                cancellationToken);

        if (category is null)
            throw new KeyNotFoundException("الفئة غير متوفره.");

        var exists = await _context.Categories
            .AsNoTracking()
            .AnyAsync(
                c =>
                    c.Id != id &&
                    !c.IsDeleted &&
                    (c.NameEn == nameEn || c.NameAr == nameAr),
                cancellationToken);

        if (exists)
            throw new InvalidOperationException("الفئة موجودة بالفعل.");

        category.NameEn = nameEn;
        category.NameAr = nameAr;

        string? oldImageUrl = null;
        string? newImageUrl = null;

        if (dto.Image is not null)
        {
            oldImageUrl = category.ImageUrl;

            newImageUrl = await _imageService.SaveImageAsync(
                dto.Image,
                "categories",
                cancellationToken);

            category.ImageUrl = newImageUrl;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newImageUrl))
                await _imageService.DeleteImageAsync(newImageUrl, "categories");

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldImageUrl))
            await _imageService.DeleteImageAsync(oldImageUrl, "categories");
    }

    public async Task DeleteCategory(
        int id,
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(
                c => c.Id == id && !c.IsDeleted,
                cancellationToken);

        if (category is null)
            throw new KeyNotFoundException("الفئة غير متوفره.");

        var categoryImageUrl = category.ImageUrl;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // Bulk soft-delete products directly in SQL.
            // This avoids loading every product into application memory.
            await _context.Products
                .Where(p => p.CategoryId == id && !p.IsDeleted)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        p => p.IsDeleted,
                        true),
                    cancellationToken);

            category.IsDeleted = true;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        // Delete the physical file only after the DB operation succeeds.
        if (!string.IsNullOrWhiteSpace(categoryImageUrl))
            await _imageService.DeleteImageAsync(categoryImageUrl, "categories");
    }
}
