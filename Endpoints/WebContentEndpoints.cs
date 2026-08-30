using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class WebContentEndpoints
    {
        public static void MapWebContentEndpoints(this WebApplication app)
        {
            app.MapGet(
                "/webcontents/admin",
                async (WebContentService svc) =>
                    Results.Ok(await svc.GetAllForAdminAsync())).AllowAnonymous();

            app.MapGet("/webcontents", async (WebContentService svc) =>
                Results.Ok(await svc.GetAllAsync())).AllowAnonymous();

            app.MapGet("/webcontents/{id}", async (int id, WebContentService svc) =>
                await svc.GetByIdAsync(id) is WebContent content
                    ? Results.Ok(content)
                    : Results.NotFound()).AllowAnonymous();

            app.MapGet(
                "/webcontents/{pageName}/{sectionName}/{contentKey}",
                async (
                    string pageName,
                    string sectionName,
                    string contentKey,
                    WebContentService svc) =>
                {
                    var contents = await svc.GetByPageSectionContentKeyAsync(
                        pageName,
                        sectionName,
                        contentKey);

                    return Results.Ok(contents);
                }).AllowAnonymous();

            app.MapPost("/webcontents", async (WebContent content, WebContentService svc) =>
                {
                    var (result, created) = await svc.CreateAsync(content);

                    return result switch
                    {
                        WebContentOperationResult.Success =>
                            Results.Created(
                                $"/webcontents/{created!.WebContentId}",
                                created),

                        WebContentOperationResult.ConfigurationNotFound =>
                            Results.NotFound(new
                            {
                                Message = "No existe una configuración activa para este contenido."
                            }),

                        WebContentOperationResult.MaxItemsReached =>
                            Results.Conflict(new
                            {
                                Message = "Se alcanzó el número máximo de elementos permitidos."
                            }),

                        _ => Results.BadRequest()
                    };
                });

            app.MapPut("/webcontents/{id}",
                async (
                    int id,
                    WebContent updated,
                    WebContentService svc) =>
                {
                    if (updated is null)
                    {
                        return Results.BadRequest(new { Message = "Payload inválido." });
                    }

                    return await svc.UpdateAsync(id, updated)
                        ? Results.NoContent()
                        : Results.NotFound();
                });

            app.MapPatch("/webcontents/{id}/deactivate",
                async (int id, WebContentService svc) =>
                {
                    var result = await svc.DeactivateAsync(id);

                    return result switch
                    {
                        WebContentOperationResult.Success =>
                            Results.NoContent(),

                        WebContentOperationResult.NotFound =>
                            Results.NotFound(),

                        WebContentOperationResult.ConfigurationNotFound =>
                            Results.NotFound(new
                            {
                                Message = "No existe una configuración activa para este contenido."
                            }),

                        WebContentOperationResult.MinItemsReached =>
                            Results.Conflict(new
                            {
                                Message = "No es posible desactivar el contenido porque se alcanzó el mínimo permitido."
                            }),

                        _ => Results.BadRequest()
                    };
                });

            app.MapPatch("/webcontents/{id}/activate",
                async (int id, WebContentService svc) =>
                {
                    var result = await svc.ActivateAsync(id);

                    return result switch
                    {
                        WebContentOperationResult.Success =>
                            Results.NoContent(),

                        WebContentOperationResult.NotFound =>
                            Results.NotFound(),

                        WebContentOperationResult.ConfigurationNotFound =>
                            Results.NotFound(new
                            {
                                Message = "No existe una configuración activa para este contenido."
                            }),

                        WebContentOperationResult.MaxItemsReached =>
                            Results.Conflict(new
                            {
                                Message = "No es posible activar el contenido porque se alcanzó el máximo permitido."
                            }),

                        _ => Results.BadRequest()
                    };
                });

            app.MapDelete("/webcontents/{id}",
                async (int id, WebContentService svc) =>
                {
                    var result = await svc.DeleteAsync(id);

                    return result switch
                    {
                        WebContentOperationResult.Success =>
                            Results.NoContent(),

                        WebContentOperationResult.NotFound =>
                            Results.NotFound(),

                        WebContentOperationResult.ConfigurationNotFound =>
                            Results.NotFound(new
                            {
                                Message = "No existe una configuración activa para este contenido."
                            }),

                        WebContentOperationResult.MinItemsReached =>
                            Results.Conflict(new
                            {
                                Message = "No es posible eliminar el contenido porque se alcanzó el mínimo permitido."
                            }),

                        _ => Results.BadRequest()
                    };
                });
        }
    }
}