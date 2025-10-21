using System.Text;

namespace EmmyLua.Unity.Generator;

/// <summary>
/// 追踪和管理已导出类型和未导出类型的引用
/// </summary>
public class TypeReferenceTracker
{
    // 存储所有已导出的类型
    private HashSet<string> ExportedTypes { get; } = new();

    // 存储所有未导出但被引用的类型
    private HashSet<string> UnexportedTypes { get; } = new();

    /// <summary>
    /// 收集所有已导出的类型名称
    /// </summary>
    public void CollectExportedTypes(List<CSType> csTypes)
    {
        foreach (var csType in csTypes)
        {
            var fullName = GetFullTypeName(csType.Namespace, csType.Name);
            ExportedTypes.Add(fullName);

            // 也添加短名称（无命名空间）
            ExportedTypes.Add(csType.Name);
        }

        // 添加常见的基础类型
        var builtInTypes = new[]
        {
            "void", "bool", "byte", "sbyte", "short", "ushort", "int", "uint",
            "long", "ulong", "float", "double", "decimal", "char", "string", "object",
            "System.Void", "System.Boolean", "System.Byte", "System.SByte",
            "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
            "System.Int64", "System.UInt64", "System.Single", "System.Double",
            "System.Decimal", "System.Char", "System.String", "System.Object",
            "boolean", "integer", "number", "any"
        };

        foreach (var builtInType in builtInTypes) ExportedTypes.Add(builtInType);
    }

    /// <summary>
    /// 检查并记录未导出的类型
    /// </summary>
    public void CheckAndRecordType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return;

        // 清理类型名称（移除泛型参数、数组符号等）
        var cleanTypeName = CleanTypeName(typeName);

        // 如果不是已导出的类型，添加到未导出列表
        if (!string.IsNullOrEmpty(cleanTypeName) &&
            !ExportedTypes.Contains(cleanTypeName) &&
            !UnexportedTypes.Contains(cleanTypeName))
            UnexportedTypes.Add(cleanTypeName);
    }

    /// <summary>
    /// 获取未导出类型的数量
    /// </summary>
    public int GetUnexportedTypeCount()
    {
        return UnexportedTypes.Count;
    }

    /// <summary>
    /// 导出未导出的类型定义到文件
    /// </summary>
    public void DumpUnexportedTypes(string outPath, string fileName)
    {
        if (UnexportedTypes.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("---@meta");
        sb.AppendLine();
        sb.AppendLine("--- Unexported types referenced in exported types");
        sb.AppendLine("--- These types are defined as aliases to 'any' for type safety");
        sb.AppendLine();

        // 排序以便输出更整洁
        var sortedTypes = UnexportedTypes.OrderBy(t => t).ToList();

        foreach (var typeName in sortedTypes) sb.AppendLine($"---@alias {typeName} any");

        var filePath = Path.Combine(outPath, fileName);
        File.WriteAllText(filePath, sb.ToString());

        Console.WriteLine($"Exported {UnexportedTypes.Count} unexported type aliases to {fileName}");
    }

    /// <summary>
    /// 清理类型名称，提取主要类型（移除泛型参数、数组等）
    /// </summary>
    private string CleanTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;

        // 移除数组符号
        typeName = typeName.TrimEnd('[', ']');

        // 处理泛型类型
        if (typeName.Contains('<'))
        {
            // 提取泛型参数中的类型
            var start = typeName.IndexOf('<');
            var end = typeName.LastIndexOf('>');

            if (start >= 0 && end > start)
            {
                var genericArgs = typeName.Substring(start + 1, end - start - 1);
                var args = SplitGenericArguments(genericArgs);

                foreach (var arg in args) CheckAndRecordType(arg.Trim());
            }

            // 返回主类型名（不包含泛型参数）
            return typeName.Substring(0, start);
        }

        return typeName;
    }

    /// <summary>
    /// 分割泛型参数（处理嵌套泛型）
    /// </summary>
    private List<string> SplitGenericArguments(string genericArgs)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in genericArgs)
            if (ch == '<')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == '>')
            {
                depth--;
                current.Append(ch);
            }
            else if (ch == ',' && depth == 0)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }

        if (current.Length > 0) result.Add(current.ToString().Trim());

        return result;
    }

    /// <summary>
    /// 获取完整的类型名称（包含命名空间）
    /// </summary>
    private static string GetFullTypeName(string namespaceName, string typeName)
    {
        return !string.IsNullOrEmpty(namespaceName)
            ? $"{namespaceName}.{typeName}"
            : typeName;
    }
}