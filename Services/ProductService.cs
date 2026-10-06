using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using System.Diagnostics;
using System.Linq.Expressions;

namespace PharmacyAPI.Services;

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

public sealed class ProductService : IProductService
{
    private const int ProductDefaultStock = 5;
    private const int VariantDefaultStock = 5;

    private const int AdminPageSize = 40;
    private const int PageSize = 50;
    private const int SearchLimit = 50;
    private const int NewArrivalLimit = 30;
    private const int RelatedProductsLimit = 30;

    private readonly ShoesDbContext _context;
    private readonly ILogger<ProductService> _logger;
    private readonly ImageService _imageService;

    public ProductService(
        ShoesDbContext context,
        ILogger<ProductService> logger,
        ImageService imageService)
    {
        _context = context;
        _logger = logger;
        _imageService = imageService;
    }

    // ============================================================
    // LIGHTWEIGHT PRODUCT PROJECTION
    // ============================================================
    //
    // Used by:
    // - GetProducts
    // - GetProductsByName
    // - GetProductsByIds
    // - GetDiscountedProducts
    // - GetNewArrivalProducts
    // - getProductsbyCategory
    //
    // Important:
    // Images are intentionally NOT loaded here.
    //
    // HasVariants is calculated by SQL using EXISTS instead of
    // loading ProductVariant entities.
    //
    private static readonly Expression<Func<Product, ProductResponseDto>>
     ProductListProjection =
         p => new ProductResponseDto
         {
             Id = p.Id,

             NameEn = p.NameEn,
             NameAr = p.NameAr,

             Price = p.Price,

             ActualPrice = 0,

             StockQuantity =
                 ProductDefaultStock,

             IsInStock =
                 p.IsInStock,

             DiscountPercentage =
                 p.DiscountPercentage,

             DiscountedPrice =
                 p.DiscountPercentage > 0
                     ? p.Price -
                       (p.Price *
                        p.DiscountPercentage / 100m)
                     : p.Price,

             CategoryId =
                 p.CategoryId,

             HasVariants =
                 p.Variants.Any(v =>
                     v.IsActive &&
                     (!v.SizeId.HasValue ||
                      (v.Size != null &&
                       v.Size.IsActive)) &&
                     (!v.HeelSizeId.HasValue ||
                      (v.HeelSize != null &&
                       v.HeelSize.IsActive))
                 ),

             Images =
                 p.Images
                     .OrderBy(i => i.SortOrder)
                     .Select(i => i.ImageUrl)
                     .ToList()
         };


    // ============================================================
    // GET PRODUCTS
    // ============================================================

    public async Task<PagedResponse<ProductResponseDto>> GetProducts(
        int page = 1,
        int? categoryId = null,
        bool? offers = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        IQueryable<Product> query =
            _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

        // SQL FILTER
        if (categoryId.HasValue &&
            categoryId.Value > 0)
        {
            query = query.Where(
                p => p.CategoryId == categoryId.Value);
        }

        // SQL FILTER
        if (offers == true)
        {
            query = query.Where(
                p => p.DiscountPercentage > 0);
        }

        // COUNT IN SQL
        var totalCount =
            await query.CountAsync(
                cancellationToken);

        // PAGINATION + PROJECTION IN SQL
        var products =
            await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(ProductListProjection)
                .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponseDto>
        {
            Items = products,

            Page = page,

            PageSize = PageSize,

            TotalCount = totalCount,

            TotalPages =
                (int)Math.Ceiling(
                    totalCount / (double)PageSize)
        };
    }

    // ============================================================
    // ADMIN GET PRODUCTS
    // ============================================================

    public async Task<PagedResponse<ProductResponseDto>> AdminGetProducts(
        int page = 1,
        int? categoryId = null,
        bool? offers = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);

        IQueryable<Product> query =
            _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

        if (categoryId.HasValue &&
            categoryId.Value > 0)
        {
            query = query.Where(
                p => p.CategoryId == categoryId.Value);
        }

