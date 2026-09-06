using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            app.MapGet("/users", async (HttpRequest request, UserService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("User", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/users/{id:int}", async (int id, UserService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var user = await svc.GetByIdAsync(id);

                return user is not null
                    ? Results.Ok(user)
                    : Results.NotFound($"No existe un usuario activo con id {id}.");
            });
        }
    }
}
