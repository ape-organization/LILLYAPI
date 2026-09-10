using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using PharmacyAPI.Data;
using PharmacyAPI.Models.Authentication;
using PharmacyAPI.Services;
using PharmacyAPI.Services.Interfaces;
using Prometheus;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(opt => opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// EF Core
builder.Services.AddDbContext<ShoesDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// EF Core
//builder.Services.AddDbContext<PharmacyDbContext>(opt =>
//    opt.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection"),
//        sqlOptions =>
//        {
//            sqlOptions.EnableRetryOnFailure(
//                maxRetryCount: 5,
//                maxRetryDelay: TimeSpan.FromSeconds(10),
//                errorNumbersToAdd: null);
//        }));

// Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(opt =>
{
    opt.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ShoesDbContext>()
.AddDefaultTokenProviders();

// JWT service
builder.Services.AddScoped<JwtHandler>();


//all services 
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<IProductService,ProductService>();
builder.Services.AddScoped<ICategoryService,CategoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ISliderService, SliderService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ISizeService, SizeService>();
builder.Services.AddScoped<IHeelSizeService, HeelSizeService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowCors", b =>
    {
        b
         .WithOrigins(
                "http://localhost:5000",
                "http://localhost:5555",
                "https://lillyadminpanel.ape-org.com",
                "https://lillyclient.ape-org.com"
            )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});




// JWT Authentication
var key = Encoding.UTF8.GetBytes(builder.Configuration["JWTSettings:securityKey"]);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWTSettings:validIssuer"],
        ValidAudience = builder.Configuration["JWTSettings:validAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("AUTH FAILED: " + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("TOKEN VALIDATED SUCCESSFULLY");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine("ON CHALLENGE ERROR: " + context.Error);
            return Task.CompletedTask;
        }
    };
});

// Global fallback authorization (all API endpoints require JWT by default)
//builder.Services.AddAuthorization(opt =>
//{
//    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
//        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
//        .RequireAuthenticatedUser()
//        .Build();
//});
builder.Services.AddAuthorization();

var app = builder.Build();
// this fro auto migration
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;

//    try
//    {
//        var db = services.GetRequiredService<ShoesDbContext>();

//        await db.Database.MigrateAsync();

//        Console.WriteLine("Database migrations applied successfully.");
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Database migration failed: {ex.Message}");
//        throw;
//    }
//}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider("/var/www/uploads"),
//    RequestPath = "/uploads"
//});

app.UseCors("AllowCors");

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpMetrics();

app.MapControllers();

app.MapMetrics();


app.Run();
