using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services
{
    public interface ICategoryService
    {
        Task<List<Category>> GetCategories(
            CancellationToken cancellationToken = default);

        Task<Category?> GetCategory(
            int id,
            CancellationToken cancellationToken = default);

        Task<Category> CreateCategory(
            CreateCategoryRequest dto,
            CancellationToken cancellationToken = default);

        Task<List<CategoryMenu>> GetCategoriesForMenu(
            CancellationToken cancellationToken = default);

        Task UpdateCategory(
            int id,
            CreateCategoryRequest dto,
            CancellationToken cancellationToken = default);

        Task DeleteCategory(
            int id,
            CancellationToken cancellationToken = default);
    }


    public class CategoryService : ICategoryService
    {
        private readonly ShoesDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ImageService _imageService;


        public CategoryService(
            ImageService imageService,
            IConfiguration configuration,
            ShoesDbContext context)
        {
            _imageService = imageService;
            _configuration = configuration;
            _context = context;
        }


        // =====================================================
        // GET CATEGORIES FOR MENU
        // =====================================================

        public async Task<List<CategoryMenu>> GetCategoriesForMenu(
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new CategoryMenu
                {
                    Id = c.Id,
                    NameEn = c.NameEn,
                    NameAr = c.NameAr
                })
                .ToListAsync(cancellationToken);
        }


        // =====================================================
        // GET ALL CATEGORIES
        // =====================================================

        public async Task<List<Category>> GetCategories(
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .ToListAsync(cancellationToken);
        }


        // =====================================================
        // GET CATEGORY
        // =====================================================

        public async Task<Category?> GetCategory(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c =>
                    c.Id == id &&
                    !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
        }


        // =====================================================
        // CREATE CATEGORY
        // =====================================================

        public async Task<Category> CreateCategory(
            CreateCategoryRequest dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);


            // -------------------------------------------------
            // VALIDATION
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(dto.NameEn))
            {
                throw new ArgumentException(
                    "Category English name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.NameAr))
            {
                throw new ArgumentException(
                    "Category Arabic name is required.");
            }


            // -------------------------------------------------
            // CHECK DUPLICATE NAME
            // -------------------------------------------------

            var exists = await _context.Categories
                .AnyAsync(
                    c =>
                        !c.IsDeleted &&
                        (
                            c.NameEn == dto.NameEn ||
                            c.NameAr == dto.NameAr
                        ),
                    cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    "الفئة موجودة بالفعل.");
            }


            // -------------------------------------------------
            // CREATE CATEGORY
            // -------------------------------------------------

            var category = new Category
            {
                NameEn = dto.NameEn.Trim(),
                NameAr = dto.NameAr.Trim(),
                IsDeleted = false
            };


            // -------------------------------------------------
            // IMAGE
            // -------------------------------------------------

            if (dto.Image is not null)
            {
                category.ImageUrl =
                    await _imageService.SaveImageAsync(
                        dto.Image,
                        "categories",
                        cancellationToken);
            }


            // -------------------------------------------------
            // SAVE
            // -------------------------------------------------

            _context.Categories.Add(category);

            await _context.SaveChangesAsync(
                cancellationToken);


            return category;
        }


        // =====================================================
        // UPDATE CATEGORY
        // =====================================================

        public async Task UpdateCategory(
            int id,
            CreateCategoryRequest dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);


            // -------------------------------------------------
            // GET CATEGORY
            // -------------------------------------------------

            var category = await _context.Categories
                .FirstOrDefaultAsync(
                    c =>
                        c.Id == id &&
                        !c.IsDeleted,
                    cancellationToken);


            if (category is null)
            {
                throw new KeyNotFoundException(
                    "الفئة غير متوفره.");
            }


            // -------------------------------------------------
            // VALIDATION
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(dto.NameEn))
            {
                throw new ArgumentException(
                    "Category English name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.NameAr))
            {
                throw new ArgumentException(
                    "Category Arabic name is required.");
            }


            // -------------------------------------------------
            // CHECK DUPLICATE NAME
            // -------------------------------------------------

            var exists = await _context.Categories
                .AnyAsync(
                    c =>
                        c.Id != id &&
                        !c.IsDeleted &&
                        (
                            c.NameEn == dto.NameEn ||
                            c.NameAr == dto.NameAr
                        ),
                    cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    "الفئة موجودة بالفعل.");
            }


            // -------------------------------------------------
            // UPDATE BASIC DATA
            // -------------------------------------------------

            category.NameEn = dto.NameEn.Trim();
            category.NameAr = dto.NameAr.Trim();


            string? oldImageUrl = null;
            string? newImageUrl = null;


            // -------------------------------------------------
            // NEW IMAGE
            // -------------------------------------------------

            if (dto.Image is not null)
            {
                oldImageUrl = category.ImageUrl;

                newImageUrl =
                    await _imageService.SaveImageAsync(
                        dto.Image,
                        "categories",
                        cancellationToken);

                category.ImageUrl = newImageUrl;
            }


            // -------------------------------------------------
            // SAVE DATABASE
            // -------------------------------------------------

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch
            {
                // Database failed after the new image was saved.
                // Delete the new image so it does not remain orphaned.

                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    _imageService.DeleteImage(newImageUrl);
                }

                throw;
            }


            // -------------------------------------------------
            // DELETE OLD IMAGE
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                _imageService.DeleteImage(oldImageUrl);
            }
        }


        // =====================================================
        // DELETE CATEGORY
        // =====================================================

        public async Task DeleteCategory(
            int id,
            CancellationToken cancellationToken = default)
        {
            // -------------------------------------------------
            // GET CATEGORY
            // -------------------------------------------------

            var category = await _context.Categories
                .FirstOrDefaultAsync(
                    c => c.Id == id,
                    cancellationToken);


            if (category is null)
            {
                throw new KeyNotFoundException(
                    "الفئة غير متوفره.");
            }


      


            // -------------------------------------------------
            // GET ALL PRODUCTS OF CATEGORY
            // -------------------------------------------------

            var products = await _context.Products
              
                .Where(p => p.CategoryId == id&&!p.IsDeleted)
                .ToListAsync(cancellationToken);


         

            // -------------------------------------------------
            // DELETE PRODUCTS
            // -------------------------------------------------

            if (products.Count > 0)
            {
                products.ForEach(p =>
                {
                    p.IsDeleted=true;
                });
                _context.Products.UpdateRange(products);
            }


            // -------------------------------------------------
            // DELETE CATEGORY
            // -------------------------------------------------
           string categoryImageUrl=category.ImageUrl;
            category.IsDeleted = true;
            _context.Categories.Update(category);


            // -------------------------------------------------
            // SAVE DATABASE
            // -------------------------------------------------

            await _context.SaveChangesAsync(
                cancellationToken);


            // -------------------------------------------------
            // DELETE CATEGORY IMAGE
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(categoryImageUrl))
            {
                _imageService.DeleteImage(
                    categoryImageUrl);
            }


         
        }
    }
}