        if (offers == true)
        {
            query = query.Where(
                p => p.DiscountPercentage > 0);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var products =
            await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * AdminPageSize)
                .Take(AdminPageSize)

                .Include(p => p.Images)

                .Include(p => p.Category)

                .Include(p => p.Variants)
                    .ThenInclude(v => v.Size)

                .Include(p => p.Variants)
                    .ThenInclude(v => v.HeelSize)

                .AsSplitQuery()

                .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponseDto>
        {
            Items =
                products
                    .Select(MapAllProduct)
                    .ToList(),

            Page = page,

            PageSize = AdminPageSize,

            TotalCount = totalCount,

            TotalPages =
                (int)Math.Ceiling(
                    totalCount /
                    (double)AdminPageSize)
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

        var product =
            await _context.Products
                .AsNoTracking()

                .Where(p =>
                    p.Id == id &&
                    !p.IsDeleted)

                .Include(p => p.Images)

                .Include(p => p.Category)

                .Include(p => p.Variants)
                    .ThenInclude(v => v.Size)

                .Include(p => p.Variants)
                    .ThenInclude(v => v.HeelSize)

                .AsSplitQuery()

                .FirstOrDefaultAsync(
                    cancellationToken);

        return product is null
            ? null
            : MapAllProduct(product);
    }

    // ============================================================
    // SEARCH PRODUCTS
    // ============================================================

    public async Task<List<ProductResponseDto>> GetProductsByName(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return [];

        var search =
            name.Trim();

        return await _context.Products
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&

                (EF.Functions.Like(
                     p.NameEn,
                     $"%{search}%") ||

                 EF.Functions.Like(
                     p.NameAr,
                     $"%{search}%")))

            .OrderByDescending(
                p => p.CreatedAt)

            .ThenByDescending(
                p => p.Id)

            .Take(SearchLimit)

            .Select(ProductListProjection)

            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // GET PRODUCTS BY IDS
    // ============================================================

    public async Task<List<ProductResponseDto>> GetProductsByIds(
        List<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds is null ||
            productIds.Count == 0)
        {
            return [];
        }

        var ids =
            productIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

        if (ids.Length == 0)
            return [];

        return await _context.Products
            .AsNoTracking()
            .Where(p =>
                ids.Contains(p.Id) &&
                !p.IsDeleted)

            .Select(ProductListProjection)

            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // DISCOUNTED PRODUCTS
    // ============================================================

    public async Task<List<ProductResponseDto>> GetDiscountedProducts(
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&
                p.DiscountPercentage > 0)

            .OrderByDescending(
                p => p.CreatedAt)

            .ThenByDescending(
                p => p.Id)

            .Take(PageSize)

            .Select(ProductListProjection)

            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // NEW ARRIVALS
    // ============================================================

    public async Task<List<ProductResponseDto>> GetNewArrivalProducts(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromDate =
                DateTime.UtcNow.AddMonths(-1);

            return await _context.Products
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted &&
                    p.CreatedAt >= fromDate)

                .OrderByDescending(
                    p => p.CreatedAt)

                .ThenByDescending(
                    p => p.Id)

                .Take(NewArrivalLimit)

                .Select(ProductListProjection)

                .ToListAsync(
                    cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    // ============================================================
    // RELATED PRODUCTS
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

            .OrderByDescending(
                p => p.CreatedAt)

            .ThenByDescending(
                p => p.Id)

            .Take(RelatedProductsLimit)

            .Select(ProductListProjection)

            .ToListAsync(
                cancellationToken);
    }

    // ============================================================
    // BEST SELLERS
    // ============================================================

