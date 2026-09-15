using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using PharmacyAPI.Services;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(
            IOrderService orderService)
        {
            _orderService = orderService;
        }
        [HttpGet("dashboard/current-month")]
        public async Task<ActionResult<DashboardStatsDto>> GetCurrentMonthStats(
     CancellationToken cancellationToken)
        {
            var result = await _orderService.GetCurrentMonthStats(
                cancellationToken);

            return Ok(result);
        }


        [HttpGet("dashboard/total")]
        public async Task<ActionResult<DashboardStatsDto>> GetTotalStats(
            CancellationToken cancellationToken)
        {
            var result = await _orderService.GetTotalStats(
                cancellationToken);

            return Ok(result);
        }
        // =====================================================
        // CREATE ORDER
        // POST: api/orders
        // =====================================================

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var order = await _orderService.CreateOrder(
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetOrder),
                    new { id = order.Id },
                    order);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // GET ALL ORDERS
        // GET: api/orders
        // =====================================================

        [HttpGet]
        public async Task<ActionResult<PagedResponse<OrderDto>>> GetOrders(
  int page = 1,
  int pageSize = 30,
  CancellationToken cancellationToken = default)
        {
            var result = await _orderService.GetOrders(
                page,
                pageSize,
                cancellationToken);

            return Ok(result);
        }




        // =====================================================
        // GET ORDER
        // GET: api/orders/5
        // =====================================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(
            int id,
            CancellationToken cancellationToken)
        {
            var order = await _orderService.GetOrder(
                id,
                cancellationToken);

            if (order == null)
            {
                return NotFound(new
                {
                    message = "الطلب غير موجود"
                });
            }

            return Ok(order);
        }

        // =====================================================
        // GET CLIENT ORDERS
        // GET: api/orders/client/5
        // =====================================================

        [HttpGet("client/{clientId:int}")]
        public async Task<IActionResult> GetClientOrders(
            int clientId,
            CancellationToken cancellationToken)
        {
            var orders =
                await _orderService.GetOrdersByClient(
                    clientId,
                    cancellationToken);

            return Ok(orders);
        }

        // =====================================================
        // UPDATE STATUS
        // PUT: api/orders/5/status
        // =====================================================

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(
            int id,
            [FromBody] string status,
            CancellationToken cancellationToken)
        {
            try
            {
                var result =
                    await _orderService.UpdateOrderStatus(
                        id,
                        status,
                        cancellationToken);

                if (!result)
                {
                    return NotFound(new
                    {
                        message = "الطلب غير موجود"
                    });
                }

                return Ok(new
                {
                    message =
                        "حاله الطلب اتعدلت بنجاح"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // CANCEL ORDER
        // PUT: api/orders/5/cancel
        // =====================================================

        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var result =
                    await _orderService.CancelOrder(
                        id,
                        cancellationToken);

                if (!result)
                {
                    return NotFound(new
                    {
                        message = "الطلب غير موجود"
                    });
                }

                return Ok(new
                {
                    message =
                        "تم الغاء الطلب بنجاح"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}