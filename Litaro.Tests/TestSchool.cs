using Litaro.Data;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public sealed class TestSchool
{
    public const short YearId = 2026;

    public static readonly TimeOnly MathStart = new(6, 30);
    public static readonly TimeOnly ChemistryStart = new(7, 25);
    public static readonly TimeOnly PhysicsStart = new(9, 0);

    public required AppDbContext Db { get; init; }
    public required AcademicPeriod Period { get; init; }
    public required AcademicPeriod ClosedPeriod { get; init; }
    public required Campus Campus { get; init; }
    public required Classroom C1001 { get; init; }
    public required Classroom C3A { get; init; }
    public required Subject Math { get; init; }
    public required Subject Chemistry { get; init; }
    public required Subject Physics { get; init; }
    public required User T1 { get; init; }
    public required User T2 { get; init; }
    public required User Marta { get; init; }
    public required Schedule PhysicsBlock { get; init; }
    public required Enrollment Ana { get; init; }
    public required Enrollment Beto { get; init; }
    public required Enrollment Caro { get; init; }
    public required Enrollment Dani { get; init; }

    public DateOnly Tuesday { get; } = new(2026, 11, 10);

    public static async Task<TestSchool> CreateAsync(AppDbContext db)
    {
        db.AcademicYears.Add(new AcademicYear
        {
            YearId = YearId,
            StartDate = Utc(2026, 1, 19),
            EndDate = Utc(2026, 11, 27),
            Active = true,
        });

        var closedPeriod = new AcademicPeriod { PeriodNumber = 3, StartDate = Utc(2026, 7, 6), EndDate = Utc(2026, 9, 25), YearId = YearId };
        var period = new AcademicPeriod { PeriodNumber = 4, StartDate = Utc(2026, 10, 5), EndDate = Utc(2026, 11, 27), YearId = YearId };
        var campus = new Campus { Name = "Sede Principal", Address = "Calle 4", Dane = "125875000001", SchoolId = 1 };
        var tenth = new Grade { Name = "Décimo", OrderNum = 10, Level = "Media" };
        var third = new Grade { Name = "Tercero", OrderNum = 3, Level = "Primaria" };
        var math = new Subject { Name = "Matemáticas", WeeklyHours = 5, KnowledgeArea = "Exactas" };
        var chemistry = new Subject { Name = "Química", WeeklyHours = 3, KnowledgeArea = "Naturales" };
        var physics = new Subject { Name = "Física", WeeklyHours = 3, KnowledgeArea = "Naturales" };

        db.AddRange(closedPeriod, period, campus, tenth, third, math, chemistry, physics);
        await db.SaveChangesAsync();

        var coord = TestData.AddRole(db, "Coord");
        var marta = TestData.AddUser(db, "Marta", "Rojas", coord);
        var t1 = TestData.AddUser(db, "Tomás", "Uno");
        var t2 = TestData.AddUser(db, "Tania", "Dos");

        db.Teachers.AddRange(
            new Teacher { TeacherId = t1.Id, Specialty = "Física" },
            new Teacher { TeacherId = t2.Id, Specialty = "Matemáticas" });

        var c1001 = new Classroom { Name = "1001", GradeId = tenth.GradeId, CampusId = campus.CampusId, ClassMinutes = 55 };
        var c3a = new Classroom { Name = "3A", GradeId = third.GradeId, CampusId = campus.CampusId, DirectorTeacherId = t1.Id };
        db.AddRange(c1001, c3a);
        await db.SaveChangesAsync();

        AcademicAssignment Assign(Classroom classroom, Subject subject, User teacher) => new()
        {
            ClassroomId = classroom.ClassroomId,
            SubjectId = subject.SubjectId,
            TeacherId = teacher.Id,
            YearId = YearId,
        };

        var math1001 = Assign(c1001, math, t2);
        var chemistry1001 = Assign(c1001, chemistry, t2);
        var physics1001 = Assign(c1001, physics, t1);
        var math3a = Assign(c3a, math, t1);
        var chemistry3a = Assign(c3a, chemistry, t1);
        db.AddRange(math1001, chemistry1001, physics1001, math3a, chemistry3a);
        await db.SaveChangesAsync();

        Schedule Block(AcademicAssignment assignment, TimeOnly start, TimeOnly end) => new()
        {
            YearId = YearId,
            CampusId = campus.CampusId,
            Type = ScheduleTypes.Class,
            Weekday = 2,
            StartTime = start,
            EndTime = end,
            TeacherId = assignment.TeacherId,
            AssignmentId = assignment.AssignmentId,
            ClassroomId = assignment.ClassroomId,
        };

        var physicsBlock = Block(physics1001, PhysicsStart, new TimeOnly(9, 55));
        db.AddRange(
            Block(math1001, MathStart, new TimeOnly(7, 25)),
            Block(chemistry1001, ChemistryStart, new TimeOnly(8, 20)),
            physicsBlock,
            Block(math3a, new TimeOnly(7, 0), new TimeOnly(8, 0)),
            Block(chemistry3a, new TimeOnly(8, 0), new TimeOnly(10, 0)));
        await db.SaveChangesAsync();

        Enrollment Enroll(string firstName, string lastName, Classroom classroom)
        {
            var user = TestData.AddUser(db, firstName, lastName);
            db.Students.Add(new Student { StudentId = user.Id, StudentCode = $"S-{user.Id}", Gender = 'F' });
            var enrollment = new Enrollment { StudentId = user.Id, ClassroomId = classroom.ClassroomId, YearId = YearId };
            db.Enrollments.Add(enrollment);
            db.SaveChanges();
            return enrollment;
        }

        var ana = Enroll("Ana", "Ruiz", c1001);
        var beto = Enroll("Beto", "Paz", c1001);
        var caro = Enroll("Caro", "Lima", c1001);
        var dani = Enroll("Dani", "Sol", c3a);

        TestData.AddNotificationType(db, NotificationCodes.AttEvasion,
            "{estudiante} ({curso}) no asistió a {materia} a las {hora}.", coord);
        TestData.AddNotificationType(db, NotificationCodes.AttClassTardy,
            "{estudiante} ({curso}) llegó tarde a {materia}. Sanción pedagógica: {observacion}", coord);
        TestData.AddNotificationType(db, NotificationCodes.AttLateCommitment,
            "{estudiante} ({curso}) completó {cantidad} llegadas tarde en el periodo {periodo}. Genere el compromiso para firma del acudiente.", coord);
        TestData.AddNotificationType(db, NotificationCodes.AttLateCitation,
            "{estudiante} ({curso}) completó {cantidad} llegadas tarde en el periodo {periodo}. Cite al acudiente.", coord);
        TestData.AddNotificationType(db, NotificationCodes.AttWeekAbsence,
            "{estudiante} ({curso}) completó {dias} días seguidos de inasistencia injustificada. Contacte a la familia.", coord);
        TestData.AddNotificationType(db, NotificationCodes.AttPeriodAbsence,
            "{estudiante} ({curso}) no asistió durante todo el periodo {periodo} sin justificación. Inicie el proceso ante las entidades competentes y gestione el retiro del SIMAT.", coord);

        await new AttendanceSettingsService(db).SaveAsync(new AttendanceSettings(20m, 1.0m, 2, 3, 5, 5, 23, ["Primaria"]));

        return new TestSchool
        {
            Db = db,
            Period = period,
            ClosedPeriod = closedPeriod,
            Campus = campus,
            C1001 = c1001,
            C3A = c3a,
            Math = math,
            Chemistry = chemistry,
            Physics = physics,
            T1 = t1,
            T2 = t2,
            Marta = marta,
            PhysicsBlock = physicsBlock,
            Ana = ana,
            Beto = beto,
            Caro = caro,
            Dani = dani,
        };
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
