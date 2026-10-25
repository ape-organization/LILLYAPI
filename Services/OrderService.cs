
using LilyAPI.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using System.Linq.Expressions;
using System.Net;
using System.Text;

namespace PharmacyAPI.Services
{
    public interface IOrderService
    {
         Task<bool> CompleteOrder(
          int id,
          CancellationToken cancellationToken = default);
        Task<OrderDto> CreateOrder(
            CreateOrderDto dto,
            CancellationToken cancellationToken = default);

        Task<OrderDto?> GetOrder(
            int id,
            CancellationToken cancellationToken = default);

        Task<PagedResponse<OrderDto>> GetOrders(
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default);

        Task<List<OrderDto>> GetOrdersByClient(
            int clientId,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateOrderStatus(
            int id,
            string status,
            CancellationToken cancellationToken = default);

        Task<bool> CancelOrder(
            int id,
            CancellationToken cancellationToken = default);

        Task<DashboardStatsDto> GetCurrentMonthStats(
            CancellationToken cancellationToken = default);

        Task<DashboardStatsDto> GetTotalStats(
            CancellationToken cancellationToken = default);
    }


    public sealed class OrderService : IOrderService
    {
        private const string DefaultAdminEmail = "deaved.makram@gmail.com";
       // private const string DefaultAdminEmail = "ereny.karam.noshy@gmail.com";
        // SQL Server unique index violations.
        private const int SqlUniqueIndexViolation = 2601;
        private const int SqlUniqueConstraintViolation = 2627;

        private readonly ShoesDbContext _context;
        private readonly IEmailQueue _emailQueue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            ShoesDbContext context,
            IEmailQueue emailQueue,
            IConfiguration configuration,
            ILogger<OrderService> logger)
        {
            _context = context;
            _emailQueue = emailQueue;
            _configuration = configuration;
            _logger = logger;
        }


        // =========================================================
        // CREATE ORDER
        // =========================================================

        public async Task<OrderDto> CreateOrder(
      CreateOrderDto dto,
      CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);


            // =====================================================
            // VALIDATE CLIENT
            // =====================================================

            if (dto.Client == null)
            {
                throw new InvalidOperationException(
                    "معلومات العميل غير متوفره.");
            }


            var clientName =
                dto.Client.Name?.Trim();


            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException(
                    "اسم العميل مطلوب.");
            }


