using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Tenancy;

public enum TenantStatus
{
    PendingVerification = 1, // signed up, email not verified yet
    Trialing = 2,            // verified, on free trial
    Active = 3,              // paid subscription
    PastDue = 4,             // payment failed, read-only grace period
    Suspended = 5,           // grace expired, blocked
    Cancelled = 6,           // explicit cancel
    PendingPayment = 7       // bank-transfer uploaded, awaiting super-admin confirmation
}

public enum BillingCycle { Monthly = 1, Annual = 2 }

/// <summary>
/// One billable customer — a hospital organisation. Owns one or more <see cref="Facility"/>
/// records (physical locations). All clinical / financial / staff data is scoped to a tenant
/// via EF Core query filters; cross-tenant access requires an explicit IgnoreQueryFilters and
/// is audit-logged.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    /// <summary>URL-safe slug used as the subdomain (e.g. <c>gracehospital</c>).</summary>
    [Required, MaxLength(40)] public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Optional vanity hostname the tenant has pointed at us (e.g. <c>app.gracehospital.com</c>).
    /// Only honoured by the resolver once <see cref="CustomDomainVerifiedAt"/> is non-null.
    /// </summary>
    [MaxLength(253)] public string? CustomDomain { get; set; }

    /// <summary>
    /// Random token the tenant must publish in a TXT record at <c>_thrivehealth.{domain}</c>
    /// to prove they control the domain. Regenerated whenever the domain is changed.
    /// </summary>
    [MaxLength(64)] public string? CustomDomainVerificationToken { get; set; }

    /// <summary>Set when the TXT record was last seen and matched. Cleared when the domain changes.</summary>
    public DateTime? CustomDomainVerifiedAt { get; set; }

    [Required, MaxLength(200)] public string LegalName { get; set; } = string.Empty;
    [MaxLength(100)] public string? BrandName { get; set; }

    [MaxLength(200)] public string? LogoUrl { get; set; }
    [MaxLength(20)]  public string? PrimaryColor { get; set; }

    /// <summary>ISO-4217 currency the tenant displays bills + receipts in.</summary>
    [Required, MaxLength(3)] public string CurrencyCode { get; set; } = "NGN";

    /// <summary>ISO 3166-1 alpha-2 country (drives compliance + payment options).</summary>
    [Required, MaxLength(2)] public string CountryCode { get; set; } = "NG";

    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(100)] public string? Lga { get; set; }
    [MaxLength(500)] public string? Address { get; set; }

    [Required, MaxLength(150)] public string OwnerEmail { get; set; } = string.Empty;
    [MaxLength(150)] public string? OwnerName { get; set; }
    [MaxLength(50)]  public string? OwnerPhone { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.PendingVerification;
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SuspendedAt { get; set; }

    /// <summary>Verified teaching hospitals get the platform free of charge.</summary>
    public bool IsTeachingHospital { get; set; }
    public DateTime? TeachingVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}

/// <summary>Reference catalogue of subscription plans (Trial / Basic / Standard / Premium / Enterprise).</summary>
public class Plan
{
    public int Id { get; set; }

    [Required, MaxLength(40)] public string Code { get; set; } = string.Empty;       // "trial", "basic", "standard", "premium", "enterprise"
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;      // "Standard"
    [MaxLength(500)] public string? Tagline { get; set; }                            // "Mid-sized clinics & polyclinics"
    public int SortOrder { get; set; }

    /// <summary>Base prices in NGN (always). Multi-currency conversion happens at display time.</summary>
    public decimal MonthlyNgn { get; set; }
    public decimal AnnualNgn { get; set; }

    // Hard caps; null = unlimited
    public int? MaxStaff { get; set; }
    public int? MaxPatientsPerMonth { get; set; }
    public int? MaxFacilities { get; set; }
    public int? MaxTeleConsultsPerMonth { get; set; }

    // Feature toggles available on the plan
    public bool TelemedicineEnabled { get; set; }
    public bool ChatPackagesEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool MultiFacilityEnabled { get; set; }
    public bool ClaimsEnabled { get; set; }
    public bool AnalyticsEnabled { get; set; }
    public bool PrioritySupport { get; set; }
    public bool SsoEnabled { get; set; }
    public bool OnPremiseAvailable { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsCustomQuote { get; set; } // Enterprise — price hidden, "contact sales"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The current (or historical) subscription a tenant is on.</summary>
public class TenantSubscription
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int PlanId { get; set; }
    public Plan? Plan { get; set; }

    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);
    public DateTime? CancelledAt { get; set; }

    /// <summary>Snapshot of the price (in tenant's currency) at the time the subscription was created.</summary>
    public decimal PriceAmount { get; set; }
    [Required, MaxLength(3)] public string PriceCurrency { get; set; } = "NGN";

    /// <summary>True when this subscription is the active one (only one per tenant at a time).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Paystack's subscription code, if managed by Paystack. Null for manual transfers.</summary>
    [MaxLength(100)] public string? PaystackSubscriptionCode { get; set; }
}

public enum PaymentMethodKind { Paystack = 1, BankTransfer = 2 }

public enum PaymentReceiptStatus
{
    Pending = 1,        // tenant submitted, awaiting super admin
    Approved = 2,       // confirmed → tenant access flips Active
    Rejected = 3        // wrong amount / wrong reference / fraudulent
}

/// <summary>Records a single payment attempt. Bank-transfer uploads sit here in Pending until
/// a super-admin reviews them; Paystack callbacks land here too for unified reporting.</summary>
public class TenantPayment
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int? SubscriptionId { get; set; }
    public TenantSubscription? Subscription { get; set; }

    public PaymentMethodKind Method { get; set; }
    public PaymentReceiptStatus Status { get; set; } = PaymentReceiptStatus.Pending;

    public decimal Amount { get; set; }
    [Required, MaxLength(3)] public string Currency { get; set; } = "NGN";

    [MaxLength(80)] public string? Reference { get; set; }      // Paystack ref or bank txn ref
    [MaxLength(80)] public string? BankAccountUsed { get; set; } // e.g. "GTB · 0123456789"
    public DateTime? PaidAt { get; set; }                        // tenant-claimed pay date

    /// <summary>Web-accessible URL of the receipt screenshot/PDF the tenant uploaded.</summary>
    [MaxLength(500)] public string? ReceiptUrl { get; set; }
    [MaxLength(120)] public string? ReceiptFileName { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
    [MaxLength(500)] public string? ReviewNotes { get; set; }
}
