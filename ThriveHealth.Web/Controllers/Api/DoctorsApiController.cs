using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>The doctors at the patient's hospital. Ratings and experience appear only where real data exists.</summary>
[Route("api/v1/doctors")]
public class DoctorsApiController : ApiControllerBase
{
    private static readonly string[] DoctorRoles = { "Consultant", "Doctor", "Medical Officer" };

    private readonly ApplicationDbContext _db;

    public DoctorsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiDoctor>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var fid = FacilityId;
        var rows = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            join u in _db.Users on ur.UserId equals u.Id
            where DoctorRoles.Contains(r.Name!) && u.FacilityId == fid && u.IsActive
            select new { u.Id, u.FirstName, u.LastName, u.Designation, u.Department, Role = r.Name })
            .ToListAsync(ct);

        var doctors = rows.GroupBy(r => r.Id).Select(g => new
        {
            g.First().Id, g.First().FirstName, g.First().LastName, g.First().Designation, g.First().Department,
            IsConsultant = g.Any(x => x.Role == "Consultant")
        }).ToList();
        var ids = doctors.Select(d => d.Id).ToList();

        var ratings = await _db.TeleSessions.AsNoTracking()
            .Where(s => s.FacilityId == fid && s.PatientRating != null && s.ClinicianId != null && ids.Contains(s.ClinicianId))
            .GroupBy(s => s.ClinicianId!)
            .Select(g => new { Id = g.Key, Average = g.Average(s => (double)s.PatientRating!), Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, ct);

        var hireDates = await _db.HrProfiles.AsNoTracking()
            .Where(h => ids.Contains(h.UserId) && h.HireDate != null)
            .ToDictionaryAsync(h => h.UserId, h => h.HireDate!.Value, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = doctors.Select(d =>
        {
            ratings.TryGetValue(d.Id, out var rating);
            int? years = hireDates.TryGetValue(d.Id, out var hired) && (today.DayNumber - hired.DayNumber) >= 365
                ? (today.DayNumber - hired.DayNumber) / 365 : null;
            return new ApiDoctor(d.Id, $"{d.FirstName} {d.LastName}".Trim(), d.Designation ?? d.Department, d.IsConsultant,
                rating is null ? null : Math.Round(rating.Average, 1), rating?.Count ?? 0, years);
        }).OrderBy(d => d.Name).ToList();

        return Ok(result);
    }
}
