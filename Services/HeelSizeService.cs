using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services.Interfaces
{
    public interface IHeelSizeService
    {
        Task<List<HeelSizeDto>> GetHeelSizes(
            CancellationToken cancellationToken = default);

        Task<HeelSizeDto?> GetHeelSize(
            int id,
            CancellationToken cancellationToken = default);

        Task<HeelSizeDto?> CreateHeelSize(
            HeelSizeDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateHeelSize(
            int id,
            HeelSizeDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteHeelSize(
            int id,
            CancellationToken cancellationToken = default);
    }
    /////////////////
  
        public class HeelSizeService : IHeelSizeService
        {
            private readonly ShoesDbContext _context;

            public HeelSizeService(ShoesDbContext context)
            {
                _context = context;
            }


            // ============================================================
            // GET ALL
            // ============================================================

            public async Task<List<HeelSizeDto>> GetHeelSizes(
                CancellationToken cancellationToken = default)
            {
                return await _context.HeelSizes
                    .AsNoTracking()
                
                    .Select(h => new HeelSizeDto
                    {
                        Id = h.Id,
                        Name = h.Name
                    })
                    .ToListAsync(cancellationToken);
            }


            // ============================================================
            // GET BY ID
            // ============================================================

            public async Task<HeelSizeDto?> GetHeelSize(
                int id,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return null;

                return await _context.HeelSizes
                    .AsNoTracking()
                    .Where(h => h.Id == id )
                    .Select(h => new HeelSizeDto
                    {
                        Id = h.Id,
                        Name = h.Name
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }


            // ============================================================
            // CREATE
            // ============================================================

            public async Task<HeelSizeDto?> CreateHeelSize(
                HeelSizeDto dto,
                CancellationToken cancellationToken = default)
            {
                var name = NormalizeName(dto.Name);

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException(
                        "Heel size name is required.");


                // --------------------------------------------------------
                // Check duplicate
                // --------------------------------------------------------

                var exists = await _context.HeelSizes
                    .AnyAsync(
                        h => h.Name == name,
                        cancellationToken);

                if (exists)
                    throw new InvalidOperationException(
                        "A heel size with this name already exists.");


                var heelSize = new HeelSize
                {
                    Name = name
                };

                _context.HeelSizes.Add(heelSize);

                await _context.SaveChangesAsync(cancellationToken);

                return new HeelSizeDto
                {
                    Id = heelSize.Id,
                    Name = heelSize.Name
                };
            }


            // ============================================================
            // UPDATE
            // ============================================================

            public async Task<bool> UpdateHeelSize(
                int id,
                HeelSizeDto dto,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return false;

                var name = NormalizeName(dto.Name);

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException(
                        "Heel size name is required.");


                var heelSize = await _context.HeelSizes
                    .FirstOrDefaultAsync(
                        h => h.Id == id,
                        cancellationToken);

                if (heelSize == null)
                    return false;


                // --------------------------------------------------------
                // Check duplicate name
                // --------------------------------------------------------

                var duplicateExists = await _context.HeelSizes
                    .AnyAsync(
                        h => h.Id != id &&
                             h.Name == name,
                        cancellationToken);

                if (duplicateExists)
                    throw new InvalidOperationException(
                        "A heel size with this name already exists.");


                heelSize.Name = name;

                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }


            // ============================================================
            // DELETE
            // ============================================================

            public async Task<bool> DeleteHeelSize(
                int id,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return false;


                // --------------------------------------------------------
                // Do not delete a heel size already used by a variant.
                // This protects historical product/order data.
                // --------------------------------------------------------

                var isUsed = await _context.ProductVariants
                    .AsNoTracking()
                    .AnyAsync(
                        v => v.HeelSizeId == id,
                        cancellationToken);

                if (isUsed)
                    throw new InvalidOperationException(
                        "This heel size cannot be deleted because it is used by a product variant.");


                var heelSize = await _context.HeelSizes
                    .FirstOrDefaultAsync(
                        h => h.Id == id,
                        cancellationToken);

                if (heelSize == null)
                    return false;
              

            _context.HeelSizes.Remove(heelSize);

                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }


            // ============================================================
            // NORMALIZE NAME
            // ============================================================

            private static string NormalizeName(string? name)
            {
                return name?.Trim() ?? string.Empty;
            }
        }
    
    ////////////////
}
