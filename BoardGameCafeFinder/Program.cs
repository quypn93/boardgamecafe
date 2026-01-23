using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

// Identity Configuration
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set to true in production with email service
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Redis Cache Configuration (optional for MVP, can be added later)
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration = builder.Configuration["Redis:ConnectionString"];
//     options.InstanceName = "BoardGameCafeFinder_";
// });

// Session Configuration
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register Application Services
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<BoardGameCafeFinder.Services.IBggSyncService, BoardGameCafeFinder.Services.BggSyncService>();
builder.Services.AddHttpClient<BoardGameCafeFinder.Services.IBggXmlApiService, BoardGameCafeFinder.Services.BggXmlApiService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.ICafeService, BoardGameCafeFinder.Services.CafeService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.IGoogleMapsCrawlerService, BoardGameCafeFinder.Services.GoogleMapsCrawlerService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.IImageStorageService, BoardGameCafeFinder.Services.ImageStorageService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.ICafeWebsiteCrawlerService, BoardGameCafeFinder.Services.CafeWebsiteCrawlerService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.IBlogService, BoardGameCafeFinder.Services.BlogService>();
builder.Services.AddScoped<BoardGameCafeFinder.Services.IEmailService, BoardGameCafeFinder.Services.EmailService>();

// Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// Add Razor Pages (for Identity UI if needed)
builder.Services.AddRazorPages();

var app = builder.Build();

// Seed admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await BoardGameCafeFinder.Data.AdminSeeder.SeedAdminUserAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding admin user.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle status code errors (404, 403, 500, etc.) with custom pages
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add Session middleware
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
