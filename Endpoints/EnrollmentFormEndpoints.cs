using System.Security.Claims;
using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Endpoints;

public static class EnrollmentFormEndpoints
{
    private const long UploadLimitBytes = 11 * 1024 * 1024;
    private const string StorageError = "No fue posible comunicarse con el almacenamiento de documentos.";

    private static readonly (string Key, PermissionFlags Flag)[] Manage =
    [
        (ProcessKeys.EnrollmentProcess, PermissionFlags.Create),
        (ProcessKeys.EnrollmentProcess, PermissionFlags.Update),
    ];

    public record CampusOptionDto(int CampusId, string Name);

    public record FormOptionsDto(
        Dictionary<string, List<CatalogOption>> Catalogs,
        List<CampusOptionDto> Campuses,
        List<GradeOptionDto> Grades);

    public static void MapEnrollmentFormEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/enrollment-process");

        group.MapGet("/form-options", (CatalogService catalogs, EnrollmentPeriodService periods, AppDbContext db) =>
                Guard(async () =>
                {
                    var campuses = await db.Campuses
                        .AsNoTracking()
                        .Where(c => c.Active)
                        .OrderBy(c => c.Name)
                        .Select(c => new CampusOptionDto(c.CampusId, c.Name))
                        .ToListAsync();

                    var options = await periods.GetOptionsAsync();
                    return Results.Ok(new FormOptionsDto(await catalogs.GetAllAsync(), campuses, options.Grades));
                }))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapGet("/people/{documentNumber}", (string documentNumber, EnrollmentPersonService people) =>
                Guard(async () => Results.Ok(await people.LookupAsync(documentNumber))))
            .RequireAnyPermission(Manage);

        group.MapGet("/admissions/{id:int}", (int id, AdmissionService svc) =>
                Guard(async () => Results.Ok(await svc.GetAsync(id, Today()))))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapPut("/admissions/{id:int}", (int id, SaveAdmissionRequest request, ClaimsPrincipal user, AdmissionService svc) =>
                Guard(async () =>
                {
                    if (CurrentUserId(user) is not { } userId)
                        return Results.Unauthorized();

                    await svc.UpdateAsync(id, request, userId);
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Update);

        group.MapPost("/admissions/{id:int}/documents/{detailId:int}",
                (int id, int detailId, IFormFile file, ClaimsPrincipal user, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        if (CurrentUserId(user) is not { } userId)
                            return Results.Unauthorized();

                        var owner = await docs.OwnerOfAdmissionAsync(id);
                        await docs.UploadAsync(owner, detailId, Upload(file), userId, DateTime.UtcNow);
                        return Results.Ok(await docs.ChecklistAsync(owner, Today()));
                    }))
            .RequireAnyPermission(Manage)
            .RequireClientHeader()
            .WithMetadata(new RequestSizeLimitAttribute(UploadLimitBytes))
            .DisableAntiforgery();

        group.MapDelete("/admissions/{id:int}/documents/{detailId:int}",
                (int id, int detailId, ClaimsPrincipal user, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        if (CurrentUserId(user) is not { } userId)
                            return Results.Unauthorized();

                        var owner = await docs.OwnerOfAdmissionAsync(id);
                        await docs.RemoveAsync(owner, detailId, userId, DateTime.UtcNow);
                        return Results.Ok(await docs.ChecklistAsync(owner, Today()));
                    }))
            .RequireAnyPermission(Manage);

        group.MapGet("/admissions/{id:int}/documents/{detailId:int}/file",
                (int id, int detailId, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        var (document, content) = await docs.OpenAsync(await docs.OwnerOfAdmissionAsync(id), detailId);
                        return Results.File(content, document.ContentType, document.FileName);
                    }))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapGet("/students/{studentId:int}/form", (int studentId, short yearId, EnrollmentFormService forms) =>
                Guard(async () => Results.Ok(await forms.GetAsync(studentId, yearId, Today()))))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapPut("/students/{studentId:int}/form", (int studentId, SaveStudentFormRequest request, EnrollmentFormService forms) =>
                Guard(async () =>
                {
                    await forms.SaveAsync(studentId, request, Today());
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Update);

        group.MapPost("/students/{studentId:int}/documents/{detailId:int}",
                (int studentId, int detailId, short yearId, IFormFile file, ClaimsPrincipal user, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        if (CurrentUserId(user) is not { } userId)
                            return Results.Unauthorized();

                        var owner = await docs.OwnerOfStudentAsync(studentId, yearId);
                        await docs.UploadAsync(owner, detailId, Upload(file), userId, DateTime.UtcNow);
                        return Results.Ok(await docs.ChecklistAsync(owner, Today()));
                    }))
            .RequireAnyPermission(Manage)
            .RequireClientHeader()
            .WithMetadata(new RequestSizeLimitAttribute(UploadLimitBytes))
            .DisableAntiforgery();

        group.MapDelete("/students/{studentId:int}/documents/{detailId:int}",
                (int studentId, int detailId, short yearId, ClaimsPrincipal user, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        if (CurrentUserId(user) is not { } userId)
                            return Results.Unauthorized();

                        var owner = await docs.OwnerOfStudentAsync(studentId, yearId);
                        await docs.RemoveAsync(owner, detailId, userId, DateTime.UtcNow);
                        return Results.Ok(await docs.ChecklistAsync(owner, Today()));
                    }))
            .RequireAnyPermission(Manage);

        group.MapGet("/students/{studentId:int}/documents/{detailId:int}/file",
                (int studentId, int detailId, short yearId, EnrollmentDocumentService docs) =>
                    Guard(async () =>
                    {
                        var (document, content) = await docs.OpenAsync(await docs.OwnerOfStudentAsync(studentId, yearId), detailId);
                        return Results.File(content, document.ContentType, document.FileName);
                    }))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapGet("/settings/documents", (EnrollmentDocumentService docs) =>
                Guard(async () => Results.Ok(await docs.GetSettingsAsync())))
            .RequirePermission(ProcessKeys.EnrollmentSettings, PermissionFlags.Read);

        group.MapPut("/settings/documents", (List<DocumentTypeSettingDto> items, EnrollmentDocumentService docs) =>
                Guard(async () => Results.Ok(await docs.SaveSettingsAsync(items))))
            .RequirePermission(ProcessKeys.EnrollmentSettings, PermissionFlags.Update);
    }

    private static DocumentUpload Upload(IFormFile file) =>
        new(file.FileName, file.ContentType, file.Length, file.OpenReadStream);

    private static DateOnly Today() => EnrollmentPeriodService.Today();

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Json(new { message = PermissionCatalog.DeniedMessage }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Otra persona modificó esta información al mismo tiempo. Intente de nuevo." });
        }
        catch (HttpRequestException)
        {
            return Results.BadRequest(new { message = StorageError });
        }
        catch (TaskCanceledException)
        {
            return Results.BadRequest(new { message = StorageError });
        }
    }
}
