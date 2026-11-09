using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class AccountEndpoints
    {
        public static void MapAccountEndpoints(this WebApplication app)
        {
            app.MapPost("/auth/activate", async (ActivateAccountRequest request, AccountService svc) =>
            {
                try
                {
                    await svc.ActivateAsync(request);
                    return Results.Ok(new { message = "Cuenta activada. Ya puedes ingresar con tu nueva contraseña." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).AllowAnonymous();

            app.MapPost("/auth/change-password", async (ChangePasswordRequest request, AccountService svc) =>
            {
                try
                {
                    await svc.ChangePasswordAsync(request);
                    return Results.Ok(new { message = "Contraseña actualizada." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).AllowAnonymous();
        }
    }
}