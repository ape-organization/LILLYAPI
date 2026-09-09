namespace PharmacyAPI.Models.RequestsModels
{
    public class UpdateProductDto
    {
        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }

        public decimal Price { get; set; }

        public decimal ActualPrice { get; set; }

        // Used when the product has no variants
        public int StockQuantity { get; set; }

        public bool IsInStock { get; set; } = true;

        public decimal DiscountPercentage { get; set; } = 0;

        // =========================
        // CATEGORY
        // =========================

        public int CategoryId { get; set; }

        // =========================
        // IMAGES
        // =========================

        public List<ProductImageDto> Images { get; set; }
            = new();

        // =========================
        // VARIANTS
        // =========================

        public List<ProductVariantDto> Variants { get; set; }
            = new();
    }
}