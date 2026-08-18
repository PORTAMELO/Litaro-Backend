using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class WebContentConfigurationEndpoints
    {
        public static void MapWebContentConfigurationEndpoints(this WebApplication app)
        {
            app.MapGet("/webcontentconfigurations", async (WebContentConfigurationService svc) =>
                Results.Ok(await svc.GetAllAsync())).AllowAnonymous();

            app.MapGet("/webcontentconfigurations/{id}", async (int id, WebContentConfigurationService svc) =>
                await svc.GetByIdAsync(id) is WebContentConfiguration configuration
                    ? Results.Ok(configuration)
                    : Results.NotFound()).AllowAnonymous();

            app.MapGet(
                "/webcontentconfigurations/{pageName}/{sectionName}/{contentKey}",
                async (
                    string pageName,
                    string sectionName,
                    string contentKey,
                    WebContentConfigurationService svc) =>
                {
                    var configuration = await svc.GetByPageSectionContentKeyAsync(
                        pageName,
                        sectionName,
                        contentKey);

                    return configuration is not null
                        ? Results.Ok(configuration)
                        : Results.NotFound();
                }).AllowAnonymous();

            app.MapPost("/webcontentconfigurations",
                async (WebContentConfiguration configuration, WebContentConfigurationService svc) =>
                {
                    var created = await svc.CreateAsync(configuration);

                    return Results.Created(
                        $"/webcontentconfigurations/{created.WebContentConfigurationId}",
                        created);
                });

            app.MapPut("/webcontentconfigurations/{id}",
                async (
                    int id,
                    WebContentConfiguration configuration,
                    WebContentConfigurationService svc) =>
                {
                    return await svc.UpdateAsync(id, configuration)
                        ? Results.NoContent()
                        : Results.NotFound();
                });

            app.MapPatch("/webcontentconfigurations/{id}/deactivate",
                async (int id, WebContentConfigurationService svc) =>
                {
                    return await svc.DeactivateAsync(id)
                        ? Results.NoContent()
                        : Results.NotFound();
                });

            app.MapPatch("/webcontentconfigurations/{id}/activate",
                async (int id, WebContentConfigurationService svc) =>
                {
                    return await svc.ActivateAsync(id)
                        ? Results.NoContent()
                        : Results.NotFound();
                });
        }
    }
}