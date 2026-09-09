namespace PharmacyAPI.Models.RequestsModels
{
    public class OrderItemDto
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        // First product image only
        public string? ImageUrl { get; set; }


        // =====================================================
        // SELECTED VARIANT
        // =====================================================

        public int? ProductVariantId { get; set; }

        public string? SizeName { get; set; }

        public string? HeelSizeName { get; set; }


        // =====================================================
        // ORDER QUANTITY & PRICE
        // =====================================================

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal ActualPrice { get; set; }

        public decimal TotalPrice { get; set; }
    }
}