namespace Chronaiq.Domain.Enums;

/// <summary>
/// A user's circadian preference. Persisted as <c>int</c> to match the
/// <c>"Chronotype"</c> column, and consumed by the Schedule agent when it places
/// energy-demanding work into the day.
/// </summary>
public enum Chronotype
{
    MorningLark = 0,
    NightOwl = 1,
    Intermediate = 2
}
