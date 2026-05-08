using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Services;

/// <summary>
/// Sweeps unattended tele-sessions and marks them no-show once the grace window expires.
///   • Patient hasn't joined within 15 minutes of scheduled start → NoShowPatient (no refund).
///   • Patient joined but clinician hasn't within 15 minutes → NoShowClinician (full refund).
/// Notifications and refund bookkeeping flow via TelemedicineService.MarkNoShowAsync.
/// </summary>
public class TeleNoShowMonitor : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TeleNoShowMonitor> _log;

    public TeleNoShowMonitor(IServiceScopeFactory scopes, ILogger<TeleNoShowMonitor> log)
    {
        _scopes = scopes; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger the first run a bit so app startup isn't slowed.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepOnceAsync(stoppingToken); }
            catch (Exception ex) { _log.LogError(ex, "Tele no-show sweep failed"); }

            try { await Task.Delay(SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tele = scope.ServiceProvider.GetRequiredService<ITelemedicineService>();
        var cutoff = DateTime.UtcNow - GraceWindow;

        // Chat-mode sessions are async by design — patients message in and reply later. There's no
        // "missed appointment" concept, so they're excluded from the no-show sweep.
        var candidates = await db.TeleSessions.AsNoTracking()
            .Where(s => s.ScheduledStartUtc <= cutoff && s.EndedAt == null
                && s.Mode != TeleSessionMode.Chat
                && (s.Status == TeleSessionStatus.Scheduled || s.Status == TeleSessionStatus.Requested || s.Status == TeleSessionStatus.PatientWaiting))
            .Select(s => new { s.Id, s.PatientJoinedAt, s.ClinicianJoinedAt, s.Status })
            .ToListAsync(ct);

        foreach (var c in candidates)
        {
            // Patient never joined → mark patient no-show (forfeit fee).
            if (c.PatientJoinedAt is null)
            {
                var (ok, _) = await tele.MarkNoShowAsync(c.Id, patientNoShow: true, ct);
                if (ok) _log.LogInformation("Tele session {Id} marked NoShowPatient", c.Id);
            }
            // Patient joined, clinician never did → clinician no-show (full refund).
            else if (c.ClinicianJoinedAt is null)
            {
                var (ok, refund) = await tele.MarkNoShowAsync(c.Id, patientNoShow: false, ct);
                if (ok) _log.LogInformation("Tele session {Id} marked NoShowClinician — refund {Refund:N2}", c.Id, refund);
            }
        }
    }
}
