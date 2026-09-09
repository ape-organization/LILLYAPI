using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Services.Interfaces;

namespace PharmacyAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SizesController : ControllerBase
    {
        private readonly ISizeService _sizeService;

        public SizesController(ISizeService sizeService)
        {
            _sizeService = sizeService;
        }


        // ============================================================
        // GET: api/sizes
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<SizeDto>>> GetSizes(
            CancellationToken cancellationToken)
        {
            var sizes = await _sizeService.GetSizes(cancellationToken);

            return Ok(sizes);
        }


        // ============================================================
        // GET: api/sizes/5
        // ============================================================

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<SizeDto>> GetSize(
            int id,
            CancellationToken cancellationToken)
        {
            var size = await _sizeService.GetSize(
                id,
                cancellationToken);

            if (size == null)
                return NotFound();

            return Ok(size);
        }


        // ============================================================
        // POST: api/sizes
        // ============================================================

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SizeDto>> CreateSize(
            [FromBody] SizeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var size = await _sizeService.CreateSize(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetSize),
                    new { id = size!.Id },
                    size);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // PUT: api/sizes/5
        // ============================================================

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSize(
            int id,
            [FromBody] SizeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _sizeService.UpdateSize(
                    id,
                    dto,
                    cancellationToken);

                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // DELETE: api/sizes/5
        // ============================================================

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSize(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _sizeService.DeleteSize(
                    id,
                    cancellationToken);

                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    message = ex.Message
                });
            }
        }
    }
}