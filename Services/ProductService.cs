using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using System.Linq.Expressions;

namespace PharmacyAPI.Services
{
    public interface IProductService
    {
        Task<PagedResponse<ProductResponseDto>> GetProducts(
            int page = 1,
            int? categoryId = null,
            bool? offers = null,
            CancellationToken cancellationToken = default);

        Task<ProductResponseDto?> GetProduct(
            int id,
            CancellationToken cancellationToken = default);

        Task<ProductDto> CreateProduct(
            ProductDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateProduct(
            int id,
            UpdateProductDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteProduct(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> CheckProductExists(
            string name,
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> GetDiscountedProducts(
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> GetProductsByName(
            string name,
            CancellationToken cancellationToken = default);

        Task<ProductResponseDto?> RemoveDiscount(
            int id,
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> GetProductsByIds(
            List<int> productIds,
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> GetBestSellerProducts(
            int count = 10,
            CancellationToken cancellationToken = default);
    }


    public class ProductService : IProductService
    {
        private const int PageSize = 50;
        private const int SearchLimit = 50;

        private readonly ShoesDbContext _context;
        private readonly ILogger<ProductService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ImageService _imageService;


        public ProductService(
            ImageService imageService,
            IConfiguration configuration,
            ShoesDbContext context,
            ILogger<ProductService> logger)
        {
            _imageService = imageService;
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }


        // ============================================================
        // GET PRODUCTS
        // ============================================================

        public async Task<PagedResponse<ProductResponseDto>> GetProducts(
            int page = 1,
            int? categoryId = null,
            bool? offers = null,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);

            var query = _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            // --------------------------------------------------------
            // CATEGORY FILTER
            // --------------------------------------------------------

            if (categoryId.HasValue)
            {
                query = query.Where(p =>
                    p.CategoryId == categoryId.Value);
            }

            // --------------------------------------------------------
            // OFFERS FILTER
            // --------------------------------------------------------

            if (offers == true)
            {
                query = query.Where(p =>
                    p.DiscountPercentage > 0);
            }

            // --------------------------------------------------------
            // FILTERED REQUEST
            //
            // As agreed:
            // return ALL matching products.
            // --------------------------------------------------------

            var hasFilters =
                categoryId.HasValue ||
                offers == true;

            if (hasFilters)
            {
                var filteredProducts = await query
                    .OrderBy(p => p.Id)
                    .Select(MapProduct())
                    .ToListAsync(cancellationToken);

                return new PagedResponse<ProductResponseDto>
                {
                    Items = filteredProducts,
                    TotalCount = filteredProducts.Count,
                    Page = 1,
                    PageSize = filteredProducts.Count,
                    TotalPages = filteredProducts.Count > 0 ? 1 : 0,
                    HasMore = false
                };
            }

            // --------------------------------------------------------
            // NORMAL PAGINATION
            // --------------------------------------------------------

            var totalCount = await query
                .CountAsync(cancellationToken);

            if (totalCount == 0)
            {
                return new PagedResponse<ProductResponseDto>
                {
                    Items = new List<ProductResponseDto>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = PageSize,
                    TotalPages = 0,
                    HasMore = false
                };
            }

            var totalPages =
                (int)Math.Ceiling(
                    totalCount / (double)PageSize);

            var products = await query
                .OrderBy(p => p.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);

            return new PagedResponse<ProductResponseDto>
            {
                Items = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = PageSize,
                TotalPages = totalPages,
                HasMore = page < totalPages
            };
        }


        // ============================================================
        // GET PRODUCT
        // ============================================================

        public async Task<ProductResponseDto?> GetProduct(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return null;

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    p.Id == id &&
                    !p.IsDeleted)
                .Select(MapProduct())
                .FirstOrDefaultAsync(cancellationToken);
        }


        // ============================================================
        // GET PRODUCTS BY NAME
        // ============================================================

        public async Task<List<ProductResponseDto>> GetProductsByName(
            string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new List<ProductResponseDto>();

            name = name.Trim();

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    (
                        EF.Functions.Like(
                            p.NameEn,
                            $"%{name}%")

                        ||

                        EF.Functions.Like(
                            p.NameAr,
                            $"%{name}%")
                    ))
                .OrderBy(p => p.Id)
                .Take(SearchLimit)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
        }


        // ============================================================
        // GET PRODUCTS BY IDS
        // ============================================================

        public async Task<List<ProductResponseDto>> GetProductsByIds(
            List<int> productIds,
            CancellationToken cancellationToken = default)
        {
            if (productIds == null || productIds.Count == 0)
                return new List<ProductResponseDto>();

            var ids = productIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return new List<ProductResponseDto>();

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    ids.Contains(p.Id) &&
                    !p.IsDeleted)
                .OrderBy(p => p.Id)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
        }


        // ============================================================
        // GET DISCOUNTED PRODUCTS
        // ============================================================

        public async Task<List<ProductResponseDto>> GetDiscountedProducts(
            CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.DiscountPercentage > 0)
                .OrderBy(p => p.Id)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
        }


        // ============================================================
        // GET BEST SELLERS
        // ============================================================

        public async Task<List<ProductResponseDto>> GetBestSellerProducts(
            int count = 10,
            CancellationToken cancellationToken = default)
        {
            count = Math.Clamp(count, 1, 100);

            var bestSellerIds = await _context.OrderItems
                .AsNoTracking()
                .Where(oi =>
                    oi.Order.Status == OrderStatus.Confirmed &&
                    !oi.Product.IsDeleted)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    SoldQuantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.SoldQuantity)
                .ThenBy(x => x.ProductId)
                .Take(count)
                .Select(x => x.ProductId)
                .ToListAsync(cancellationToken);

            if (bestSellerIds.Count == 0)
                return new List<ProductResponseDto>();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p =>
                    bestSellerIds.Contains(p.Id) &&
                    !p.IsDeleted)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);

