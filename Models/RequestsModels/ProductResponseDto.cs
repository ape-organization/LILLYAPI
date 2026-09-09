namespace PharmacyAPI.Models.RequestsModels
{
    public class ProductResponseDto
    {
        public int Id { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }

        public decimal Price { get; set; }

        public decimal ActualPrice { get; set; }

        // Used when the product has no variants
        public int StockQuantity { get; set; }

        public bool IsInStock { get; set; }

        public decimal DiscountPercentage { get; set; }

        // =========================
        // CATEGORY
        // =========================

        public int CategoryId { get; set; }

        public CategoryResponseDto? Category { get; set; }

        // =========================
        // IMAGES
        // =========================

        public List<ProductImageResponseDto> Images { get; set; }
            = new();

        // =========================
        // VARIANTS
        // =========================

        public List<ProductVariantResponseDto> Variants { get; set; }
            = new();
    }


    public class CategoryResponseDto
    {
        public int Id { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;
    }


    public class ProductImageResponseDto
    {
        public int Id { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }


    public class ProductVariantResponseDto
    {
        public int Id { get; set; }

        public int? SizeId { get; set; }

        public string? SizeName { get; set; }

        public int? HeelSizeId { get; set; }

        public string? HeelSizeName { get; set; }

        public int StockQuantity { get; set; }
    }
}