using System.Linq.Expressions;

namespace Litaro.Services;

public static class QueryFilterHelper
{
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, IDictionary<string, string> filters)
    {
        if (filters is null || filters.Count == 0)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var (key, rawValue) in filters)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) continue;

            var property = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

            Expression propertyAccess;
            Type propertyType;

            if (property is not null)
            {
                propertyAccess = Expression.Property(parameter, property);
                propertyType = property.PropertyType;
            }
            else
            {
                var userNavProperty = typeof(T).GetProperty("User");

                var nestedProperty = userNavProperty?.PropertyType
                    .GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));

                if (userNavProperty is null || nestedProperty is null) continue;

                propertyAccess = Expression.Property(Expression.Property(parameter, userNavProperty), nestedProperty);
                propertyType = nestedProperty.PropertyType;
            }

            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            Expression? comparison;

            try
            {
                if (targetType == typeof(string))
                {
                    var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;

                    var propertyToLower = Expression.Call(propertyAccess, toLower);
                    var valueExpr = Expression.Constant(rawValue.ToLower());
                    comparison = Expression.Call(propertyToLower, containsMethod, valueExpr);
                }
                else if (targetType == typeof(bool))
                {
                    var boolValue = Convert.ToBoolean(rawValue);
                    comparison = Expression.Equal(propertyAccess, Expression.Constant(boolValue, propertyType));
                }
                else if (targetType == typeof(DateTime))
                {
                    var dateValue = DateTime.Parse(rawValue);
                    comparison = Expression.Equal(propertyAccess, Expression.Constant(dateValue, propertyType));
                }
                else if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(decimal) ||
                        targetType == typeof(double) || targetType == typeof(byte) || targetType == typeof(short))
                {
                    var numericValue = Convert.ChangeType(rawValue, targetType);
                    comparison = Expression.Equal(propertyAccess, Expression.Constant(numericValue, propertyType));
                }
                else
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            combined = combined is null ? comparison : Expression.AndAlso(combined, comparison);
        }

        return combined is null ? query : query.Where(Expression.Lambda<Func<T, bool>>(combined, parameter));
    }
}
