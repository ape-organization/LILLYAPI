using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Services.Interfaces;

namespace PharmacyAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HeelSizesController : ControllerBase
    {
        private readonly IHeelSizeService _heelSizeService;

        public HeelSizesController(IHeelSizeService heelSizeService)
        {
            _heelSizeService = heelSizeService;
        }


        // ============================================================
        // GET: api/heelsizes
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<HeelSizeDto>>> GetHeelSizes(
            CancellationToken cancellationToken)
        {
            var heelSizes = await _heelSizeService
                .GetHeelSizes(cancellationToken);

            return Ok(heelSizes);
        }


        // ============================================================
        // GET: api/heelsizes/5
        // ============================================================

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<HeelSizeDto>> GetHeelSize(
            int id,
            CancellationToken cancellationToken)
        {
            var heelSize = await _heelSizeService
                .GetHeelSize(id, cancellationToken);

            if (heelSize == null)
                return NotFound();

            return Ok(heelSize);
        }


        // ============================================================
        // POST: api/heelsizes
        // ============================================================

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<HeelSizeDto>> CreateHeelSize(
            [FromBody] HeelSizeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var heelSize = await _heelSizeService
                    .CreateHeelSize(dto, cancellationToken);

                return CreatedAtAction(
                    nameof(GetHeelSize),
                    new { id = heelSize!.Id },
                    heelSize);
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
        // PUT: api/heelsizes/5
        // ============================================================

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateHeelSize(
            int id,
            [FromBody] HeelSizeDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _heelSizeService
                    .UpdateHeelSize(
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
        // DELETE: api/heelsizes/5
        // ============================================================

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteHeelSize(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _heelSizeService
                    .DeleteHeelSize(
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