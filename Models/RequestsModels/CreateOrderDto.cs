namespace PharmacyAPI.Models.RequestsModels
{
    public class CreateOrderDto
    {
        public ClientOrderDto Client { get; set; } = null!;

        public List<CreateOrderItemDto> Items { get; set; } = new();
    }


    // =========================================================
    // CLIENT
    // =========================================================

    public class ClientOrderDto
    {
        public string Name { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string Address { get; set; } = string.Empty;
    }


    // =========================================================
    // ORDER ITEM
    // =========================================================

    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }

        // Nullable because some products have no variants.
        public int? ProductVariantId { get; set; }

        public int Quantity { get; set; }
    }
}