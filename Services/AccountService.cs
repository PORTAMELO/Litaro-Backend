using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record ActivateAccountRequest(string Login, DateOnly BirthDate, string NewPassword);

public record ChangePasswordRequest(string Login, string CurrentPassword, string NewPassword);

public class AccountService(AppDbContext db, UserManager<User> userManager, SignInManager<User> signInManager)
{
    private const string ActivationError = "Los datos no coinciden con una cuenta pendiente de activar.";
    private const string LockedOutError = "Demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo.";
    private const string CredentialsError = "Usuario o contraseña incorrectos.";

    public async Task<User?> FindByLoginAsync(string? login)
    {
        var value = (login ?? string.Empty).Trim();
        if (value.Length == 0)
            return null;

        if (value.Contains('@'))
            return await userManager.FindByEmailAsync(value);

        var matches = await db.Users.Where(u => u.DocumentNumber == value).Take(2).ToListAsync();
        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task ActivateAsync(ActivateAccountRequest request)
    {
        var user = await FindByLoginAsync(request.Login);

        if (user is null || !user.Active || user.PasswordHash is not null)
            throw new InvalidOperationException(ActivationError);

        if (await userManager.IsLockedOutAsync(user))
            throw new InvalidOperationException(LockedOutError);

        if (!await BirthDateMatchesAsync(db, user.Id, request.BirthDate))
        {
            await userManager.AccessFailedAsync(user);
            throw new InvalidOperationException(
                await userManager.IsLockedOutAsync(user) ? LockedOutError : ActivationError);
        }

        user.MustChangePassword = false;
        var result = await userManager.AddPasswordAsync(user, request.NewPassword ?? string.Empty);
        if (!result.Succeeded)
            throw new InvalidOperationException(Describe(result));

        await userManager.ResetAccessFailedCountAsync(user);
    }

    public static async Task<bool> BirthDateMatchesAsync(AppDbContext db, int userId, DateOnly birthDate)
    {
        var own = await db.Students.AsNoTracking()
            .Where(s => s.StudentId == userId && s.Active)
            .Select(s => (DateTime?)s.BirthDate)
            .FirstOrDefaultAsync();

        if (own is { } date && DateOnly.FromDateTime(date) == birthDate)
            return true;

        if (!await db.Parents.AnyAsync(p => p.ParentId == userId && p.Active))
            return false;

        var children = await db.ParentStudents.AsNoTracking()
            .Where(ps => ps.ParentId == userId)
            .Select(ps => ps.Student.BirthDate)
            .ToListAsync();

        return children.Any(child => DateOnly.FromDateTime(child) == birthDate);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await FindByLoginAsync(request.Login);
        if (user is null || !user.Active || user.PasswordHash is null)
            throw new InvalidOperationException(CredentialsError);

        var check = await signInManager.CheckPasswordSignInAsync(user, request.CurrentPassword ?? string.Empty, lockoutOnFailure: true);
        if (check.IsLockedOut)
            throw new InvalidOperationException(LockedOutError);
        if (!check.Succeeded)
            throw new InvalidOperationException(CredentialsError);

        if (request.CurrentPassword == request.NewPassword)
            throw new InvalidOperationException("La nueva contraseña debe ser distinta a la actual.");

        user.MustChangePassword = false;
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword ?? string.Empty);
        if (!result.Succeeded)
            throw new InvalidOperationException(Describe(result));
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Code switch
        {
            "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres.",
            "PasswordRequiresUpper" => "Debe tener al menos una letra mayúscula.",
            "PasswordRequiresLower" => "Debe tener al menos una letra minúscula.",
            "PasswordRequiresDigit" => "Debe tener al menos un número.",
            "PasswordRequiresNonAlphanumeric" => "Debe tener al menos un carácter especial.",
            "PasswordRequiresUniqueChars" => "Debe tener más caracteres distintos.",
            "PasswordMismatch" => "La contraseña actual no es correcta.",
            "UserAlreadyHasPassword" => ActivationError,
            _ => e.Description
        }));
}