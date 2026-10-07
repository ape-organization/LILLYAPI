
using LilyAPI.Services;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Models.Responses;
using System.Linq.Expressions;

namespace PharmacyAPI.Services
{
    public interface IOrderService
    {
        Task<Order> CreateOrder(
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
        private readonly ShoesDbContext _context;


        public OrderService(ShoesDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // CREATE ORDER
        // =========================================================

        public async Task<Order> CreateOrder(
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

                if (displayedPrice.Value != currentPrice)
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
                throw new KeyNotFoundException(
                    "تغير سعر بعض المنتجات. يرجى مراجعة الطلب قبل إتمام الشراء."
                    );
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
                client.Name =
                    clientName;

                client.Address =
                    address;

                client.Email =
                    email;

                client.UpdatedAt =
                    now;
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
                        OrderStatus.Confirmed,

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

            await _context.SaveChangesAsync(
                cancellationToken);


            return order;
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


            var stats =
                await _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled &&
                        o.OrderDate >=
                            startDate &&
                        o.OrderDate <
                            endDate)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Orders =
                            g.Count(),

                        Sales =
                            g.Sum(o =>
                                o.TotalAmount),

                        Gain =
                            g.SelectMany(o =>
                                o.Items)
                                .Sum(i =>
                                    (i.UnitPrice -
                                     i.ActualPrice) *
                                    i.Quantity)
                    })
                    .FirstOrDefaultAsync(
                        cancellationToken);


            if (stats == null)
            {
                return new DashboardStatsDto
                {
                    Orders = 0,
                    Sales = 0,
                    Gain = 0
                };
            }


            return new DashboardStatsDto
            {
                Orders =
                    stats.Orders,

                Sales =
                    Math.Round(
                        stats.Sales,
                        2,
                        MidpointRounding.AwayFromZero),

                Gain =
                    Math.Round(
                        stats.Gain,
                        2,
                        MidpointRounding.AwayFromZero)
            };
        }


        // =========================================================
        // TOTAL DASHBOARD
        // =========================================================

        public async Task<DashboardStatsDto> GetTotalStats(
            CancellationToken cancellationToken = default)
        {
            var stats =
                await _context.Orders
                    .AsNoTracking()
                    .Where(o =>
                        o.Status !=
                            OrderStatus.Cancelled)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Orders =
                            g.Count(),

                        Sales =
                            g.Sum(o =>
                                o.TotalAmount),

                        Gain =
                            g.SelectMany(o =>
                                o.Items)
                                .Sum(i =>
                                    (i.UnitPrice -
                                     i.ActualPrice) *
                                    i.Quantity)
                    })
                    .FirstOrDefaultAsync(
                        cancellationToken);


            if (stats == null)
            {
                return new DashboardStatsDto
                {
                    Orders = 0,
                    Sales = 0,
                    Gain = 0
                };
            }


            return new DashboardStatsDto
            {
                Orders =
                    stats.Orders,

                Sales =
                    Math.Round(
                        stats.Sales,
                        2,
                        MidpointRounding.AwayFromZero),

                Gain =
                    Math.Round(
                        stats.Gain,
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
