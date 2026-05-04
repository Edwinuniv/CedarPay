using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Implementations;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Implementations;
using MoneyTransfer.Services.Interfaces;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ✅ Logging Configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// ✅ Stripe Configuration
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
Console.WriteLine($"Stripe configured: {!string.IsNullOrEmpty(StripeConfiguration.ApiKey)}");
Console.WriteLine($"Stripe Secret Key loaded: {(string.IsNullOrEmpty(builder.Configuration["Stripe:SecretKey"]) ? "NO" : "YES")}");

// ✅ External Services Check
Console.WriteLine($"Google Client ID loaded: {(string.IsNullOrEmpty(builder.Configuration["Authentication:Google:ClientId"]) ? "NO" : "YES")}");
Console.WriteLine($"Google Client Secret loaded: {(string.IsNullOrEmpty(builder.Configuration["Authentication:Google:ClientSecret"]) ? "NO" : "YES")}");
Console.WriteLine($"Email Username loaded: {(string.IsNullOrEmpty(builder.Configuration["Email:Username"]) ? "NO" : "YES")}");
Console.WriteLine($"Email Password loaded: {(string.IsNullOrEmpty(builder.Configuration["Email:Password"]) ? "NO" : "YES")}");
Console.WriteLine($"Gemini API Key loaded: {(string.IsNullOrEmpty(builder.Configuration["Gemini:ApiKey"]) ? "NO" : "YES")}");

// ✅ Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Identity Configuration
builder.Services.AddDefaultIdentity<User>(options =>
{
    // Sign in settings
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Password settings (you can adjust these)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// ✅ Authentication
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    });

// ✅ Cookie Configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// ✅ MVC & Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ✅ File Upload Limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; // 100MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; // 100MB
});

// ✅ SignalR for Real-time Chat
builder.Services.AddSignalR();

// ✅ Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AgentOrAdmin", policy => policy.RequireRole("agent", "admin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// ✅ HTTP Client & Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrencyExchangeService, CurrencyExchangeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// ✅ Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBeneficiaryRepository, BeneficiaryRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<ICurrencyRepository, CurrencyRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ITopUpRepository, TopUpRepository>();
builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddScoped<IAgentApplicationRepository, AgentApplicationRepository>();

// ✅ API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ✅ Seed Roles and Users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await RoleSeeder.SeedRolesAsync(services);
        await UserSeeder.SeedUsersAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// ✅ Development vs Production Configuration
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ✅ Middleware Pipeline
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ SignalR Hubs
app.MapHub<MoneyTransfer.Hubs.ChatHub>("/chatHub");

// ✅ Routes
app.MapStaticAssets();
app.MapRazorPages();

// ✅ Custom Route for Bot
app.MapControllerRoute(
    name: "bot",
    pattern: "Bot/{action=Chat}/{id?}",
    defaults: new { controller = "Bot" });

// ✅ Custom Route for Agent Application
app.MapControllerRoute(
    name: "agentApplication",
    pattern: "AgentApplication/{action=MyApplication}/{id?}",
    defaults: new { controller = "AgentApplication" });

// ✅ Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();