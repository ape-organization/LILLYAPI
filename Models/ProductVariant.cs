namespace PharmacyAPI.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        // =========================
        // PRODUCT
        // =========================

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        // =========================
        // SIZE
        // =========================

        public int? SizeId { get; set; }

        public Size? Size { get; set; }

        // =========================
        // HEEL SIZE
        // =========================

        public int? HeelSizeId { get; set; }

        public HeelSize? HeelSize { get; set; }

        // =========================
        // STOCK
        // =========================

        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}