using System.Globalization;
using System.Text;

namespace Litaro.Services;

public record ScheduleBlock(byte Weekday, int SubjectId, TimeOnly StartTime, TimeOnly EndTime);

public static class AttendanceRules
{
    private const int DefaultClassMinutes = 60;

    public static decimal Hours(TimeOnly start, TimeOnly end, short? classMinutes)
    {
        var minutes = (decimal)(end - start).TotalMinutes;
        return minutes / (classMinutes ?? DefaultClassMinutes);
    }

    public static byte WeekdayOf(DateOnly date) => (byte)date.DayOfWeek;

    public static Dictionary<int, decimal> PlannedHours(
        IEnumerable<ScheduleBlock> blocks,
        DateOnly from,
        DateOnly to,
        short? classMinutes)
    {
        var byWeekday = blocks.ToLookup(block => block.Weekday);
        var planned = new Dictionary<int, decimal>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var block in byWeekday[WeekdayOf(date)])
            {
                planned[block.SubjectId] = planned.GetValueOrDefault(block.SubjectId) +
                                           Hours(block.StartTime, block.EndTime, classMinutes);
            }
        }

        return planned;
    }

    public static decimal Percent(decimal unjustified, decimal planned) =>
        planned <= 0 ? 0 : Math.Round(unjustified / planned * 100, 2);

    public static bool IsLoss(decimal percent, decimal lossPercent) => percent >= lossPercent;

    public static bool IsEvasion(TimeOnly sessionStart, IEnumerable<(TimeOnly Start, bool Absent)> takenSessionsThatDay) =>
        takenSessionsThatDay.Any(session => session.Start < sessionStart && !session.Absent);

    public static bool IsFirstClassOfDay(TimeOnly start, IEnumerable<TimeOnly> classStartsThatDay) =>
        !classStartsThatDay.Any(other => other < start);

    public static DateOnly ExcuseDeadline(DateOnly rangeEnd, int businessDays)
    {
        var date = rangeEnd;
        var remaining = businessDays;

        while (remaining > 0)
        {
            date = date.AddDays(1);

            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                remaining--;
        }

        return date;
    }

    public static List<DateOnly> AbsenceStreakStarts(IReadOnlyList<(DateOnly Day, bool FullyAbsent)> schoolDays, int length)
    {
        var starts = new List<DateOnly>();
        var run = 0;
        var runStart = default(DateOnly);

        foreach (var (day, fullyAbsent) in schoolDays)
        {
            if (!fullyAbsent)
            {
                run = 0;
                continue;
            }

            if (run == 0)
                runStart = day;

            run++;

            if (run == length)
                starts.Add(runStart);
        }

        return starts;
    }

    public static bool IsDailyLevel(string? gradeLevel, IEnumerable<string> dailyLevels)
    {
        if (string.IsNullOrWhiteSpace(gradeLevel))
            return false;

        var level = Plain(gradeLevel);
        return dailyLevels.Any(candidate => Plain(candidate) == level);
    }

    public static DateOnly NightlyRunDate(DateTime nowLocal, int hour)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        return nowLocal.Hour >= hour ? today : today.AddDays(-1);
    }

    public static bool ShouldRunNightly(DateTime nowLocal, DateOnly? lastRunDate, int hour) =>
        lastRunDate is null || lastRunDate < NightlyRunDate(nowLocal, hour);

    private static string Plain(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString();
    }
}
