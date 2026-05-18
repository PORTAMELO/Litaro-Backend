using Microsoft.EntityFrameworkCore;
using Litaro.Models;

namespace Litaro.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<School> Schools => Set<School>();

    public DbSet<Campus> Campuses => Set<Campus>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();

    public DbSet<User> Users => Set<User>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
            entity.Property(e => e.CreationDate).HasDefaultValueSql("SYSDATETIME()");
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
            entity.Property(e => e.Status).HasMaxLength(10).HasDefaultValue("ACTIVE");
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
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.DocumentType).HasMaxLength(5).IsRequired();
            entity.Property(e => e.DocumentNumber).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => new { e.DocumentType, e.DocumentNumber }).IsUnique();
            entity.HasIndex(e => e.DocumentNumber);
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreationDate).HasDefaultValueSql("SYSDATETIME()");
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
        });

        modelBuilder.Entity<Classroom>(entity =>
        {
            entity.ToTable("Classroom");
            entity.HasKey(e => e.ClassroomId);
            entity.Property(e => e.Name).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Campus)
                .WithMany()
                .HasForeignKey(e => e.CampusId)
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
            entity.ToTable("Schedule");
            entity.HasKey(e => e.ScheduleId);
            entity.Property(e => e.Weekday).IsRequired();
            entity.Property(e => e.StartTime).IsRequired();
            entity.Property(e => e.EndTime).IsRequired();
            entity.HasIndex(e => new { e.AssignmentId, e.Weekday, e.StartTime }).IsUnique();
            entity.HasOne(e => e.AcademicAssignment)
                .WithMany()
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollment");
            entity.HasKey(e => e.EnrollmentId);
            entity.Property(e => e.Status).HasMaxLength(12).HasDefaultValue("ACTIVE");
            entity.Property(e => e.EnrollmentDate).HasDefaultValueSql("CAST(GETDATE() AS DATE)");
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
            entity.Property(e => e.RecordedDate).HasDefaultValueSql("SYSDATETIME()");
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
            entity.Property(e => e.Date).HasDefaultValueSql("CAST(GETDATE() AS DATE)");
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

    }
}