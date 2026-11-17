using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Identity;

namespace Litaro.Tests;

public static class TestData
{
    private static int documentSeed = 1000;

    public static IdentityRole<int> AddRole(AppDbContext db, string name)
    {
        var role = new IdentityRole<int>(name) { NormalizedName = name.ToUpperInvariant() };
        db.Roles.Add(role);
        db.SaveChanges();
        return role;
    }

    public static User AddUser(AppDbContext db, string firstName, string lastName, params IdentityRole<int>[] roles)
    {
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            DocumentType = "CC",
            DocumentNumber = Interlocked.Increment(ref documentSeed).ToString(),
            CampusId = 1,
        };
        db.Users.Add(user);
        db.SaveChanges();

        foreach (var role in roles)
            db.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });

        db.SaveChanges();
        return user;
    }

    public static List<CharacteristicDetail> AddCatalog(
        AppDbContext db,
        CatalogKey key,
        bool link,
        params (string Valor, string Nombre, string? Codigo)[] rows)
    {
        var characteristic = new Characteristic { Name = key.Name };
        db.Characteristics.Add(characteristic);
        db.SaveChanges();

        if (link)
        {
            db.ColumnConfigurations.Add(new ColumnConfiguration
            {
                TableName = key.Table,
                ColumnName = key.Column,
                Visible = true,
                CharacteristicId = characteristic.CharacteristicId,
            });
        }

        var details = new List<CharacteristicDetail>();

        foreach (var (valor, nombre, codigo) in rows)
        {
            var detail = new CharacteristicDetail
            {
                CharacteristicId = characteristic.CharacteristicId,
                Nombre = nombre,
                Valor = valor,
                Codigo = codigo,
            };
            db.CharacteristicDetails.Add(detail);
            db.SaveChanges();
            details.Add(detail);
        }

        db.SaveChanges();
        return details;
    }

    public static List<CharacteristicDetail> AddDocumentTypes(AppDbContext db, params (string Name, string State)[] types)
    {
        var characteristic = new Characteristic { Name = Catalogs.EnrollmentDocuments };
        db.Characteristics.Add(characteristic);
        db.SaveChanges();

        var details = new List<CharacteristicDetail>();

        foreach (var (name, state) in types)
        {
            var detail = new CharacteristicDetail
            {
                CharacteristicId = characteristic.CharacteristicId,
                Nombre = name,
                Valor = state,
            };
            db.CharacteristicDetails.Add(detail);
            db.SaveChanges();
            details.Add(detail);
        }

        return details;
    }

    public static NotificationType AddNotificationType(AppDbContext db, string code, string template, params IdentityRole<int>[] roles)
    {
        var type = new NotificationType { Code = code, Name = code, Category = "Pruebas", Template = template };
        db.NotificationTypes.Add(type);
        db.SaveChanges();

        foreach (var role in roles)
            db.NotificationTypeRecipients.Add(new NotificationTypeRecipient { NotificationTypeId = type.NotificationTypeId, RoleId = role.Id });

        db.SaveChanges();
        return type;
    }
}
