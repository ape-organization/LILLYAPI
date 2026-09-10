namespace PharmacyAPI.Models.RequestsModels
{
    public class BestSellerProductDto
    {
        public int Id { get; set; }

        public string NameEn { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal ActualPrice { get; set; }

        public decimal DiscountPercentage { get; set; }

        public bool IsInStock { get; set; }

        public List<ProductImageResponseDto> Images { get; set; }
            = new();
    }
}