            // Preserve best-seller ranking.
            var ranking = bestSellerIds
                .Select((id, index) => new
                {
                    id,
                    index
                })
                .ToDictionary(x => x.id, x => x.index);

            products.Sort((a, b) =>
                ranking[a.Id].CompareTo(
                    ranking[b.Id]));

            return products;
        }


        // ============================================================
        // CHECK PRODUCT EXISTS
        // ============================================================

        public async Task<bool> CheckProductExists(
            string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            name = name.Trim();

            return await _context.Products
                .AsNoTracking()
                .AnyAsync(
                    p =>
                        !p.IsDeleted &&
                        p.NameEn == name,
                    cancellationToken);
        }


        // ============================================================
        // CREATE PRODUCT
        // ============================================================

        public async Task<ProductDto> CreateProduct(
            ProductDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            NormalizeProductDto(dto);

            ValidateBasicProductData(dto);

            // --------------------------------------------------------
            // Determine whether this product uses variants.
            // --------------------------------------------------------

            var hasVariants =
                dto.Variants != null &&
                dto.Variants.Count > 0;

            // Product-level stock is only meaningful when there
            // are no variants.
            if (hasVariants)
            {
                dto.StockQuantity = 0;
            }

            await ValidateProductName(
                dto.NameEn,
                null,
                cancellationToken);

            await ValidateCategory(
                dto.CategoryId,
                cancellationToken);

            await ValidateVariants(
                dto.Variants,
                cancellationToken);


            var uploadedImageUrls = new List<string>();

            try
            {
                var product = new Product
                {
                    NameEn = dto.NameEn,

                    NameAr = dto.NameAr,

                    DescriptionEn = dto.DescriptionEn,

                    DescriptionAr = dto.DescriptionAr,

                    Price = dto.Price,

                    ActualPrice = dto.ActualPrice,

                    StockQuantity = dto.StockQuantity,

                    DiscountPercentage =
                        dto.DiscountPercentage,

                    CategoryId = dto.CategoryId,

                    IsDeleted = false,

                    IsInStock = CalculateProductStockStatus(
                        dto.StockQuantity,
                        dto.IsInStock,
                        dto.Variants)
                };


                // ====================================================
                // IMAGES
                // ====================================================

                await AddNewImages(
                    product,
                    dto.Images,
                    uploadedImageUrls,
                    cancellationToken);


                // ====================================================
                // VARIANTS
                // ====================================================

                if (hasVariants)
                {
                    foreach (var variantDto in dto.Variants)
                    {
                        product.Variants.Add(
                            new ProductVariant
                            {
                                SizeId = variantDto.SizeId,

                                HeelSizeId =
                                    variantDto.HeelSizeId,

                                StockQuantity =
                                    variantDto.StockQuantity,

                                IsActive = true
                            });
                    }
                }


                // ====================================================
                // SAVE
                // ====================================================

                _context.Products.Add(product);

                await _context.SaveChangesAsync(
                    cancellationToken);


                // ====================================================
                // UPDATE DTO
                // ====================================================

                dto.Id = product.Id;

                dto.StockQuantity =
                    product.StockQuantity;

                dto.IsInStock =
                    product.IsInStock;

                dto.Images = product.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i =>
                        new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            SortOrder = i.SortOrder
                        })
                    .ToList();

