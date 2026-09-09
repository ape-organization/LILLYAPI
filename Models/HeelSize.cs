namespace PharmacyAPI.Models
{
    public class HeelSize
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<ProductVariant> ProductVariants { get; set; }
            = new List<ProductVariant>();
    }
}