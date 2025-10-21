using System.Text;
using EmmyLua.Unity.Generator.XLua;

namespace EmmyLua.Unity.Generator.ToLua;

/// <summary>
/// Dumps ToLua binding information to EmmyLua definition files
/// </summary>
public class ToLuaDumper : IDumper
{
    public string Name => "ToLuaDumper";

    // 500kb
    private static readonly int SingleFileLength = 500 * 1024;

    private int Count { get; set; } = 0;

    private Dictionary<string, bool> NamespaceDict { get; } = new();
    
    // 类型引用追踪器
    private TypeReferenceTracker TypeTracker { get; } = new();

    public void Dump(List<CSType> csTypes, string outPath)
    {
        try
        {
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);

            // 第一遍：收集所有已导出的类型
            TypeTracker.CollectExportedTypes(csTypes);

            var sb = new StringBuilder();
            ResetSb(sb);

            // 第二遍：导出类型并收集未导出的引用类型
            foreach (var csType in csTypes)
                try
                {
                    switch (csType)
                    {
                        case CSClassType csClassType:
                            HandleCsClassType(csClassType, sb);
                            break;
                        case CSInterface csInterface:
                            HandleCsInterface(csInterface, sb);
                            break;
                        case CSEnumType csEnumType:
                            HandleCsEnumType(csEnumType, sb);
                            break;
                        case CSDelegate csDelegate:
                            HandleCsDelegate(csDelegate, sb);
                            break;
                    }

                    sb.AppendLine();
                    CacheOrDumpToFile(sb, outPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error dumping type '{csType.Name}': {e.Message}");
                }

            if (sb.Length > 0) CacheOrDumpToFile(sb, outPath, true);

            DumpNamespace(sb, outPath);
            TypeTracker.DumpUnexportedTypes(outPath, "tolua_noexport_types.lua");

            Console.WriteLine($"Successfully generated {Count} ToLua definition files.");
            Console.WriteLine($"Found {TypeTracker.GetUnexportedTypeCount()} unexported types referenced in exported types.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fatal error during ToLua dump: {e.Message}");
            throw;
        }
    }

    private void DumpNamespace(StringBuilder sb, string outPath)
    {
        sb.Clear();
        sb.AppendLine("---@meta");
        sb.AppendLine();
        sb.AppendLine("---ToLua global namespace");

        foreach (var (namespaceString, isNamespace) in NamespaceDict)
        {
            if (isNamespace)
            {
                sb.AppendLine($"---@type namespace <\"{namespaceString}\">");
                sb.AppendLine($"{namespaceString} = {{}}");
            }
            else
            {
                sb.AppendLine($"---@type {namespaceString}");
                sb.AppendLine($"{namespaceString} = {{}}");
            }

            sb.AppendLine();
        }

        var filePath = Path.Combine(outPath, "tolua_namespace.lua");
        File.WriteAllText(filePath, sb.ToString());
    }

    private void CacheOrDumpToFile(StringBuilder sb, string outPath, bool force = false)
    {
        if (sb.Length > SingleFileLength || force)
            try
            {
                var filePath = Path.Combine(outPath, $"tolua_dump_{Count}.lua");
                File.WriteAllText(filePath, sb.ToString());
                ResetSb(sb);
                Count++;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error writing file: {e.Message}");
                throw;
            }
    }

    private void ResetSb(StringBuilder sb)
    {
        sb.Clear();
        sb.AppendLine("---@meta");
        sb.AppendLine();
    }

    private void HandleCsClassType(CSClassType csClassType, StringBuilder sb)
    {
        RegisterNamespace(csClassType.Namespace, csClassType.Name);

        var classFullName = GetFullTypeName(csClassType.Namespace, csClassType.Name);

        // 检查基类和接口
        TypeTracker.CheckAndRecordType(csClassType.BaseClass);
        foreach (var iface in csClassType.Interfaces)
        {
            TypeTracker.CheckAndRecordType(iface);
        }

        LuaAnnotationFormatter.WriteCommentAndLocation(sb, csClassType.Comment, csClassType.Location);
        LuaAnnotationFormatter.WriteTypeAnnotation(
            sb, "class", classFullName, csClassType.BaseClass, csClassType.Interfaces, csClassType.GenericTypes);

        // Write constructor overloads
        if (!csClassType.IsStatic)
        {
            var ctors = GetCtorList(csClassType);
            if (ctors.Count > 0)
                foreach (var ctor in ctors)
                {
                    // 检查构造函数参数类型
                    foreach (var param in ctor.Params)
                    {
                        TypeTracker.CheckAndRecordType(param.TypeName);
                    }
                    LuaAnnotationFormatter.WriteConstructorOverload(sb, ctor, classFullName);
                }
            else
                sb.AppendLine($"---@overload fun(): {classFullName}");

            // ToLua 常用 .New() 方法创建对象
            sb.AppendLine($"---Create a new instance of {classFullName}");
            sb.AppendLine($"---@return {classFullName}");
            sb.AppendLine($"function {csClassType.Name}.New()");
            sb.AppendLine("end");
            sb.AppendLine();
        }

        sb.AppendLine($"{csClassType.Name} = {{}}");
        sb.AppendLine();

        // Write fields and events
        foreach (var field in csClassType.Fields)
        {
            // 检查字段类型
            TypeTracker.CheckAndRecordType(field.TypeName);
            
            LuaAnnotationFormatter.WriteCommentAndLocation(sb, field.Comment, field.Location);
            
            // 区分事件和普通字段
            if (field.IsEvent)
            {
                LuaAnnotationFormatter.WriteEventAnnotation(sb, field.TypeName, csClassType.Name, field.Name);
            }
            else
            {
                LuaAnnotationFormatter.WriteFieldAnnotation(sb, field.TypeName, csClassType.Name, field.Name);
            }
        }

        // Write methods
        foreach (var method in csClassType.Methods)
        {
            if (method.Name == ".ctor")
                continue;

            // 检查返回类型
            TypeTracker.CheckAndRecordType(method.ReturnTypeName);
            
            // 检查参数类型
            foreach (var param in method.Params)
            {
                TypeTracker.CheckAndRecordType(param.TypeName);
            }

            LuaAnnotationFormatter.WriteCommentAndLocation(sb, method.Comment, method.Location);
            var outParams = LuaAnnotationFormatter.WriteParameterAnnotations(sb, method.Params);
            LuaAnnotationFormatter.WriteReturnAnnotation(sb, method.ReturnTypeName, outParams);
            LuaAnnotationFormatter.WriteMethodDeclaration(sb, csClassType.Name, method.Name, method.Params,
                method.IsStatic);
        }
    }

