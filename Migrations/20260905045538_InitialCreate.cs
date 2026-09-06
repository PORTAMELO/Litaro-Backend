using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "AcademicYear",
                schema: "public",
                columns: table => new
                {
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYear", x => x.YearId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColumnConfigurations",
                schema: "public",
                columns: table => new
                {
                    ColumnConfigurationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableName = table.Column<string>(type: "text", nullable: false),
                    ColumnName = table.Column<string>(type: "text", nullable: false),
                    Filterable = table.Column<bool>(type: "boolean", nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnConfigurations", x => x.ColumnConfigurationId);
                });

            migrationBuilder.CreateTable(
                name: "Grade",
                schema: "public",
                columns: table => new
                {
                    GradeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrderNum = table.Column<byte>(type: "smallint", nullable: false),
                    Level = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grade", x => x.GradeId);
                });

            migrationBuilder.CreateTable(
                name: "School",
                schema: "public",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_School", x => x.SchoolId);
                });

            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "public",
                columns: table => new
                {
                    SubjectId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeeklyHours = table.Column<byte>(type: "smallint", nullable: false),
                    knowledgeArea = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subject", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "WebContentConfiguration",
                schema: "public",
                columns: table => new
                {
                    WebContentConfigurationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageName = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    MinItems = table.Column<int>(type: "integer", nullable: false),
                    MaxItems = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateJson = table.Column<string>(type: "text", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebContentConfiguration", x => x.WebContentConfigurationId);
                    table.UniqueConstraint("AK_WebContentConfiguration_PageName_SectionName_ContentKey", x => new { x.PageName, x.SectionName, x.ContentKey });
                });

            migrationBuilder.CreateTable(
                name: "AcademicPeriod",
                schema: "public",
                columns: table => new
                {
                    PeriodId = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodNumber = table.Column<byte>(type: "smallint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    YearId = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPeriod", x => x.PeriodId);
                    table.ForeignKey(
                        name: "FK_AcademicPeriod_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campus",
                schema: "public",
                columns: table => new
                {
                    CampusId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Dane = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SchoolId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campus", x => x.CampusId);
                    table.ForeignKey(
                        name: "FK_Campus_School_SchoolId",
                        column: x => x.SchoolId,
                        principalSchema: "public",
                        principalTable: "School",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebContent",
                schema: "public",
                columns: table => new
                {
                    WebContentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageName = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    SectionName = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebContent", x => x.WebContentId);
                    table.ForeignKey(
                        name: "FK_WebContent_WebContentConfiguration_PageName_SectionName_Con~",
                        columns: x => new { x.PageName, x.SectionName, x.ContentKey },
                        principalSchema: "public",
                        principalTable: "WebContentConfiguration",
                        principalColumns: new[] { "PageName", "SectionName", "ContentKey" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classroom",
                schema: "public",
                columns: table => new
                {
                    ClassroomId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    CampusId = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classroom", x => x.ClassroomId);
                    table.ForeignKey(
                        name: "FK_Classroom_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classroom_Grade_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "public",
                        principalTable: "Grade",
                        principalColumn: "GradeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    CampusId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "public",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parent",
                schema: "public",
                columns: table => new
                {
                    ParentId = table.Column<int>(type: "integer", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parent", x => x.ParentId);
                    table.ForeignKey(
                        name: "FK_Parent_User_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Student",
                schema: "public",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StudentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Gender = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.StudentId);
                    table.ForeignKey(
                        name: "FK_Student_User_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teacher",
                schema: "public",
                columns: table => new
                {
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    Specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teacher", x => x.TeacherId);
                    table.ForeignKey(
                        name: "FK_Teacher_User_TeacherId",
                        column: x => x.TeacherId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollment",
                schema: "public",
                columns: table => new
                {
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "ACTIVE"),
                    EnrollmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_DATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollment", x => x.EnrollmentId);
                    table.ForeignKey(
                        name: "FK_Enrollment_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollment_Classroom_ClassroomId",
                        column: x => x.ClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollment_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentStudent",
                schema: "public",
                columns: table => new
                {
                    ParentStudentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    PrimaryContact = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudent", x => x.ParentStudentId);
                    table.ForeignKey(
                        name: "FK_ParentStudent_Parent_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "public",
                        principalTable: "Parent",
                        principalColumn: "ParentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentStudent_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicAssignment",
                schema: "public",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicAssignment", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_AcademicAssignment_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicAssignment_Classroom_ClassroomId",
                        column: x => x.ClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicAssignment_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicAssignment_Teacher_TeacherId",
                        column: x => x.TeacherId,
                        principalSchema: "public",
                        principalTable: "Teacher",
                        principalColumn: "TeacherId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendance",
                schema: "public",
                columns: table => new
                {
                    AttendanceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Observation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendance_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendance_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeScore",
                schema: "public",
                columns: table => new
                {
                    GradeScoreId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    PeriodId = table.Column<short>(type: "smallint", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeScore", x => x.GradeScoreId);
                    table.ForeignKey(
                        name: "FK_GradeScore_AcademicPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "public",
                        principalTable: "AcademicPeriod",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeScore_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeScore_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentLog",
                schema: "public",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    Type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Observation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UserRecordedId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentLog", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_StudentLog_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentLog_User_UserRecordedId",
                        column: x => x.UserRecordedId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schedule",
                schema: "public",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<int>(type: "integer", nullable: false),
                    Weekday = table.Column<byte>(type: "smallint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedule", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_Schedule_AcademicAssignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "public",
                        principalTable: "AcademicAssignment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicAssignment_ClassroomId_SubjectId_YearId",
                schema: "public",
                table: "AcademicAssignment",
                columns: new[] { "ClassroomId", "SubjectId", "YearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicAssignment_SubjectId",
                schema: "public",
                table: "AcademicAssignment",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicAssignment_TeacherId",
                schema: "public",
                table: "AcademicAssignment",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicAssignment_YearId",
                schema: "public",
                table: "AcademicAssignment",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriod_PeriodNumber_YearId",
                schema: "public",
                table: "AcademicPeriod",
                columns: new[] { "PeriodNumber", "YearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriod_YearId",
                schema: "public",
                table: "AcademicPeriod",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "public",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "public",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "public",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "public",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "public",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Date",
                schema: "public",
                table: "Attendance",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_EnrollmentId_SubjectId_Date",
                schema: "public",
                table: "Attendance",
                columns: new[] { "EnrollmentId", "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_SubjectId",
                schema: "public",
                table: "Attendance",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Campus_SchoolId",
                schema: "public",
                table: "Campus",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Classroom_CampusId",
                schema: "public",
                table: "Classroom",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_Classroom_GradeId",
                schema: "public",
                table: "Classroom",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_ClassroomId_YearId",
                schema: "public",
                table: "Enrollment",
                columns: new[] { "ClassroomId", "YearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_StudentId",
                schema: "public",
                table: "Enrollment",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_StudentId_YearId",
                schema: "public",
                table: "Enrollment",
                columns: new[] { "StudentId", "YearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_YearId",
                schema: "public",
                table: "Enrollment",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_Grade_OrderNum",
                schema: "public",
                table: "Grade",
                column: "OrderNum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeScore_EnrollmentId_SubjectId_PeriodId",
                schema: "public",
                table: "GradeScore",
                columns: new[] { "EnrollmentId", "SubjectId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeScore_PeriodId",
                schema: "public",
                table: "GradeScore",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeScore_SubjectId",
                schema: "public",
                table: "GradeScore",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudent_ParentId_StudentId",
                schema: "public",
                table: "ParentStudent",
                columns: new[] { "ParentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudent_StudentId",
                schema: "public",
                table: "ParentStudent",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_AssignmentId_Weekday_StartTime",
                schema: "public",
                table: "Schedule",
                columns: new[] { "AssignmentId", "Weekday", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_School_Nit",
                schema: "public",
                table: "School",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_StudentCode",
                schema: "public",
                table: "Student",
                column: "StudentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentLog_EnrollmentId",
                schema: "public",
                table: "StudentLog",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLog_UserRecordedId",
                schema: "public",
                table: "StudentLog",
                column: "UserRecordedId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "public",
                table: "User",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_User_CampusId",
                schema: "public",
                table: "User",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_User_DocumentNumber",
                schema: "public",
                table: "User",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_User_DocumentType_DocumentNumber",
                schema: "public",
                table: "User",
                columns: new[] { "DocumentType", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "public",
                table: "User",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebContent_PageName_SectionName_ContentKey",
                schema: "public",
                table: "WebContent",
                columns: new[] { "PageName", "SectionName", "ContentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_WebContent_PageName_SectionName_DisplayOrder",
                schema: "public",
                table: "WebContent",
                columns: new[] { "PageName", "SectionName", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WebContentConfiguration_PageName_SectionName_ContentKey",
                schema: "public",
                table: "WebContentConfiguration",
                columns: new[] { "PageName", "SectionName", "ContentKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Attendance",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ColumnConfigurations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "GradeScore",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ParentStudent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Schedule",
                schema: "public");

            migrationBuilder.DropTable(
                name: "StudentLog",
                schema: "public");

            migrationBuilder.DropTable(
                name: "WebContent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AcademicPeriod",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Parent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AcademicAssignment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Enrollment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "WebContentConfiguration",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Teacher",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AcademicYear",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Classroom",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Student",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Grade",
                schema: "public");

            migrationBuilder.DropTable(
                name: "User",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Campus",
                schema: "public");

            migrationBuilder.DropTable(
                name: "School",
                schema: "public");
        }
    }
}
