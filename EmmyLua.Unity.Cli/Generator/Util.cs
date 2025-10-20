namespace EmmyLua.Unity.Generator;

/// <summary>
/// Utility class for Lua code generation
/// </summary>
public static class LuaTypeConverter
{
    // Lua keywords that need to be escaped
    private static readonly HashSet<string> LuaKeywords = new(StringComparer.Ordinal)
    {
        "function", "end", "local", "nil", "true", "false", "and", "or", "not",
        "if", "then", "else", "elseif", "while", "do", "repeat", "until",
        "for", "in", "break", "return", "goto"
    };

    // C# to Lua type mapping
    private static readonly Dictionary<string, string> TypeMapping = new(StringComparer.Ordinal)
    {
        { "System.Int32", "integer" },
        { "System.Int64", "integer" },
        { "System.Int16", "integer" },
        { "System.Byte", "integer" },
        { "System.SByte", "integer" },
        { "System.UInt32", "integer" },
        { "System.UInt64", "integer" },
        { "System.UInt16", "integer" },
        { "int", "integer" },
        { "long", "integer" },
        { "short", "integer" },
        { "byte", "integer" },
        { "sbyte", "integer" },
        { "uint", "integer" },
        { "ulong", "integer" },
        { "ushort", "integer" },

        { "System.Single", "number" },
        { "System.Double", "number" },
        { "System.Decimal", "number" },
        { "float", "number" },
        { "double", "number" },
        { "decimal", "number" },

        { "System.Boolean", "boolean" },
        { "bool", "boolean" },

        { "System.String", "string" },
        { "string", "string" },

        { "System.Object", "any" },
        { "object", "any" },

        { "System.Void", "void" },
        { "void", "void" }
    };

    /// <summary>
    /// Convert a C# identifier to a Lua-compatible name by escaping keywords
    /// </summary>
    public static string ConvertToLuaCompatibleName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return LuaKeywords.Contains(name) ? "_" + name : name;
    }

    /// <summary>
    /// Check if a name is a Lua keyword
    /// </summary>
    public static bool IsLuaKeyword(string name)
    {
        return !string.IsNullOrEmpty(name) && LuaKeywords.Contains(name);
    }

    /// <summary>
    /// Convert a C# type name to Lua type annotation
    /// </summary>
    public static string ConvertToLuaTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "any";

        // Try exact match first
        if (TypeMapping.TryGetValue(typeName, out var luaType))
            return luaType;

        // Handle generic types
        if (typeName.Contains('<')) return ConvertGenericType(typeName);

        // Return original type name for custom types
        return typeName;
    }

    /// <summary>
    /// Convert generic C# types to Lua format
    /// </summary>
    private static string ConvertGenericType(string typeName)
    {
        // Simple handling for common generic types
        if (typeName.StartsWith("System.Collections.Generic.List<"))
        {
            var innerType = ExtractGenericArgument(typeName);
            return $"{ConvertToLuaTypeName(innerType)}[]";
        }

        if (typeName.StartsWith("System.Collections.Generic.Dictionary<")) return "table";

        return typeName;
    }

    /// <summary>
    /// Extract the inner type from a generic type string
    /// </summary>
    private static string ExtractGenericArgument(string genericType)
    {
        var start = genericType.IndexOf('<');
        var end = genericType.LastIndexOf('>');
        if (start >= 0 && end > start) return genericType.Substring(start + 1, end - start - 1).Trim();
        return "any";
    }
}