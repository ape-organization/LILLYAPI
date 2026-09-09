using System.Linq.Expressions;
using System.Data;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

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

        Task<List<OrderDto>> GetOrders(
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


    public class OrderService : IOrderService
    {
        private readonly ShoesDbContext _context;


        public OrderService(ShoesDbContext context)
        {
            _context = context;
        }


        // =====================================================
        // CREATE ORDER
        // =====================================================

        public async Task<Order> CreateOrder(
        CreateOrderDto dto,
        CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            // =========================================================
            // VALIDATE CLIENT
            // =========================================================

            if (dto.Client == null)
            {
                throw new InvalidOperationException(
                    "معلومات العميل غير متوفره.");
            }

            if (string.IsNullOrWhiteSpace(dto.Client.Name))
            {
                throw new InvalidOperationException(
                    "اسم العميل مطلوب.");
            }

            if (string.IsNullOrWhiteSpace(dto.Client.PhoneNumber))
            {
                throw new InvalidOperationException(
                    "رقم العميل مطلوب.");
            }

            if (string.IsNullOrWhiteSpace(dto.Client.Address))
            {
                throw new InvalidOperationException(
                    "عنوان العميل مطلوب.");
            }


            // =========================================================
            // VALIDATE ITEMS
            // =========================================================

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
            }


            // =========================================================
            // MERGE DUPLICATE ITEMS
            // =========================================================

            var requestedItems = dto.Items
                .GroupBy(x => new
                {
                    x.ProductId,
                    x.ProductVariantId
                })
                .Select(g => new RequestedOrderItem
                {
                    ProductId = g.Key.ProductId,
                    ProductVariantId = g.Key.ProductVariantId,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();


            // =========================================================
            // VALIDATE MERGED QUANTITIES
            // =========================================================

            if (requestedItems.Any(x => x.Quantity <= 0))
            {
                throw new InvalidOperationException(
                    "الكميه غير صحيحه.");
            }


            var productIds = requestedItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();


            // =========================================================
            // START TRANSACTION
            // =========================================================

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                // =====================================================
                // LOAD PRODUCTS + VARIANTS
                // =====================================================

                var products = await _context.Products
                    .Where(p =>
                        productIds.Contains(p.Id) &&
                        !p.IsDeleted)
                    .Include(p => p.Variants)
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
                // FIND CLIENT
                // =====================================================

                var phoneNumber =
                    dto.Client.PhoneNumber.Trim();

                var client = await _context.Clients
                    .FirstOrDefaultAsync(
                        c => c.PhoneNumber == phoneNumber,
                        cancellationToken);

                var now = DateTime.UtcNow;


                // =====================================================
                // CREATE / UPDATE CLIENT
                // =====================================================

                if (client == null)
                {
                    client = new Client
                    {
                        Name = dto.Client.Name.Trim(),

                        PhoneNumber = phoneNumber,

                        Address = dto.Client.Address.Trim(),

                        Email =
                            string.IsNullOrWhiteSpace(dto.Client.Email)
                                ? null
                                : dto.Client.Email.Trim(),

                        CreatedAt = now
                    };

                    _context.Clients.Add(client);
                }
                else
                {
                    client.Name =
                        dto.Client.Name.Trim();

                    client.Address =
                        dto.Client.Address.Trim();

                    client.Email =
                        string.IsNullOrWhiteSpace(dto.Client.Email)
                            ? null
                            : dto.Client.Email.Trim();

                    client.UpdatedAt = now;
                }


                // =====================================================
                // CREATE ORDER
                // =====================================================

                var order = new Order
                {
                    Client = client,

                    OrderDate = now,

                    Status = OrderStatus.Confirmed,

                    TotalAmount = 0
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
                    // DETERMINE WHETHER PRODUCT USES VARIANTS
                    // =================================================

                    var hasVariants =
                        product.Variants.Any();


                    // =================================================
                    // PRODUCT HAS VARIANTS
                    // =================================================

                    if (hasVariants)
                    {
                        // -------------------------------------------------
                        // A product that has variants MUST receive a variant
                        // -------------------------------------------------

                        if (!requestedItem.ProductVariantId.HasValue)
                        {
                            throw new InvalidOperationException(
                                "يجب اختيار المقاس أو الاختيار الخاص بالمنتج.");
                        }


                        // -------------------------------------------------
                        // FIND VARIANT
                        // -------------------------------------------------

                        variant =
                            product.Variants.FirstOrDefault(
                                v =>
                                    v.Id ==
                                    requestedItem.ProductVariantId.Value);


                        // -------------------------------------------------
                        // VARIANT MUST BELONG TO PRODUCT
                        // -------------------------------------------------

                        if (variant == null)
                        {
                            throw new InvalidOperationException(
                                "اختيار المنتج غير صحيح.");
                        }


                        // -------------------------------------------------
                        // VARIANT MUST BE ACTIVE
                        // -------------------------------------------------

                        if (!variant.IsActive)
                        {
                            throw new InvalidOperationException(
                                "الاختيار الخاص بالمنتج غير متاح حالياً.");
                        }


                        // -------------------------------------------------
                        // CHECK VARIANT STOCK
                        // -------------------------------------------------

                        if (variant.StockQuantity <
                            requestedItem.Quantity)
                        {
                            throw new InvalidOperationException(
                                "الكمية المطلوبة من المنتج غير متوفرة.");
                        }


                        // -------------------------------------------------
                        // DECREASE VARIANT STOCK
                        // -------------------------------------------------

                        variant.StockQuantity -=
                            requestedItem.Quantity;
                    }


                    // =================================================
                    // PRODUCT DOES NOT HAVE VARIANTS
                    // =================================================

                    else
                    {
                        // -------------------------------------------------
                        // A product without variants must NOT receive
                        // a variant ID.
                        // -------------------------------------------------

                        if (requestedItem.ProductVariantId.HasValue)
                        {
                            throw new InvalidOperationException(
                                "اختيار المنتج غير صحيح.");
                        }


                        // -------------------------------------------------
                        // CHECK PRODUCT STOCK
                        // -------------------------------------------------

                        if (product.StockQuantity <
                            requestedItem.Quantity)
                        {
                            throw new InvalidOperationException(
                                "الكمية المطلوبة من المنتج غير متوفرة.");
                        }


                        // -------------------------------------------------
                        // DECREASE PRODUCT STOCK
                        // -------------------------------------------------

                        product.StockQuantity -=
                            requestedItem.Quantity;
                    }


                    // =================================================
                    // CALCULATE SELLING PRICE
                    // =================================================

                    var unitPrice =
                        CalculateSellingPrice(product);


                    // =================================================
                    // CALCULATE SUBTOTAL
                    // =================================================

                    var subtotal =
                        unitPrice *
                        requestedItem.Quantity;

                    total += subtotal;


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


                // =====================================================
                // COMMIT
                // =====================================================

                await transaction.CommitAsync(
                    cancellationToken);


                return order;
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                throw;
            }
        }
        // =====================================================
        // CALCULATE SELLING PRICE
        // =====================================================

        private static decimal CalculateSellingPrice(
            Product product)
        {
            var price =
                product.Price;


            if (product.DiscountPercentage > 0)
            {
                price =
                    price -
                    (
                        price *
                        product.DiscountPercentage /
                        100
                    );
            }


            return Math.Round(
                price,
                2,
                MidpointRounding.AwayFromZero);
        }


        // =====================================================
        // GET ALL ORDERS
        // =====================================================

        public async Task<List<OrderDto>> GetOrders(
            CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.OrderDate)
                .Select(OrderProjection())
                .ToListAsync(cancellationToken);
        }


        // =====================================================
        // GET ORDER BY ID
        // =====================================================

        public async Task<OrderDto?> GetOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id)
                .Select(OrderProjection())
                .FirstOrDefaultAsync(cancellationToken);
        }


        // =====================================================
        // GET ORDERS BY CLIENT
        // =====================================================

        public async Task<List<OrderDto>> GetOrdersByClient(
            int clientId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.ClientId == clientId)
                .OrderByDescending(o => o.OrderDate)
                .Select(OrderProjection())
                .ToListAsync(cancellationToken);
        }


        // =====================================================
        // UPDATE ORDER STATUS
        // =====================================================

        public async Task<bool> UpdateOrderStatus(
            int id,
            string status,
            CancellationToken cancellationToken = default)
        {
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


            var order = await _context.Orders
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

            if (order.Status == orderStatus)
            {
                return true;
            }


            // =================================================
            // DO NOT CHANGE A CANCELLED ORDER
            // =================================================
            //
            // Cancellation already restores stock.
            //
            // Allowing a cancelled order to become Confirmed
            // again would create a stock inconsistency.
            //
            // =================================================

            if (order.Status == OrderStatus.Cancelled)
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


        // =====================================================
        // CANCEL ORDER
        // =====================================================

        public async Task<bool> CancelOrder(
            int id,
            CancellationToken cancellationToken = default)
        {
            // =================================================
            // START TRANSACTION
            // =================================================

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);


            try
            {
                // =================================================
                // LOAD ORDER ITEMS
                // =================================================
                //
                // We need the product/variant IDs so we can
                // return the stock.
                //
                // =================================================

                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(
                        o => o.Id == id,
                        cancellationToken);


                if (order == null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }


                // =================================================
                // ALREADY CANCELLED
                // =================================================

                if (order.Status == OrderStatus.Cancelled)
                {
                    await transaction.CommitAsync(
                        cancellationToken);

                    return true;
                }


                // =================================================
                // RESTORE STOCK
                // =================================================

                foreach (var item in order.Items)
                {
                    // =============================================
                    // VARIANT STOCK
                    // =============================================

                    if (item.ProductVariantId.HasValue)
                    {
                        var variant =
                            await _context.ProductVariants
                                .FirstOrDefaultAsync(
                                    v =>
                                        v.Id ==
                                        item.ProductVariantId.Value,
                                    cancellationToken);


                        if (variant != null)
                        {
                            variant.StockQuantity +=
                                item.Quantity;
                        }
                    }
                    else
                    {
                        // =============================================
                        // PRODUCT STOCK
                        // =============================================

                        var product =
                            await _context.Products
                                .FirstOrDefaultAsync(
                                    p =>
                                        p.Id ==
                                        item.ProductId,
                                    cancellationToken);


                        if (product != null)
                        {
                            product.StockQuantity +=
                                item.Quantity;
                        }
                    }
                }


                // =================================================
                // CANCEL ORDER
                // =================================================

                order.Status =
                    OrderStatus.Cancelled;


                await _context.SaveChangesAsync(
                    cancellationToken);


                // =================================================
                // COMMIT
                // =================================================

                await transaction.CommitAsync(
                    cancellationToken);


                return true;
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                throw;
            }
        }


        // =====================================================
        // CURRENT MONTH DASHBOARD
        // =====================================================
        //
        // ONE DATABASE QUERY.
        //
        // Previously:
        //
        // 1. Count
        // 2. Sales
        // 3. Gain
        //
        // Now:
        //
        // 1. One aggregate query.
        //
        // =====================================================

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
                            g.SelectMany(o => o.Items)
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


        // =====================================================
        // TOTAL DASHBOARD
        // =====================================================
        //
        // ONE DATABASE QUERY.
        //
        // =====================================================

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
                            g.SelectMany(o => o.Items)
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


        // =====================================================
        // ORDER PROJECTION
        // =====================================================
        //
        // Projection is used instead of Include().
        //
        // Only required fields are selected.
        //
        // Only the FIRST product image is returned.
        //
        // =====================================================

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

                            // =====================================
                            // FIRST IMAGE
                            // =====================================

                            ImageUrl =
                                i.Product.Images
                                    .OrderBy(image =>
                                        image.SortOrder)
                                    .Select(image =>
                                        image.ImageUrl)
                                    .FirstOrDefault(),

                            // =====================================
                            // VARIANT
                            // =====================================

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

                            // =====================================
                            // QUANTITY
                            // =====================================

                            Quantity =
                                i.Quantity,

                            // =====================================
                            // PRICE
                            // =====================================

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


        // =====================================================
        // INTERNAL REQUESTED ITEM
        // =====================================================

        private sealed class RequestedOrderItem
        {
            public int ProductId { get; init; }

            public int? ProductVariantId { get; init; }

            public int Quantity { get; init; }
        }
    }
}