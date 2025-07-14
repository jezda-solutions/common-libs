using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jezda.Common.Helpers;

public static class PermissionHelper
{
    public static List<string> GetRegisteredPermissions(Type type)
    {
        var permissions = new List<string>();

        var nestedTypes = type.GetNestedTypes();

        foreach (var nestedType in nestedTypes)
        {
            var fields = nestedType.GetFields(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                var value = field.GetValue(null);
                if (value is string s)
                    permissions.Add(s);
            }
        }

        return permissions;
    }

    /// <summary>
    /// Encodes a collection of user permissions into a bitmask representation.
    /// </summary>
    /// <remarks>The method uses the provided <paramref name="permissionType"/> to determine the mapping of
    /// permission names to bit indices. Permissions not found in the mapping are ignored. The resulting bitmask can be
    /// used for efficient permission checks.</remarks>
    /// <param name="userPermissions">A collection of permission names to be encoded. Each name should correspond to a valid permission defined in the
    /// specified <paramref name="permissionType"/>.</param>
    /// <param name="permissionType">The type that defines the set of available permissions. This type must provide a mapping of permission names to
    /// bit indices.</param>
    /// <returns>A <see langword="long"/> value representing the encoded bitmask, where each bit corresponds to a permission
    /// included in <paramref name="userPermissions"/>.</returns>
    public static long EncodeBitmask(
        IEnumerable<string> userPermissions, 
        Type permissionType)
    {
        long mask = 0;

        var allPermissions = GetPermissions(permissionType);

        foreach (var permission in userPermissions)
        {
            if (allPermissions.TryGetValue(permission, out int bitIndex))
            {
                mask |= 1L << bitIndex;
            }
        }
        return mask;
    }

    /// <summary>
    /// Dekodira bitmasku u listu stringova
    /// Bitmaska dolazi iz JWT tokena i predstavlja kodirane permisije
    /// Permisije dekodiramo i preko ove liste kasnije pravimo claimove, gde svaki claim
    /// ima tip permissions i value je svaki item pojedinacno iz ove liste
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
    public static IEnumerable<string> DecodeBitmask(long mask, Type type)
    {
        var permissions = GetPermissions(type);

        foreach (var permission in permissions)
        {
            if ((mask & 1L << permission.Value) != 0)
            {
                yield return permission.Key;
            }
        }
    }

    /// <summary>
    /// Izvlaci sve permisije iz tipa koji je prosledjen kao parametar koristeci refleksiju
    /// </summary>
    /// <param name="rootType"></param>
    /// <returns></returns>
    private static Dictionary<string, int> GetPermissions(Type rootType)
    {
        var result = new Dictionary<string, int>();
        var allPermissions = new List<string>();

        // Dodaj root tip + svi nested tipovi
        var typesToSearch = new List<Type> { rootType };
        CollectAllNestedTypes(rootType, typesToSearch);

        foreach (var type in typesToSearch)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                             .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

            foreach (var field in fields)
            {
                if (field.GetRawConstantValue() is string value && !allPermissions.Contains(value))
                {
                    allPermissions.Add(value);
                }
            }
        }

        allPermissions.Sort();

        // Dodeli indekse
        for (int i = 0; i < allPermissions.Count; i++)
        {
            result[allPermissions[i]] = i;
        }

        return result;
    }

    private static void CollectAllNestedTypes(Type type, List<Type> allTypes)
    {
        var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        foreach (var nestedType in nestedTypes)
        {
            allTypes.Add(nestedType);
            CollectAllNestedTypes(nestedType, allTypes);
        }
    }
}
