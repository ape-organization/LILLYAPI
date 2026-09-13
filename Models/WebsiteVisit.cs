namespace PharmacyAPI.Models
{
    public class WebsiteVisit
    {
        public long Id { get; set; }

        public string VisitorId { get; set; } = null!;

        public DateTime VisitedAt { get; set; }
    }
}
