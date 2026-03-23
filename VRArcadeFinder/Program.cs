using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json if exists (contains sensitive values, not committed to git)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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

// Configure application cookie - must be after AddIdentity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "VRArcadeFinder.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Session Configuration
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register Application Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<VRArcadeFinder.Services.IArcadeService, VRArcadeFinder.Services.ArcadeService>();
builder.Services.AddScoped<VRArcadeFinder.Services.IGoogleMapsCrawlerService, VRArcadeFinder.Services.GoogleMapsCrawlerService>();
builder.Services.AddScoped<VRArcadeFinder.Services.IImageStorageService, VRArcadeFinder.Services.ImageStorageService>();
builder.Services.AddScoped<VRArcadeFinder.Services.IBlogService, VRArcadeFinder.Services.BlogService>();
builder.Services.AddScoped<VRArcadeFinder.Services.IEmailService, VRArcadeFinder.Services.EmailService>();

// Crawl Settings
builder.Services.Configure<VRArcadeFinder.Models.CrawlSettings>(
    builder.Configuration.GetSection("CrawlSettings"));

// Auto Crawl Service (background service)
builder.Services.AddSingleton<VRArcadeFinder.Services.IAutoCrawlService, VRArcadeFinder.Services.AutoCrawlService>();
builder.Services.AddHostedService(sp => (VRArcadeFinder.Services.AutoCrawlService)sp.GetRequiredService<VRArcadeFinder.Services.IAutoCrawlService>());

// Payment Services
builder.Services.AddScoped<VRArcadeFinder.Services.StripePaymentService>();
builder.Services.AddScoped<VRArcadeFinder.Services.IPaymentServiceFactory, VRArcadeFinder.Services.PaymentServiceFactory>();
builder.Services.AddScoped<VRArcadeFinder.Services.IPaymentService>(sp =>
    sp.GetRequiredService<VRArcadeFinder.Services.IPaymentServiceFactory>().GetPaymentService());
builder.Services.AddScoped<VRArcadeFinder.Services.IStripeService>(sp =>
    sp.GetRequiredService<VRArcadeFinder.Services.StripePaymentService>());

// Localization Configuration
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Supported cultures for i18n
var supportedCultures = new[]
{
    new CultureInfo("en"),  // English
    new CultureInfo("vi"),  // Vietnamese
    new CultureInfo("ja"),  // Japanese
    new CultureInfo("ko"),  // Korean
    new CultureInfo("zh"),  // Chinese
    new CultureInfo("th"),  // Thai
    new CultureInfo("es"),  // Spanish
    new CultureInfo("de")   // German
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Add MVC Controllers and Views with localization
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

// Add Razor Pages (for Identity UI if needed)
builder.Services.AddRazorPages()
    .AddViewLocalization();

var app = builder.Build();

// Seed admin user and cities
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await VRArcadeFinder.Data.AdminSeeder.SeedAdminUserAsync(services);

        // Seed cities for auto crawl
        var autoCrawlService = services.GetRequiredService<VRArcadeFinder.Services.IAutoCrawlService>();
        await autoCrawlService.SeedCitiesAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding admin user or cities.");
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

// Localization middleware - must be before routing
app.UseRequestLocalization();

// Custom culture middleware for SEO-friendly URL prefixes (/vi/, /en/, etc.)
app.UseCultureFromUrl();

app.UseRouting();

// Add Session middleware
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map routes with culture prefix for SEO (e.g., /vi/arcade/123)
app.MapControllerRoute(
    name: "culture",
    pattern: "{culture:regex(^(en|vi|ja|ko|zh|th|es|de)$)}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
