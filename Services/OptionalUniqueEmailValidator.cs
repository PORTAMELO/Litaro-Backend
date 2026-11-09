using System.ComponentModel.DataAnnotations;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class OptionalUniqueEmailValidator : IUserValidator<User>
{
    private static readonly EmailAddressAttribute EmailFormat = new();

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
            return IdentityResult.Success;

        if (!EmailFormat.IsValid(email))
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidEmail",
                Description = $"El correo '{email}' no es válido."
            });

        var normalized = manager.NormalizeEmail(email);
        var duplicated = await manager.Users
            .AnyAsync(u => u.NormalizedEmail == normalized && u.Id != user.Id);

        return duplicated
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateEmail",
                Description = $"El correo '{email}' ya está registrado por otro usuario."
            })
            : IdentityResult.Success;
    }
}
