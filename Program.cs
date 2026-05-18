using Microsoft.EntityFrameworkCore;
using Litaro.Data;
using Litaro.Services;
using Litaro.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ↓ Un línea por cada módulo nuevo
builder.Services.AddScoped<SchoolService>();
builder.Services.AddScoped<CampusService>();
builder.Services.AddScoped<AcademicYearService>();
builder.Services.AddScoped<AcademicPeriodService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<TeacherService>();
builder.Services.AddScoped<ParentService>();
builder.Services.AddScoped<ParentStudentService>();
builder.Services.AddScoped<GradeService>();
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddScoped<SubjectService>();
builder.Services.AddScoped<AcademicAssignmentService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<GradeScoreService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<StudentLogService>();

var app = builder.Build();

// ↓ Una línea por cada módulo nuevo
app.MapSchoolEndpoints();
app.MapCampusEndpoints();
app.MapAcademicYearEndpoints();
app.MapAcademicPeriodEndpoints();
app.MapUserEndpoints();
app.MapStudentEndpoints();
app.MapTeacherEndpoints();
app.MapParentEndpoints();
app.MapParentStudentEndpoints();
app.MapGradeEndpoints();
app.MapClassroomEndpoints();
app.MapSubjectEndpoints();
app.MapAcademicAssignmentEndpoints();
app.MapScheduleEndpoints();
app.MapEnrollmentEndpoints();
app.MapGradeScoreEndpoints();
app.MapAttendanceEndpoints();
app.MapStudentLogEndpoints();

app.Run();