    public async Task<List<BestSellerProductDto>> GetBestSellerProducts(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        count =
            Math.Clamp(
                count,
                1,
                100);

        var fromDate =
            DateTime.UtcNow.AddMonths(-2);

        var ranking =
            await _context.OrderItems
                .AsNoTracking()

                .Where(oi =>
                    oi.Order != null &&

                    oi.Order.Status ==
                        OrderStatus.Confirmed &&

                    oi.Order.OrderDate >=
                        fromDate &&

                    oi.Product != null &&

                    !oi.Product.IsDeleted &&

                    oi.Product.IsInStock)

                .Select(oi => new
                {
                    oi.ProductId,
                    OrderId = oi.OrderId
                })

                .Distinct()

                .GroupBy(x => x.ProductId)

                .Select(g => new
                {
                    ProductId = g.Key,

                    Count =
                        g.Count()
                })

                .OrderByDescending(
                    x => x.Count)

                .ThenByDescending(
                    x => x.ProductId)

                .Take(count)

                .ToListAsync(
                    cancellationToken);

        if (ranking.Count == 0)
            return [];

        var productIds =
            ranking
                .Select(x => x.ProductId)
                .ToArray();

        var products =
            await _context.Products
                .AsNoTracking()

                .Where(p =>
                    productIds.Contains(p.Id) &&

                    !p.IsDeleted &&

                    p.IsInStock)

                .Include(p => p.Images)

                .ToListAsync(
                    cancellationToken);

        var productsById =
            products.ToDictionary(
                p => p.Id);

        var result =
            new List<BestSellerProductDto>(
                ranking.Count);

        foreach (var rank in ranking)
        {
            if (!productsById.TryGetValue(
                    rank.ProductId,
                    out var product))
            {
                continue;
            }

            result.Add(
                new BestSellerProductDto
                {
                    Id =
                        product.Id,

                    NameEn =
                        product.NameEn,

                    NameAr =
                        product.NameAr,

                    Price =
                        product.Price,

                    ActualPrice =
                        0,

                    StockQuantity =
                        ProductDefaultStock,

                    IsInStock =
                        product.IsInStock,

                    DiscountPercentage =
                        product.DiscountPercentage,

                    DiscountedPrice =
                        product.DiscountPercentage > 0

                            ? product.Price -
                              (product.Price *
                               product.DiscountPercentage /
                               100m)

                            : product.Price,

                    Images =
                        product.Images?
                            .OrderBy(
                                i => i.SortOrder)

                            .Select(
                                i => i.ImageUrl)

                            .ToList()

                        ?? []
                });
        }

        return result;
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

        var normalizedName =
            name.Trim();

        return await _context.Products
            .AsNoTracking()

            .AnyAsync(
                p =>
                    !p.IsDeleted &&
                    p.NameEn == normalizedName,

                cancellationToken);
    }

    // ============================================================
    // CREATE PRODUCT
    // ============================================================

    // ============================================================
    // CREATE PRODUCT
    // ============================================================

    // ============================================================
    // CREATE PRODUCT
    // ============================================================

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

        // One DB round-trip for product-name + category validation.
        await ValidateProductAndCategory(
            dto.NameEn,
            null,
            dto.CategoryId,
            cancellationToken);

        // One DB round-trip for all size + heel-size validation.
        await ValidateVariants(
            dto.Variants,
            cancellationToken);

