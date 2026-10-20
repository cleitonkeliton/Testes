using System.Globalization;

namespace Cstic.KnowledgeBase.Services;

/// <summary>Dates are stored in UTC and shown in Recife time (UTC−3, no daylight saving).</summary>
public static class Dates
{
    public static string Format(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
        .ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Format(DateTime? utc) => utc is null ? "—" : Format(utc.Value);
}
