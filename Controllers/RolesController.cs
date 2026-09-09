using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.Authentication;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly ShoesDbContext _context;
        private readonly RoleManager<ApplicationRole> roleManager;
        public RolesController(ShoesDbContext context,
            RoleManager<ApplicationRole> _roleManager)
        {
            _context = context;
            roleManager = _roleManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Role>>> GetRoles()
        {
            var roles = await roleManager.Roles
        .Select(r => r.Name)
        .ToListAsync();

            return Ok(roles);
        }

       

      

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null) return NotFound();
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
