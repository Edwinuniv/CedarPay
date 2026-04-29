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

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

Console.WriteLine($"Stripe configured: {!string.IsNullOrEmpty(StripeConfiguration.ApiKey)}");
var stripeKey = builder.Configuration["Stripe:SecretKey"];
Console.WriteLine($"Stripe Secret Key loaded: {(string.IsNullOrEmpty(stripeKey) ? "NO" : "YES")}");

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
Console.WriteLine($"Google Client ID loaded: {(string.IsNullOrEmpty(googleClientId) ? "NO" : "YES")}");
Console.WriteLine($"Google Client Secret loaded: {(string.IsNullOrEmpty(googleClientSecret) ? "NO" : "YES")}");

var emailUsername = builder.Configuration["Email:Username"];
var emailPassword = builder.Configuration["Email:Password"];
Console.WriteLine($"Email Username loaded: {(string.IsNullOrEmpty(emailUsername) ? "NO" : "YES")}");
Console.WriteLine($"Email Password loaded: {(string.IsNullOrEmpty(emailPassword) ? "NO" : "YES")}");

// Gemini API Key check (secret, stored in User Secrets or Environment Variable)
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
Console.WriteLine($"Gemini API Key loaded: {(string.IsNullOrEmpty(geminiApiKey) ? "NO" : "YES")}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration
        .GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<User>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600;
});

builder.Services.AddSignalR();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AgentOrAdmin", policy =>
        policy.RequireRole("agent", "admin"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
});

builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrencyExchangeService, CurrencyExchangeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    RoleSeeder.SeedRolesAsync(services).Wait();
    UserSeeder.SeedUsersAsync(services).Wait();
}

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<MoneyTransfer.Hubs.ChatHub>("/chatHub");

app.MapStaticAssets();
app.MapRazorPages();

app.MapControllerRoute(
    name: "bot",
    pattern: "Bot/{action=Chat}/{id?}",
    defaults: new { controller = "Bot" });

app.MapControllerRoute(
    name: "agentApplication",
    pattern: "AgentApplication/{action=MyApplication}/{id?}",
    defaults: new { controller = "AgentApplication" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();