    private void RegisterNamespace(string namespaceName, string typeName)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            var firstNamespace = namespaceName.Split('.').FirstOrDefault();
            if (firstNamespace != null) NamespaceDict.TryAdd(firstNamespace, true);
        }
        else
        {
            NamespaceDict.TryAdd(typeName, false);
        }
    }

    private static string GetFullTypeName(string namespaceName, string typeName)
    {
        return !string.IsNullOrEmpty(namespaceName)
            ? $"{namespaceName}.{typeName}"
            : typeName;
    }

    private void HandleCsInterface(CSInterface csInterface, StringBuilder sb)
    {
        RegisterNamespace(csInterface.Namespace, csInterface.Name);

        var interfaceFullName = GetFullTypeName(csInterface.Namespace, csInterface.Name);

        // 检查接口继承
        foreach (var iface in csInterface.Interfaces)
        {
            TypeTracker.CheckAndRecordType(iface);
        }

        LuaAnnotationFormatter.WriteCommentAndLocation(sb, csInterface.Comment, csInterface.Location);
        LuaAnnotationFormatter.WriteTypeAnnotation(sb, "class", interfaceFullName, "", csInterface.Interfaces);

        sb.AppendLine($"{csInterface.Name} = {{}}");
        sb.AppendLine();

        // Write interface members
        foreach (var field in csInterface.Fields)
        {
            // 检查字段类型
            TypeTracker.CheckAndRecordType(field.TypeName);
            
            LuaAnnotationFormatter.WriteCommentAndLocation(sb, field.Comment, field.Location);
            
            // 区分事件和普通字段
            if (field.IsEvent)
            {
                LuaAnnotationFormatter.WriteEventAnnotation(sb, field.TypeName, csInterface.Name, field.Name);
            }
            else
            {
                LuaAnnotationFormatter.WriteFieldAnnotation(sb, field.TypeName, csInterface.Name, field.Name);
            }
        }

        foreach (var method in csInterface.Methods)
        {
            // 检查返回类型和参数类型
            TypeTracker.CheckAndRecordType(method.ReturnTypeName);
            foreach (var param in method.Params)
            {
                TypeTracker.CheckAndRecordType(param.TypeName);
            }
            
            LuaAnnotationFormatter.WriteCommentAndLocation(sb, method.Comment, method.Location);
            var outParams = LuaAnnotationFormatter.WriteParameterAnnotations(sb, method.Params);
            LuaAnnotationFormatter.WriteReturnAnnotation(sb, method.ReturnTypeName, outParams);
            LuaAnnotationFormatter.WriteMethodDeclaration(sb, csInterface.Name, method.Name, method.Params, false);
        }
    }

    private void HandleCsEnumType(CSEnumType csEnumType, StringBuilder sb)
    {
        RegisterNamespace(csEnumType.Namespace, csEnumType.Name);

        var enumFullName = GetFullTypeName(csEnumType.Namespace, csEnumType.Name);

        LuaAnnotationFormatter.WriteCommentAndLocation(sb, csEnumType.Comment, csEnumType.Location);

        // ToLua 枚举定义
        sb.AppendLine($"---@class {enumFullName}");

        foreach (var field in csEnumType.Fields)
        {
            var luaTypeName = LuaTypeConverter.ConvertToLuaTypeName(field.TypeName);
            sb.AppendLine($"---@field public {field.Name} {luaTypeName}");
        }

        sb.AppendLine($"{csEnumType.Name} = {{}}");
        sb.AppendLine();

        foreach (var field in csEnumType.Fields)
        {
            if (!string.IsNullOrEmpty(field.Comment)) sb.AppendLine($"---{field.Comment}");
            // 使用实际的枚举值，如果没有则默认为 0
            var enumValue = field.ConstantValue ?? 0;
            sb.AppendLine($"{csEnumType.Name}.{field.Name} = {enumValue}");
        }

        sb.AppendLine();
    }

    private void HandleCsDelegate(CSDelegate csDelegate, StringBuilder sb)
    {
        RegisterNamespace(csDelegate.Namespace, csDelegate.Name);

        // 检查委托的返回类型和参数类型
        TypeTracker.CheckAndRecordType(csDelegate.InvokeMethod.ReturnTypeName);
        foreach (var param in csDelegate.InvokeMethod.Params)
        {
            TypeTracker.CheckAndRecordType(param.TypeName);
        }

        LuaAnnotationFormatter.WriteCommentAndLocation(sb, csDelegate.Comment, csDelegate.Location);
        LuaAnnotationFormatter.WriteDelegateAlias(sb, csDelegate.Name, csDelegate.InvokeMethod);
    }

    private List<CSTypeMethod> GetCtorList(CSClassType csClassType)
    {
        return csClassType.Methods.FindAll(method => method.Name == ".ctor");
    }
}