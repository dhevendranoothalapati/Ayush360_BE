using Ayush360.Hms.Api.Data;
using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Hms.Api.Registration;
using Ayush360.Platform.Data;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Hms.Api.Consultations;

// =====================================================================================================
//  Doctor availability: a doctor can pause seeing OPD patients with a reason (In OT, emergency call, ward rounds,
//  lunch, meeting, other) and an expected return time. The front desk queue shows it next to the doctor, together
//  with the doctor's average consultation time, so the desk can tell patients roughly when they will be seen.
//
//  GET  /api/doctor/availability        own status
//  POST /api/doctor/availability/away   { reason, note, expectedBackMinutes }
//  POST /api/doctor/availability/back
//  Starting a consultation also ends the pause (ConsultationService.StartAsync).
//  A pause left open from an earlier day is ignored (treated as available).
// =====================================================================================================

public sealed record DoctorAvailabilityDto(
    bool IsAway,
    string Reason, // OPERATION_THEATRE, EMERGENCY_CALL, WARD_ROUNDS, LUNCH_BREAK, MEETING, OTHER or ""
    string Note,
    DateTimeOffset? Since,
    DateTimeOffset? ExpectedBackAt);

public sealed class GoAwayRequestDto
{
    public string Reason { get; set; } = "";
    public string Note { get; set; } = "";

    /// <summary>Minutes from now (5 to 480), or null = not sure.</summary>
    public int? ExpectedBackMinutes { get; set; }
}

/// <summary>One doctor on the front desk queue: status + pace.</summary>
public sealed record FrontDeskDoctorDto(
    long DoctorId,
    string Name,
    DoctorAvailabilityDto Availability,
    int AvgConsultMinutes,
    string AvgBasis, // TODAY (3+ patients seen today), RECENT (last 30 days), DEFAULT (no history yet)
    int SeenToday,
    DateTimeOffset? FirstStartedAt, // first consultation of the day: null = OPD not started yet
    DateTimeOffset? LastCompletedAt);

