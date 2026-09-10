using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services.Interfaces
{
    public interface ISizeService
    {
        Task<List<SizeDto>> GetSizes(
            CancellationToken cancellationToken = default);

        Task<SizeDto?> GetSize(
            int id,
            CancellationToken cancellationToken = default);

        Task<SizeDto?> CreateSize(
            SizeDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateSize(
            int id,
            SizeDto dto,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteSize(
            int id,
            CancellationToken cancellationToken = default);
    }
    //////

        public class SizeService : ISizeService
        {
            private readonly ShoesDbContext _context;

            public SizeService(ShoesDbContext context)
            {
                _context = context;
            }


            // ============================================================
            // GET ALL
            // ============================================================

            public async Task<List<SizeDto>> GetSizes(
                CancellationToken cancellationToken = default)
            {
                return await _context.Sizes
                    .AsNoTracking()
                 
                    .Select(s => new SizeDto
                    {
                        Id = s.Id,
                        Name = s.Name
                    })
                    .ToListAsync(cancellationToken);
            }


            // ============================================================
            // GET BY ID
            // ============================================================

            public async Task<SizeDto?> GetSize(
                int id,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return null;

                return await _context.Sizes
                    .AsNoTracking()
                    .Where(s => s.Id == id )
                    .Select(s => new SizeDto
                    {
                        Id = s.Id,
                        Name = s.Name
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }


            // ============================================================
            // CREATE
            // ============================================================

            public async Task<SizeDto?> CreateSize(
                SizeDto dto,
                CancellationToken cancellationToken = default)
            {
                var name = NormalizeName(dto.Name);

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException(
                        "Size name is required.");


                // --------------------------------------------------------
                // Check duplicate
                // --------------------------------------------------------

                var exists = await _context.Sizes
                    .AnyAsync(
                        s => s.Name == name,
                        cancellationToken);

                if (exists)
                    throw new InvalidOperationException(
                        "A size with this name already exists.");


                var size = new Size
                {
                    Name = name
                };

                _context.Sizes.Add(size);

                await _context.SaveChangesAsync(cancellationToken);

                return new SizeDto
                {
                    Id = size.Id,
                    Name = size.Name
                };
            }


            // ============================================================
            // UPDATE
            // ============================================================

            public async Task<bool> UpdateSize(
                int id,
                SizeDto dto,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return false;

                var name = NormalizeName(dto.Name);

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException(
                        "Size name is required.");


                var size = await _context.Sizes
                    .FirstOrDefaultAsync(
                        s => s.Id == id,
                        cancellationToken);

                if (size == null)
                    return false;


                // --------------------------------------------------------
                // Check duplicate name
                //
                // Exclude the current size itself.
                // --------------------------------------------------------

                var duplicateExists = await _context.Sizes
                    .AnyAsync(
                        s => s.Id != id &&
                             s.Name == name,
                        cancellationToken);

                if (duplicateExists)
                    throw new InvalidOperationException(
                        "A size with this name already exists.");


                size.Name = name;

                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }


            // ============================================================
            // DELETE
            // ============================================================

            public async Task<bool> DeleteSize(
                int id,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return false;


                // --------------------------------------------------------
                // Check whether this size is being used.
                //
                // We must NOT delete a size that is referenced by a
                // ProductVariant.
                // --------------------------------------------------------

                var isUsed = await _context.ProductVariants
                    .AsNoTracking()
                    .AnyAsync(
                        v => v.SizeId == id,
                        cancellationToken);

                if (isUsed)
                    throw new InvalidOperationException(
                        "This size cannot be deleted because it is used by a product variant.");


                var size = await _context.Sizes
                    .FirstOrDefaultAsync(
                        s => s.Id == id,
                        cancellationToken);

                if (size == null)
                    return false;

            _context.Sizes.Remove(size);

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
    



    //////
}


