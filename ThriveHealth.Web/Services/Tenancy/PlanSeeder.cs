using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Services.Tenancy;

/// <summary>
/// Seeds the catalogue of subscription plans on first start. Plans can later be edited from the
/// super-admin console (price, limits, features); the seeder only inserts missing rows so manual
/// edits aren't overwritten on every restart.
/// </summary>
public static class PlanSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var existingCodes = await db.Plans.Select(p => p.Code).ToListAsync();

        foreach (var p in BuiltIn())
        {
            if (existingCodes.Contains(p.Code)) continue;
            db.Plans.Add(p);
        }
        await db.SaveChangesAsync();
    }

    private static IEnumerable<Plan> BuiltIn() => new[]
    {
        new Plan
        {
            Code = "trial", Name = "Trial", Tagline = "30-day free trial — try Standard with caps",
            SortOrder = 0, MonthlyNgn = 0m, AnnualNgn = 0m,
            MaxStaff = 5, MaxPatientsPerMonth = 100, MaxFacilities = 1, MaxTeleConsultsPerMonth = 25,
            TelemedicineEnabled = true, ChatPackagesEnabled = true, AiEnabled = false,
            MultiFacilityEnabled = false, ClaimsEnabled = false, AnalyticsEnabled = true,
            PrioritySupport = false, SsoEnabled = false, OnPremiseAvailable = false,
            IsActive = true, IsCustomQuote = false
        },
        new Plan
        {
            Code = "basic", Name = "Basic", Tagline = "Solo doctors and small clinics",
            SortOrder = 1, MonthlyNgn = 39_000m, AnnualNgn = 374_400m,
            MaxStaff = 10, MaxPatientsPerMonth = 500, MaxFacilities = 1, MaxTeleConsultsPerMonth = 100,
            TelemedicineEnabled = false, ChatPackagesEnabled = false, AiEnabled = false,
            MultiFacilityEnabled = false, ClaimsEnabled = false, AnalyticsEnabled = false,
            PrioritySupport = false, SsoEnabled = false, OnPremiseAvailable = false,
            IsActive = true
        },
        new Plan
        {
            Code = "standard", Name = "Standard", Tagline = "Mid-sized clinics & polyclinics",
            SortOrder = 2, MonthlyNgn = 99_000m, AnnualNgn = 950_400m,
            MaxStaff = 30, MaxPatientsPerMonth = 2_000, MaxFacilities = 1, MaxTeleConsultsPerMonth = 500,
            TelemedicineEnabled = true, ChatPackagesEnabled = true, AiEnabled = false,
            MultiFacilityEnabled = false, ClaimsEnabled = true, AnalyticsEnabled = true,
            PrioritySupport = false, SsoEnabled = false, OnPremiseAvailable = false,
            IsActive = true
        },
        new Plan
        {
            Code = "premium", Name = "Premium", Tagline = "Hospitals (50–200 beds)",
            SortOrder = 3, MonthlyNgn = 249_000m, AnnualNgn = 2_390_400m,
            MaxStaff = 100, MaxPatientsPerMonth = 10_000, MaxFacilities = 5, MaxTeleConsultsPerMonth = 2_500,
            TelemedicineEnabled = true, ChatPackagesEnabled = true, AiEnabled = true,
            MultiFacilityEnabled = true, ClaimsEnabled = true, AnalyticsEnabled = true,
            PrioritySupport = true, SsoEnabled = false, OnPremiseAvailable = false,
            IsActive = true
        },
        new Plan
        {
            Code = "enterprise", Name = "Enterprise", Tagline = "Hospital chains, parastatals, teaching hospitals",
            SortOrder = 4, MonthlyNgn = 600_000m, AnnualNgn = 5_760_000m,
            MaxStaff = null, MaxPatientsPerMonth = null, MaxFacilities = null, MaxTeleConsultsPerMonth = null,
            TelemedicineEnabled = true, ChatPackagesEnabled = true, AiEnabled = true,
            MultiFacilityEnabled = true, ClaimsEnabled = true, AnalyticsEnabled = true,
            PrioritySupport = true, SsoEnabled = true, OnPremiseAvailable = true,
            IsActive = true, IsCustomQuote = true
        }
    };
}