            var phoneNumber =
                dto.Client.PhoneNumber?.Trim();


            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                throw new InvalidOperationException(
                    "رقم العميل مطلوب.");
            }


            var address =
                dto.Client.Address?.Trim();


            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    "عنوان العميل مطلوب.");
            }


            // =====================================================
            // VALIDATE ITEMS
            // =====================================================

            if (dto.Items == null || dto.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "الطلب يجب ان يحتوي علي الاقل علي منتج واحد.");
            }


            foreach (var item in dto.Items)
            {
                if (item.ProductId <= 0)
                {
                    throw new InvalidOperationException(
                        "منتج غير متوفر.");
                }


                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException(
                        "الكميه يجب ان تكون اكبر من الصفر.");
                }


                if (item.ProductVariantId.HasValue &&
                    item.ProductVariantId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "الاختيار الخاص بالمنتج غير صحيح.");
                }


                // =================================================
                // VALIDATE DISPLAYED PRICE
                // =================================================

                if (item.DisplayedUnitPrice.HasValue &&
                    item.DisplayedUnitPrice.Value < 0)
                {
                    throw new InvalidOperationException(
                        "سعر المنتج غير صحيح.");
                }
            }


            // =====================================================
            // MERGE DUPLICATE ITEMS
            // =====================================================

            var requestedItems =
                dto.Items
                    .GroupBy(x => new
                    {
                        x.ProductId,
                        x.ProductVariantId
                    })
                    .Select(g => new RequestedOrderItem
                    {
                        ProductId =
                            g.Key.ProductId,

                        ProductVariantId =
                            g.Key.ProductVariantId,

                        Quantity =
                            g.Sum(x => x.Quantity),
                        

                        DisplayedUnitPrice =
                            g.Select(x => x.DisplayedUnitPrice)
                                .FirstOrDefault()
                    })
                    .ToList();


            // =====================================================
            // VALIDATE MERGED QUANTITIES
            // =====================================================

            if (requestedItems.Any(x => x.Quantity <= 0))
            {
                throw new InvalidOperationException(
                    "الكميه غير صحيحه.");
            }


            // =====================================================
            // PRODUCT IDS
            // =====================================================

            var productIds =
                requestedItems
                    .Select(x => x.ProductId)
                    .Distinct()
                    .ToList();


            // =====================================================
            // LOAD PRODUCTS
            // =====================================================
            //
            // Only active variants are loaded.
            //
            // No stock is checked, decreased,
            // or changed here.
            //
            // =====================================================

            var products =
                await _context.Products
                    .AsTracking()
                    .Where(p =>
                        productIds.Contains(p.Id) &&
                        !p.IsDeleted)
                    .Include(p =>
                        p.Variants
                            .Where(v => v.IsActive))
                    .ToDictionaryAsync(
                        p => p.Id,
                        cancellationToken);


            // =====================================================
            // CHECK PRODUCTS EXIST
            // =====================================================

            if (products.Count != productIds.Count)
            {
                var missingProductId =
                    productIds.First(
                        id => !products.ContainsKey(id));


                throw new KeyNotFoundException(
                    $"المنتج {missingProductId} غير موجود.");
            }


            // =====================================================
            // CHECK PRICE CHANGES
            // =====================================================
            //
            // The customer sent the price that was displayed
            // on the checkout page.
            //
            // We compare it with the CURRENT database price.
            //
            // The displayed price is NEVER used as the actual
            // order price.
            //
            // =====================================================

            var priceChanges =
                new List<PriceChangeItemDto>();


            foreach (var requestedItem in requestedItems)
            {
                var product =
                    products[requestedItem.ProductId];


                var currentPrice =
                    CalculateSellingPrice(product);


                var displayedPrice =
                    requestedItem.DisplayedUnitPrice;


                // -------------------------------------------------
                // If the frontend did not send a displayed price,
                // do not block the order.
                //
                // The backend price is still authoritative.
                // -------------------------------------------------

                if (!displayedPrice.HasValue)
                {
                    continue;
                }


                // -------------------------------------------------
                // Compare prices
                // -------------------------------------------------

                // The displayed price is compared at 2 decimals,
                // the same precision used by CalculateSellingPrice.
                if (Math.Round(
                        displayedPrice.Value,
                        2,
                        MidpointRounding.AwayFromZero) != currentPrice)
                {
                    priceChanges.Add(
                        new PriceChangeItemDto
                        {
                            ProductId =
                                product.Id,

                            OldPrice =
                                displayedPrice.Value,

                            NewPrice =
                                currentPrice
                        });
                }
            }


            // =====================================================
            // STOP ORDER IF PRICE CHANGED
            // =====================================================

            if (priceChanges.Count > 0)
            {
                throw new PriceChangedException(
                    "تغير سعر بعض المنتجات. يرجى مراجعة الطلب قبل إتمام الشراء.",
                    priceChanges);
            }


            // =====================================================
            // FIND CLIENT
            // =====================================================

            var client =
                await _context.Clients
                    .FirstOrDefaultAsync(
                        c => c.PhoneNumber == phoneNumber,
                        cancellationToken);


            var now =
                DateTime.UtcNow;


            // =====================================================
            // CLIENT EMAIL
            // =====================================================

            var email =
                string.IsNullOrWhiteSpace(dto.Client.Email)
                    ? null
                    : dto.Client.Email.Trim();


            // =====================================================
            // CREATE / UPDATE CLIENT
            // =====================================================

            var isNewClient =
                client == null;

            if (client == null)
            {
                client = new Client
                {
                    Name =
                        clientName,

                    PhoneNumber =
                        phoneNumber,

                    Address =
                        address,

                    Email =
                        email,

                    CreatedAt =
                        now
                };


                _context.Clients.Add(client);
            }
            else
            {
                UpdateClientDetails(
                    client,
                    clientName,
                    address,
                    email,
                    now);
            }


            // =====================================================
            // CREATE ORDER
            // =====================================================

            var order =
                new Order
                {
                    Client =
                        client,

                    OrderDate =
                        now,

                    Status =
                        OrderStatus.Pending,

                    Note=
                        dto.Note?.Trim(),
                    TotalAmount =
                        0
                };


            decimal total = 0;


            // =====================================================
            // PROCESS ORDER ITEMS
            // =====================================================

            foreach (var requestedItem in requestedItems)
            {
                var product =
                    products[requestedItem.ProductId];


                ProductVariant? variant = null;


                // =================================================
                // DETERMINE VARIANT STATE
                // =================================================

                var hasVariants =
                    product.Variants.Count > 0;


                // =================================================
                // PRODUCT HAS VARIANTS
                // =================================================

                if (hasVariants)
                {
                    // A product with variants MUST receive
                    // a variant.

                    if (!requestedItem.ProductVariantId.HasValue)
                    {
                        throw new InvalidOperationException(
                            "يجب اختيار المقاس أو الاختيار الخاص بالمنتج.");
                    }


                    // =================================================
                    // FIND ACTIVE VARIANT
                    // =================================================

                    variant =
                        product.Variants.FirstOrDefault(
                            v =>
                                v.Id ==
                                requestedItem.ProductVariantId.Value);


                    // =================================================
                    // VARIANT VALIDATION
                    // =================================================

                    if (variant == null)
                    {
                        throw new InvalidOperationException(
                            "اختيار المنتج غير صحيح.");
                    }


                    if (!variant.IsActive)
                    {
                        throw new InvalidOperationException(
                            "الاختيار الخاص بالمنتج غير متاح حالياً.");
                    }


                    // =================================================
                    // IMPORTANT
                    // =================================================
                    //
                    // StockQuantity is intentionally NOT checked,
                    // decreased, or changed.
                    //
                    // =================================================
                }


                // =================================================
                // PRODUCT WITHOUT VARIANTS
                // =================================================

                else
                {
                    // A product without variants MUST NOT receive
                    // a variant ID.

                    if (requestedItem.ProductVariantId.HasValue)
                    {
                        throw new InvalidOperationException(
                            "اختيار المنتج غير صحيح.");
                    }


                    // =================================================
                    // IMPORTANT
                    // =================================================
                    //
                    // StockQuantity is intentionally NOT checked,
                    // decreased, or changed.
                    //
                    // =================================================
                }


                // =================================================
                // CALCULATE CURRENT SELLING PRICE
                // =================================================
                //
                // IMPORTANT:
                //
                // This is the actual price used for the order.
                //
                // It is NOT the price sent by Angular.
                //
                // =================================================

                var unitPrice =
                    CalculateSellingPrice(product);


                // =================================================
                // CALCULATE SUBTOTAL
                // =================================================

                var subtotal =
                    unitPrice *
                    requestedItem.Quantity;


                total +=
                    subtotal;


                // =================================================
                // CREATE ORDER ITEM
                // =================================================

                order.Items.Add(
                    new OrderItem
                    {
                        ProductId =
                            product.Id,

                        ProductVariantId =
                            variant?.Id,

                        Quantity =
                            requestedItem.Quantity,

                        UnitPrice =
                            unitPrice,
                       

                        ActualPrice =
                            product.ActualPrice
                    });
            }


            // =====================================================
            // FINAL ORDER TOTAL
            // =====================================================

            order.TotalAmount =
                Math.Round(
                    total,
                    2,
                    MidpointRounding.AwayFromZero);


            // =====================================================
            // ADD ORDER
            // =====================================================

            _context.Orders.Add(order);


            // =====================================================
            // SAVE EVERYTHING
            // =====================================================

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException ex) when (
                isNewClient &&
                IsUniqueViolation(ex))
            {
                // Another request created a client with the same
                // phone number at the same time. Attach the order
                // to that client and save again.
                _context.Entry(client).State =
                    EntityState.Detached;

                client =
                    await _context.Clients
                        .FirstOrDefaultAsync(
                            c => c.PhoneNumber == phoneNumber,
                            cancellationToken)
                    ?? throw new InvalidOperationException(
                        "تعذر حفظ بيانات العميل.", ex);

                UpdateClientDetails(
                    client,
                    clientName,
                    address,
                    email,
                    now);

                order.Client =
                    client;

                await _context.SaveChangesAsync(
                    cancellationToken);
            }


            // =====================================================
            // QUEUE ADMIN EMAIL
            // =====================================================
            //
            // Sent by a background service, so the checkout
            // response never waits for the mail API and a mail
            // failure can never fail or crash the order.
            //
            // =====================================================

            try
            {
                var adminEmail =
                    _configuration["OrderNotifications:AdminEmail"];

                var queued =
                    _emailQueue.TryEnqueue(
                        new EmailMessage(
                            string.IsNullOrWhiteSpace(adminEmail)
                                ? DefaultAdminEmail
                                : adminEmail,
                            $"إشعار طلب جديد رقم #{order.Id}",
                            BuildOrderEmailBody(client, products, order)));

                if (!queued)
                {
                    _logger.LogWarning(
                        "Email queue is full. Order {OrderId} email was not queued.",
                        order.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to queue email for order {OrderId}.",
                    order.Id);
            }

            return await GetOrder(
                       order.Id,
                       cancellationToken)
                   ?? throw new InvalidOperationException(
                       "تعذر تحميل الطلب بعد حفظه.");
        }


        // =========================================================
        // CLIENT HELPERS
        // =========================================================

        private static void UpdateClientDetails(
            Client client,
            string clientName,
            string address,
            string? email,
            DateTime now)
        {
            client.Name =
                clientName;

            client.Address =
                address;

            // Keep the saved email when the customer
            // leaves the field empty.
            if (email != null)
            {
                client.Email =
                    email;
            }

            client.UpdatedAt =
                now;
        }


        private static bool IsUniqueViolation(
            DbUpdateException ex)
        {
            return ex.InnerException is SqlException
            {
                Number: SqlUniqueIndexViolation or SqlUniqueConstraintViolation
            };
        }


        // =========================================================
        // ORDER EMAIL BODY
        // =========================================================

        private static string BuildOrderEmailBody(
            Client client,
            Dictionary<int, Product> products,
            Order order)
        {
                var itemsHtml = new StringBuilder();

                foreach (var item in order.Items)
                {
                    var product = products[item.ProductId];
                    var subtotal = item.UnitPrice * item.Quantity;

                    var variantText = item.ProductVariantId.HasValue
                        ? $@"<div style='font-size:12px;color:#64748b;margin-top:5px;'>
                        رقم الاختيار: {item.ProductVariantId.Value}
                     </div>"
                        : "";

                    itemsHtml.Append($@"
                <tr>
                    <td style='padding:14px 10px;border-bottom:1px solid #e2e8f0;'>
                        <strong>{WebUtility.HtmlEncode(product.NameAr)}</strong>
                        {variantText}
                    </td>
                    <td style='padding:14px 6px;text-align:center;border-bottom:1px solid #e2e8f0;'>
                        {item.Quantity}
                    </td>
                    <td dir='ltr' style='padding:14px 6px;text-align:center;border-bottom:1px solid #e2e8f0;white-space:nowrap;'>
                        {item.UnitPrice:N2}
                    </td>
                    <td dir='ltr' style='padding:14px 10px;text-align:center;border-bottom:1px solid #e2e8f0;font-weight:bold;white-space:nowrap;'>
                        {subtotal:N2}
                    </td>
                </tr>");
                }

                var clientNameHtml =
                    WebUtility.HtmlEncode(client.Name ?? "");

                var clientPhoneHtml =
                    WebUtility.HtmlEncode(client.PhoneNumber ?? "");

                var clientAddressHtml =
                    WebUtility.HtmlEncode(client.Address ?? "");

                var clientEmailHtml =
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(client.Email)
                            ? "غير مسجل"
                            : client.Email);

                var orderNote = string.IsNullOrWhiteSpace(order.Note)
                    ? "لا توجد ملاحظات"
                    : WebUtility.HtmlEncode(order.Note);

                var emailBody = $@"
<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>

<body dir='rtl' style='margin:0;padding:0;background:#f1f5f9;
    font-family:Tahoma,Arial,sans-serif;color:#1e293b;'>

<div style='max-width:760px;margin:30px auto;padding:0 12px;'>

    <!-- HEADER -->
    <div style='background:#172554;padding:30px 24px;
        border-radius:14px 14px 0 0;text-align:center;'>

        <h1 style='color:#ffffff;margin:0;font-size:27px;'>
            إشعار طلب جديد
        </h1>
    </div>

    <!-- CONTENT -->
    <div style='background:#ffffff;padding:26px 20px;'>

        <!-- ORDER SUMMARY -->
        <div style='background:#eff6ff;border:1px solid #dbeafe;
            border-radius:10px;padding:20px;margin-bottom:28px;'>

            <table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td>
                        <div style='font-size:12px;color:#64748b;'>
                            رقم الطلب
                        </div>

                        <div dir='ltr' style='text-align:right;font-size:25px;
                            font-weight:bold;color:#1d4ed8;margin-top:8px;'>
                            #{order.Id}
                        </div>
                    </td>

                    <td style='text-align:left;vertical-align:top;'>
                        <span style='display:inline-block;background:#fef3c7;
                            color:#92400e;padding:8px 13px;border-radius:20px;
                            font-size:12px;font-weight:bold;'>
                            قيد الانتظار
                        </span>
                    </td>
                </tr>
            </table>
        </div>

        <!-- CLIENT DETAILS -->
        <h2 style='font-size:19px;color:#0f172a;margin:0 0 15px;'>
            بيانات العميل
        </h2>

        <table role='presentation' width='100%' cellpadding='0' cellspacing='0'
            style='border:1px solid #e2e8f0;border-radius:10px;
            margin-bottom:30px;font-size:14px;'>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;
                    width:115px;color:#64748b;'>
                    اسم العميل
                </td>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;font-weight:bold;'>
                    {clientNameHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;color:#64748b;'>
                    رقم الهاتف
                </td>
                <td dir='ltr' style='padding:14px;text-align:right;border-bottom:1px solid #e2e8f0;'>
                    {clientPhoneHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;border-bottom:1px solid #e2e8f0;color:#64748b;'>
                    البريد الإلكتروني
                </td>
                <td dir='ltr' style='padding:14px;text-align:right;border-bottom:1px solid #e2e8f0;word-break:break-word;'>
                    {clientEmailHtml}
                </td>
            </tr>

            <tr>
                <td style='padding:14px;color:#64748b;'>
                    عنوان العميل
                </td>
                <td style='padding:14px;line-height:1.9;'>
                    {clientAddressHtml}
                </td>
            </tr>
        </table>

        <!-- ORDER ITEMS -->
        <h2 style='font-size:19px;color:#0f172a;margin:0 0 15px;'>
            تفاصيل المنتجات
        </h2>

        <table width='100%' cellpadding='0' cellspacing='0'
            style='border-collapse:collapse;font-size:12px;'>

            <thead>
                <tr style='background:#f8fafc;'>
                    <th style='padding:13px 8px;text-align:right;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        المنتج
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        الكمية
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        سعر الوحدة
                    </th>
                    <th style='padding:13px 5px;text-align:center;color:#475569;border-bottom:2px solid #e2e8f0;'>
                        الإجمالي
                    </th>
                </tr>
            </thead>

            <tbody>
                {itemsHtml}
            </tbody>
        </table>

        <!-- CUSTOMER NOTE -->
        <div style='margin-top:24px;padding:18px;background:#fff7ed;
            border-right:4px solid #f97316;border-radius:6px;'>

            <strong style='color:#9a3412;font-size:15px;'>
                ملاحظات الطلب
            </strong>

            <p style='margin:10px 0 0;color:#334155;font-size:14px;line-height:2;white-space:pre-wrap;'>
                {orderNote}
            </p>
        </div>

        <!-- TOTAL -->
        <div style='margin-top:24px;background:#f8fafc;
            border-radius:10px;padding:22px;'>

            <table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
                <tr>
                    <td style='font-size:15px;color:#475569;'>
                        إجمالي قيمة الطلب
                    </td>

                    <td dir='ltr' style='text-align:left;font-size:23px;
                        font-weight:bold;color:#1d4ed8;white-space:nowrap;'>
                        {order.TotalAmount:N2}
                    </td>
                </tr>
            </table>
        </div>

    </div>

    <!-- FOOTER -->
    <div style='background:#0f172a;padding:18px;
        text-align:center;border-radius:0 0 14px 14px;'>

        <p style='margin:0;color:#94a3b8;font-size:12px;line-height:2;'>
            يُرجى عدم الرد مباشرةً على هذه الرسالة.
        </p>
    </div>

</div>
</body>
</html>";

                return emailBody;
        }



        // =========================================================
        // CALCULATE SELLING PRICE
        // =========================================================

        private static decimal CalculateSellingPrice(
            Product product)
        {
            var price =
                product.Price;


            if (product.DiscountPercentage > 0)
            {
                price -=
                    price *
                    product.DiscountPercentage /
                    100;
            }


            return Math.Round(
                price,
                2,
                MidpointRounding.AwayFromZero);
        }


        // =========================================================
        // GET ORDERS
        // =========================================================

        public async Task<PagedResponse<OrderDto>> GetOrders(
            int page = 1,
            int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            page =
                Math.Max(
                    page,
                    1);


            pageSize =
                Math.Clamp(
                    pageSize,
                    1,
                    100);


            var items =
                await _context.Orders
                    .AsNoTracking()
                    .OrderByDescending(
                        o => o.OrderDate)
                    .ThenByDescending(
                        o => o.Id)
                    .Skip(
                        (page - 1) *
                        pageSize)
                    .Take(
                        pageSize + 1)
                    .Select(
                        OrderProjection())
                    .ToListAsync(
                        cancellationToken);


            var hasMore =
                items.Count >
                pageSize;


            if (hasMore)
            {
                items.RemoveAt(
                    items.Count - 1);
            }


            return new PagedResponse<OrderDto>
            {
                Items =
                    items,

                Page =
                    page,

                PageSize =
                    pageSize,

                HasMore =
                    hasMore
            };
        }


        // =========================================================
        // GET ORDER BY ID
        // =========================================================

        public async Task<OrderDto?> GetOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return null;
            }


            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderProjection())
                .FirstOrDefaultAsync(
                    cancellationToken);
        }


        // =========================================================
        // GET ORDERS BY CLIENT
        // =========================================================

        public async Task<List<OrderDto>> GetOrdersByClient(
            int clientId,
            CancellationToken cancellationToken = default)
        {
            if (clientId <= 0)
            {
                return [];
            }


            return await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.ClientId ==
                    clientId)
                .OrderByDescending(
                    o => o.OrderDate)
                .ThenByDescending(
                    o => o.Id)
                .Select(
                    OrderProjection())
                .ToListAsync(
                    cancellationToken);
        }


        // =========================================================
        // UPDATE ORDER STATUS
        // =========================================================

        public async Task<bool> UpdateOrderStatus(
            int id,
            string status,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            if (string.IsNullOrWhiteSpace(status))
            {
                throw new InvalidOperationException(
                    "حاله الطلب غير متوفره.");
            }


            if (!Enum.TryParse<OrderStatus>(
                    status.Trim(),
                    true,
                    out var orderStatus))
            {
                throw new InvalidOperationException(
                    "حاله الطلب ليست مدعومه.");
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // NOTHING TO UPDATE
            // =================================================

            if (order.Status ==
                orderStatus)
            {
                return true;
            }


            // =================================================
            // CANCELLED ORDER IS FINAL
            // =================================================

            if (order.Status ==
                OrderStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "لا يمكن تغيير حالة طلب ملغي.");
            }


            order.Status =
                orderStatus;


            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }


        // =========================================================
        // CANCEL ORDER
        // =========================================================
        //
        // IMPORTANT:
        // This method ONLY changes the order status.
        //
        // It NEVER changes Product.StockQuantity.
        //
        // It NEVER changes ProductVariant.StockQuantity.
        //
        // =========================================================

        public async Task<bool> CancelOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // ALREADY CANCELLED
            // =================================================

            if (order.Status ==
                OrderStatus.Cancelled)
            {
                return true;
            }


            // =================================================
            // CANCEL ORDER
            // =================================================

            order.Status =
                OrderStatus.Cancelled;


            // =================================================
            // SAVE
            // =================================================

            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }

        public async Task<bool> CompleteOrder(
           int id,
           CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                return false;
            }


            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


            if (order == null)
            {
                return false;
            }


            // =================================================
            // ALREADY COMPLETED
            // =================================================

            if (order.Status ==
                OrderStatus.Confirmed)
            {
                return true;
            }


            // =================================================
            // COMPLETE ORDER
            // =================================================

            order.Status =
                OrderStatus.Confirmed;

            // =================================================
            // SAVE
            // =================================================

            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }

        // =========================================================
        // CURRENT MONTH DASHBOARD
        // =========================================================

        public async Task<DashboardStatsDto> GetCurrentMonthStats(
            CancellationToken cancellationToken = default)
        {
            var now =
                DateTime.UtcNow;


            var startDate =
                new DateTime(
                    now.Year,
                    now.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);


            var endDate =
                startDate.AddMonths(1);


            return await GetStats(
                _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled &&
                        o.OrderDate >=
                            startDate &&
                        o.OrderDate <
                            endDate),
                cancellationToken);
        }


        // =========================================================
        // TOTAL DASHBOARD
        // =========================================================

        public async Task<DashboardStatsDto> GetTotalStats(
            CancellationToken cancellationToken = default)
        {
            return await GetStats(
                _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled),
                cancellationToken);
        }


        // =========================================================
        // DASHBOARD AGGREGATES
        // =========================================================
        //
        // Plain COUNT / SUM queries. Each one translates to a
        // single SQL aggregate, with no grouping or SelectMany.
        //
        // =========================================================

        private static async Task<DashboardStatsDto> GetStats(
            IQueryable<Order> orders,
            CancellationToken cancellationToken)
        {
            var count =
                await orders.CountAsync(
                    cancellationToken);

            if (count == 0)
            {
                return new DashboardStatsDto
                {
                    Orders = 0,
                    Sales = 0,
                    Gain = 0
                };
            }

            var sales =
                await orders.SumAsync(
                    o => o.TotalAmount,
                    cancellationToken);

            var gain =
                await orders
                    .SelectMany(o => o.Items)
                    .SumAsync(
                        i =>
                            (i.UnitPrice -
                             i.ActualPrice) *
                            i.Quantity,
                        cancellationToken);

            return new DashboardStatsDto
            {
                Orders =
                    count,

                Sales =
                    Math.Round(
                        sales,
                        2,
                        MidpointRounding.AwayFromZero),

                Gain =
                    Math.Round(
                        gain,
                        2,
                        MidpointRounding.AwayFromZero)
            };
        }


        // =========================================================
        // ORDER PROJECTION
        // =========================================================
        //
        // Projection instead of Include().
        //
        // Only required fields are selected.
        //
        // Only the first image is returned.
        //
        // Only the selected variant is returned.
        //
        // =========================================================

        private static Expression<Func<Order, OrderDto>>
            OrderProjection()
        {
            return o => new OrderDto
            {
                Id =
                    o.Id,

                ClientId =
                    o.ClientId,

                ClientName =
                    o.Client.Name,

                PhoneNumber =
                    o.Client.PhoneNumber,
                Note=o.Note,

                Email =
                    o.Client.Email,

                Address =
                    o.Client.Address,

                OrderDate =
                    o.OrderDate,

                TotalAmount =
                    o.TotalAmount,

                Status =
                    o.Status.ToString(),

                Items =
                    o.Items
                        .Select(i => new OrderItemDto
                        {
                            Id =
                                i.Id,

                            ProductId =
                                i.ProductId,

                            ProductName =
                                i.Product.NameEn,

                            // =================================================
                            // FIRST PRODUCT IMAGE
                            // =================================================

                            ImageUrl =
                                i.Product.Images
                                    .OrderBy(image =>
                                        image.SortOrder)
                                    .Select(image =>
                                        image.ImageUrl)
                                    .FirstOrDefault(),

                            // =================================================
                            // VARIANT
                            // =================================================

                            ProductVariantId =
                                i.ProductVariantId,

                            SizeName =
                                i.ProductVariant != null &&
                                i.ProductVariant.Size != null
                                    ? i.ProductVariant.Size.Name
                                    : null,

                            HeelSizeName =
                                i.ProductVariant != null &&
                                i.ProductVariant.HeelSize != null
                                    ? i.ProductVariant.HeelSize.Name
                                    : null,

                            // =================================================
                            // QUANTITY
                            // =================================================

                            Quantity =
                                i.Quantity,

                            // =================================================
                            // PRICE
                            // =================================================

                            UnitPrice =
                                i.UnitPrice,

                            ActualPrice =
                                i.ActualPrice,

                            TotalPrice =
                                i.UnitPrice *
                                i.Quantity
                        })
                        .ToList()
            };
        }


        // =========================================================
        // INTERNAL REQUESTED ITEM
        // =========================================================

        private sealed class RequestedOrderItem
        {
            public int ProductId { get; init; }

            public int? ProductVariantId { get; init; }

            public int Quantity { get; init; }

            public decimal? DisplayedUnitPrice { get; set; }
        }
    }
}
