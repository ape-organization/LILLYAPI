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

        Task<List<BestSellerProductDto>> GetBestSellerProducts(
            int count = 10,
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> GetNewArrivalProducts(
            CancellationToken cancellationToken = default);

        Task<List<ProductResponseDto>> getProductsbyCategory(
            int categoryID,
            int productID,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<ProductResponseDto>> AdminGetProducts(
       int page = 1,
       int? categoryId = null,
       bool? offers = null,
       CancellationToken cancellationToken = default);
    }


    public class ProductService : IProductService
    {
        private const int AdminPageSize = 20; /// for admin 
        private const int PageSize = 50;
        private const int SearchLimit = 50;
        private const int NewArrivalLimit = 30;
        private const int RelatedProductsLimit = 30;

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
        // GET NEW ARRIVALS
        // ============================================================

        public async Task<List<ProductResponseDto>> GetNewArrivalProducts(
            CancellationToken cancellationToken = default)
        {
            var fromDate = DateTime.UtcNow.AddMonths(-1);

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.CreatedAt >= fromDate)
                .OrderByDescending(p => p.CreatedAt)
                .Take(NewArrivalLimit)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
        }


        // ============================================================
        // GET PRODUCTS FROM SAME CATEGORY
        // ============================================================

        public async Task<List<ProductResponseDto>> getProductsbyCategory(
            int categoryID,
            int productID,
            CancellationToken cancellationToken = default)
        {
            if (categoryID <= 0)
                return [];

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.CategoryId == categoryID &&
                    p.Id != productID)
                .OrderByDescending(p => p.CreatedAt)
                .Take(RelatedProductsLimit)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
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

            IQueryable<Product> query = _context.Products
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
            // TOTAL COUNT
            // --------------------------------------------------------

            var totalCount =
                await query.CountAsync(cancellationToken);

            if (totalCount == 0)
            {
                return new PagedResponse<ProductResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = PageSize,
                    TotalPages = 0,
                    HasMore = false
                };
            }

            // --------------------------------------------------------
            // PAGINATION
            // --------------------------------------------------------

            var totalPages =
                (int)Math.Ceiling(
                    totalCount / (double)PageSize);

            var skip =
                (page - 1) * PageSize;

            // --------------------------------------------------------
            // IMPORTANT:
            //
            // MapProduct is intentionally used instead of
            // MapAllProduct for list requests.
            //
            // List pages do not need:
            // - DescriptionEn
            // - DescriptionAr
            // - Category object
            //
            // This reduces SQL/result payload.
            // --------------------------------------------------------

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
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

        public async Task<PagedResponse<ProductResponseDto>> AdminGetProducts(
        int page = 1,
        int? categoryId = null,
        bool? offers = null,
        CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);

            IQueryable<Product> query = _context.Products
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
            // TOTAL COUNT
            // --------------------------------------------------------

            var totalCount =
                await query.CountAsync(cancellationToken);

            if (totalCount == 0)
            {
                return new PagedResponse<ProductResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = AdminPageSize,
                    TotalPages = 0,
                    HasMore = false
                };
            }

            // --------------------------------------------------------
            // PAGINATION
            // --------------------------------------------------------

            var totalPages =
                (int)Math.Ceiling(
                    totalCount / (double)PageSize);

            var skip =
                (page - 1) * PageSize;

            // --------------------------------------------------------
            // IMPORTANT:
            //
            // MapProduct is intentionally used instead of
            // MapAllProduct for list requests.
            //
            // List pages do not need:
            // - DescriptionEn
            // - DescriptionAr
            // - Category object
            //
            // This reduces SQL/result payload.
            // --------------------------------------------------------

            var products = await query
               .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(PageSize)
                .Select(MapAllProduct())
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
        // GET SINGLE PRODUCT
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
                .OrderByDescending(p => p.CreatedAt)
                .Select(MapAllProduct())
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
                return [];

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
                .OrderByDescending(p => p.CreatedAt)
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
                return [];

            var ids = productIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return [];

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    ids.Contains(p.Id))
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
                .OrderByDescending(p => p.CreatedAt)
                .Select(MapProduct())
                .ToListAsync(cancellationToken);
        }


        // ============================================================
        // GET BEST SELLERS
        // ============================================================

        public async Task<List<BestSellerProductDto>> GetBestSellerProducts(
            int count = 10,
            CancellationToken cancellationToken = default)
        {
            count = Math.Clamp(count, 1, 100);

            var fromDate =
                DateTime.UtcNow.AddMonths(-2);

            // --------------------------------------------------------
            // Each product counts ONCE per confirmed order.
            // Quantity is intentionally ignored.
            // --------------------------------------------------------

            var bestSellerIds = await _context.OrderItems
                .AsNoTracking()
                .Where(oi =>
                    oi.Order.Status == OrderStatus.Confirmed &&
                    oi.Order.OrderDate >= fromDate &&
                    !oi.Product.IsDeleted &&
                    oi.Product.IsInStock)
                .Select(oi => new
                {
                    oi.ProductId,
                    oi.OrderId
                })
                .Distinct()
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    OrderCount = g.Count()
                })
                .OrderByDescending(x => x.OrderCount)
                .Take(count)
                .ToListAsync(cancellationToken);

            if (bestSellerIds.Count == 0)
                return [];


            // --------------------------------------------------------
            // Load only top products.
            // --------------------------------------------------------

            var productIds = bestSellerIds
                .Select(x => x.ProductId)
                .ToList();

            var products = await _context.Products
                .AsNoTracking()
                .Where(p =>
                    productIds.Contains(p.Id) &&
                    !p.IsDeleted)
                .Select(p => new BestSellerProductDto
                {
                    Id = p.Id,

                    NameEn = p.NameEn,

                    NameAr = p.NameAr,

                    Price = p.Price,

                    ActualPrice = p.ActualPrice,

                    DiscountPercentage =
                        p.DiscountPercentage,

                    IsInStock =
                        p.IsInStock,

                    Images = p.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageResponseDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            SortOrder = i.SortOrder
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);


            // --------------------------------------------------------
            // Restore ranking.
            // --------------------------------------------------------

            var ranking = bestSellerIds
                .Select((x, index) => new
                {
                    x.ProductId,
                    Rank = index
                })
                .ToDictionary(
                    x => x.ProductId,
                    x => x.Rank);

            return products
                .OrderBy(p => ranking[p.Id])
                .ToList();
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

            var hasVariants = dto.Variants.Count > 0;

            if (hasVariants)
            {
                dto.StockQuantity = 0;
            }

            // ------------------------------------------------------------
            // VALIDATION
            // ------------------------------------------------------------

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

            // ------------------------------------------------------------
            // CREATE ENTITY
            // ------------------------------------------------------------

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

                IsInStock = dto.IsInStock
            };

            var uploadedImageUrls =
                new List<string>();

            try
            {
                // --------------------------------------------------------
                // IMAGES
                // --------------------------------------------------------

                await AddNewImages(
                    product,
                    dto.Images,
                    uploadedImageUrls,
                    cancellationToken);

                // --------------------------------------------------------
                // VARIANTS
                // --------------------------------------------------------

                if (hasVariants)
                {
                    foreach (var variantDto in dto.Variants)
                    {
                        product.Variants.Add(
                            new ProductVariant
                            {
                                SizeId =
                                    variantDto.SizeId,

                                HeelSizeId =
                                    variantDto.HeelSizeId,

                                StockQuantity =
                                    variantDto.StockQuantity,

                                IsActive = true
                            });
                    }
                }

                // --------------------------------------------------------
                // ADD ENTITY GRAPH
                // --------------------------------------------------------

                var previousAutoDetect =
                    _context.ChangeTracker.AutoDetectChangesEnabled;

                try
                {
                    _context.ChangeTracker.AutoDetectChangesEnabled = false;

                    _context.Products.Add(product);
                }
                finally
                {
                    _context.ChangeTracker.AutoDetectChangesEnabled =
                        previousAutoDetect;
                }

                // One explicit change detection.
                _context.ChangeTracker.DetectChanges();

                // --------------------------------------------------------
                // SAVE
                // --------------------------------------------------------

                await _context.SaveChangesAsync(
                    cancellationToken);

                // --------------------------------------------------------
                // UPDATE DTO
                // --------------------------------------------------------

                dto.Id = product.Id;

                dto.StockQuantity =
                    product.StockQuantity;

                dto.IsInStock =
                    product.IsInStock;

                dto.Images =
                    product.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            SortOrder = i.SortOrder
                        })
                        .ToList();

                dto.Variants =
                    product.Variants
                        .OrderBy(v => v.Id)
                        .Select(v => new ProductVariantDto
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
                // Remove files if database operation fails.
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

            // ------------------------------------------------------------
            // LOAD PRODUCT
            // ------------------------------------------------------------

            var product =
                await _context.Products
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(
                        p =>
                            p.Id == id &&
                            !p.IsDeleted,
                        cancellationToken);

            if (product == null)
                return false;

            // ------------------------------------------------------------
            // VALIDATION
            // ------------------------------------------------------------

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

            var uploadedImageUrls =
                new List<string>();

            var imagesToDelete =
                new List<string>();

            try
            {
                // --------------------------------------------------------
                // UPDATE BASIC DATA
                // --------------------------------------------------------

                product.NameEn =
                    dto.NameEn;

                product.NameAr =
                    dto.NameAr;

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

                product.IsInStock =
                    dto.IsInStock;

                // --------------------------------------------------------
                // IMAGES
                // --------------------------------------------------------

                await UpdateImages(
                    product,
                    dto.Images,
                    uploadedImageUrls,
                    imagesToDelete,
                    cancellationToken);

                // --------------------------------------------------------
                // VARIANTS
                // --------------------------------------------------------

                UpdateVariants(
                    product,
                    dto.Variants);

                // --------------------------------------------------------
                // SAVE
                // --------------------------------------------------------

                await _context.SaveChangesAsync(
                    cancellationToken);

                // --------------------------------------------------------
                // DELETE OLD FILES ONLY AFTER DB SUCCESS
                // --------------------------------------------------------

                if (imagesToDelete.Count > 0)
                {
                    DeleteUploadedImages(
                        imagesToDelete.Distinct(
                            StringComparer.OrdinalIgnoreCase));
                }

                return true;
            }
            catch
            {
                // New uploads can safely be deleted.
                // Existing images are preserved.
                DeleteUploadedImages(
                    uploadedImageUrls);

                throw;
            }
        }
        // ============================================================
        // UPDATE VARIANTS
        // ============================================================

        private void UpdateVariants(
            Product product,
            List<ProductVariantDto>? requestedVariants)
        {
            requestedVariants ??= [];


            // --------------------------------------------------------
            // Existing variants lookup.
            //
            // Tuple avoids string allocations.
            // --------------------------------------------------------

            var existingVariants =
                product.Variants.ToDictionary(
                    v => BuildVariantKey(
                        v.SizeId,
                        v.HeelSizeId));


            var requestedKeys =
                new HashSet<
                    (int? SizeId, int? HeelSizeId)>();


            // --------------------------------------------------------
            // Add/update requested variants.
            // --------------------------------------------------------

            foreach (var dto in requestedVariants)
            {
                var key =
                    BuildVariantKey(
                        dto.SizeId,
                        dto.HeelSizeId);

                requestedKeys.Add(key);


                if (existingVariants.TryGetValue(
                    key,
                    out var existingVariant))
                {
                    existingVariant.StockQuantity =
                        dto.StockQuantity;

                    existingVariant.IsActive =
                        true;

                    continue;
                }


                product.Variants.Add(
                    new ProductVariant
                    {
                        SizeId =
                            dto.SizeId,

                        HeelSizeId =
                            dto.HeelSizeId,

                        StockQuantity =
                            dto.StockQuantity,

                        IsActive = true
                    });
            }


            // --------------------------------------------------------
            // Deactivate removed variants.
            //
            // We DO NOT physically delete variants because
            // historical OrderItems can reference them.
            // --------------------------------------------------------

            foreach (var existingVariant in product.Variants)
            {
                var key =
                    BuildVariantKey(
                        existingVariant.SizeId,
                        existingVariant.HeelSizeId);


                if (!requestedKeys.Contains(key))
                {
                    existingVariant.IsActive =
                        false;

                    existingVariant.StockQuantity =
                        0;
                }
            }
        }


        // ============================================================
        // CREATE IMAGES
        // ============================================================

        private async Task AddNewImages(
    Product product,
    List<ProductImageDto>? images,
    List<string> uploadedImageUrls,
    CancellationToken cancellationToken)
        {
            if (images == null || images.Count == 0)
                return;

            var imageDtos =
                images
                    .Where(i => i.Image != null)
                    .OrderBy(i => i.SortOrder)
                    .ToList();

            if (imageDtos.Count == 0)
                return;

            // ------------------------------------------------------------
            // UPLOAD IN PARALLEL
            // ------------------------------------------------------------

            var uploadTasks =
                imageDtos.Select(async imageDto =>
                {
                    var imageUrl =
                        await _imageService.SaveImageAsync(
                            imageDto.Image!,
                            "products",
                            cancellationToken);

                    return new
                    {
                        imageDto.SortOrder,
                        ImageUrl = imageUrl
                    };
                });

            var uploaded =
                await Task.WhenAll(uploadTasks);

            // ------------------------------------------------------------
            // ADD TO EF GRAPH
            // ------------------------------------------------------------

            foreach (var item in uploaded.OrderBy(x => x.SortOrder))
            {
                uploadedImageUrls.Add(
                    item.ImageUrl);

                product.Images.Add(
                    new ProductImage
                    {
                        ImageUrl =
                            item.ImageUrl,

                        SortOrder =
                            item.SortOrder
                    });
            }
        }


        // ============================================================
        // UPDATE IMAGES
        // ============================================================

        private async Task UpdateImages(
    Product product,
    List<ProductImageDto>? requestedImages,
    List<string> uploadedImageUrls,
    List<string> imagesToDelete,
    CancellationToken cancellationToken)
        {
            requestedImages ??= [];

            // ------------------------------------------------------------
            // EXISTING IMAGE IDS
            // ------------------------------------------------------------

            var existingImageIds =
                requestedImages
                    .Where(i => i.Id > 0)
                    .Select(i => i.Id)
                    .ToHashSet();

            var existingImagesById =
                product.Images
                    .Where(i => i.Id > 0)
                    .ToDictionary(i => i.Id);

            // ------------------------------------------------------------
            // REMOVE DELETED IMAGES
            // ------------------------------------------------------------

            foreach (var image in product.Images
                         .Where(i =>
                             i.Id > 0 &&
                             !existingImageIds.Contains(i.Id))
                         .ToList())
            {
                if (!string.IsNullOrWhiteSpace(
                        image.ImageUrl))
                {
                    imagesToDelete.Add(
                        image.ImageUrl);
                }

                _context.ProductImages.Remove(image);
            }

            // ------------------------------------------------------------
            // EXISTING + NEW IMAGES
            // ------------------------------------------------------------

            var uploadOperations =
                new List<(
                    ProductImageDto Dto,
                    ProductImage? Existing
                )>();

            foreach (var imageDto in requestedImages
                         .OrderBy(i => i.SortOrder))
            {
                // --------------------------------------------------------
                // EXISTING IMAGE
                // --------------------------------------------------------

                if (imageDto.Id > 0)
                {
                    if (!existingImagesById.TryGetValue(
                            imageDto.Id,
                            out var existingImage))
                    {
                        throw new KeyNotFoundException(
                            $"الصورة رقم {imageDto.Id} غير موجودة");
                    }

                    existingImage.SortOrder =
                        imageDto.SortOrder;

                    // No new file = nothing to upload.
                    if (imageDto.Image != null)
                    {
                        uploadOperations.Add(
                            (
                                imageDto,
                                existingImage
                            ));
                    }

                    continue;
                }

                // --------------------------------------------------------
                // NEW IMAGE
                // --------------------------------------------------------

                if (imageDto.Image != null)
                {
                    uploadOperations.Add(
                        (
                            imageDto,
                            null
                        ));
                }
            }

            // ------------------------------------------------------------
            // UPLOAD NEW / REPLACED IMAGES IN PARALLEL
            // ------------------------------------------------------------

            if (uploadOperations.Count > 0)
            {
                var uploadTasks =
                    uploadOperations.Select(async operation =>
                    {
                        var imageUrl =
                            await _imageService.SaveImageAsync(
                                operation.Dto.Image!,
                                "products",
                                cancellationToken);

                        return new
                        {
                            operation.Dto,
                            operation.Existing,
                            ImageUrl = imageUrl
                        };
                    });

                var uploaded =
                    await Task.WhenAll(uploadTasks);

                // --------------------------------------------------------
                // APPLY UPLOAD RESULTS
                // --------------------------------------------------------

                foreach (var item in uploaded
                             .OrderBy(x => x.Dto.SortOrder))
                {
                    uploadedImageUrls.Add(
                        item.ImageUrl);

                    // ----------------------------------------------------
                    // REPLACE EXISTING
                    // ----------------------------------------------------

                    if (item.Existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(
                                item.Existing.ImageUrl))
                        {
                            imagesToDelete.Add(
                                item.Existing.ImageUrl);
                        }

                        item.Existing.ImageUrl =
                            item.ImageUrl;

                        item.Existing.SortOrder =
                            item.Dto.SortOrder;

                        continue;
                    }

                    // ----------------------------------------------------
                    // ADD NEW
                    // ----------------------------------------------------

                    product.Images.Add(
                        new ProductImage
                        {
                            ImageUrl =
                                item.ImageUrl,

                            SortOrder =
                                item.Dto.SortOrder
                        });
                }
            }

            // ------------------------------------------------------------
            // NORMALIZE SORT ORDER
            // ------------------------------------------------------------

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


            // --------------------------------------------------------
            // Direct database UPDATE.
            //
            // Old version:
            //
            // SELECT product
            // UPDATE product
            // SELECT product
            //
            // New version:
            //
            // UPDATE product
            // SELECT product
            // --------------------------------------------------------

            var affected =
                await _context.Products
                    .Where(p =>
                        p.Id == id &&
                        !p.IsDeleted)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                p => p.DiscountPercentage,
                                0m),
                        cancellationToken);


            if (affected == 0)
                return null;


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


            // --------------------------------------------------------
            // Direct SQL UPDATE.
            //
            // No SELECT.
            // No entity materialization.
            // No change tracking.
            // --------------------------------------------------------

            var affected =
                await _context.Products
                    .Where(p =>
                        p.Id == id &&
                        !p.IsDeleted)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                p => p.IsDeleted,
                                true),
                        cancellationToken);


            return affected > 0;
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

                Price = p.Price,

                ActualPrice = p.ActualPrice,

                StockQuantity = p.StockQuantity,

                IsInStock = p.IsInStock,

                DiscountPercentage = p.DiscountPercentage,

                CategoryId = p.CategoryId,

                // --------------------------------------------------------
                // ONLY FIRST 2 IMAGES
                // --------------------------------------------------------

                Images = p.Images
                    .OrderBy(i => i.SortOrder)
                    .Take(2)
                    .Select(i => new ProductImageResponseDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        SortOrder = i.SortOrder
                    })
                    .ToList(),

                // --------------------------------------------------------
                // ONLY CHECK WHETHER ACTIVE VARIANT EXISTS
                // --------------------------------------------------------

                HasVariants = p.Variants
                    .Any(v => v.IsActive)
            };
        }
        // ============================================================
        // FULL PRODUCT PROJECTION
        // ============================================================

        private static Expression<
            Func<Product, ProductResponseDto>>
            MapAllProduct()
        {
            return p => new ProductResponseDto
            {
                Id =
                    p.Id,

                NameEn =
                    p.NameEn,

                NameAr =
                    p.NameAr,

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
            var query =
                _context.Products
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
        // VALIDATE CREATE DTO
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


        // ============================================================
        // VALIDATE UPDATE DTO
        // ============================================================

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
            // Every variant needs at least one option.
            // --------------------------------------------------------

            foreach (var variant in variants)
            {
                if (!variant.SizeId.HasValue &&
                    !variant.HeelSizeId.HasValue)
                {
                    throw new InvalidOperationException(
                        "كل منتج فرعي يجب ان يحتوي على مقاس او مقاس كعب");
                }

                if (variant.StockQuantity < 0)
                {
                    throw new InvalidOperationException(
                        "كميه المخزون لا يمكن ان تكون اقل من الصفر");
                }
            }


            // --------------------------------------------------------
            // Detect duplicate combinations.
            //
            // HashSet avoids GroupBy allocations.
            // --------------------------------------------------------

            var combinations =
                new HashSet<
                    (int? SizeId, int? HeelSizeId)>();


            foreach (var variant in variants)
            {
                var key =
                    BuildVariantKey(
                        variant.SizeId,
                        variant.HeelSizeId);


                if (!combinations.Add(key))
                {
                    throw new InvalidOperationException(
                        "لا يمكن تكرار نفس تركيبة المقاس ومقاس الكعب");
                }
            }


            // --------------------------------------------------------
            // Collect unique IDs.
            // --------------------------------------------------------

            var sizeIds =
                variants
                    .Where(v => v.SizeId.HasValue)
                    .Select(v => v.SizeId!.Value)
                    .Distinct()
                    .ToList();


            var heelSizeIds =
                variants
                    .Where(v => v.HeelSizeId.HasValue)
                    .Select(v => v.HeelSizeId!.Value)
                    .Distinct()
                    .ToList();


            // --------------------------------------------------------
            // Validate sizes.
            //
            // We intentionally use two queries because these are
            // different tables and the same DbContext cannot execute
            // EF operations concurrently.
            // --------------------------------------------------------

            if (sizeIds.Count > 0)
            {
                var validSizes =
                    await _context.Sizes
                        .AsNoTracking()
                        .CountAsync(
                            s => sizeIds.Contains(s.Id),
                            cancellationToken);


                if (validSizes != sizeIds.Count)
                {
                    throw new KeyNotFoundException(
                        "واحد او اكثر من المقاسات غير متوفر");
                }
            }


            // --------------------------------------------------------
            // Validate heel sizes.
            // --------------------------------------------------------

            if (heelSizeIds.Count > 0)
            {
                var validHeelSizes =
                    await _context.HeelSizes
                        .AsNoTracking()
                        .CountAsync(
                            h => heelSizeIds.Contains(h.Id),
                            cancellationToken);


                if (validHeelSizes != heelSizeIds.Count)
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
                dto.NameEn?.Trim() ??
                string.Empty;


            dto.NameAr =
                dto.NameAr?.Trim() ??
                string.Empty;


            dto.DescriptionEn =
                string.IsNullOrWhiteSpace(
                    dto.DescriptionEn)
                    ? null
                    : dto.DescriptionEn.Trim();


            dto.DescriptionAr =
                string.IsNullOrWhiteSpace(
                    dto.DescriptionAr)
                    ? null
                    : dto.DescriptionAr.Trim();


            dto.Images ??= [];

            dto.Variants ??= [];
        }


        // ============================================================
        // NORMALIZE UPDATE DTO
        // ============================================================

        private static void NormalizeProductDto(
            UpdateProductDto dto)
        {
            dto.NameEn =
                dto.NameEn?.Trim() ??
                string.Empty;


            dto.NameAr =
                dto.NameAr?.Trim() ??
                string.Empty;


            dto.DescriptionEn =
                string.IsNullOrWhiteSpace(
                    dto.DescriptionEn)
                    ? null
                    : dto.DescriptionEn.Trim();


            dto.DescriptionAr =
                string.IsNullOrWhiteSpace(
                    dto.DescriptionAr)
                    ? null
                    : dto.DescriptionAr.Trim();


            dto.Images ??= [];

            dto.Variants ??= [];
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

        private static (
            int? SizeId,
            int? HeelSizeId)
            BuildVariantKey(
                int? sizeId,
                int? heelSizeId)
        {
            return (
                sizeId,
                heelSizeId);
        }


        // ============================================================
        // DELETE MULTIPLE IMAGE FILES
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