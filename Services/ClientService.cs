using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;

namespace PharmacyAPI.Services
{
    public interface IClientService
    {
        Task<Client?> GetByPhone(
            string phone,
            CancellationToken cancellationToken = default);
    }

    public class ClientService : IClientService
    {
        private readonly ShoesDbContext _context;

        public ClientService(ShoesDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET CLIENT BY PHONE
        // =====================================================

        public async Task<Client?> GetByPhone(
            string phone,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            phone = phone.Trim();

            return await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PhoneNumber == phone,
                    cancellationToken);
        }
    }
}