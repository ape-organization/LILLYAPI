using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Models;
using PharmacyAPI.Models.Authentication;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RolesController : ControllerBase
    {
        // Seeded roles the application depends on.
        private static readonly HashSet<string> ProtectedRoles =
            new(StringComparer.OrdinalIgnoreCase) { "Admin", "User" };

        private readonly RoleManager<ApplicationRole> roleManager;
        public RolesController(RoleManager<ApplicationRole> _roleManager)
        {
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

        // Role keys are strings (e.g. "role-admin"), not ints.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            if (role.Name is not null && ProtectedRoles.Contains(role.Name))
                return BadRequest(new { message = "This role cannot be deleted." });

            var result = await roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            return NoContent();
        }
    }
}
