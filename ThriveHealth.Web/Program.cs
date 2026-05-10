using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Hubs;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Portal;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;
using ThriveHealth.Web.Services.Integrations;
using ThriveHealth.Web.Services.Tenancy;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddScoped<TenantStampingInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    // Resolve from the per-request scope so the interceptor sees the current tenant.
    options.AddInterceptors(sp.GetRequiredService<TenantStampingInterceptor>());
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthentication()
    .AddCookie(PortalAuth.Scheme, options =>
    {
        options.Cookie.Name = "ThriveHealth.Portal";
        options.LoginPath = "/Portal/Login";
        options.LogoutPath = "/Portal/Logout";
        options.AccessDeniedPath = "/Portal/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<IPasswordHasher<PortalAccount>, PasswordHasher<PortalAccount>>();
builder.Services.AddScoped<IPortalAuthService, PortalAuthService>();
builder.Services.AddScoped<ITelemedicineService, TelemedicineService>();
builder.Services.AddScoped<ITeleConsultActions, TeleConsultActions>();
builder.Services.AddScoped<ITeleNotifier, TeleNotifier>();
builder.Services.AddScoped<ITeleChatService, TeleChatService>();
builder.Services.AddHttpClient("webpush");
builder.Services.AddScoped<IWebPushService, WebPushService>();
builder.Services.AddHostedService<TeleNoShowMonitor>();
builder.Services.AddSingleton<ILiveKitTokenService, LiveKitTokenService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddSingleton<ICustomDomainVerifier, CustomDomainVerifier>();
builder.Services.AddHostedService<TenantLifecycleSweep>();
builder.Services.AddScoped<INhmisReportService, NhmisReportService>();
builder.Services.AddScoped<IIdsrService, IdsrService>();

builder.Services.AddScoped<IHospitalNumberGenerator, HospitalNumberGenerator>();
builder.Services.AddScoped<IMpiService, MpiService>();
builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IEncounterService, EncounterService>();
builder.Services.AddScoped<IIcdSearchService, IcdSearchService>();
builder.Services.AddScoped<IDrugInteractionService, DrugInteractionService>();
builder.Services.AddScoped<IDispenseService, DispenseService>();
builder.Services.AddScoped<IMarSlotGenerator, MarSlotGenerator>();
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddScoped<ITriageService, TriageService>();
builder.Services.AddScoped<IOrderSetService, OrderSetService>();
builder.Services.AddScoped<ILabService, LabService>();
builder.Services.AddScoped<IImagingService, ImagingService>();
builder.Services.AddScoped<IFormularyService, FormularyService>();
builder.Services.AddScoped<IClaimsService, ClaimsService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IGrnService, GrnService>();
builder.Services.AddScoped<IStockTakeService, StockTakeService>();
builder.Services.AddScoped<ITheatreService, TheatreService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<ICashierShiftService, CashierShiftService>();
builder.Services.AddScoped<IMaternityService, MaternityService>();
builder.Services.AddScoped<IImmunizationService, ImmunizationService>();
builder.Services.AddScoped<IBatch13Numbering, Batch13Numbering>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISmsGateway, LoggingSmsGateway>();
builder.Services.AddScoped<IEmailGateway, LoggingEmailGateway>();
builder.Services.AddScoped<IPaymentGateway, LoggingPaymentGateway>();

// ---- AI ----
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
{
    var aiProvider = builder.Configuration[$"{AiOptions.SectionName}:Provider"]?.ToLowerInvariant() ?? "stub";
    switch (aiProvider)
    {
        case "openrouter":
            builder.Services.AddHttpClient<IAiService, OpenRouterAiService>();
            break;
        case "anthropic":
            builder.Services.AddHttpClient<IAiService, AnthropicAiService>();
            break;
        default:
            builder.Services.AddSingleton<IAiService, StubAiService>();
            break;
    }
}
builder.Services.AddScoped<IClinicalAiService, ClinicalAiService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("portal-auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", failureStatus: HealthStatus.Unhealthy);

builder.Services.AddSignalR();

builder.Services.AddControllersWithViews();
// Generate clean lowercase URLs from tag helpers (`asp-controller`, `asp-action`, `Url.Action`)
// so /Patients/Index renders as /patients and /Account/Login as /login. Affects link generation
// only — incoming routes are case-insensitive either way.
builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
// Allow AJAX endpoints to send the antiforgery token via a header so they can stay [ValidateAntiForgeryToken]
// instead of carrying a hidden form field. Keeps the SPA-style auto-save endpoints CSRF-safe.
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-Token");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (ctx, next) =>
{
    var headers = ctx.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(self), microphone=(self), display-capture=(self), geolocation=()";
    if (!ctx.Response.Headers.ContainsKey("Content-Security-Policy"))
    {
        // LiveKit Cloud uses *.livekit.cloud (HTTPS + WSS). Self-hosted instances are 'self'-relative
        // to the host's chosen domain — operators can extend this list when going self-hosted.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://code.jquery.com; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
            "img-src 'self' data: https:; " +
            "media-src 'self' blob:; " +
            "connect-src 'self' http: ws: https: wss:; " +
            "frame-ancestors 'self'";
    }
    await next();
});

app.UseRouting();
app.UseMiddleware<TenantResolverMiddleware>();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Attribute-routed controllers (SuperAdmin*, Onboarding, ...) and the conventional
// {controller}/{action}/{id} pattern are both registered by this single call.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<QueueHub>("/hubs/queue");
app.MapHub<BedHub>("/hubs/beds");
app.MapHub<TeleChatHub>("/hubs/telechat");

app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await ThriveHealth.Web.Services.Tenancy.PlanSeeder.SeedAsync(services);
    var db = services.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(services);
    await DemoSeeder.SeedAsync(services);
}

app.Run();
