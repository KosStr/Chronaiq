using Chronaiq.Domain.Entities;
using Chronaiq.Domain.Enums;

namespace Chronaiq.Application.Features.Tasks.Scheduling;

/// <summary>
/// Pure, deterministic scheduling heuristic — no EF, no I/O — so it can be unit-tested in
/// isolation and reused by the Schedule agent. It packs unscheduled tasks into each day's
/// preferred working window and, crucially, positions the highest-energy work at the time of
/// day that best fits the user's chronotype:
/// <list type="bullet">
///   <item><description><b>Morning Lark</b> — energy descending, so the hardest tasks land first (early).</description></item>
///   <item><description><b>Night Owl</b> — energy ascending, so the hardest tasks land last (late).</description></item>
///   <item><description><b>Intermediate</b> — peak-centered, hardest task around midday with lighter work flanking it.</description></item>
/// </list>
/// Times are treated as UTC wall-clock for this prototype (the schema stores no per-user
/// timezone); wiring an IANA zone per user is the natural next step.
/// </summary>
public static class ChronotypeScheduler
{
    /// <summary>
    /// Assigns <see cref="CalTask.ScheduledStart"/>/<see cref="CalTask.ScheduledEnd"/> in place.
    /// </summary>
    /// <param name="tasks">Candidate tasks (typically unscheduled and incomplete).</param>
    /// <param name="chronotype">Drives intra-day ordering.</param>
    /// <param name="workStart">Start of the daily working window.</param>
    /// <param name="workEnd">End of the daily working window.</param>
    /// <param name="firstDay">First calendar day to place work on (inclusive).</param>
    /// <param name="skipWeekends">When true, Saturdays and Sundays are left free.</param>
    /// <param name="maxDays">Safety bound on how many days the planner will span.</param>
    /// <returns>The tasks that were placed, in chronological order.</returns>
    public static IReadOnlyList<CalTask> Plan(
        IReadOnlyList<CalTask> tasks,
        Chronotype chronotype,
        TimeOnly workStart,
        TimeOnly workEnd,
        DateOnly firstDay,
        bool skipWeekends = true,
        int maxDays = 90)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        if (workEnd <= workStart)
        {
            throw new ArgumentException("Preferred work end must be after work start.", nameof(workEnd));
        }

        var windowMinutes = (int)(workEnd - workStart).TotalMinutes;

        // Global priority: hardest and longest work is placed first.
        var remaining = new Queue<CalTask>(tasks
            .OrderByDescending(t => t.EnergyRequirement)
            .ThenByDescending(t => t.DurationMinutes)
            .ThenBy(t => t.CreatedAt));

        var placed = new List<CalTask>(tasks.Count);
        var day = firstDay;

        for (var dayCount = 0; dayCount < maxDays && remaining.Count > 0; day = day.AddDays(1))
        {
            if (skipWeekends && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            dayCount++;

            // Greedily fill this day's capacity from the priority queue.
            var dayTasks = new List<CalTask>();
            var used = 0;
            while (remaining.Count > 0)
            {
                var next = remaining.Peek();
                var fitsFresh = dayTasks.Count == 0; // always place at least one task per day to guarantee progress.
                if (used + next.DurationMinutes <= windowMinutes || fitsFresh)
                {
                    dayTasks.Add(remaining.Dequeue());
                    used += next.DurationMinutes;
                }
                else
                {
                    break;
                }
            }

            // Order the day's tasks by time-of-day according to chronotype, then lay them end-to-end.
            var ordered = OrderWithinDay(dayTasks, chronotype);
            var cursor = new DateTimeOffset(day.ToDateTime(workStart), TimeSpan.Zero);
            foreach (var task in ordered)
            {
                task.ScheduledStart = cursor;
                task.ScheduledEnd = cursor.AddMinutes(task.DurationMinutes);
                cursor = task.ScheduledEnd.Value;
                placed.Add(task);
            }
        }

        return placed;
    }

    /// <summary>
    /// Reorders a single day's tasks so the highest-energy task occupies the chronotype's peak
    /// time-of-day. Input need not be pre-sorted.
    /// </summary>
    private static IReadOnlyList<CalTask> OrderWithinDay(IReadOnlyList<CalTask> dayTasks, Chronotype chronotype)
    {
        var byEnergyDesc = dayTasks
            .OrderByDescending(t => t.EnergyRequirement)
            .ThenByDescending(t => t.DurationMinutes)
            .ToList();

        return chronotype switch
        {
            // Hardest first → sits at the start of the morning window.
            Chronotype.MorningLark => byEnergyDesc,

            // Hardest last → sits at the end of the window (evening).
            Chronotype.NightOwl => Enumerable.Reverse(byEnergyDesc).ToList(),

            // Peak-centered: fold the energy-descending list outward from the middle.
            _ => CenterFold(byEnergyDesc)
        };
    }

    /// <summary>
    /// Places the first (highest-energy) item near the center and alternates the rest to the
    /// end/front, producing a "rise then fall" energy profile across the day.
    /// </summary>
    private static IReadOnlyList<CalTask> CenterFold(IReadOnlyList<CalTask> energyDesc)
    {
        var result = new LinkedList<CalTask>();
        for (var i = 0; i < energyDesc.Count; i++)
        {
            if (i % 2 == 0)
            {
                result.AddLast(energyDesc[i]);
            }
            else
            {
                result.AddFirst(energyDesc[i]);
            }
        }

        return result.ToList();
    }
}
