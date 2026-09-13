using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services
{
    public class WebsiteVisitService
    {
        private readonly ShoesDbContext _context;

        public WebsiteVisitService(ShoesDbContext context)
        {
            _context = context;
        }

        public async Task RecordVisit(
            string visitorId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(visitorId))
                return;

            visitorId = visitorId.Trim();

            // Don't record the same visitor more than once per day.
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var alreadyVisited = await _context.websiteVisits
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.VisitorId == visitorId &&
                        x.VisitedAt >= today &&
                        x.VisitedAt < tomorrow,
                    cancellationToken);

            if (alreadyVisited)
                return;

            _context.websiteVisits.Add(new WebsiteVisit
            {
                VisitorId = visitorId,
                VisitedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<MonthlyVisitorsDto> GetCurrentMonthVisitors(
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var startOfMonth = new DateTime(
                now.Year,
                now.Month,
                1
            );

            var startOfNextMonth = startOfMonth.AddMonths(1);

            var visitors = await _context.websiteVisits
                .AsNoTracking()
                .Where(x =>
                    x.VisitedAt >= startOfMonth &&
                    x.VisitedAt < startOfNextMonth)
                .Select(x => x.VisitorId)
                .Distinct()
                .CountAsync(cancellationToken);

            return new MonthlyVisitorsDto
            {
                Year = startOfMonth.Year,
                Month = startOfMonth.Month,
                Visitors = visitors
            };
        }


    }
}
