namespace PharmacyAPI.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;


        // =====================================================
        // PRODUCT
        // =====================================================

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;


        // =====================================================
        // PRODUCT VARIANT
        // =====================================================
        //
        // Nullable because a product may have no variants.
        //
        // Example:
        //
        // Product without sizes:
        //     ProductVariantId = null
        //
        // Product with size:
        //     ProductVariantId = 15
        //
        // Product with size + heel:
        //     ProductVariantId = 21
        //
        // =====================================================

        public int? ProductVariantId { get; set; }

        public ProductVariant? ProductVariant { get; set; }


        // =====================================================
        // QUANTITY
        // =====================================================

        public int Quantity { get; set; }


        // =====================================================
        // PRICE AT TIME OF ORDER
        // =====================================================

        public decimal UnitPrice { get; set; }

        public decimal ActualPrice { get; set; }
    }
}