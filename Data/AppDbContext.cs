using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<School> Schools => Set<School>();

    public DbSet<Campus> Campuses => Set<Campus>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();

    public new DbSet<User> Users => Set<User>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Parent> Parents => Set<Parent>();

    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<Classroom> Classrooms => Set<Classroom>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<AcademicAssignment> AcademicAssignments => Set<AcademicAssignment>();

    public DbSet<Schedule> Schedules => Set<Schedule>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<GradeScore> GradeScores => Set<GradeScore>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<StudentLog> StudentLogs => Set<StudentLog>();

    public DbSet<WebContent> WebContents => Set<WebContent>();

    public DbSet<WebContentConfiguration> WebContentConfigurations => Set<WebContentConfiguration>();

    public DbSet<ColumnConfiguration> ColumnConfigurations => Set<ColumnConfiguration>();

    public DbSet<Characteristic> Characteristics => Set<Characteristic>();

    public DbSet<CharacteristicDetail> CharacteristicDetails => Set<CharacteristicDetail>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Space> Spaces => Set<Space>();

    public DbSet<StudyPlan> StudyPlans => Set<StudyPlan>();

    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();

    public DbSet<EnrollmentPeriod> EnrollmentPeriods => Set<EnrollmentPeriod>();

    public DbSet<PreEnrollment> PreEnrollments => Set<PreEnrollment>();

    public DbSet<AdmissionRequest> AdmissionRequests => Set<AdmissionRequest>();

    public DbSet<AdmissionRequestEvent> AdmissionRequestEvents => Set<AdmissionRequestEvent>();

    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();

    public DbSet<NotificationTypeRecipient> NotificationTypeRecipients => Set<NotificationTypeRecipient>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();

    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();

    public DbSet<AttendanceAbsence> AttendanceAbsences => Set<AttendanceAbsence>();

    public DbSet<ClassTardiness> ClassTardinesses => Set<ClassTardiness>();

    public DbSet<LateArrival> LateArrivals => Set<LateArrival>();

    public DbSet<AttendanceExcuse> AttendanceExcuses => Set<AttendanceExcuse>();

    public DbSet<AbsenceLoss> AbsenceLosses => Set<AbsenceLoss>();

    public DbSet<PeriodPlannedHours> PeriodPlannedHours => Set<PeriodPlannedHours>();

    public DbSet<JobRun> JobRuns => Set<JobRun>();

    public DbSet<GradingScheme> GradingSchemes => Set<GradingScheme>();

    public DbSet<GradeActivity> GradeActivities => Set<GradeActivity>();

    public DbSet<ActivityScore> ActivityScores => Set<ActivityScore>();

    public DbSet<GradingDeadline> GradingDeadlines => Set<GradingDeadline>();

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("School");
            entity.HasKey(e => e.SchoolId);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Nit).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Nit).IsUnique();
            entity.Property(e => e.Address).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreationDate).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Campus>(entity =>
        {
            entity.ToTable("Campus");
            entity.HasKey(e => e.CampusId);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Active).HasDefaultValue(true);

            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);

        });

        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.ToTable("AcademicYear");
            entity.HasKey(e => e.YearId);
            entity.Property(e => e.YearId).ValueGeneratedNever();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
        });

        modelBuilder.Entity<AcademicPeriod>(entity =>
        {
            entity.ToTable("AcademicPeriod");
            entity.HasKey(e => e.PeriodId);
            entity.Property(e => e.PeriodNumber).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.HasIndex(e => new { e.PeriodNumber, e.YearId }).IsUnique();
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.Property(e => e.DocumentType).HasMaxLength(5).IsRequired();
            entity.Property(e => e.DocumentNumber).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => new { e.DocumentType, e.DocumentNumber }).IsUnique();
            entity.HasIndex(e => e.DocumentNumber);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreationDate).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Student");
            entity.HasKey(e => e.StudentId);
            entity.Property(e => e.StudentId).ValueGeneratedNever();
            entity.Property(e => e.StudentCode).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.StudentCode).IsUnique();
            entity.Property(e => e.BirthDate).IsRequired();
            entity.Property(e => e.Gender).HasMaxLength(1).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasIndex(e => e.SimatPerId).IsUnique();
            entity.Property(e => e.Neighborhood).HasMaxLength(100);
            entity.Property(e => e.Eps).HasMaxLength(100);
            entity.Property(e => e.BloodType).HasMaxLength(5);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teacher");
            entity.HasKey(e => e.TeacherId);
            entity.Property(e => e.TeacherId).ValueGeneratedNever();
            entity.Property(e => e.Specialty).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Parent>(entity =>
        {
            entity.ToTable("Parent");
            entity.HasKey(e => e.ParentId);
            entity.Property(e => e.ParentId).ValueGeneratedNever();
            entity.Property(e => e.Relationship).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParentStudent>(entity =>
        {
            entity.ToTable("ParentStudent");
            entity.HasKey(e => e.ParentStudentId);
            entity.Property(e => e.PrimaryContact).HasDefaultValue(true);
            entity.HasIndex(e => new { e.ParentId, e.StudentId }).IsUnique();
            entity.HasOne(e => e.Parent)
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.ToTable("Grade");
            entity.HasKey(e => e.GradeId);
            entity.Property(e => e.Name).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OrderNum).IsRequired();
            entity.HasIndex(e => e.OrderNum).IsUnique();
            entity.Property(e => e.Level).HasMaxLength(15).IsRequired();
            entity.HasIndex(e => e.SimatCode).IsUnique();
        });

        modelBuilder.Entity<Classroom>(entity =>
        {
            entity.ToTable("Classroom", t =>
                t.HasCheckConstraint("CK_Classroom_ClassMinutes",
                    "\"ClassMinutes\" IS NULL OR \"ClassMinutes\" BETWEEN 20 AND 180"));
            entity.HasKey(e => e.ClassroomId);
            entity.Property(e => e.Name).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DirectorTeacher)
                .WithMany()
                .HasForeignKey(e => e.DirectorTeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subject");
            entity.HasKey(e => e.SubjectId);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.WeeklyHours).IsRequired();
            entity.Property(e => e.KnowledgeArea).HasMaxLength(80).IsRequired()
                .HasColumnName("knowledgeArea");
        });

        modelBuilder.Entity<AcademicAssignment>(entity =>
        {
            entity.ToTable("AcademicAssignment");
            entity.HasKey(e => e.AssignmentId);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasIndex(e => new { e.ClassroomId, e.SubjectId, e.YearId }).IsUnique();
            entity.HasIndex(e => new { e.AssignmentId, e.TeacherId, e.ClassroomId })
                .IsUnique()
                .HasDatabaseName("UX_AcademicAssignment_Assignment_Teacher_Classroom");
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("Schedule", t =>
            {
                t.HasCheckConstraint("CK_Schedule_Weekday", "\"Weekday\" BETWEEN 1 AND 6");
                t.HasCheckConstraint("CK_Schedule_Time", "\"EndTime\" > \"StartTime\"");
                t.HasCheckConstraint("CK_Schedule_Type",
                    "\"Type\" IN ('CLASS','PARENT_ATTENTION','ACCOMPANIMENT','MEETING','OTHER')");
                t.HasCheckConstraint("CK_Schedule_ClassFields",
                    "(\"Type\" = 'CLASS' AND \"AssignmentId\" IS NOT NULL AND \"ClassroomId\" IS NOT NULL) OR " +
                    "(\"Type\" <> 'CLASS' AND \"AssignmentId\" IS NULL AND \"ClassroomId\" IS NULL)");
            });
            entity.HasKey(e => e.ScheduleId);
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired().HasDefaultValue(ScheduleTypes.Class);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Weekday).IsRequired();
            entity.Property(e => e.StartTime).IsRequired();
            entity.Property(e => e.EndTime).IsRequired();
            entity.HasIndex(e => new { e.YearId, e.TeacherId, e.Weekday });
            entity.HasIndex(e => new { e.YearId, e.ClassroomId, e.Weekday });
            entity.HasIndex(e => new { e.YearId, e.SpaceId, e.Weekday });
            entity.HasIndex(e => new { e.YearId, e.CampusId });
            entity.HasIndex(e => e.AssignmentId);
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicAssignment)
                .WithMany()
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Space)
                .WithMany()
                .HasForeignKey(e => e.SpaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollment");
            entity.HasKey(e => e.EnrollmentId);
            entity.Property(e => e.Status).HasMaxLength(12).HasDefaultValue("ACTIVE");
            entity.Property(e => e.StatusReason).HasMaxLength(150);
            entity.Property(e => e.EnrollmentDate).HasDefaultValueSql("CURRENT_DATE");
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => new { e.ClassroomId, e.YearId });
            entity.HasIndex(e => new { e.StudentId, e.YearId }).IsUnique();
            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GradeScore>(entity =>
        {
            entity.ToTable("GradeScore");
            entity.HasKey(e => e.GradeScoreId);
            entity.Property(e => e.Value).HasColumnType("decimal(4,2)").IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.RecordedDate).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.EnrollmentId, e.SubjectId, e.PeriodId }).IsUnique();
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicPeriod)
                .WithMany()
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendance");
            entity.HasKey(e => e.AttendanceId);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Observation).HasMaxLength(200);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.EnrollmentId, e.SubjectId, e.Date }).IsUnique();
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentLog>(entity =>
        {
            entity.ToTable("StudentLog");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.Date).HasDefaultValueSql("CURRENT_DATE");
            entity.Property(e => e.Type).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Observation).HasMaxLength(1000).IsRequired();
            entity.HasIndex(e => e.EnrollmentId);
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserRecordedId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WebContentConfiguration>(entity =>
        {
            entity.ToTable("WebContentConfiguration");
            entity.HasKey(e => e.WebContentConfigurationId);
            entity.Property(e => e.PageName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SectionName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContentKey)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.HasIndex(e => new
            {
                e.PageName,
                e.SectionName,
                e.ContentKey
            }).IsUnique();
        });


        modelBuilder.Entity<WebContent>(entity =>
        {
            entity.ToTable("WebContent");
            entity.HasKey(e => e.WebContentId);
            entity.Property(e => e.PageName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SectionName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContentKey)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.HasIndex(x => new
            {
                x.PageName,
                x.SectionName,
                x.DisplayOrder
            });
            entity.HasOne<WebContentConfiguration>()
                .WithMany()
                .HasForeignKey(x => new
                {
                    x.PageName,
                    x.SectionName,
                    x.ContentKey
                })
                .HasPrincipalKey(x => new
                {
                    x.PageName,
                    x.SectionName,
                    x.ContentKey
                });
        });

        modelBuilder.Entity<Characteristic>(entity =>
        {
            entity.ToTable("Characteristic");
            entity.HasKey(e => e.CharacteristicId);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.CreationDate).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<CharacteristicDetail>(entity =>
        {
            entity.ToTable("CharacteristicDetail");
            entity.HasKey(e => e.CharacteristicDetailId);
            entity.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Valor).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreationDate).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Characteristic)
                .WithMany()
                .HasForeignKey(e => e.CharacteristicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ColumnConfiguration>(entity =>
        {
            entity.HasOne(e => e.Characteristic)
                .WithMany()
                .HasForeignKey(e => e.CharacteristicId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermission", t =>
                t.HasCheckConstraint("CK_RolePermission_Permissions", "\"Permissions\" BETWEEN 0 AND 15"));
            entity.HasKey(e => e.RolePermissionId);
            entity.Property(e => e.TableName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.View).HasMaxLength(200);
            entity.HasIndex(e => new { e.RoleId, e.TableName }).IsUnique();
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Space>(entity =>
        {
            entity.ToTable("Space", t =>
                t.HasCheckConstraint("CK_Space_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0"));
            entity.HasKey(e => e.SpaceId);
            entity.Property(e => e.Name).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasIndex(e => new { e.CampusId, e.Name }).IsUnique();
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudyPlan>(entity =>
        {
            entity.ToTable("StudyPlan", t =>
                t.HasCheckConstraint("CK_StudyPlan_WeeklyHours", "\"WeeklyHours\" BETWEEN 1 AND 40"));
            entity.HasKey(e => e.StudyPlanId);
            entity.HasIndex(e => new { e.YearId, e.GradeId, e.SubjectId }).IsUnique();
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TeacherAvailability>(entity =>
        {
            entity.ToTable("TeacherAvailability", t =>
            {
                t.HasCheckConstraint("CK_TeacherAvailability_Weekday", "\"Weekday\" BETWEEN 1 AND 6");
                t.HasCheckConstraint("CK_TeacherAvailability_Time", "\"EndTime\" > \"StartTime\"");
            });
            entity.HasKey(e => e.TeacherAvailabilityId);
            entity.HasIndex(e => new { e.YearId, e.TeacherId, e.Weekday });
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Teacher)
                .WithMany()
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EnrollmentPeriod>(entity =>
        {
            entity.ToTable("EnrollmentPeriod", t =>
            {
                t.HasCheckConstraint("CK_EnrollmentPeriod_PreEnrollment", "\"PreEnrollmentEnd\" >= \"PreEnrollmentStart\"");
                t.HasCheckConstraint("CK_EnrollmentPeriod_Admission", "\"AdmissionEnd\" >= \"AdmissionStart\"");
            });
            entity.HasKey(e => e.EnrollmentPeriodId);
            entity.HasIndex(e => e.YearId).IsUnique();
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PreEnrollment>(entity =>
        {
            entity.ToTable("PreEnrollment", t =>
                t.HasCheckConstraint("CK_PreEnrollment_Status", "\"Status\" IN ('RESERVED','CONFIRMED','DECLINED','LOST')"));
            entity.HasKey(e => e.PreEnrollmentId);
            entity.Property(e => e.Status).HasMaxLength(12).IsRequired().HasDefaultValue(PreEnrollmentStatuses.Reserved);
            entity.Property(e => e.ResponseNote).HasMaxLength(300);
            entity.HasIndex(e => new { e.YearId, e.StudentId }).IsUnique();
            entity.HasIndex(e => new { e.YearId, e.Status });
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdmissionRequest>(entity =>
        {
            entity.ToTable("AdmissionRequest", t =>
                t.HasCheckConstraint("CK_AdmissionRequest_Status", "\"Status\" IN ('PENDING','APPROVED','REJECTED','WAITLIST')"));
            entity.HasKey(e => e.AdmissionRequestId);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DocumentType).HasMaxLength(5).IsRequired();
            entity.Property(e => e.DocumentNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.GuardianName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.GuardianPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(12).IsRequired().HasDefaultValue(AdmissionStatuses.Pending);
            entity.Property(e => e.DecisionNote).HasMaxLength(500);
            entity.HasIndex(e => new { e.YearId, e.Status });
            entity.HasIndex(e => new { e.YearId, e.DocumentNumber });
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DecidedBy)
                .WithMany()
                .HasForeignKey(e => e.DecidedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdmissionRequestEvent>(entity =>
        {
            entity.ToTable("AdmissionRequestEvent");
            entity.HasKey(e => e.AdmissionRequestEventId);
            entity.Property(e => e.FromStatus).HasMaxLength(12);
            entity.Property(e => e.ToStatus).HasMaxLength(12).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasIndex(e => e.AdmissionRequestId);
            entity.HasOne(e => e.AdmissionRequest)
                .WithMany()
                .HasForeignKey(e => e.AdmissionRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationType>(entity =>
        {
            entity.ToTable("NotificationType");
            entity.HasKey(e => e.NotificationTypeId);
            entity.Property(e => e.Code).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Template).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
        });

        modelBuilder.Entity<NotificationTypeRecipient>(entity =>
        {
            entity.ToTable("NotificationTypeRecipient", t =>
                t.HasCheckConstraint("CK_NotificationTypeRecipient_Target", "(\"RoleId\" IS NULL) <> (\"UserId\" IS NULL)"));
            entity.HasKey(e => e.NotificationTypeRecipientId);
            entity.HasIndex(e => new { e.NotificationTypeId, e.RoleId }).IsUnique();
            entity.HasIndex(e => new { e.NotificationTypeId, e.UserId }).IsUnique();
            entity.HasOne(e => e.NotificationType)
                .WithMany()
                .HasForeignKey(e => e.NotificationTypeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification", t =>
                t.HasCheckConstraint("CK_Notification_Status", "\"Status\" IN ('PENDING','DONE')"));
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.SourceTable).HasMaxLength(50);
            entity.Property(e => e.DedupKey).HasMaxLength(150);
            entity.Property(e => e.Status).HasMaxLength(10).IsRequired().HasDefaultValue(NotificationStatuses.Pending);
            entity.Property(e => e.DoneNote).HasMaxLength(300);
            entity.HasIndex(e => e.DedupKey).IsUnique().HasFilter("\"DedupKey\" IS NOT NULL");
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.NotificationTypeId, e.StudentId, e.YearId });
            entity.HasOne(e => e.NotificationType)
                .WithMany()
                .HasForeignKey(e => e.NotificationTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DoneBy)
                .WithMany()
                .HasForeignKey(e => e.DoneById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificationRead>(entity =>
        {
            entity.ToTable("NotificationRead");
            entity.HasKey(e => new { e.NotificationId, e.UserId });
            entity.HasOne(e => e.Notification)
                .WithMany()
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.ToTable("AttendanceSession", t =>
            {
                t.HasCheckConstraint("CK_AttendanceSession_Kind", "\"Kind\" IN ('CLASS','DAILY')");
                t.HasCheckConstraint("CK_AttendanceSession_Fields",
                    "(\"Kind\" = 'CLASS' AND \"AssignmentId\" IS NOT NULL AND \"SubjectId\" IS NOT NULL AND " +
                    "\"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND \"EndTime\" > \"StartTime\") OR " +
                    "(\"Kind\" = 'DAILY' AND \"AssignmentId\" IS NULL AND \"SubjectId\" IS NULL AND " +
                    "\"StartTime\" IS NULL AND \"EndTime\" IS NULL)");
            });
            entity.HasKey(e => e.AttendanceSessionId);
            entity.Property(e => e.Kind).HasMaxLength(5).IsRequired();
            entity.HasIndex(e => new { e.ClassroomId, e.Date, e.StartTime })
                .IsUnique()
                .HasFilter("\"Kind\" = 'CLASS'")
                .HasDatabaseName("UX_AttendanceSession_Class");
            entity.HasIndex(e => new { e.ClassroomId, e.Date })
                .IsUnique()
                .HasFilter("\"Kind\" = 'DAILY'")
                .HasDatabaseName("UX_AttendanceSession_Daily");
            entity.HasIndex(e => new { e.YearId, e.Date });
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicAssignment)
                .WithMany()
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TakenBy)
                .WithMany()
                .HasForeignKey(e => e.TakenById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceAbsence>(entity =>
        {
            entity.ToTable("AttendanceAbsence", t =>
                t.HasCheckConstraint("CK_AttendanceAbsence_Status", "\"Status\" IN ('ABSENT','EVASION')"));
            entity.HasKey(e => e.AttendanceAbsenceId);
            entity.Property(e => e.Status).HasMaxLength(8).IsRequired();
            entity.HasIndex(e => new { e.AttendanceSessionId, e.EnrollmentId }).IsUnique();
            entity.HasIndex(e => e.EnrollmentId);
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Excuse)
                .WithMany()
                .HasForeignKey(e => e.AttendanceExcuseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClassTardiness>(entity =>
        {
            entity.ToTable("ClassTardiness");
            entity.HasKey(e => e.ClassTardinessId);
            entity.Property(e => e.Observation).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => new { e.AttendanceSessionId, e.EnrollmentId }).IsUnique();
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.AttendanceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RecordedBy)
                .WithMany()
                .HasForeignKey(e => e.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LateArrival>(entity =>
        {
            entity.ToTable("LateArrival");
            entity.HasKey(e => e.LateArrivalId);
            entity.HasIndex(e => new { e.EnrollmentId, e.Date }).IsUnique();
            entity.HasIndex(e => e.Date);
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RecordedBy)
                .WithMany()
                .HasForeignKey(e => e.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceExcuse>(entity =>
        {
            entity.ToTable("AttendanceExcuse", t =>
                t.HasCheckConstraint("CK_AttendanceExcuse_Range", "\"EndAt\" > \"StartAt\""));
            entity.HasKey(e => e.AttendanceExcuseId);
            entity.Property(e => e.StartAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Reason).HasMaxLength(300);
            entity.HasIndex(e => e.StudentId);
            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AbsenceLoss>(entity =>
        {
            entity.ToTable("AbsenceLoss");
            entity.HasKey(e => e.AbsenceLossId);
            entity.Property(e => e.UnjustifiedHours).HasColumnType("numeric(7,2)");
            entity.Property(e => e.PlannedHours).HasColumnType("numeric(7,2)");
            entity.Property(e => e.Percent).HasColumnType("numeric(7,2)");
            entity.HasIndex(e => new { e.EnrollmentId, e.SubjectId, e.PeriodId }).IsUnique();
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicPeriod)
                .WithMany()
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PeriodPlannedHours>(entity =>
        {
            entity.ToTable("PeriodPlannedHours");
            entity.HasKey(e => e.PeriodPlannedHoursId);
            entity.Property(e => e.PlannedHours).HasColumnType("numeric(7,2)");
            entity.HasIndex(e => new { e.PeriodId, e.ClassroomId, e.SubjectId }).IsUnique();
            entity.HasOne(e => e.AcademicPeriod)
                .WithMany()
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JobRun>(entity =>
        {
            entity.ToTable("JobRun");
            entity.HasKey(e => e.JobName);
            entity.Property(e => e.JobName).HasMaxLength(50);
        });

        modelBuilder.Entity<GradingScheme>(entity =>
        {
            entity.ToTable("GradingScheme", t =>
                t.HasCheckConstraint("CK_GradingScheme_Mode", "\"Mode\" IN ('WEIGHTED','AVERAGE')"));
            entity.HasKey(e => e.GradingSchemeId);
            entity.Property(e => e.Mode).HasMaxLength(10);
            entity.HasIndex(e => new { e.ClassroomId, e.SubjectId, e.PeriodId }).IsUnique();
            entity.HasOne(e => e.Classroom)
                .WithMany()
                .HasForeignKey(e => e.ClassroomId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicPeriod)
                .WithMany()
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GradeActivity>(entity =>
        {
            entity.ToTable("GradeActivity", t =>
            {
                t.HasCheckConstraint("CK_GradeActivity_Component", "\"Component\" IN ('HETERO','SABER','SELF','PEER')");
                t.HasCheckConstraint("CK_GradeActivity_Weight", "\"Weight\" IS NULL OR (\"Weight\" > 0 AND \"Weight\" <= 100)");
            });
            entity.HasKey(e => e.GradeActivityId);
            entity.Property(e => e.Component).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Competencies).HasMaxLength(1000);
            entity.Property(e => e.Weight).HasColumnType("numeric(5,2)");
            entity.HasIndex(e => e.GradingSchemeId);
            entity.HasIndex(e => new { e.GradingSchemeId, e.Component })
                .IsUnique()
                .HasFilter("\"Component\" <> 'HETERO'");
            entity.HasOne(e => e.GradingScheme)
                .WithMany(s => s.Activities)
                .HasForeignKey(e => e.GradingSchemeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActivityScore>(entity =>
        {
            entity.ToTable("ActivityScore", t =>
                t.HasCheckConstraint("CK_ActivityScore_Value", "\"Value\" BETWEEN 10 AND 50"));
            entity.HasKey(e => e.ActivityScoreId);
            entity.Property(e => e.Value).HasColumnType("numeric(3,1)");
            entity.HasIndex(e => new { e.GradeActivityId, e.EnrollmentId }).IsUnique();
            entity.HasIndex(e => e.EnrollmentId);
            entity.HasOne(e => e.GradeActivity)
                .WithMany(a => a.Scores)
                .HasForeignKey(e => e.GradeActivityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Enrollment)
                .WithMany()
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RecordedBy)
                .WithMany()
                .HasForeignKey(e => e.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GradingDeadline>(entity =>
        {
            entity.ToTable("GradingDeadline", t =>
            {
                t.HasCheckConstraint("CK_GradingDeadline_Kind", "\"Kind\" IN ('PLANNER','DESIGN_LOCK')");
                t.HasCheckConstraint("CK_GradingDeadline_Period", "(\"Kind\" = 'PLANNER') = (\"PeriodId\" IS NULL)");
            });
            entity.HasKey(e => e.GradingDeadlineId);
            entity.Property(e => e.Kind).HasMaxLength(20);
            entity.HasIndex(e => new { e.YearId, e.Kind, e.PeriodId }).IsUnique().AreNullsDistinct(false);
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcademicPeriod)
                .WithMany()
                .HasForeignKey(e => e.PeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UpdatedBy)
                .WithMany()
                .HasForeignKey(e => e.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Document");
            entity.HasKey(e => e.DocumentId);
            entity.Property(e => e.Module).HasMaxLength(40);
            entity.Property(e => e.DocumentType).HasMaxLength(40);
            entity.Property(e => e.EntityTable).HasMaxLength(50);
            entity.Property(e => e.StoragePath).HasMaxLength(300);
            entity.Property(e => e.FileName).HasMaxLength(200);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.HasIndex(e => e.StoragePath).IsUnique();
            entity.HasIndex(e => new { e.DocumentType, e.EntityTable, e.EntityId })
                .IsUnique()
                .HasFilter("\"IsCurrent\"");
            entity.HasOne(e => e.AcademicYear)
                .WithMany()
                .HasForeignKey(e => e.YearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReplacedBy)
                .WithMany()
                .HasForeignKey(e => e.ReplacedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}