public sealed class DoctorAvailabilityService(
    HospitalDbContext db,
    HospitalClock clock,
    ICurrentUser currentUser,
    ILogger<DoctorAvailabilityService> logger)
{
    public const int DefaultConsultMinutes = 10;
    private const int MaxNoteLength = 200;

    private static readonly DoctorAvailabilityDto Available = new(false, "", "", null, null);

    public async Task<DoctorAvailabilityDto> GetMineAsync(CancellationToken ct)
    {
        var me = await RequireDoctorAsync(ct);
        return ToDto(await OpenBreakAsync(me.Id, ct));
    }

    public async Task<DoctorAvailabilityDto> GoAwayAsync(GoAwayRequestDto request, CancellationToken ct)
    {
        var me = await RequireDoctorAsync(ct);

        var errors = new Dictionary<string, string>();
        DoctorAwayReason? reason = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Reason) && !char.IsDigit(request.Reason.Trim()[0]) && !request.Reason.Contains(','))
            {
                var parsed = EnumText.FromCode<DoctorAwayReason>(request.Reason.Trim());
                if (Enum.IsDefined(parsed)) reason = parsed;
            }
        }
        catch (ArgumentException)
        {
        }

        var note = (request.Note ?? "").Trim();
        if (reason is null) errors["reason"] = "Choose a reason";
        if (reason == DoctorAwayReason.Other && note.Length == 0) errors["note"] = "Say where you are";
        if (note.Length > MaxNoteLength) errors["note"] = $"Maximum {MaxNoteLength} characters";
        if (request.ExpectedBackMinutes is < 5 or > 480) errors["expectedBackMinutes"] = "Choose between 5 minutes and 8 hours";
        if (errors.Count > 0)
        {
            throw new AppValidationException("Please correct the highlighted fields", errors);
        }

        var now = clock.UtcNow;
        // Changing the reason (e.g. lunch -> OT) closes the previous pause and opens a new one: history stays exact.
        foreach (var open in await db.DoctorBreaks.Where(x => x.DoctorId == me.Id && x.EndedAt == null).ToListAsync(ct))
        {
            open.EndedAt = now;
        }

        var pause = new DoctorBreak
        {
            TenantId = me.TenantId,
            DoctorId = me.Id,
            Reason = reason!.Value,
            Note = note.Length == 0 ? null : note,
            StartedAt = now,
            ExpectedBackAt = request.ExpectedBackMinutes is { } minutes ? now.AddMinutes(minutes) : null,
        };
        db.DoctorBreaks.Add(pause);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Doctor {DoctorId} paused OPD: {Reason}", me.Id, pause.Reason);
        return ToDto(pause);
    }

    public async Task<DoctorAvailabilityDto> BackAsync(CancellationToken ct)
    {
        var me = await RequireDoctorAsync(ct);
        var now = clock.UtcNow;
        foreach (var open in await db.DoctorBreaks.Where(x => x.DoctorId == me.Id && x.EndedAt == null).ToListAsync(ct))
        {
            open.EndedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Available;
    }

    /// <summary>Status and pace of the given doctors on a day, for the front desk queue.</summary>
    public async Task<IReadOnlyList<FrontDeskDoctorDto>> BoardAsync(
        IReadOnlyCollection<(long Id, string Name)> doctors, DateOnly day, CancellationToken ct)
    {
        if (doctors.Count == 0)
        {
            return [];
        }

        var ids = doctors.Select(d => d.Id).ToList();
        var dayStart = clock.StartOf(day);
        var dayEnd = clock.StartOf(day.AddDays(1));
        var recentStart = clock.StartOf(day.AddDays(-30));
        var isToday = day == clock.Today;

        var consults = await db.Encounters.AsNoTracking()
            .Where(e => ids.Contains(e.DoctorId) && e.StartedAt >= recentStart && e.StartedAt < dayEnd)
            .Select(e => new { e.DoctorId, e.StartedAt, e.CompletedAt })
            .ToListAsync(ct);

        var pauses = isToday
            ? await db.DoctorBreaks.AsNoTracking()
                .Where(x => ids.Contains(x.DoctorId) && x.EndedAt == null && x.StartedAt >= dayStart)
                .ToListAsync(ct)
            : new List<DoctorBreak>();

        return doctors.Select(d =>
            {
                var mine = consults.Where(c => c.DoctorId == d.Id).ToList();
                var todays = mine.Where(c => c.StartedAt >= dayStart).ToList();
                // Minutes per finished consultation; very short / very long ones (left open, forgotten) are ignored.
                var todayMinutes = todays
                    .Where(c => c.CompletedAt is not null)
                    .Select(c => (c.CompletedAt!.Value - c.StartedAt).TotalMinutes)
                    .Where(m => m is >= 1 and <= 90)
                    .ToList();
                var recentMinutes = mine
                    .Where(c => c.CompletedAt is not null)
                    .Select(c => (c.CompletedAt!.Value - c.StartedAt).TotalMinutes)
                    .Where(m => m is >= 1 and <= 90)
                    .ToList();

                var (avg, basis) = todayMinutes.Count >= 3 ? (todayMinutes.Average(), "TODAY")
                    : recentMinutes.Count >= 3 ? (recentMinutes.Average(), "RECENT")
                    : ((double)DefaultConsultMinutes, "DEFAULT");

                var pause = pauses.Where(x => x.DoctorId == d.Id).MaxBy(x => x.StartedAt);
                return new FrontDeskDoctorDto(
                    d.Id,
                    d.Name,
                    ToDto(pause),
                    Math.Max(1, (int)Math.Round(avg)),
                    basis,
                    todays.Count(c => c.CompletedAt is not null),
                    todays.Count > 0 ? todays.Min(c => c.StartedAt) : null,
                    todays.Where(c => c.CompletedAt is not null).Select(c => c.CompletedAt).Max());
            })
            .ToList();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Open pause started today (one left open from an earlier day doesn't count).</summary>
    private async Task<DoctorBreak?> OpenBreakAsync(long doctorId, CancellationToken ct)
    {
        var dayStart = clock.StartOf(clock.Today);
        return await db.DoctorBreaks.AsNoTracking()
            .Where(x => x.DoctorId == doctorId && x.EndedAt == null && x.StartedAt >= dayStart)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    private static DoctorAvailabilityDto ToDto(DoctorBreak? pause) =>
        pause is null
            ? Available
            : new DoctorAvailabilityDto(true, EnumText.ToCode(pause.Reason), pause.Note ?? "", pause.StartedAt, pause.ExpectedBackAt);

    private async Task<Doctor> RequireDoctorAsync(CancellationToken ct)
    {
        if (!currentUser.HasPermission("ENCOUNTER_EDIT"))
        {
            throw new ForbiddenException();
        }

        var userId = currentUser.UserId;
        return (userId is null ? null : await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, ct))
               ?? throw new ForbiddenException("Only doctors can set their availability.");
    }
}
