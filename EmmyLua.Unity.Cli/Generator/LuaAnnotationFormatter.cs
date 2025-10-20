using System.Text;

namespace EmmyLua.Unity.Generator;

/// <summary>
/// Formatter for generating EmmyLua annotations for XLua bindings
/// </summary>
public static class LuaAnnotationFormatter
{
    /// <summary>
    /// Write a comment and source location annotation
    /// </summary>
    public static void WriteCommentAndLocation(StringBuilder sb, string comment, string location, int indent = 0)
    {
        var indentSpaces = new string(' ', indent);
        if (!string.IsNullOrEmpty(comment))
        {
            sb.AppendLine($"{indentSpaces}---{comment.Replace("\n", "\n---")}");
        }

        if (location.StartsWith("file://"))
        {
            var escapedLocation = location.Replace("\"", "'");
            sb.AppendLine($"{indentSpaces}---@source \"{escapedLocation}\"");
        }
    }

    /// <summary>
    /// Write a type annotation (class, enum, interface)
    /// </summary>
    public static void WriteTypeAnnotation(
        StringBuilder sb,
        string tag,
        string fullName,
        string baseClass = "",
        List<string>? interfaces = null,
        List<string>? genericTypes = null)
    {
        interfaces ??= [];
        genericTypes ??= [];

        sb.Append($"---@{tag} {fullName}");

        // Add generic type parameters
        if (genericTypes.Count > 0)
        {
            sb.Append($"<{string.Join(", ", genericTypes)}>");
        }

        // Add inheritance
        if (!string.IsNullOrEmpty(baseClass))
        {
            sb.Append($": {baseClass}");
            foreach (var csInterface in interfaces)
            {
                sb.Append($", {csInterface}");
            }
        }
        else if (interfaces.Count > 0)
        {
            sb.Append($": {string.Join(", ", interfaces)}");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Write a field annotation
    /// </summary>
    public static void WriteFieldAnnotation(StringBuilder sb, string typeName, string className, string fieldName)
    {
        var luaTypeName = LuaTypeConverter.ConvertToLuaTypeName(typeName);
        sb.AppendLine($"---@type {luaTypeName}");
        sb.AppendLine($"{className}.{fieldName} = nil");
        sb.AppendLine();
    }

    /// <summary>
    /// Write parameter annotations for a method
    /// </summary>
    public static List<CSParam> WriteParameterAnnotations(StringBuilder sb, List<CSParam> parameters)
    {
        var outParams = new List<CSParam>();

        foreach (var param in parameters)
        {
            if (param.Kind is Microsoft.CodeAnalysis.RefKind.Out or Microsoft.CodeAnalysis.RefKind.Ref)
            {
                outParams.Add(param);
            }

            // Don't write annotation for out parameters (they're only in return type)
            if (param.Kind != Microsoft.CodeAnalysis.RefKind.Out)
            {
                var luaTypeName = LuaTypeConverter.ConvertToLuaTypeName(param.TypeName);

                if (!string.IsNullOrEmpty(param.Comment))
                {
                    var comment = param.Comment.Replace("\n", "\n---");
                    sb.AppendLine($"---@param {param.Name} {luaTypeName} {comment}");
                }
                else
                {
                    sb.AppendLine($"---@param {param.Name} {luaTypeName}");
                }
            }
        }

        return outParams;
    }

    /// <summary>
    /// Write return type annotation
    /// </summary>
    public static void WriteReturnAnnotation(StringBuilder sb, string returnTypeName, List<CSParam> outParams)
    {
        var luaReturnType = LuaTypeConverter.ConvertToLuaTypeName(returnTypeName);
        sb.Append($"---@return {luaReturnType}");

        if (outParams.Count > 0)
        {
            var outTypes = string.Join(", ", outParams.Select(p => LuaTypeConverter.ConvertToLuaTypeName(p.TypeName)));
            sb.Append($", {outTypes}");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Write a method declaration
    /// </summary>
    public static void WriteMethodDeclaration(
        StringBuilder sb,
        string className,
        string methodName,
        List<CSParam> parameters,
        bool isStatic)
    {
        var separator = isStatic ? "." : ":";
        sb.Append($"function {className}{separator}{methodName}(");

        var paramNames = parameters
            .Select(p => LuaTypeConverter.ConvertToLuaCompatibleName(p.Name))
            .ToList();

        sb.Append(string.Join(", ", paramNames));
        sb.AppendLine(")");
        sb.AppendLine("end");
        sb.AppendLine();
    }

    /// <summary>
    /// Write a constructor overload annotation
    /// </summary>
    public static void WriteConstructorOverload(StringBuilder sb, CSTypeMethod ctor, string classFullName)
    {
        var paramsString = string.Join(", ",
            ctor.Params.Select(p => $"{p.Name}: {LuaTypeConverter.ConvertToLuaTypeName(p.TypeName)}"));
        sb.AppendLine($"---@overload fun({paramsString}): {classFullName}");
    }

    /// <summary>
    /// Write a delegate alias annotation
    /// </summary>
    public static void WriteDelegateAlias(StringBuilder sb, string delegateName, CSTypeMethod invokeMethod)
    {
        var paramsString = string.Join(", ",
            invokeMethod.Params.Select(p => $"{p.Name}: {LuaTypeConverter.ConvertToLuaTypeName(p.TypeName)}"));
        var returnType = LuaTypeConverter.ConvertToLuaTypeName(invokeMethod.ReturnTypeName);
        sb.AppendLine($"---@alias {delegateName} fun({paramsString}): {returnType}");
    }
}