using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Services;

[ApiController]
[Route("api/[controller]")]
public class WebsiteVisitsController : ControllerBase
{
    private readonly WebsiteVisitService _service;

    public WebsiteVisitsController(WebsiteVisitService service)
    {
        _service = service;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RecordVisit(
        [FromBody] WebsiteVisitDto request,
        CancellationToken cancellationToken)
    {
        await _service.RecordVisit(
            request.VisitorId,
            cancellationToken);

        return Ok();
    }

    [HttpGet("monthly")]
    [Authorize]
    public async Task<ActionResult<List<MonthlyVisitorsDto>>> GetMonthlyVisitors(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetCurrentMonthVisitors( cancellationToken);

        return Ok(result);
    }
}