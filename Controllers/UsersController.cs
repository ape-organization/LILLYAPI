using Google.Authenticator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.Authentication;
using PharmacyAPI.Models.RequestsModels;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ShoesDbContext _context;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly RoleManager<ApplicationRole> roleManager;
        private readonly IConfiguration _configuration;
        private readonly JwtHandler _jwtHandler;
        public UsersController(ShoesDbContext context,
            UserManager<ApplicationUser> _userManager,
            RoleManager<ApplicationRole> _roleManager,
            JwtHandler jwtHandler
            , IConfiguration configuration
            )
        {
            _context = context;
            userManager = _userManager;
            roleManager=_roleManager;
            _jwtHandler = jwtHandler;
            _configuration = configuration;
        }

        //authentication section 
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(UserDto userForAuthentication)
        {
            var user = await userManager.FindByEmailAsync(userForAuthentication.Email);

            if (user == null ||
                !await userManager.CheckPasswordAsync(user, userForAuthentication.Password) ||
                !user.IsActive)
            {
                return Ok(new
                {
                    IsAuthSuccessful = false,
                    ErrorMessage = "Invalid Authentication"
                });
            }

            // 🔹 Get claims INCLUDING roles
            var claims = await _jwtHandler.GetClaims(user);

            // 🔹 Create signing credentials
            var signingCredentials = _jwtHandler.GetSigningCredentials();

            // 🔹 Generate token
            var tokenOptions = _jwtHandler.GenerateTokenOptions(signingCredentials, claims);
            var accessToken = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

            // 🔹 Generate refresh token
            var refreshToken = GenerateRefreshToken();
            int.TryParse(_configuration["JWTSettings:RefreshTokenValidityInDays"],
                out int refreshTokenValidityInDays);

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenValidityInDays);

            await userManager.UpdateAsync(user);

            return Ok(new
            {
                IsAuthSuccessful = true,
                Token = accessToken,
                RefreshToken = refreshToken
            });
        }
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(
       [FromBody] Token tokenRequest)
        {
            if (tokenRequest == null)
            {
                return BadRequest("Invalid request");
            }

            try
            {
                // =====================================================
                // GET PRINCIPAL FROM EXPIRED ACCESS TOKEN
                // =====================================================

                var principal =
                    _jwtHandler.GetPrincipalFromExpiredToken(
                        tokenRequest.AccessToken
                    );

                if (principal == null)
                {
                    return Unauthorized();
                }


                // =====================================================
                // GET USERNAME
                // =====================================================

                var username =
                    principal.Identity?.Name;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized();
                }


                // =====================================================
                // FIND USER
                // =====================================================

                var user =
                    await userManager.FindByNameAsync(
                        username
                    );

                if (user == null)
                {
                    return Unauthorized();
                }


                // =====================================================
                // CHECK REFRESH TOKEN
                // =====================================================

                if (
                    string.IsNullOrEmpty(user.RefreshToken) ||
                    user.RefreshToken != tokenRequest.RefreshToken ||
                    user.RefreshTokenExpiryTime <= DateTime.UtcNow
                )
                {
                    return Unauthorized();
                }


                // =====================================================
                // GENERATE NEW ACCESS TOKEN
                // =====================================================

                var claims =
                    await _jwtHandler.GetClaims(user);

                var signingCredentials =
                    _jwtHandler.GetSigningCredentials();

                var tokenOptions =
                    _jwtHandler.GenerateTokenOptions(
                        signingCredentials,
                        claims
                    );

                var newAccessToken =
                    new JwtSecurityTokenHandler()
                        .WriteToken(tokenOptions);


                // =====================================================
                // GENERATE NEW REFRESH TOKEN
                // =====================================================

                var newRefreshToken =
                    GenerateRefreshToken();


                int.TryParse(
                    _configuration[
                        "JWTSettings:RefreshTokenValidityInDays"
                    ],
                    out int refreshTokenValidityInDays
                );


                user.RefreshToken =
                    newRefreshToken;

                user.RefreshTokenExpiryTime =
                    DateTime.UtcNow.AddDays(
                        refreshTokenValidityInDays
                    );


                await userManager.UpdateAsync(user);


                // =====================================================
                // RESPONSE
                // =====================================================

                return Ok(new
                {
                    Token = newAccessToken,

                    RefreshToken = newRefreshToken
                });
            }
            catch
            {
                return Unauthorized();
            }
        }


        [HttpGet]
        [AllowAnonymous]

        //[Authorize]
        //need edit to get user role too
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetUsers()
        {
            return await _context.Users.Where(user => user.IsActive == true).ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApplicationUser>> GetUser(int id)
        {
            //var user = await _context.Users

            //    .FirstOrDefaultAsync(u => u.app == id);

            //if (user == null) return NotFound();

            //return new UserDto
            //{
            //    Id = user.Id,
            //    Username = user.Username,
            //    Email = user.Email,
            //    RoleName = user.Role.Name
            //};
            return null;
        }

        [HttpPost("addUser")]

        // [Authorize]
        public async Task<ActionResult<ApplicationUser>> CreateUser([FromBody]UserDto UserDto)
        {

            try
            {


                var userExists = await userManager.FindByEmailAsync(UserDto.Email);

                if (userExists != null)
                {
                   
                    if(userExists.IsActive==false)
                    {
                        userExists.Name = UserDto.Name;
                        userExists.IsActive = true;
                        await userManager.UpdateAsync(userExists);
                        return Ok(new { Status = true, Message = "User reactivated successfully!" });
                    }
                    return Ok(new  { Status = false, Message = "User already exists!" });
                }
                ApplicationUser user = new ApplicationUser()
                {
                    Email = UserDto.Email,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    UserName = UserDto.Email,
                   
                    Name = UserDto.Name,
                    
                    IsActive = true
                };
                var result = await userManager.CreateAsync(user, UserDto.Password);
                if (!result.Succeeded)
                    return Ok(new 
                    {
                        Status = false,
                        Message = "User creation failed! Please check user details and try again."
                    });



                if (await roleManager.RoleExistsAsync(UserDto.Role))
                {
                    await userManager.AddToRoleAsync(user, UserDto.Role);
                }
                return Ok(new 
                {
                    Status = true,
                    Message = user.Id,
                });
            }
            catch (Exception e)
            {
                return BadRequest(new
                {
                    Status = false,
                });
            }




          

        }
      
        [HttpPut("editUser")]
        [Authorize]
        public async Task<IActionResult> UpdateUser(UpdateUserDto updateUserDto)
        {
            var user = await userManager.FindByEmailAsync( updateUserDto.Email);
            if (user == null) return NotFound();

            user.Name = updateUserDto.name;
            user.Email = updateUserDto.Email;


            await userManager.UpdateAsync(user);
            return Ok(new
            {
                Status = true,
                Message = user.Id,
            });
        }

        [HttpPost()]
        [Authorize]
        public async Task<IActionResult> DeleteUser(UpdateUserDto data)
        {
            var user = await userManager.FindByIdAsync(data.Id.ToString());
            if (user == null) return NotFound();
            user.IsActive=false;
            await userManager.UpdateAsync(user);
            return NoContent();
        }

        public static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }
}
