using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services.Interfaces;

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

public class HeelSizeService : IHeelSizeService
{
    private readonly ShoesDbContext _context;

    public HeelSizeService(ShoesDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET ALL ACTIVE HEEL SIZES
    // ============================================================

    public async Task<List<HeelSizeDto>> GetHeelSizes(
        CancellationToken cancellationToken = default)
    {
        return await _context.HeelSizes
            .AsNoTracking()
            .Where(h => h.IsActive)
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
            .Where(h =>
                h.Id == id &&
                h.IsActive)
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
        ArgumentNullException.ThrowIfNull(dto);

        var name = NormalizeName(dto.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Heel size name is required.");
        }

        // --------------------------------------------------------
        // CHECK DUPLICATE ACTIVE HEEL SIZE
        // --------------------------------------------------------

        var exists = await _context.HeelSizes
            .AnyAsync(
                h => h.IsActive &&
                     h.Name == name,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "A heel size with this name already exists.");
        }

        // --------------------------------------------------------
        // CREATE
        // --------------------------------------------------------

        var heelSize = new HeelSize
        {
            Name = name,
            IsActive = true
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

        ArgumentNullException.ThrowIfNull(dto);

        var name = NormalizeName(dto.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Heel size name is required.");
        }

        // --------------------------------------------------------
        // FIND ACTIVE HEEL SIZE
        // --------------------------------------------------------

        var heelSize = await _context.HeelSizes
            .FirstOrDefaultAsync(
                h => h.Id == id &&
                     h.IsActive,
                cancellationToken);

        if (heelSize is null)
            return false;

        // --------------------------------------------------------
        // CHECK DUPLICATE ACTIVE NAME
        // --------------------------------------------------------

        var duplicateExists = await _context.HeelSizes
            .AnyAsync(
                h => h.Id != id &&
                     h.IsActive &&
                     h.Name == name,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "A heel size with this name already exists.");
        }

        // --------------------------------------------------------
        // UPDATE
        // --------------------------------------------------------

        heelSize.Name = name;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ============================================================
    // SOFT DELETE
    // ============================================================

    public async Task<bool> DeleteHeelSize(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        // --------------------------------------------------------
        // FIND ACTIVE HEEL SIZE
        // --------------------------------------------------------

        var heelSize = await _context.HeelSizes
            .FirstOrDefaultAsync(
                h => h.Id == id &&
                     h.IsActive,
                cancellationToken);

        if (heelSize is null)
            return false;

        // --------------------------------------------------------
        // SOFT DELETE
        // --------------------------------------------------------

        heelSize.IsActive = false;

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