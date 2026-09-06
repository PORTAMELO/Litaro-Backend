using System.Security.Claims;
using Litaro.Data;
using Litaro.Endpoints;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Base de datos
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(90); //Segundos
        }
    );
});

// Identity
builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole<int>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// Cookie Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "Litaro.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None; // cambiar a Strict si front y back comparten dominio
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    options.SlidingExpiration = true;

    // API: devolver 401/403 en JSON, no redirigir a HTML
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Autorización: todos los endpoints requieren auth por defecto
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",    // Vite dev
                "http://localhost:3000",     // Create React App dev
                "https://litaro-frontend.vercel.app"  // Frontend deploy
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Servicios de negocio
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
builder.Services.AddScoped<WebContentConfigurationService>();
builder.Services.AddScoped<WebContentService>();
builder.Services.AddScoped<SchemaService>();
builder.Services.AddScoped<ColumnConfigurationService>();
builder.Services.AddScoped<ForeignKeyResolverService>();
builder.Services.AddScoped<CharacteristicService>();
builder.Services.AddScoped<LookupService>();

var app = builder.Build();

// Pipeline
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Endpoints de auth
app.MapPost("/auth/login", async (LoginRequest req,
    SignInManager<User> signInManager,
    UserManager<User> userManager) =>
{
    var user = await userManager.FindByEmailAsync(req.Email);
    if (user is null)
        return Results.Json(new { step = "USER_NULL" }, statusCode: 401);

    if (!user.Active)
        return Results.Json(new { step = "USER_INACTIVE" }, statusCode: 401);

    var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
    if (!result.Succeeded)
        return Results.Json(new
        {
            step = "PASSWORD_CHECK_FAILED",
            isLockedOut = result.IsLockedOut,
            isNotAllowed = result.IsNotAllowed,
            requiresTwoFactor = result.RequiresTwoFactor
        }, statusCode: 401);

    if (user.MustChangePassword)
    {
        return Results.Json(new { step = "MUST_CHANGE_PASSWORD", userId = user.Id }, statusCode: 200);
    }

    var roles = await userManager.GetRolesAsync(user);
    var claims = new List<System.Security.Claims.Claim>
    {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Email, user.Email!),
            new(System.Security.Claims.ClaimTypes.GivenName, user.FirstName),
            new(System.Security.Claims.ClaimTypes.Surname, user.LastName)
    };
    claims.AddRange(roles.Select(r => new System.Security.Claims.Claim(
        System.Security.Claims.ClaimTypes.Role, r)));

    var principal = new System.Security.Claims.ClaimsPrincipal(
        new System.Security.Claims.ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await signInManager.Context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            IsPersistent = false
        });

    return Results.Ok(new { user.FirstName, user.LastName, user.Email, Role = roles.FirstOrDefault() });
})
.AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

app.MapGet("/auth/session", (HttpContext ctx) =>
{
    var user = ctx.User;

    if (user.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        Id = user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
        Email = user.FindFirstValue(System.Security.Claims.ClaimTypes.Email),
        Role = user.FindFirstValue(System.Security.Claims.ClaimTypes.Role)
    });
});

// Endpoints de negocio
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
app.MapWebContentConfigurationEndpoints();
app.MapWebContentEndpoints();
app.MapSchemaEndpoints();
app.MapColumnConfigurationEndpoints();
app.MapCharacteristicEndpoints();
app.MapLookupEndpoints();

app.Run();

record LoginRequest(string Email, string Password);