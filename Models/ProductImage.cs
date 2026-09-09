namespace PharmacyAPI.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public string ImageUrl { get; set; } = string.Empty;

        // Controls the display order
        public int SortOrder { get; set; }
    }
}