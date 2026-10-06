
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Models;
using PharmacyAPI.Models.Authentication;

namespace PharmacyAPI.Data
{
    public class ShoesDbContext
        : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ShoesDbContext(
            DbContextOptions<ShoesDbContext> options)
            : base(options)
        {
        }

        // ============================================================
        // PRODUCTS
        // ============================================================

        public DbSet<Product> Products { get; set; }

        public DbSet<ProductImage> ProductImages { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; }

        // ============================================================
        // VISITORS
        // ============================================================

        public DbSet<WebsiteVisit> websiteVisits { get; set; }

        // ============================================================
        // CATEGORIES
        // ============================================================

        public DbSet<Category> Categories { get; set; }

        // ============================================================
        // SIZE MANAGEMENT
        // ============================================================

        public DbSet<Size> Sizes { get; set; }

        public DbSet<HeelSize> HeelSizes { get; set; }

        // ============================================================
        // ORDERS
        // ============================================================

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        // ============================================================
        // CLIENTS
        // ============================================================

        public DbSet<Client> Clients { get; set; }

        // ============================================================
        // SLIDERS
        // ============================================================

        public DbSet<Slider> Sliders { get; set; }

        // ============================================================
        // MODEL CONFIGURATION
        // ============================================================

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========================================================
            // PRODUCT
            // ========================================================

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.NameEn)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.NameAr)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.DescriptionEn)
                    .HasMaxLength(4000);

                entity.Property(p => p.DescriptionAr)
                    .HasMaxLength(4000);

                entity.Property(p => p.Price)
                    .HasPrecision(18, 2);

                entity.Property(p => p.ActualPrice)
                    .HasPrecision(18, 2);

                entity.Property(p => p.DiscountPercentage)
                    .HasPrecision(5, 2);

                entity.Property(p => p.StockQuantity)
                    .IsRequired();

                entity.Property(p => p.IsInStock)
                    .IsRequired();

                entity.Property(p => p.IsDeleted)
                    .IsRequired();

                // ----------------------------------------------------
                // Product -> Category
                // ----------------------------------------------------

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // PRODUCT INDEXES
                //
                // These support the main catalog queries:
                //
                // !IsDeleted
                // Category filtering
                // Offers filtering
                // Pagination by Id
                // ----------------------------------------------------

                entity.HasIndex(p => new
                {
                    p.IsDeleted,
                    p.CategoryId,
                    p.Id
                });

                entity.HasIndex(p => new
                {
                    p.IsDeleted,
                    p.DiscountPercentage,
                    p.Id
                });

                // Product name search / lookup.
                entity.HasIndex(p => p.NameEn);
            });

            // ========================================================
            // CATEGORY
            // ========================================================

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.NameEn)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.NameAr)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.ImageUrl)
                    .HasMaxLength(1000);

                entity.Property(c => c.IsDeleted)
                    .IsRequired();

                // ----------------------------------------------------
                // Unique category name
                // ----------------------------------------------------

                
                
    entity.HasIndex(c => c.NameEn)
    .IsUnique()
    .HasFilter("[IsDeleted] = 0");

                // ----------------------------------------------------
                // Category listing excluding deleted categories
                // ----------------------------------------------------

                entity.HasIndex(c => c.IsDeleted);
            });

            // ========================================================
            // PRODUCT IMAGE
            // ========================================================

            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.Property(i => i.ImageUrl)
                    .IsRequired()
                    .HasMaxLength(1000);

                entity.Property(i => i.SortOrder)
                    .IsRequired();

                // ----------------------------------------------------
                // Product -> Images
                // ----------------------------------------------------

                entity.HasOne(i => i.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ----------------------------------------------------
                // Important for:
                //
                // ORDER BY SortOrder
                // First product image projection
                // ----------------------------------------------------

                entity.HasIndex(i => new
                {
                    i.ProductId,
                    i.SortOrder
                });
            });

            // ========================================================
            // PRODUCT VARIANT
            // ========================================================

            modelBuilder.Entity<ProductVariant>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.Property(v => v.StockQuantity)
                    .IsRequired();

                entity.Property(v => v.IsActive)
                    .IsRequired();

                // ----------------------------------------------------
                // Product -> Variants
                // ----------------------------------------------------

                entity.HasOne(v => v.Product)
                    .WithMany(p => p.Variants)
                    .HasForeignKey(v => v.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ----------------------------------------------------
                // Variant -> Size
                // ----------------------------------------------------

                entity.HasOne(v => v.Size)
                    .WithMany(s => s.ProductVariants)
                    .HasForeignKey(v => v.SizeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // Variant -> HeelSize
                // ----------------------------------------------------

                entity.HasOne(v => v.HeelSize)
                    .WithMany(h => h.ProductVariants)
                    .HasForeignKey(v => v.HeelSizeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // VARIANT INDEXES
                //
                // Main query:
                //
                // ProductId + IsActive
                //
                // This is especially important because only active
                // variants are loaded by the optimized services.
                // ----------------------------------------------------
                entity.HasIndex(v => new
                {
                    v.ProductId,
                    v.SizeId,
                    v.HeelSizeId
                });
                entity.HasIndex(v => new
                {
                    v.ProductId,
                    v.IsActive
                });

                entity.HasIndex(v => v.SizeId);

                entity.HasIndex(v => v.HeelSizeId);
            });

            // ========================================================
            // SIZE
            // ========================================================

            modelBuilder.Entity<Size>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.Property(s => s.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(h => h.Name)
    .IsUnique()
    .HasFilter("[IsActive] = 1");
            });

            // ========================================================
            // HEEL SIZE
            // ========================================================

            modelBuilder.Entity<HeelSize>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.Property(h => h.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(h => h.Name)
      .IsUnique()
      .HasFilter("[IsActive] = 1");
            });

            // ========================================================
            // ORDER
            // ========================================================

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.Property(o => o.TotalAmount)
                    .HasPrecision(18, 2);

                entity.Property(o => o.OrderDate)
                    .IsRequired();

                // ----------------------------------------------------
                // Client -> Orders
                // ----------------------------------------------------

                entity.HasOne(o => o.Client)
                    .WithMany(c => c.Orders)
                    .HasForeignKey(o => o.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // ORDER INDEXES
                //
                // 1. General order pagination:
                //
                // OrderDate DESC, Id DESC
                //
                // 2. Dashboard:
                //
                // Status + OrderDate
                //
                // 3. Client order history:
                //
                // ClientId + OrderDate
                // ----------------------------------------------------

                entity.HasIndex(o => new
                {
                    o.OrderDate,
                    o.Id
                });

                entity.HasIndex(o => new
                {
                    o.Status,
                    o.OrderDate
                });

                entity.HasIndex(o => new
                {
                    o.ClientId,
                    o.OrderDate
                });
            });

            // ========================================================
            // ORDER ITEM
            // ========================================================

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.Property(i => i.UnitPrice)
                    .HasPrecision(18, 2);

                entity.Property(i => i.ActualPrice)
                    .HasPrecision(18, 2);

                entity.Property(i => i.Quantity)
                    .IsRequired();

                // ----------------------------------------------------
                // Order -> OrderItems
                // ----------------------------------------------------

                entity.HasOne(i => i.Order)
                    .WithMany(o => o.Items)
                    .HasForeignKey(i => i.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ----------------------------------------------------
                // Product -> OrderItems
                //
                // Historical orders must preserve their products.
                // ----------------------------------------------------

                entity.HasOne(i => i.Product)
                    .WithMany(p => p.OrderItems)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // ProductVariant -> OrderItems
                //
                // Historical orders must preserve their variants.
                // ----------------------------------------------------

                entity.HasOne(i => i.ProductVariant)
                    .WithMany()
                    .HasForeignKey(i => i.ProductVariantId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ----------------------------------------------------
                // ORDER ITEM INDEXES
                //
                // OrderId:
                // Loading items belonging to an order.
                //
                // ProductId:
                // Best sellers / product sales analysis.
                //
                // ProductVariantId:
                // Variant-related order lookups.
                // ----------------------------------------------------

                entity.HasIndex(i => i.OrderId);

                entity.HasIndex(i => i.ProductId);

                entity.HasIndex(i => i.ProductVariantId);
            });

            // ========================================================
            // WEBSITE VISITS
            // ========================================================

            modelBuilder.Entity<WebsiteVisit>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.VisitorId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(x => x.VisitedAt)
                    .IsRequired();

                // ----------------------------------------------------
                // Dashboard visit statistics
                //
                // Date range first, visitor second.
                // ----------------------------------------------------

                entity.HasIndex(x => new
                {
                    x.VisitedAt,
                    x.VisitorId
                });
            });

            // ========================================================
            // CLIENT
            // ========================================================

            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(c => c.PhoneNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(c => c.Email)
                    .HasMaxLength(320);

                entity.Property(c => c.Address)
                    .HasMaxLength(1000);

                // ----------------------------------------------------
                // Checkout performs:
                //
                // WHERE PhoneNumber = ...
                //
                // Unique also prevents duplicate clients.
                // ----------------------------------------------------

                entity.HasIndex(c => c.PhoneNumber)
                    .IsUnique();
            });


            // ========================================================
            // SLIDER
            // ========================================================

            modelBuilder.Entity<Slider>(entity =>
            {
                entity.HasKey(s => s.Id);
            });

            // ========================================================
            // IDENTITY
            // ========================================================

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                // Used when looking up users by phone.
                entity.HasIndex(u => u.PhoneNumber);
            });

            // ========================================================
            // ROLE SEED
            // ========================================================

            modelBuilder.Entity<ApplicationRole>().HasData(
                new ApplicationRole
                {
                    Id = "role-user",
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp =
                        "11111111-1111-1111-1111-111111111111"
                },
                new ApplicationRole
                {
                    Id = "role-admin",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp =
                        "22222222-2222-2222-2222-222222222222"
                }
            );
        }
    }
}
