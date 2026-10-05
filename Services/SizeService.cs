using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services.Interfaces;

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

public class SizeService : ISizeService
{
    private readonly ShoesDbContext _context;

    public SizeService(ShoesDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET ALL ACTIVE SIZES
    // ============================================================

    public async Task<List<SizeDto>> GetSizes(
        CancellationToken cancellationToken = default)
    {
        return await _context.Sizes
            .AsNoTracking()
            .Where(s => s.IsActive)
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
            .Where(s =>
                s.Id == id &&
                s.IsActive)
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
        try
        {
            ArgumentNullException.ThrowIfNull(dto);

            var name = NormalizeName(dto.Name);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Size name is required.");
            }

            // --------------------------------------------------------
            // CHECK DUPLICATE ACTIVE SIZE
            // --------------------------------------------------------

            var exists = await _context.Sizes
                .AnyAsync(
                    s => s.IsActive &&
                         s.Name == name,
                    cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    "A size with this name already exists.");
            }

            // --------------------------------------------------------
            // CREATE
            // --------------------------------------------------------

            var size = new Size
            {
                Name = name,
                IsActive = true
            };

            _context.Sizes.Add(size);

            await _context.SaveChangesAsync(cancellationToken);

            return new SizeDto
            {
                Id = size.Id,
                Name = size.Name
            };
        }
        catch(Exception e)
        {
                       // Log the exception (you can use a logging framework)
            Console.WriteLine($"Error creating size: {e.Message}");
            return null;
        }
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

        ArgumentNullException.ThrowIfNull(dto);

        var name = NormalizeName(dto.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Size name is required.");
        }

        // --------------------------------------------------------
        // FIND ACTIVE SIZE
        // --------------------------------------------------------

        var size = await _context.Sizes
            .FirstOrDefaultAsync(
                s => s.Id == id &&
                     s.IsActive,
                cancellationToken);

        if (size is null)
            return false;

        // --------------------------------------------------------
        // CHECK DUPLICATE ACTIVE NAME
        // --------------------------------------------------------

        var duplicateExists = await _context.Sizes
            .AnyAsync(
                s => s.Id != id &&
                     s.IsActive &&
                     s.Name == name,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "A size with this name already exists.");
        }

        // --------------------------------------------------------
        // UPDATE
        // --------------------------------------------------------

        size.Name = name;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ============================================================
    // SOFT DELETE
    // ============================================================

    public async Task<bool> DeleteSize(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        // --------------------------------------------------------
        // FIND ACTIVE SIZE
        // --------------------------------------------------------

        var size = await _context.Sizes
            .FirstOrDefaultAsync(
                s => s.Id == id &&
                     s.IsActive,
                cancellationToken);

        if (size is null)
            return false;

        // --------------------------------------------------------
        // SOFT DELETE
        // --------------------------------------------------------

        size.IsActive = false;

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