using PharmacyAPI.Models.RequestsModels;

namespace LilyAPI.Services
{
    public class PriceChangedException : Exception
    {
        public List<PriceChangeItemDto> PriceChanges { get; }

        public PriceChangedException(
            string message,
            List<PriceChangeItemDto> priceChanges)
            : base(message)
        {
            PriceChanges = priceChanges;
        }
    }
}
