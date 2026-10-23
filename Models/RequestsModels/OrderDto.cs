
namespace PharmacyAPI.Models.RequestsModels
{
    public class OrderDto
    {
        public int Id { get; set; }

        public int ClientId { get; set; }

        public string ClientName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Address { get; set; }

        public DateTime OrderDate { get; set; }

        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }

        public string Status { get; set; } = string.Empty;

        public List<OrderItemDto> Items { get; set; } = new();
    }
}
