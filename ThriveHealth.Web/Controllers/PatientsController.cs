using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class PatientsController : Controller
{

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHospitalNumberGenerator _hospNumber;
    private readonly IMpiService _mpi;

    public PatientsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IHospitalNumberGenerator hospNumber,
        IMpiService mpi)
    {
        _db = db;
        _userManager = userManager;
        _hospNumber = hospNumber;
        _mpi = mpi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        const int pageSize = 25;
        var user = await _userManager.GetUserAsync(User);
        var facilityId = user?.FacilityId ?? 0;

        var query = _db.Patients.AsNoTracking()
            .Where(p => p.FacilityId == facilityId && !p.IsMergedAlias);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.HospitalNumber, "%" + t + "%") ||
                EF.Functions.ILike(p.FirstName, "%" + t + "%") ||
                EF.Functions.ILike(p.LastName, "%" + t + "%") ||
                (p.MiddleName != null && EF.Functions.ILike(p.MiddleName, "%" + t + "%")) ||
                (p.Phone != null && p.Phone.Contains(t)) ||
                (p.Nin != null && p.Nin.Contains(t)));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        return View(new PatientListViewModel { Search = q, Patients = rows, Total = total });
    }

    [HttpGet, HasPermission(Permissions.PatientsRegister)]
    public async Task<IActionResult> Register()
    {
        await PopulateHmoListAsync();
        return View(new PatientRegisterViewModel());
    }

    private async Task PopulateHmoListAsync()
    {
        ViewBag.Hmos = await _db.Payers.AsNoTracking()
            .Where(p => p.IsActive && p.OrgType == ThriveHealth.Web.Models.Insurance.PayerOrgType.Hmo)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Code })
            .ToListAsync();
    }

    [HttpPost, HasPermission(Permissions.PatientsRegister), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(PatientRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateHmoListAsync();
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null || !user.FacilityId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Your user is not assigned to a facility.");
            return View(model);
        }

        if (!model.ConfirmAcceptDuplicate)
        {
            var matches = await _mpi.FindPotentialMatchesAsync(
                user.FacilityId.Value, model.FirstName, model.LastName,
                model.DateOfBirth, model.Phone, model.Nin);

            var auto = matches.FirstOrDefault(m => m.Score >= 90);
            if (auto is not null)
            {
                ViewBag.AutoMatch = auto;
                ViewBag.Matches = matches;
                return View("RegisterDuplicate", model);
            }

            if (matches.Any(m => m.Score >= 70))
            {
                ViewBag.Matches = matches.Where(m => m.Score >= 70).ToList();
                return View("RegisterDuplicate", model);
            }
        }

        var patient = new Patient
        {
            FacilityId = user.FacilityId.Value,
            HospitalNumber = await _hospNumber.NextAsync(user.FacilityId.Value),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim(),
            Title = model.Title,
            DateOfBirth = model.DateOfBirth,
            IsDateOfBirthEstimated = model.IsDateOfBirthEstimated,
            Sex = model.Sex,
            Gender = model.Gender,
            Phone = model.Phone,
            AlternatePhone = model.AlternatePhone,
            WhatsAppOptIn = model.WhatsAppOptIn,
            Email = model.Email,
            StreetAddress = model.StreetAddress,
            Lga = model.Lga,
            State = model.State,
            Postcode = model.Postcode,
            MaritalStatus = model.MaritalStatus,
            Occupation = model.Occupation,
            Religion = model.Religion,
            StateOfOrigin = model.StateOfOrigin,
            EthnicGroup = model.EthnicGroup,
            PreferredLanguage = model.PreferredLanguage,
            Nin = model.Nin,
            DriversLicense = model.DriversLicense,
            VotersCard = model.VotersCard,
            Passport = model.Passport,
            CreatedById = user.Id
        };

        if (!string.IsNullOrWhiteSpace(model.NextOfKinName) && !string.IsNullOrWhiteSpace(model.NextOfKinRelationship))
        {
            patient.NextOfKin.Add(new PatientNextOfKin
            {
                Name = model.NextOfKinName!,
                Relationship = model.NextOfKinRelationship!,
                Phone = model.NextOfKinPhone,
                Address = model.NextOfKinAddress,
                IsPrimary = true
            });
        }

        var primaryType = model.PrimaryPayerType;
        var primaryName = model.PrimaryPayerName;
        if (model.PrimaryHmoId.HasValue)
        {
            var hmo = await _db.Payers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == model.PrimaryHmoId.Value
                    && p.OrgType == ThriveHealth.Web.Models.Insurance.PayerOrgType.Hmo
                    && p.IsActive);
            if (hmo != null)
            {
                primaryType = PayerType.Hmo;
                primaryName = hmo.Name;
            }
        }
        patient.Payers.Add(new PatientPayer
        {
            Type = primaryType,
            Name = string.IsNullOrWhiteSpace(primaryName)
                ? PayerLabel(primaryType)
                : primaryName!,
            MembershipNumber = model.PrimaryPayerMembershipNumber,
            IsPrimary = true,
            IsActive = true
        });

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Registered {patient.FullName} · {patient.HospitalNumber}";
        return RedirectToAction(nameof(Profile), new { id = patient.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Profile(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var facilityId = user?.FacilityId ?? 0;

        var patient = await _db.Patients
            .Include(p => p.Facility)
            .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == facilityId);
        if (patient is null) return NotFound();

        var vm = new PatientProfileViewModel
        {
            Patient = patient,
            NextOfKin = await _db.PatientNextOfKin.AsNoTracking().Where(x => x.PatientId == id).ToListAsync(),
            Payers = await _db.PatientPayers.AsNoTracking().Where(x => x.PatientId == id && x.IsActive).ToListAsync(),
            Allergies = await _db.Allergies.AsNoTracking().Where(x => x.PatientId == id && x.IsActive).OrderByDescending(x => x.Severity).ToListAsync(),
            Problems = await _db.Problems.AsNoTracking().Where(x => x.PatientId == id).OrderBy(x => x.Status).ThenByDescending(x => x.OnsetDate).ToListAsync(),
            Medications = await _db.Medications.AsNoTracking().Where(x => x.PatientId == id).OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.RecordedAt).ToListAsync(),
            Vitals = await _db.Vitals.AsNoTracking().Where(x => x.PatientId == id).OrderByDescending(x => x.RecordedAt).Take(20).ToListAsync(),
            Documents = await _db.PatientDocuments.AsNoTracking().Where(x => x.PatientId == id).OrderByDescending(x => x.UploadedAt).ToListAsync(),
            Encounters = await _db.Encounters.AsNoTracking()
                .Include(e => e.Clinician)
                .Include(e => e.Clinic)
                .Include(e => e.Diagnoses)
                .Where(e => e.PatientId == id)
                .OrderByDescending(e => e.StartedAt).Take(20).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        var user = await _userManager.GetUserAsync(User);
        var facilityId = user?.FacilityId ?? 0;

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(Array.Empty<object>());

        var t = q.Trim();
        var rows = await _db.Patients.AsNoTracking()
            .Where(p => p.FacilityId == facilityId && !p.IsMergedAlias &&
                (EF.Functions.ILike(p.HospitalNumber, "%" + t + "%") ||
                 EF.Functions.ILike(p.FirstName, t + "%") ||
                 EF.Functions.ILike(p.LastName, t + "%") ||
                 (p.Phone != null && p.Phone.Contains(t))))
            .OrderBy(p => p.LastName)
            .Take(15)
            .Select(p => new
            {
                p.Id,
                p.HospitalNumber,
                Name = p.FirstName + " " + p.LastName,
                p.Phone,
                Sex = p.Sex.ToString(),
                Age = p.DateOfBirth.HasValue
                    ? (int?)null
                    : null
            })
            .ToListAsync();

        return Json(rows);
    }

    private static string PayerLabel(PayerType t) => t switch
    {
        PayerType.OutOfPocket => "Out-of-pocket",
        PayerType.Nhia => "NHIA",
        PayerType.Hmo => "HMO",
        PayerType.StateInsurance => "State insurance",
        PayerType.Employer => "Corporate / Retainership",
        PayerType.Donor => "Donor / NGO",
        PayerType.FreeMaternalChild => "Free maternal & child",
        PayerType.Bhcpf => "BHCPF",
        _ => t.ToString()
    };
}
