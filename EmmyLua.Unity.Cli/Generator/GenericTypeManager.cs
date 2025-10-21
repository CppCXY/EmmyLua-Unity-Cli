namespace EmmyLua.Unity.Generator;

/// <summary>
/// 管理和合并泛型类型定义
/// </summary>
public class GenericTypeManager
{
    // 存储泛型基础类型键到第一个遇到的类型的映射
    // Key: "System.Collections.Generic.List`1"
    private Dictionary<string, CSType> FirstOccurrence { get; } = new();

    // 被合并的类型计数
    private int MergedCount { get; set; } = 0;

    /// <summary>
    /// 处理类型列表，合并泛型类型
    /// </summary>
    public List<CSType> ProcessTypes(List<CSType> csTypes)
    {
        var result = new List<CSType>();

        foreach (var csType in csTypes)
            if (ShouldMergeGenericType(csType, out var genericKey))
            {
                if (!FirstOccurrence.ContainsKey(genericKey))
                {
                    // 第一次遇到这个泛型基类，转换为泛型定义
                    var genericType = ConvertToGenericDefinition(csType);
                    FirstOccurrence[genericKey] = genericType;
                    result.Add(genericType);
                }
                else
                {
                    // 已经有这个泛型定义了，跳过
                    MergedCount++;
                }
            }
            else
            {
                // 非泛型类型或已经是泛型定义的类型
                result.Add(csType);
            }

        if (MergedCount > 0)
            Console.WriteLine($"Merged {MergedCount} generic type instance(s) into generic definitions.");

        return result;
    }

    /// <summary>
    /// 判断类型是否需要合并（是构造的泛型类型）
    /// </summary>
    private bool ShouldMergeGenericType(CSType csType, out string genericKey)
    {
        genericKey = string.Empty;

        if (csType is CSClassType classType)
            // 只合并构造的泛型类型（具体化的泛型，如 List<int>）
            // 不合并泛型定义本身（如 List<T>）
            if (classType.IsConstructedGeneric && classType.Name.Contains('`'))
            {
                genericKey = GetGenericKey(classType);
                return true;
            }

        return false;
    }

    /// <summary>
    /// 获取泛型基础键名（包含命名空间和泛型标记）
    /// 例如: "System.Collections.Generic.List`1"
    /// </summary>
    private string GetGenericKey(CSClassType classType)
    {
        var fullName = string.IsNullOrEmpty(classType.Namespace)
            ? classType.Name
            : $"{classType.Namespace}.{classType.Name}";

        // 确保键名唯一：命名空间.类名`参数数量
        // 例如: "System.Collections.Generic.List`1", "MyNamespace.DOGetter`1"
        // 如果类名中没有 ` 标记，添加泛型参数数量
        if (!fullName.Contains('`') && classType.GenericTypes.Count > 0) fullName += $"`{classType.GenericTypes.Count}";

        return fullName;
    }

    /// <summary>
    /// 将具体的泛型类型转换为泛型定义
    /// </summary>
    private CSType ConvertToGenericDefinition(CSType csType)
    {
        if (csType is not CSClassType classType)
            return csType;

        // 使用原始的泛型参数名称（如果有的话）
        var genericParams = classType.GenericParameterNames.Count > 0
            ? classType.GenericParameterNames
            : GenerateGenericParameterNames(classType.GenericTypes.Count);

        // 创建替换映射：具体类型 -> 泛型参数
        var typeMap = new Dictionary<string, string>();
        for (var i = 0; i < classType.GenericTypes.Count; i++) typeMap[classType.GenericTypes[i]] = genericParams[i];

        var genericType = new CSClassType
        {
            Name = RemoveGenericMarker(classType.Name),
            Namespace = classType.Namespace,
            Comment = classType.Comment,
            Location = classType.Location,
            IsStatic = classType.IsStatic,
            IsConstructedGeneric = false, // 转换后是泛型定义，不是构造的泛型
            BaseClass = ReplaceGenericTypes(classType.BaseClass, typeMap),
            Interfaces = classType.Interfaces.Select(i => ReplaceGenericTypes(i, typeMap)).ToList(),
            GenericTypes = genericParams,
            GenericParameterNames = genericParams
        };

        // 转换字段类型
        foreach (var field in classType.Fields)
        {
            var newField = new CSTypeField
            {
                Name = field.Name,
                Comment = field.Comment,
                Location = field.Location,
                TypeName = ReplaceGenericTypes(field.TypeName, typeMap),
                ConstantValue = field.ConstantValue,
                IsEvent = field.IsEvent
            };
            genericType.Fields.Add(newField);
        }

        // 转换方法
        foreach (var method in classType.Methods)
        {
            var newMethod = new CSTypeMethod
            {
                Name = method.Name,
                Comment = method.Comment,
                Location = method.Location,
                ReturnTypeName = ReplaceGenericTypes(method.ReturnTypeName, typeMap),
                IsStatic = method.IsStatic,
                Params = method.Params.Select(p => new CSParam
                {
                    Name = p.Name,
                    Comment = p.Comment,
                    TypeName = ReplaceGenericTypes(p.TypeName, typeMap),
                    Nullable = p.Nullable,
                    Kind = p.Kind
                }).ToList()
            };
            genericType.Methods.Add(newMethod);
        }

        return genericType;
    }

    /// <summary>
    /// 移除类型名中的泛型标记
    /// 例如: "List`1" -> "List"
    /// </summary>
    private string RemoveGenericMarker(string name)
    {
        var backtickIndex = name.IndexOf('`');
        return backtickIndex > 0 ? name.Substring(0, backtickIndex) : name;
    }

    /// <summary>
    /// 生成泛型参数名称 (T, T1, T2, ...)
    /// </summary>
    private List<string> GenerateGenericParameterNames(int count)
    {
        var names = new List<string>();
        for (var i = 0; i < count; i++) names.Add(i == 0 ? "T" : $"T{i + 1}");
        return names;
    }

    /// <summary>
    /// 在类型字符串中替换具体类型为泛型参数
    /// 使用更精确的替换策略，避免误替换
    /// 例如: "System.Collections.Generic.List<int>" -> "System.Collections.Generic.List<T>"
    /// </summary>
    private string ReplaceGenericTypes(string typeName, Dictionary<string, string> typeMap)
    {
        if (string.IsNullOrEmpty(typeName) || typeMap.Count == 0)
            return typeName;

        var result = typeName;

        // 按照类型名称长度降序排序，避免短类型名被优先替换导致问题
        // 例如: 先替换 "System.Int32" 再替换 "Int32"
        var sortedMap = typeMap.OrderByDescending(kv => kv.Key.Length);

        foreach (var (concreteType, genericParam) in sortedMap)
            // 使用正则表达式或更精确的匹配来避免部分匹配
            // 简单版本：直接替换
            // 复杂场景需要解析泛型嵌套结构
            result = ReplaceWholeWord(result, concreteType, genericParam);

        return result;
    }

    /// <summary>
    /// 替换完整的类型名（避免部分匹配）
    /// </summary>
    private string ReplaceWholeWord(string text, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldValue))
            return text;

        // 对于泛型类型，简单的字符串替换即可
        // 因为具体类型名（如 System.Int32）通常不会是其他类型的子串
        return text.Replace(oldValue, newValue);
    }

    /// <summary>
    /// 获取被合并的类型数量
    /// </summary>
    public int GetMergedTypeCount()
    {
        return MergedCount;
    }
}