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
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;


        public CategoriesController(
            ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }


        // =====================================================
        // GET MENU
        // =====================================================

        [HttpGet("menu")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CategoryMenu>>>
            GetCategoriesForMenu(
                CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _categoryService
                    .GetCategoriesForMenu(cancellationToken);

                return Ok(categories);
            }
            catch(Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        // =====================================================
        // GET ALL
        // =====================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<Category>>>
            GetCategories(
                CancellationToken cancellationToken)
        {
            try { 
            var categories = await _categoryService
                .GetCategories(cancellationToken);

            return Ok(categories);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        // =====================================================
        // GET BY ID
        // =====================================================

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Category>>
            GetCategory(
                int id,
                CancellationToken cancellationToken)
        {
            try { 
            var category = await _categoryService
                .GetCategory(
                    id,
                    cancellationToken);


            if (category is null)
                return NotFound();


            return Ok(category);
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
        public async Task<ActionResult<Category>>
            CreateCategory(
                [FromForm] CreateCategoryRequest dto,
                CancellationToken cancellationToken)
        {
            if (dto is null)
            {
                return BadRequest(
                    "بيانات الفئات مطلوبه");
            }


            if (string.IsNullOrWhiteSpace(dto.NameEn) &&
                string.IsNullOrWhiteSpace(dto.NameAr))
            {
                return BadRequest(
                    "اسم الفئه مطلوب");
            }


            try
            {
                var category = await _categoryService
                    .CreateCategory(
                        dto,
                        cancellationToken);


                return CreatedAtAction(
                    nameof(GetCategory),
                    new
                    {
                        id = category.Id
                    },
                    category);
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
            UpdateCategory(
                int id,
                [FromForm] CreateCategoryRequest dto,
                CancellationToken cancellationToken)
        {
            if (dto is null)
            {
                return BadRequest(
                    "بيانات الفئه مطلوبه");
            }


            if (string.IsNullOrWhiteSpace(dto.NameEn) &&
                string.IsNullOrWhiteSpace(dto.NameAr))
            {
                return BadRequest(
                    "اسم الفئه مطلوب");
            }


            try
            {
                await _categoryService
                    .UpdateCategory(
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


        // =====================================================
        // DELETE
        // =====================================================

        [HttpDelete("{id:int}")]
        public async Task<IActionResult>
            DeleteCategory(
                int id,
                CancellationToken cancellationToken)
        {
            try
            {
                await _categoryService
                    .DeleteCategory(
                        id,
                        cancellationToken);

                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}