                dto.Variants = product.Variants
                    .OrderBy(v => v.Id)
                    .Select(v =>
                        new ProductVariantDto
                        {
                            SizeId = v.SizeId,
                            HeelSizeId = v.HeelSizeId,
                            StockQuantity = v.StockQuantity
                        })
                    .ToList();

                return dto;
            }
            catch
            {
                DeleteUploadedImages(uploadedImageUrls);

                throw;
            }
        }


        // ============================================================
        // UPDATE PRODUCT
        // ============================================================

        public async Task<bool> UpdateProduct(
            int id,
            UpdateProductDto dto,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return false;

            ArgumentNullException.ThrowIfNull(dto);

            NormalizeProductDto(dto);

            ValidateBasicProductData(dto);


            // --------------------------------------------------------
            // Load product with images and variants.
            //
            // We need tracking here because this is an update.
            // --------------------------------------------------------

            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(
                    p =>
                        p.Id == id &&
                        !p.IsDeleted,
                    cancellationToken);

            if (product == null)
                return false;


            await ValidateProductName(
                dto.NameEn,
                id,
                cancellationToken);

            await ValidateCategory(
                dto.CategoryId,
                cancellationToken);

            await ValidateVariants(
                dto.Variants,
                cancellationToken);


            var hasVariants =
                dto.Variants != null &&
                dto.Variants.Count > 0;


            // --------------------------------------------------------
            // If variants exist, product-level stock is not used.
            // --------------------------------------------------------

            if (hasVariants)
            {
                dto.StockQuantity = 0;
            }


            var uploadedImageUrls = new List<string>();

            var imagesToDelete = new List<string>();


            try
            {
                // ====================================================
                // BASIC DATA
                // ====================================================

                product.NameEn = dto.NameEn;

                product.NameAr = dto.NameAr;

                product.DescriptionEn =
                    dto.DescriptionEn;

                product.DescriptionAr =
                    dto.DescriptionAr;

                product.Price =
                    dto.Price;

                product.ActualPrice =
                    dto.ActualPrice;

                product.StockQuantity =
                    dto.StockQuantity;

                product.DiscountPercentage =
                    dto.DiscountPercentage;

                product.CategoryId =
                    dto.CategoryId;


                // ====================================================
                // IMAGES
                // ====================================================

                await UpdateImages(
                    product,
                    dto.Images,
                    uploadedImageUrls,
                    imagesToDelete,
                    cancellationToken);


                // ====================================================
                // VARIANTS
                // ====================================================

                UpdateVariants(
                    product,
                    dto.Variants);


              


                // ====================================================
                // SAVE DATABASE
                // ====================================================

                await _context.SaveChangesAsync(
                    cancellationToken);


                // ====================================================
                // DELETE OLD FILES AFTER DATABASE SUCCESS
                // ====================================================

                DeleteUploadedImages(
                    imagesToDelete.Distinct(
                        StringComparer.OrdinalIgnoreCase));


                return true;
            }
            catch
            {
                // Only newly uploaded files are removed here.
                // Existing files are untouched.
                DeleteUploadedImages(uploadedImageUrls);

                throw;
            }
        }


        // ============================================================
        // UPDATE VARIANTS
        // ============================================================

        private void UpdateVariants(
            Product product,
            List<ProductVariantDto> requestedVariants)
        {
            requestedVariants ??=
                new List<ProductVariantDto>();


            // --------------------------------------------------------
            // Existing active/inactive variants indexed by their
            // Size + HeelSize combination.
            // --------------------------------------------------------

            var existingVariants =
                product.Variants
                    .ToDictionary(
                        v => BuildVariantKey(
                            v.SizeId,
                            v.HeelSizeId));


            var requestedKeys = new HashSet<string>();


            // --------------------------------------------------------
            // Process requested variants.
            // --------------------------------------------------------

            foreach (var dto in requestedVariants)
            {
                var key = BuildVariantKey(
                    dto.SizeId,
                    dto.HeelSizeId);

                requestedKeys.Add(key);


                // ----------------------------------------------------
                // Existing variant
                // ----------------------------------------------------

                if (existingVariants.TryGetValue(
                        key,
                        out var existingVariant))
                {
                    existingVariant.StockQuantity =
                        dto.StockQuantity;

                    existingVariant.IsActive = true;

                    continue;
                }


                // ----------------------------------------------------
                // New variant
                // ----------------------------------------------------

                product.Variants.Add(
                    new ProductVariant
                    {
                        SizeId = dto.SizeId,

                        HeelSizeId =
                            dto.HeelSizeId,

                        StockQuantity =
                            dto.StockQuantity,

                        IsActive = true
                    });
            }


            // --------------------------------------------------------
            // Variants removed from the product are NOT deleted.
            //
            // They are deactivated to preserve historical orders.
            // --------------------------------------------------------

            foreach (var existingVariant in product.Variants)
            {
                var key = BuildVariantKey(
                    existingVariant.SizeId,
                    existingVariant.HeelSizeId);

                if (!requestedKeys.Contains(key))
                {
                    existingVariant.IsActive = false;

                    existingVariant.StockQuantity = 0;
                }
            }
        }


        // ============================================================
        // CREATE IMAGES
        // ============================================================

        private async Task AddNewImages(
            Product product,
            List<ProductImageDto> images,
            List<string> uploadedImageUrls,
            CancellationToken cancellationToken)
        {
            if (images == null || images.Count == 0)
                return;

            var sortOrder = 0;

            foreach (var imageDto in images
                         .OrderBy(i => i.SortOrder))
            {
                if (imageDto.Image == null)
                    continue;

                var imageUrl =
                    await _imageService.SaveImageAsync(
                        imageDto.Image,
                        "products",
                        cancellationToken);

                uploadedImageUrls.Add(imageUrl);

                product.Images.Add(
                    new ProductImage
                    {
                        ImageUrl = imageUrl,
                        SortOrder = sortOrder++
                    });
            }
        }


        // ============================================================
        // UPDATE IMAGES
        // ============================================================

        private async Task UpdateImages(
            Product product,
            List<ProductImageDto> requestedImages,
            List<string> uploadedImageUrls,
            List<string> imagesToDelete,
            CancellationToken cancellationToken)
        {
            requestedImages ??=
                new List<ProductImageDto>();


            // --------------------------------------------------------
            // Existing IDs sent by the client.
            // --------------------------------------------------------

            var existingImageIds =
                requestedImages
                    .Where(i => i.Id > 0)
                    .Select(i => i.Id)
                    .ToHashSet();


            // --------------------------------------------------------
            // Remove images no longer associated with product.
            // --------------------------------------------------------

            var removedImages =
                product.Images
                    .Where(i =>
                        !existingImageIds.Contains(i.Id))
                    .ToList();


            foreach (var image in removedImages)
            {
                if (!string.IsNullOrWhiteSpace(
                        image.ImageUrl))
                {
                    imagesToDelete.Add(
                        image.ImageUrl);
                }

                _context.ProductImages.Remove(image);
            }


            // --------------------------------------------------------
            // Process requested images.
            // --------------------------------------------------------

            foreach (var imageDto in requestedImages
                         .OrderBy(i => i.SortOrder))
            {
                // ====================================================
                // EXISTING IMAGE
                // ====================================================

                if (imageDto.Id > 0)
                {
                    var existingImage =
                        product.Images.FirstOrDefault(
                            i => i.Id == imageDto.Id);

                    if (existingImage == null)
                    {
                        throw new KeyNotFoundException(
                            $"الصورة رقم {imageDto.Id} غير موجودة");
                    }


                    existingImage.SortOrder =
                        imageDto.SortOrder;


                    // ------------------------------------------------
                    // Replace physical image file.
                    // ------------------------------------------------

                    if (imageDto.Image != null)
                    {
                        var newImageUrl =
                            await _imageService.SaveImageAsync(
                                imageDto.Image,
                                "products",
                                cancellationToken);

                        uploadedImageUrls.Add(
                            newImageUrl);

                        imagesToDelete.Add(
                            existingImage.ImageUrl);

                        existingImage.ImageUrl =
                            newImageUrl;
                    }

                    continue;
                }


                // ====================================================
                // NEW IMAGE
                // ====================================================

                if (imageDto.Image != null)
                {
                    var newImageUrl =
                        await _imageService.SaveImageAsync(
                            imageDto.Image,
                            "products",
                            cancellationToken);

                    uploadedImageUrls.Add(
                        newImageUrl);

                    product.Images.Add(
                        new ProductImage
                        {
                            ImageUrl = newImageUrl,

                            SortOrder =
                                imageDto.SortOrder
                        });
                }
            }


            // --------------------------------------------------------
            // Normalize sort order.
            // --------------------------------------------------------

            var orderedImages =
                product.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .ToList();

            for (var i = 0;
                 i < orderedImages.Count;
                 i++)
            {
                orderedImages[i].SortOrder = i;
            }
        }


        // ============================================================
        // REMOVE DISCOUNT
        // ============================================================

        public async Task<ProductResponseDto?> RemoveDiscount(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return null;

            var product = await _context.Products
                .FirstOrDefaultAsync(
                    p =>
                        p.Id == id &&
                        !p.IsDeleted,
                    cancellationToken);

            if (product == null)
                return null;

            product.DiscountPercentage = 0;

            await _context.SaveChangesAsync(
                cancellationToken);

            return await GetProduct(
                id,
                cancellationToken);
        }


        // ============================================================
        // DELETE PRODUCT
        // ============================================================

        public async Task<bool> DeleteProduct(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
                return false;

            var product = await _context.Products
                .FirstOrDefaultAsync(
                    p =>
                        p.Id == id &&
                        !p.IsDeleted,
                    cancellationToken);

            if (product == null)
                return false;

            // --------------------------------------------------------
            // Soft delete.
            // --------------------------------------------------------

            product.IsDeleted = true;

            await _context.SaveChangesAsync(
                cancellationToken);

            return true;
        }


        // ============================================================
        // PRODUCT PROJECTION
        // ============================================================

        private static Expression<
            Func<Product, ProductResponseDto>>
            MapProduct()
        {
            return p => new ProductResponseDto
            {
                Id = p.Id,

                NameEn = p.NameEn,

                NameAr = p.NameAr,

                DescriptionEn =
                    p.DescriptionEn,

                DescriptionAr =
                    p.DescriptionAr,

                Price =
                    p.Price,

                ActualPrice =
                    p.ActualPrice,

                StockQuantity =
                    p.StockQuantity,

                IsInStock =
                    p.IsInStock,

                DiscountPercentage =
                    p.DiscountPercentage,

                CategoryId =
                    p.CategoryId,

                Category =
                    p.Category == null
                        ? null
                        : new CategoryResponseDto
                        {
                            Id =
                                p.Category.Id,

                            NameEn =
                                p.Category.NameEn,

                            NameAr =
                                p.Category.NameAr
                        },

                Images =
                    p.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i =>
                            new ProductImageResponseDto
                            {
                                Id =
                                    i.Id,

                                ImageUrl =
                                    i.ImageUrl,

                                SortOrder =
                                    i.SortOrder
                            })
                        .ToList(),

                Variants =
                    p.Variants
                        .Where(v => v.IsActive)
                        .OrderBy(v => v.Id)
                        .Select(v =>
                            new ProductVariantResponseDto
                            {
                                Id =
                                    v.Id,

                                SizeId =
                                    v.SizeId,

                                SizeName =
                                    v.Size != null
                                        ? v.Size.Name
                                        : null,

                                HeelSizeId =
                                    v.HeelSizeId,

                                HeelSizeName =
                                    v.HeelSize != null
                                        ? v.HeelSize.Name
                                        : null,

                                StockQuantity =
                                    v.StockQuantity
                            })
                        .ToList()
            };
        }


        // ============================================================
        // VALIDATE PRODUCT NAME
        // ============================================================

        private async Task ValidateProductName(
            string name,
            int? excludedProductId,
            CancellationToken cancellationToken)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.NameEn == name);

            if (excludedProductId.HasValue)
            {
                query = query.Where(
                    p => p.Id != excludedProductId.Value);
            }

            var exists =
                await query.AnyAsync(
                    cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    excludedProductId.HasValue
                        ? "منتج اخر بنفس الاسم الانجليزي موجود بالفعل"
                        : "منتج بنفس الاسم بالانجليزيه موجود من قبل");
            }
        }


        // ============================================================
        // VALIDATE CATEGORY
        // ============================================================

        private async Task ValidateCategory(
            int categoryId,
            CancellationToken cancellationToken)
        {
            var exists =
                await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(
                        c =>
                            c.Id == categoryId &&
                            !c.IsDeleted,
                        cancellationToken);

            if (!exists)
            {
                throw new KeyNotFoundException(
                    "الفئة غير متوفرة");
            }
        }


        // ============================================================
        // VALIDATE BASIC PRODUCT DATA
        // ============================================================

        private static void ValidateBasicProductData(
            ProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NameEn))
            {
                throw new InvalidOperationException(
                    "الاسم بالانجليزيه مطلوب");
            }

            if (string.IsNullOrWhiteSpace(dto.NameAr))
            {
                throw new InvalidOperationException(
                    "الاسم بالعربية مطلوب");
            }

            if (dto.Price < 0)
            {
                throw new InvalidOperationException(
                    "السعر لا يمكن ان يكون بالسالب");
            }

            if (dto.ActualPrice < 0)
            {
                throw new InvalidOperationException(
                    "السعر الفعلي لا يمكن ان يكون بالسالب");
            }

            if (dto.StockQuantity < 0)
            {
                throw new InvalidOperationException(
                    "الكميه لا يمكن ان تكون اقل من الصفر");
            }

            if (dto.DiscountPercentage < 0 ||
                dto.DiscountPercentage > 100)
            {
                throw new InvalidOperationException(
                    "النسبه يجب ان تكون من 0 الي 100");
            }

            var discountedPrice =
                dto.Price -
                (
                    dto.Price *
                    dto.DiscountPercentage /
                    100
                );

            if (dto.ActualPrice > discountedPrice)
            {
                throw new InvalidOperationException(
                    "السعر الفعلي يجب ان يكون اقل من السعر بعد الخصم");
            }
        }


        private static void ValidateBasicProductData(
            UpdateProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NameEn))
            {
                throw new InvalidOperationException(
                    "الاسم بالانجليزيه مطلوب");
            }

            if (string.IsNullOrWhiteSpace(dto.NameAr))
            {
                throw new InvalidOperationException(
                    "الاسم بالعربية مطلوب");
            }

            if (dto.Price < 0)
            {
                throw new InvalidOperationException(
                    "السعر لا يمكن ان يكون بالسالب");
            }

            if (dto.ActualPrice < 0)
            {
                throw new InvalidOperationException(
                    "السعر الفعلي لا يمكن ان يكون بالسالب");
            }

            if (dto.StockQuantity < 0)
            {
                throw new InvalidOperationException(
                    "الكميه لا يمكن ان تكون اقل من الصفر");
            }

            if (dto.DiscountPercentage < 0 ||
                dto.DiscountPercentage > 100)
            {
                throw new InvalidOperationException(
                    "النسبه يجب ان تكون من 0 الي 100");
            }

            var discountedPrice =
                dto.Price -
                (
                    dto.Price *
                    dto.DiscountPercentage /
                    100
                );

            if (dto.ActualPrice > discountedPrice)
            {
                throw new InvalidOperationException(
                    "السعر الفعلي يجب ان يكون اقل من السعر بعد الخصم");
            }
        }


        // ============================================================
        // VALIDATE VARIANTS
        // ============================================================

        private async Task ValidateVariants(
            List<ProductVariantDto>? variants,
            CancellationToken cancellationToken)
        {
            if (variants == null ||
                variants.Count == 0)
            {
                return;
            }


            // --------------------------------------------------------
            // Stock
            // --------------------------------------------------------

            if (variants.Any(v =>
                    v.StockQuantity < 0))
            {
                throw new InvalidOperationException(
                    "كميه المخزون لا يمكن ان تكون اقل من الصفر");
            }


            // --------------------------------------------------------
            // Every variant needs at least one option.
            // --------------------------------------------------------

            if (variants.Any(v =>
                    !v.SizeId.HasValue &&
                    !v.HeelSizeId.HasValue))
            {
                throw new InvalidOperationException(
                    "كل منتج فرعي يجب ان يحتوي على مقاس او مقاس كعب");
            }


            // --------------------------------------------------------
            // No duplicate combinations.
            // --------------------------------------------------------

            var duplicateCombination =
                variants
                    .GroupBy(v =>
                        new
                        {
                            v.SizeId,
                            v.HeelSizeId
                        })
                    .Any(g => g.Count() > 1);

            if (duplicateCombination)
            {
                throw new InvalidOperationException(
                    "لا يمكن تكرار نفس تركيبة المقاس ومقاس الكعب");
            }


            // --------------------------------------------------------
            // Collect IDs.
            // --------------------------------------------------------

            var sizeIds =
                variants
                    .Where(v =>
                        v.SizeId.HasValue)
                    .Select(v =>
                        v.SizeId!.Value)
                    .Distinct()
                    .ToList();


            var heelSizeIds =
                variants
                    .Where(v =>
                        v.HeelSizeId.HasValue)
                    .Select(v =>
                        v.HeelSizeId!.Value)
                    .Distinct()
                    .ToList();


            // --------------------------------------------------------
            // Validate sizes and heel sizes in TWO queries.
            //
            // This is intentional: don't query once per variant.
            // --------------------------------------------------------

            if (sizeIds.Count > 0)
            {
                var existingSizeCount =
                    await _context.Sizes
                        .AsNoTracking()
                        .CountAsync(
                            s => sizeIds.Contains(s.Id),
                            cancellationToken);

                if (existingSizeCount != sizeIds.Count)
                {
                    throw new KeyNotFoundException(
                        "واحد او اكثر من المقاسات غير متوفر");
                }
            }


            if (heelSizeIds.Count > 0)
            {
                var existingHeelSizeCount =
                    await _context.HeelSizes
                        .AsNoTracking()
                        .CountAsync(
                            h => heelSizeIds.Contains(h.Id),
                            cancellationToken);

                if (existingHeelSizeCount != heelSizeIds.Count)
                {
                    throw new KeyNotFoundException(
                        "واحد او اكثر من مقاسات الكعب غير متوفر");
                }
            }
        }


        // ============================================================
        // NORMALIZE CREATE DTO
        // ============================================================

        private static void NormalizeProductDto(
            ProductDto dto)
        {
            dto.NameEn =
                dto.NameEn?.Trim() ?? string.Empty;

            dto.NameAr =
                dto.NameAr?.Trim() ?? string.Empty;

            dto.DescriptionEn =
                string.IsNullOrWhiteSpace(dto.DescriptionEn)
                    ? null
                    : dto.DescriptionEn.Trim();

            dto.DescriptionAr =
                string.IsNullOrWhiteSpace(dto.DescriptionAr)
                    ? null
                    : dto.DescriptionAr.Trim();

            dto.Images ??=
                new List<ProductImageDto>();

            dto.Variants ??=
                new List<ProductVariantDto>();
        }


        // ============================================================
        // NORMALIZE UPDATE DTO
        // ============================================================

        private static void NormalizeProductDto(
            UpdateProductDto dto)
        {
            dto.NameEn =
                dto.NameEn?.Trim() ?? string.Empty;

            dto.NameAr =
                dto.NameAr?.Trim() ?? string.Empty;

            dto.DescriptionEn =
                string.IsNullOrWhiteSpace(dto.DescriptionEn)
                    ? null
                    : dto.DescriptionEn.Trim();

            dto.DescriptionAr =
                string.IsNullOrWhiteSpace(dto.DescriptionAr)
                    ? null
                    : dto.DescriptionAr.Trim();

            dto.Images ??=
                new List<ProductImageDto>();

            dto.Variants ??=
                new List<ProductVariantDto>();
        }


        // ============================================================
        // PRODUCT STOCK STATUS
        // ============================================================

        private static bool CalculateProductStockStatus(
            int productStock,
            bool requestedIsInStock,
            List<ProductVariantDto>? variants)
        {
            if (variants != null &&
                variants.Count > 0)
            {
                return variants.Any(
                    v => v.StockQuantity > 0);
            }

            return requestedIsInStock &&
                   productStock > 0;
        }


        // ============================================================
        // VARIANT KEY
        // ============================================================

        private static string BuildVariantKey(
            int? sizeId,
            int? heelSizeId)
        {
            return $"{sizeId?.ToString() ?? "null"}:" +
                   $"{heelSizeId?.ToString() ?? "null"}";
        }


        // ============================================================
        // DELETE UPLOADED IMAGES
        // ============================================================

        private void DeleteUploadedImages(
            IEnumerable<string> imageUrls)
        {
            foreach (var imageUrl in imageUrls)
            {
                DeleteImage(imageUrl);
            }
        }


        // ============================================================
        // DELETE IMAGE FILE
        // ============================================================

        private void DeleteImage(
            string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            try
            {
                var fileName =
                    Path.GetFileName(imageUrl);

                if (string.IsNullOrWhiteSpace(fileName))
                    return;


                var uploadPath =
                    _configuration[
                        "FileStorage:UploadPath"];


                if (string.IsNullOrWhiteSpace(uploadPath))
                {
                    _logger.LogWarning(
                        "FileStorage:UploadPath is not configured.");

                    return;
                }


                var imagesFolder =
                    Path.Combine(
                        uploadPath,
                        "products");


                var filePath =
                    Path.Combine(
                        imagesFolder,
                        fileName);


                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete product image: {ImageUrl}",
                    imageUrl);
            }
        }
    }
}