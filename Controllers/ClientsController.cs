using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Services;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class ClientsController : ControllerBase
    {
        private readonly IClientService _clientService;

        public ClientsController(
            IClientService clientService)
        {
            _clientService = clientService;
        }

        // =====================================================
        // GET CLIENT BY PHONE
        // GET: api/clients/by-phone/01012345678
        // =====================================================

        // Not anonymous: it returns the customer's name,
        // address and email for any phone number.
        [HttpGet("by-phone/{phone}")]
       
        public async Task<IActionResult> GetByPhone(
            string phone,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                {
                    return BadRequest(new
                    {
                        message = "رقم الهاتف مطلوب"
                    });
                }

                var client = await _clientService.GetByPhone(
                    phone,
                    cancellationToken);

                if (client == null)
                {
                    return NotFound(new
                    {
                        message = "العميل غير متوفر"
                    });
                }

                return Ok(client);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }
}