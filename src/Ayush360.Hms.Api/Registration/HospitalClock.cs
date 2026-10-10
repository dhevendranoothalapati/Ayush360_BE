namespace Ayush360.Hms.Api.Registration;

/// <summary>
/// "Today" and local times for the hospital. All demo hospitals are in India (Asia/Kolkata).
/// Later this will read core.tenants.time_zone (passed in the token) for hospitals in other time zones.
/// </summary>
public sealed class HospitalClock
{
    private readonly TimeZoneInfo _zone;

    public HospitalClock()
    {
        try
        {
            _zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            _zone = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "IST");
        }
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <summary>Current local date and time at the hospital.</summary>
    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _zone);

    /// <summary>Today's date at the hospital.</summary>
    public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);

    /// <summary>
    /// Midnight at the start of a local date, as an exact moment in UTC.
    /// (PostgreSQL timestamptz only accepts UTC values from Npgsql, so everything we hand to EF is UTC.)
    /// </summary>
    public DateTimeOffset StartOf(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Reads "2026-10-04T10:30" (browser datetime-local, hospital time) as an exact moment.</summary>
    public DateTimeOffset? ParseLocal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var withOffset) && value.Contains('Z', StringComparison.OrdinalIgnoreCase))
        {
            return withOffset.ToUniversalTime();
        }

        if (!DateTime.TryParse(value, out var local))
        {
            return null;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Formats a moment as "2026-10-04T10:30" in hospital time (for datetime-local inputs).</summary>
    public string ToLocalInput(DateTimeOffset? value) =>
        value is null ? "" : TimeZoneInfo.ConvertTime(value.Value, _zone).ToString("yyyy-MM-dd'T'HH:mm");
}