        var product = new Product
        {
            NameEn = dto.NameEn,
            NameAr = dto.NameAr,
            DescriptionEn = dto.DescriptionEn,
            DescriptionAr = dto.DescriptionAr,
            Price = dto.Price,
            ActualPrice = 0,
            StockQuantity = ProductDefaultStock,
            DiscountPercentage = dto.DiscountPercentage,
            IsInStock = dto.IsInStock,
            CategoryId = dto.CategoryId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        var uploadedImages = new List<string>();

        try
        {
            // --------------------------------------------------------
            // IMAGES
            // --------------------------------------------------------

            if (dto.Images is { Count: > 0 })
            {
                var imageResults = await UploadImagesAsync(
                    dto.Images,
                    "products",
                    cancellationToken);

                uploadedImages.AddRange(
                    imageResults.Select(x => x.Url));

                foreach (var image in imageResults)
                {
                    product.Images.Add(new ProductImage
                    {
                        ImageUrl = image.Url,
                        SortOrder = image.SortOrder
                    });
                }
            }

            // --------------------------------------------------------
            // VARIANTS
            // --------------------------------------------------------

            if (dto.Variants is { Count: > 0 })
            {
                foreach (var variantDto in dto.Variants)
                {
                    product.Variants.Add(new ProductVariant
                    {
                        SizeId = variantDto.SizeId,
                        HeelSizeId = variantDto.HeelSizeId,
                        StockQuantity = VariantDefaultStock,
                        IsActive = true
                    });
                }
            }

            // --------------------------------------------------------
            // TRACK + SAVE
            // --------------------------------------------------------

            _context.Products.Add(product);

            await _context.SaveChangesAsync(cancellationToken);

            // --------------------------------------------------------
            // RESPONSE
            // --------------------------------------------------------

            dto.Id = product.Id;
            dto.StockQuantity = ProductDefaultStock;
            dto.IsInStock = product.IsInStock;

            dto.Images = product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto
                {
                    ImageUrl = i.ImageUrl,
                    SortOrder = i.SortOrder
                })
                .ToList();

            return dto;
        }
        catch
        {
            if (uploadedImages.Count > 0)
            {
                await _imageService.DeleteImagesAsync(
                    uploadedImages,
                    "products",
                    CancellationToken.None);
            }

            throw;
        }
    }

    // ============================================================
    // UPDATE PRODUCT
    // ============================================================

    // ============================================================
    // UPDATE PRODUCT
    // ============================================================

    public async Task<bool> UpdateProduct(
        int id,
        UpdateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0 || dto is null)
            return false;

        NormalizeProductDto(dto);
        ValidateBasicProductData(dto);

        // One DB round-trip for product-name + category validation.
        await ValidateProductAndCategory(
            dto.NameEn,
            id,
            dto.CategoryId,
            cancellationToken);

        // One DB round-trip for all size + heel-size validation.
        await ValidateVariants(
            dto.Variants,
            cancellationToken);

        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p =>
                p.Id == id &&
                !p.IsDeleted)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
            return false;

        var newUploadedImages = new List<string>();
        var oldImagesToDelete = new List<string>();

        try
        {
            product.NameEn = dto.NameEn;
            product.NameAr = dto.NameAr;
            product.DescriptionEn = dto.DescriptionEn;
            product.DescriptionAr = dto.DescriptionAr;
            product.Price = dto.Price;
            product.ActualPrice = 0;
            product.StockQuantity = ProductDefaultStock;
            product.DiscountPercentage = dto.DiscountPercentage;
            product.IsInStock = dto.IsInStock;
            product.CategoryId = dto.CategoryId;

            var imagesChanged =
                dto.Images is not null &&
                dto.Images.Any(x => x.Image is not null);

            if (imagesChanged)
            {
                await UpdateImagesAsync(
                    product,
                    dto.Images!,
                    newUploadedImages,
                    oldImagesToDelete,
                    cancellationToken);
            }

            UpdateVariants(product, dto.Variants);

            await _context.SaveChangesAsync(cancellationToken);

            if (imagesChanged && oldImagesToDelete.Count > 0)
            {
                await _imageService.DeleteImagesAsync(
                    oldImagesToDelete,
                    "products",
                    cancellationToken);
            }

            return true;
        }
        catch
        {
            if (newUploadedImages.Count > 0)
            {
                await _imageService.DeleteImagesAsync(
                    newUploadedImages,
                    "products",
                    CancellationToken.None);
            }

            throw;
        }
    }

    // ============================================================
    // UPDATE IMAGES
    // ============================================================

    private async Task UpdateImagesAsync(
    Product product,
    List<ProductImageDto>? requestedImages,
    List<string> newlyUploadedImages,
    List<string> oldImagesToDelete,
    CancellationToken cancellationToken)
    {
        var requested =
            requestedImages?
                .OrderBy(i => i.SortOrder)
                .ToList()
            ?? [];

        var existingImages =
            product.Images.ToList();

        var existingByUrl =
            existingImages
                .ToDictionary(
                    i => i.ImageUrl,
                    StringComparer.OrdinalIgnoreCase);

        var requestedExistingUrls =
            requested
                .Where(i =>
                    !string.IsNullOrWhiteSpace(i.ImageUrl))
                .Select(i =>
                    i.ImageUrl!.Trim())
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);


        // ============================================================
        // REMOVE IMAGES THAT ARE NO LONGER REQUESTED
        // ============================================================

        foreach (var existing in existingImages)
        {
            if (!requestedExistingUrls.Contains(
                    existing.ImageUrl))
            {
                oldImagesToDelete.Add(
                    existing.ImageUrl);

                _context.ProductImages.Remove(
                    existing);
            }
        }


        // ============================================================
        // UPDATE / ADD IMAGES
        // ============================================================

        foreach (var requestedImage in requested)
        {
            var imageUrl =
                requestedImage.ImageUrl?.Trim();


            // --------------------------------------------------------
            // EXISTING IMAGE
            // --------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                existingByUrl.TryGetValue(
                    imageUrl,
                    out var existing))
            {
                existing.SortOrder =
                    requestedImage.SortOrder;


                // No new file:
                // keep existing physical image.
                if (requestedImage.Image is null)
                {
                    continue;
                }


                // ----------------------------------------------------
                // REPLACE EXISTING IMAGE
                // ----------------------------------------------------

                var oldUrl =
                    existing.ImageUrl;

                var newUrl =
                    await _imageService.SaveImageAsync(
                        requestedImage.Image,
                        "products",
                        cancellationToken);

                newlyUploadedImages.Add(
                    newUrl);

                existing.ImageUrl =
                    newUrl;

                oldImagesToDelete.Add(
                    oldUrl);

                continue;
            }


            // --------------------------------------------------------
            // NEW IMAGE
            // --------------------------------------------------------

            if (requestedImage.Image is null)
            {
                continue;
            }

            var uploadedUrl =
                await _imageService.SaveImageAsync(
                    requestedImage.Image,
                    "products",
                    cancellationToken);

            newlyUploadedImages.Add(
                uploadedUrl);

            product.Images.Add(
                new ProductImage
                {
                    ImageUrl =
                        uploadedUrl,

                    SortOrder =
                        requestedImage.SortOrder
                });
        }


        // ============================================================
        // NORMALIZE SORT ORDER
        // ============================================================

        var finalImages =
            product.Images
                .Where(i =>
                    _context.Entry(i).State !=
                    EntityState.Deleted)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .ToList();

        for (
            var i = 0;
            i < finalImages.Count;
            i++)
        {
            finalImages[i].SortOrder =
                i;
        }
    }



    // ============================================================
    // UPLOAD IMAGES
    // ============================================================

    private async Task<List<UploadedImageResult>>
       UploadImagesAsync(
           List<ProductImageDto> images,
           string folder,
           CancellationToken cancellationToken)
    {
        var validImages =
            images
                .Where(i => i.Image is not null)
                .OrderBy(i => i.SortOrder)
                .ToList();

        if (validImages.Count == 0)
            return [];

        var urls =
            await _imageService.SaveImagesAsync(
                validImages
                    .Select(i => i.Image!)
                    .ToList(),
                folder,
                cancellationToken);

        return
            urls
                .Select(
                    (url, index) =>
                        new UploadedImageResult(
                            url,
                            validImages[index].SortOrder))
                .ToList();
    }
    // ============================================================
    // UPDATE VARIANTS
    // ============================================================

    // ============================================================
    // UPDATE VARIANTS
    // ============================================================

    private void UpdateVariants(
        Product product,
        List<ProductVariantDto>? variantDtos)
    {
        variantDtos ??= [];

        // --------------------------------------------------------
        // BUILD LOOKUP OF EXISTING VARIANTS
        // --------------------------------------------------------

        var existingVariants =
            product.Variants
                .ToDictionary(
                    v =>
                        BuildVariantKey(
                            v.SizeId,
                            v.HeelSizeId));

        // --------------------------------------------------------
        // TRACK INCOMING VARIANT KEYS
        // --------------------------------------------------------

        var incomingKeys =
            new HashSet<(
                int? SizeId,
                int? HeelSizeId)>();

        // --------------------------------------------------------
        // UPDATE EXISTING / ADD NEW
        // --------------------------------------------------------

        foreach (var variantDto in variantDtos)
        {
            var key =
                BuildVariantKey(
                    variantDto.SizeId,
                    variantDto.HeelSizeId);

            incomingKeys.Add(key);

            // ----------------------------------------------------
            // EXISTING VARIANT
            // ----------------------------------------------------

            if (existingVariants.TryGetValue(
                    key,
                    out var existingVariant))
            {
                // Keep the existing database record.

                // IMPORTANT:
                // Do NOT reset StockQuantity here.
                // Existing stock must be preserved.

                existingVariant.IsActive =
                    true;

                continue;
            }

            // ----------------------------------------------------
            // NEW VARIANT
            // ----------------------------------------------------

            product.Variants.Add(
                new ProductVariant
                {
                    SizeId =
                        variantDto.SizeId,

                    HeelSizeId =
                        variantDto.HeelSizeId,

                    StockQuantity =
                        VariantDefaultStock,

                    IsActive =
                        true
                });
        }

        // --------------------------------------------------------
        // DEACTIVATE REMOVED VARIANTS
        // --------------------------------------------------------

        foreach (var existingVariant in product.Variants)
        {
            var key =
                BuildVariantKey(
                    existingVariant.SizeId,
                    existingVariant.HeelSizeId);

            if (!incomingKeys.Contains(key))
            {
                existingVariant.IsActive =
                    false;
            }
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

        var affected =
            await _context.Products

                .Where(p =>
                    p.Id == id &&
                    !p.IsDeleted)

                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            p =>
                                p.DiscountPercentage,
                            0),

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

        var affected =
            await _context.Products

                .Where(p =>
                    p.Id == id &&
                    !p.IsDeleted)

                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            p =>
                                p.IsDeleted,
                            true),

                    cancellationToken);

        return affected > 0;
    }

    // ============================================================
    // VALIDATE PRODUCT + CATEGORY
    // ============================================================

    private async Task ValidateProductAndCategory(
        string name,
        int? currentId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            throw new ArgumentException(
                "A valid category is required.");
        }

        var normalizedName = name.Trim();

        // UNION ALL makes product-name and category validation
        // one SQL round-trip instead of two remote DB round-trips.
        var validationResults = await _context.Products
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&
                p.NameEn == normalizedName &&
                (!currentId.HasValue || p.Id != currentId.Value))
            .Select(_ => new { Type = 1, Id = 0 })
            .Concat(
                _context.Categories
                    .AsNoTracking()
                    .Where(c =>
                        c.Id == categoryId &&
                        !c.IsDeleted)
                    .Select(_ => new { Type = 2, Id = 0 }))
            .ToListAsync(cancellationToken);

        if (validationResults.Any(x => x.Type == 1))
        {
            throw new InvalidOperationException(
                "A product with this name already exists.");
        }

        if (!validationResults.Any(x => x.Type == 2))
        {
            throw new ArgumentException(
                "The selected category does not exist.");
        }
    }

    // ============================================================
    // VALIDATE PRODUCT NAME
    // ============================================================

    private async Task ValidateProductName(
        string name,
        int? currentId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();

        var exists = await _context.Products
            .AsNoTracking()
            .AnyAsync(
                p =>
                    !p.IsDeleted &&
                    p.NameEn == normalizedName &&
                    (!currentId.HasValue ||
                     p.Id != currentId.Value),
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "A product with this name already exists.");
        }
    }

    // ============================================================
    // VALIDATE CATEGORY
    // ============================================================

    private async Task ValidateCategory(
        int categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            throw new ArgumentException(
                "A valid category is required.");
        }

        var exists = await _context.Categories
            .AsNoTracking()
            .AnyAsync(
                c =>
                    c.Id == categoryId &&
                    !c.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new ArgumentException(
                "The selected category does not exist.");
        }
    }

    // ============================================================
    // VALIDATE CREATE DTO
    // ============================================================

    private static void ValidateBasicProductData(
        ProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.NameEn))
        {
            throw new ArgumentException(
                "English product name is required.");
        }

        if (string.IsNullOrWhiteSpace(
                dto.NameAr))
        {
            throw new ArgumentException(
                "Arabic product name is required.");
        }

        if (dto.Price < 0)
        {
            throw new ArgumentException(
                "Product price cannot be negative.");
        }

        if (dto.DiscountPercentage < 0 ||
            dto.DiscountPercentage > 100)
        {
            throw new ArgumentException(
                "Discount percentage must be between 0 and 100.");
        }
    }

    // ============================================================
    // VALIDATE UPDATE DTO
    // ============================================================

    private static void ValidateBasicProductData(
        UpdateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.NameEn))
        {
            throw new ArgumentException(
                "English product name is required.");
        }

        if (string.IsNullOrWhiteSpace(
                dto.NameAr))
        {
            throw new ArgumentException(
                "Arabic product name is required.");
        }

        if (dto.Price < 0)
        {
            throw new ArgumentException(
                "Product price cannot be negative.");
        }

        if (dto.DiscountPercentage < 0 ||
            dto.DiscountPercentage > 100)
        {
            throw new ArgumentException(
                "Discount percentage must be between 0 and 100.");
        }
    }

    // ============================================================
    // VALIDATE VARIANTS
    // ============================================================

    // ============================================================
    // VALIDATE VARIANTS
    // ============================================================

    // ============================================================
    // VALIDATE VARIANTS
    // ============================================================

    // ============================================================
    // VALIDATE VARIANTS
    // ============================================================

    private async Task ValidateVariants(
        List<ProductVariantDto>? variants,
        CancellationToken cancellationToken)
    {
        if (variants is null || variants.Count == 0)
            return;

        var keys = new HashSet<(int? SizeId, int? HeelSizeId)>();
        var sizeIds = new HashSet<int>();
        var heelSizeIds = new HashSet<int>();

        foreach (var variant in variants)
        {
            if (!variant.SizeId.HasValue &&
                !variant.HeelSizeId.HasValue)
            {
                throw new ArgumentException(
                    "Each variant must have a size or heel size.");
            }

            var key = BuildVariantKey(
                variant.SizeId,
                variant.HeelSizeId);

            if (!keys.Add(key))
            {
                throw new ArgumentException(
                    "Duplicate product variant detected.");
            }

            if (variant.SizeId.HasValue)
                sizeIds.Add(variant.SizeId.Value);

            if (variant.HeelSizeId.HasValue)
                heelSizeIds.Add(variant.HeelSizeId.Value);
        }

        // One SQL round-trip validates both tables.
        var validReferences = await _context.Sizes
            .AsNoTracking()
            .Where(s =>
                s.IsActive &&
                sizeIds.Contains(s.Id))
            .Select(s => new
            {
                Type = 1,
                Id = s.Id
            })
            .Concat(
                _context.HeelSizes
                    .AsNoTracking()
                    .Where(h =>
                        h.IsActive &&
                        heelSizeIds.Contains(h.Id))
                    .Select(h => new
                    {
                        Type = 2,
                        Id = h.Id
                    }))
            .ToListAsync(cancellationToken);

        var validSizeIds = validReferences
            .Where(x => x.Type == 1)
            .Select(x => x.Id)
            .ToHashSet();

        var validHeelSizeIds = validReferences
            .Where(x => x.Type == 2)
            .Select(x => x.Id)
            .ToHashSet();

        if (validSizeIds.Count != sizeIds.Count)
        {
            throw new ArgumentException(
                "One or more selected sizes do not exist or are inactive.");
        }

        if (validHeelSizeIds.Count != heelSizeIds.Count)
        {
            throw new ArgumentException(
                "One or more selected heel sizes do not exist or are inactive.");
        }
    }

    // ============================================================
    // NORMALIZE CREATE DTO
    // ============================================================

    private static void NormalizeProductDto(
        ProductDto dto)
    {
        dto.NameEn =
            dto.NameEn?.Trim()
            ?? string.Empty;

        dto.NameAr =
            dto.NameAr?.Trim()
            ?? string.Empty;

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
            dto.NameEn?.Trim()
            ?? string.Empty;

        dto.NameAr =
            dto.NameAr?.Trim()
            ?? string.Empty;

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
    // VARIANT KEY
    // ============================================================

    private static (
        int? SizeId,
        int? HeelSizeId) BuildVariantKey(
            int? sizeId,
            int? heelSizeId)
    {
        return (
            sizeId,
            heelSizeId);
    }

    // ============================================================
    // FULL PRODUCT MAPPER
    // ============================================================

    private static ProductResponseDto MapAllProduct(
        Product product)
    {
        var discount =
            product.DiscountPercentage;

        var activeVariants =
            product.Variants?
                .Where(IsVisibleVariant)
                .ToList()
            ?? [];

        return new ProductResponseDto
        {
            Id =
                product.Id,

            NameEn =
                product.NameEn,

            NameAr =
                product.NameAr,

            DescriptionEn =
                product.DescriptionEn,

            DescriptionAr =
                product.DescriptionAr,

            Price =
                product.Price,

            ActualPrice =
                0,

            StockQuantity =
                ProductDefaultStock,

            IsInStock =
                product.IsInStock,

            DiscountPercentage =
                discount,

            DiscountedPrice =
                discount > 0
                    ? product.Price -
                      (product.Price *
                       discount / 100m)
                    : product.Price,

            CategoryId =
                product.CategoryId,

            Category =
                product.Category is null
                    ? null
                    : new CategoryResponseDto
                    {
                        Id =
                            product.Category.Id,

                        NameEn =
                            product.Category.NameEn,

                        NameAr =
                            product.Category.NameAr
                    },

            HasVariants =
                activeVariants.Count > 0,

            Images =
                product.Images?
                    .OrderBy(
                        i => i.SortOrder)

                    .Select(
                        i => i.ImageUrl)

                    .ToList()
                ?? [],

            Variants =
                activeVariants

                    .Select(
                        v =>
                            new ProductVariantResponseDto
                            {
                                Id =
                                    v.Id,

                                SizeId =
                                    v.SizeId,

                                SizeName =
                                    v.Size?.IsActive == true
                                        ? v.Size.Name
                                        : null,

                                HeelSizeId =
                                    v.HeelSizeId,

                                HeelSizeName =
                                    v.HeelSize?.IsActive == true
                                        ? v.HeelSize.Name
                                        : null,

                                StockQuantity =
                                    v.StockQuantity
                            })

                    .ToList()
        };
    }

    // ============================================================
    // VISIBLE VARIANT
    // ============================================================

    private static bool IsVisibleVariant(
        ProductVariant variant)
    {
        if (!variant.IsActive)
            return false;

        // If variant has a size,
        // the size itself must still be active.
        if (variant.SizeId.HasValue &&
            variant.Size?.IsActive != true)
        {
            return false;
        }

        // If variant has a heel size,
        // the heel size itself must still be active.
        if (variant.HeelSizeId.HasValue &&
            variant.HeelSize?.IsActive != true)
        {
            return false;
        }

        return true;
    }

    // ============================================================
    // UPLOADED IMAGE RESULT
    // ============================================================

    private sealed record UploadedImageResult(
        string Url,
        int SortOrder);
}