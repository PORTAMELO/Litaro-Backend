namespace Litaro.Services;

public record PromotionSettings(
    decimal Period1Weight,
    decimal Period2Weight,
    decimal Period3Weight,
    decimal ExceptionMinGrade,
    decimal ExceptionRestAverage,
    decimal ExceptionAttendancePercent,
    int PiarLostAreas,
    List<int> FundamentalSubjectIds);

public record SubjectFinal(int SubjectId, string Name, string Area, decimal Hours, decimal? Final, bool LostLastYear = false);

public record AreaFinal(string Area, decimal Final, bool Passed);

public record PromotionInput(
    byte GradeOrder,
    bool Preschool,
    bool HasPiar,
    IReadOnlyList<SubjectFinal> Subjects,
    decimal AttendancePercent);

public record PromotionOutcome(string Result, string Rule, List<AreaFinal> Areas, List<string> LostSubjects, bool UsedException);

public static class PromotionRules
{
    public const string Promoted = "PROMOTED";
    public const string Failed = "FAILED";
    public const string Pending = "PENDING";

    public const byte EleventhGrade = 11;

    private static readonly byte[] FundamentalGrades = [1, 2];
    private static readonly byte[] NoExceptionGrades = [1, 5, 11];
    private static readonly byte[] NoEarlyPromotionGrades = [9, 10, 11];

    public static bool AllowsEarlyPromotion(byte gradeOrder) => !NoEarlyPromotionGrades.Contains(gradeOrder);

    public static decimal? YearFinal(IReadOnlyList<(decimal Weight, decimal? Final)> periods)
    {
        if (periods.Count == 0 || periods.Any(p => p.Final is null))
            return null;

        var weight = periods.Sum(p => p.Weight);
        return weight == 0m ? null : GradingRules.Round(periods.Sum(p => p.Weight * p.Final!.Value) / weight);
    }

    public static List<AreaFinal> Areas(IEnumerable<SubjectFinal> subjects) =>
        subjects
            .Where(s => s.Final is not null)
            .GroupBy(s => s.Area)
            .Select(group =>
            {
                var hours = group.Sum(s => s.Hours);
                var final = hours == 0m
                    ? group.Average(s => s.Final!.Value)
                    : group.Sum(s => s.Hours * s.Final!.Value) / hours;
                var rounded = GradingRules.Round(final);
                return new AreaFinal(group.Key, rounded, GradingRules.Passes(rounded));
            })
            .OrderBy(a => a.Area)
            .ToList();

    public static PromotionOutcome Evaluate(PromotionInput input, PromotionSettings settings)
    {
        if (input.Preschool)
            return new PromotionOutcome(Promoted, "En preescolar no se reprueba el grado (SIEE Art. 8).", [], [], false);

        if (input.Subjects.Count == 0)
            return new PromotionOutcome(Pending, "El curso no tiene asignaturas asignadas.", [], [], false);

        var areas = Areas(input.Subjects);

        if (input.Subjects.Any(s => s.Final is null))
            return new PromotionOutcome(Pending, "Faltan notas definitivas en una o más asignaturas.", areas, [], false);

        var lostSubjects = input.Subjects.Where(s => !GradingRules.Passes(s.Final!.Value)).ToList();
        var lostNames = lostSubjects.Select(s => s.Name).ToList();
        var lostAreas = areas.Where(a => !a.Passed).ToList();

        PromotionOutcome Outcome(string result, string rule, bool exception = false) =>
            new(result, rule, areas, lostNames, exception);

        if (input.HasPiar)
        {
            return lostAreas.Count >= settings.PiarLostAreas
                ? Outcome(Failed, $"Estudiante con PIAR con {lostAreas.Count} áreas perdidas (SIEE Art. 18.2).")
                : Outcome(Promoted, $"Estudiante con PIAR con menos de {settings.PiarLostAreas} áreas perdidas (SIEE Art. 18.2).");
        }

        if (FundamentalGrades.Contains(input.GradeOrder))
        {
            var fundamentals = input.Subjects.Where(s => settings.FundamentalSubjectIds.Contains(s.SubjectId)).ToList();

            if (fundamentals.Count == 0)
                return Outcome(Pending, "Configure las asignaturas fundamentales en los parámetros de promoción.");

            return fundamentals.Any(s => !GradingRules.Passes(s.Final!.Value))
                ? Outcome(Failed, "Perdió una asignatura fundamental de 1° y 2° (SIEE Art. 9).")
                : Outcome(Promoted, "Aprobó las asignaturas fundamentales de 1° y 2° (SIEE Art. 9).");
        }

        if (input.GradeOrder >= EleventhGrade)
        {
            return lostAreas.Count == 0 && lostSubjects.Count == 0
                ? Outcome(Promoted, "Aprobó todas las áreas y asignaturas (SIEE Art. 9).")
                : Outcome(Failed, "En grado once debe aprobar todas las áreas y asignaturas (SIEE Art. 9).");
        }

        if (lostAreas.Count == 0)
            return Outcome(Promoted, "Aprobó todas las áreas (SIEE Art. 9).");

        if (MeetsException(input, settings, lostSubjects, lostAreas, areas))
            return Outcome(Promoted, "Promovido con una asignatura perdida (SIEE Art. 9, parágrafo uno).", true);

        return Outcome(Failed, $"Desempeño bajo en {lostAreas.Count} {(lostAreas.Count == 1 ? "área" : "áreas")} (SIEE Art. 11).");
    }

    private static bool MeetsException(
        PromotionInput input,
        PromotionSettings settings,
        List<SubjectFinal> lostSubjects,
        List<AreaFinal> lostAreas,
        List<AreaFinal> areas)
    {
        if (NoExceptionGrades.Contains(input.GradeOrder) || lostSubjects.Count != 1 || lostAreas.Count != 1)
            return false;

        var lost = lostSubjects[0];
        if (lost.Final!.Value < settings.ExceptionMinGrade || lost.LostLastYear)
            return false;

        var rest = areas.Where(a => a.Passed).ToList();
        if (rest.Count > 0 && rest.Average(a => a.Final) < settings.ExceptionRestAverage)
            return false;

        return input.AttendancePercent >= settings.ExceptionAttendancePercent;
    }
}
