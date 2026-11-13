namespace Litaro.Models
{
    public static class AttendanceSessionKinds
    {
        public const string Class = "CLASS";
        public const string Daily = "DAILY";
    }

    public static class AbsenceStatuses
    {
        public const string Absent = "ABSENT";
        public const string Evasion = "EVASION";
    }

    public static class EnrollmentStatuses
    {
        public const string Active = "ACTIVE";
        public const string Withdrawn = "WITHDRAWN";
    }

    public class AttendanceSession
    {
        public int AttendanceSessionId { get; set; }
        public short YearId { get; set; }
        public int ClassroomId { get; set; }
        public DateOnly Date { get; set; }
        public string Kind { get; set; } = AttendanceSessionKinds.Class;
        public int? AssignmentId { get; set; }
        public int? SubjectId { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public int TakenById { get; set; }
        public DateTime TakenAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedById { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Classroom Classroom { get; set; } = null!;
        public AcademicAssignment? AcademicAssignment { get; set; }
        public Subject? Subject { get; set; }
        public User TakenBy { get; set; } = null!;
        public User? UpdatedBy { get; set; }
    }

    public class AttendanceAbsence
    {
        public int AttendanceAbsenceId { get; set; }
        public int AttendanceSessionId { get; set; }
        public int EnrollmentId { get; set; }
        public string Status { get; set; } = AbsenceStatuses.Absent;
        public int? AttendanceExcuseId { get; set; }
        public AttendanceSession Session { get; set; } = null!;
        public Enrollment Enrollment { get; set; } = null!;
        public AttendanceExcuse? Excuse { get; set; }
    }

    public class ClassTardiness
    {
        public int ClassTardinessId { get; set; }
        public int AttendanceSessionId { get; set; }
        public int EnrollmentId { get; set; }
        public string Observation { get; set; } = string.Empty;
        public int RecordedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public AttendanceSession Session { get; set; } = null!;
        public Enrollment Enrollment { get; set; } = null!;
        public User RecordedBy { get; set; } = null!;
    }

    public class LateArrival
    {
        public int LateArrivalId { get; set; }
        public int EnrollmentId { get; set; }
        public int CampusId { get; set; }
        public DateOnly Date { get; set; }
        public DateTime ArrivedAt { get; set; } = DateTime.UtcNow;
        public int RecordedById { get; set; }
        public Enrollment Enrollment { get; set; } = null!;
        public Campus Campus { get; set; } = null!;
        public User RecordedBy { get; set; } = null!;
    }

    public class AttendanceExcuse
    {
        public int AttendanceExcuseId { get; set; }
        public int StudentId { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string? Reason { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Student Student { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
    }

    public class AbsenceLoss
    {
        public int AbsenceLossId { get; set; }
        public int EnrollmentId { get; set; }
        public int SubjectId { get; set; }
        public short PeriodId { get; set; }
        public decimal UnjustifiedHours { get; set; }
        public decimal PlannedHours { get; set; }
        public decimal Percent { get; set; }
        public DateTime FlaggedAt { get; set; } = DateTime.UtcNow;
        public Enrollment Enrollment { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public AcademicPeriod AcademicPeriod { get; set; } = null!;
    }

    public class PeriodPlannedHours
    {
        public int PeriodPlannedHoursId { get; set; }
        public short PeriodId { get; set; }
        public int ClassroomId { get; set; }
        public int SubjectId { get; set; }
        public decimal PlannedHours { get; set; }
        public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
        public AcademicPeriod AcademicPeriod { get; set; } = null!;
        public Classroom Classroom { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
    }

    public class JobRun
    {
        public string JobName { get; set; } = string.Empty;
        public DateOnly LastRunDate { get; set; }
    }
}
