using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Identity;

namespace Litaro.Services;

public static class ApiJson
{
    public static readonly IReadOnlySet<string> HiddenUserProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(IdentityUser<int>.PasswordHash),
        nameof(IdentityUser<int>.SecurityStamp),
        nameof(IdentityUser<int>.ConcurrencyStamp),
    };

    public static void Configure(JsonSerializerOptions options)
    {
        var resolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = resolver.WithAddedModifier(HideUserSecrets);
    }

    private static void HideUserSecrets(JsonTypeInfo typeInfo)
    {
        if (!typeof(IdentityUser<int>).IsAssignableFrom(typeInfo.Type))
            return;

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            if (typeInfo.Properties[i].AttributeProvider is MemberInfo member && HiddenUserProperties.Contains(member.Name))
                typeInfo.Properties.RemoveAt(i);
        }
    }
}
