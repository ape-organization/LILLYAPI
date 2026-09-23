using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Services;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SliderController : ControllerBase
    {
        private readonly ISliderService _sliderService;


        public SliderController(
            ISliderService sliderService)
        {
            _sliderService = sliderService;
        }


       
        // =====================================================
        // GET ALL
        // =====================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<Slider>>>
            GetSliders(
                CancellationToken cancellationToken)
        {
            try { 
            var categories = await _sliderService
                .GetSliders(cancellationToken);

            return Ok(categories);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }



        // =====================================================
        // CREATE
        // =====================================================

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<Slider>>
            CreateCategory(
                [FromForm] sliderDto dto,
                CancellationToken cancellationToken)
        {
            if (dto is null)
            {
                return BadRequest(
                    "الصور مطلوبه");
            }



            try
            {
                var slider = await _sliderService
                    .CreateSlider(
                        dto,
                        cancellationToken);


                return slider;
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        // =====================================================
        // UPDATE
        // =====================================================

        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult>
            UpdateSlider(
                int id,
                [FromForm] sliderDto dto,
                CancellationToken cancellationToken)
        {
            if (dto is null)
            {
                return BadRequest(
                    "الصوره مطلوبه");
            }



            try
            {
                await _sliderService
                    .UpdateSlider(
                        id,
                        dto,
                        cancellationToken);

                return Ok(true);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }


       
    }
}