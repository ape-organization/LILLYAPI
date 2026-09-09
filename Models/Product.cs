namespace PharmacyAPI.Models
{
    public class Product
    {
        public int Id { get; set; }

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

        public bool HasDiscount =>
            DiscountPercentage > 0;

        public decimal DiscountedPrice =>
            DiscountPercentage > 0
                ? Price - (Price * DiscountPercentage / 100)
                : Price;

        // =========================
        // CATEGORY
        // =========================

        public int CategoryId { get; set; }

        public Category Category { get; set; } = null!;

        // =========================
        // IMAGES
        // =========================

        public ICollection<ProductImage> Images { get; set; }
            = new List<ProductImage>();

        // =========================
        // VARIANTS
        // =========================

        public ICollection<ProductVariant> Variants { get; set; }
            = new List<ProductVariant>();

        // =========================
        // ORDERS
        // =========================

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();

        // =========================
        // SOFT DELETE
        // =========================

        public bool IsDeleted { get; set; } = false;
    }
}