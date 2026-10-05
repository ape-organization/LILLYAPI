
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services;

public interface ISliderService
{
    Task<List<Slider>> GetSliders(
        CancellationToken cancellationToken = default);

    Task<Slider?> GetSlider(
        int id,
        CancellationToken cancellationToken = default);

    Task<Slider> CreateSlider(
        sliderDto dto,
        CancellationToken cancellationToken = default);

    Task UpdateSlider(
        int id,
        sliderDto dto,
        CancellationToken cancellationToken = default);
}

public sealed class SliderService : ISliderService
{
    private const string ImageFolder = "sliders";

    private readonly ShoesDbContext _context;
    private readonly ImageService _imageService;

    public SliderService(
        ShoesDbContext context,
        ImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    // =====================================================
    // GET ALL SLIDERS
    // =====================================================

    public async Task<List<Slider>> GetSliders(
        CancellationToken cancellationToken = default)
    {
        return await _context.Sliders
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // =====================================================
    // GET SLIDER
    // =====================================================

    public async Task<Slider?> GetSlider(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        return await _context.Sliders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == id,
                cancellationToken);
    }

    // =====================================================
    // CREATE SLIDER
    // =====================================================

    public async Task<Slider> CreateSlider(
        sliderDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var slider = new Slider();

        // -------------------------------------------------
        // IMAGE
        // -------------------------------------------------

        if (dto.Image is not null)
        {
            slider.ImageUrl = await _imageService.SaveImageAsync(
                dto.Image,
                ImageFolder,
                cancellationToken);
        }

        _context.Sliders.Add(slider);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Database failed after the image was created.
            // Delete the newly created image.
            if (!string.IsNullOrWhiteSpace(slider.ImageUrl))
            {
                await _imageService.DeleteImageAsync(
                    slider.ImageUrl,
                    ImageFolder,
                    CancellationToken.None);
            }

            throw;
        }

        return slider;
    }

    // =====================================================
    // UPDATE SLIDER
    // =====================================================

    public async Task UpdateSlider(
        int id,
        sliderDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                "Slider ID must be greater than zero.");
        }

        var slider = await _context.Sliders
            .FirstOrDefaultAsync(
                s => s.Id == id,
                cancellationToken);

        if (slider is null)
        {
            throw new KeyNotFoundException(
                "السلايدر غير موجود");
        }

        // Keep the old image unless a new image was supplied.
        var oldImageUrl = slider.ImageUrl;
        string? newImageUrl = null;

        // -------------------------------------------------
        // NEW IMAGE
        // -------------------------------------------------

        if (dto.Image is not null)
        {
            newImageUrl = await _imageService.SaveImageAsync(
                dto.Image,
                ImageFolder,
                cancellationToken);

            slider.ImageUrl = newImageUrl;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Database failed after the new image was created.
            // Delete the NEW image only.
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                await _imageService.DeleteImageAsync(
                    newImageUrl,
                    ImageFolder,
                    CancellationToken.None);
            }

            throw;
        }

        // -------------------------------------------------
        // DELETE OLD IMAGE AFTER DB SUCCESS
        // -------------------------------------------------

        if (!string.IsNullOrWhiteSpace(newImageUrl) &&
            !string.IsNullOrWhiteSpace(oldImageUrl))
        {
            await _imageService.DeleteImageAsync(
                oldImageUrl,
                ImageFolder,
                CancellationToken.None);
        }
    }
}
