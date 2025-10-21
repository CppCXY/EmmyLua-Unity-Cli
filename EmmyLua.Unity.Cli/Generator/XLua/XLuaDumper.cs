using System.Text;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator.XLua;

public class XLuaDumper : IDumper
{
    public string Name => "XLuaDumper";

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
            TypeTracker.DumpUnexportedTypes(outPath, "xlua_noexport_types.lua");

            Console.WriteLine($"Successfully generated {Count} Lua definition files.");
            Console.WriteLine($"Found {TypeTracker.GetUnexportedTypeCount()} unexported types referenced in exported types.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fatal error during dump: {e.Message}");
            throw;
        }
    }

    private void DumpNamespace(StringBuilder sb, string outPath)
    {
        sb.AppendLine("CS = {}");
        foreach (var (namespaceString, isNamespace) in NamespaceDict)
            if (isNamespace)
                sb.AppendLine($"---@type namespace <\"{namespaceString}\">\nCS.{namespaceString} = {{}}");
            else
                sb.AppendLine($"---@type {namespaceString}\nCS.{namespaceString} = {{}}");

        var filePath = Path.Combine(outPath, "xlua_namespace.lua");
        File.WriteAllText(filePath, sb.ToString());
    }

    private void CacheOrDumpToFile(StringBuilder sb, string outPath, bool force = false)
    {
        if (sb.Length > SingleFileLength || force)
            try
            {
                var filePath = Path.Combine(outPath, $"xlua_dump_{Count}.lua");
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
        }

        sb.AppendLine($"local {csClassType.Name} = {{}}");

        // Write fields
        foreach (var field in csClassType.Fields)
        {
            // 检查字段类型
            TypeTracker.CheckAndRecordType(field.TypeName);
            
            LuaAnnotationFormatter.WriteCommentAndLocation(sb, field.Comment, field.Location);
            LuaAnnotationFormatter.WriteFieldAnnotation(sb, field.TypeName, csClassType.Name, field.Name);
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
        
        // 检查接口继承
        foreach (var iface in csInterface.Interfaces)
        {
            TypeTracker.CheckAndRecordType(iface);
        }
        
        // 检查字段类型
        foreach (var field in csInterface.Fields)
        {
            TypeTracker.CheckAndRecordType(field.TypeName);
        }
        
        // 检查方法类型
        foreach (var method in csInterface.Methods)
        {
            TypeTracker.CheckAndRecordType(method.ReturnTypeName);
            foreach (var param in method.Params)
            {
                TypeTracker.CheckAndRecordType(param.TypeName);
            }
        }
        
        sb.AppendLine($"---@interface {csInterface.Name}");
    }

    private void HandleCsEnumType(CSEnumType csEnumType, StringBuilder sb)
    {
        RegisterNamespace(csEnumType.Namespace, csEnumType.Name);

        var classFullName = GetFullTypeName(csEnumType.Namespace, csEnumType.Name);

        LuaAnnotationFormatter.WriteCommentAndLocation(sb, csEnumType.Comment, csEnumType.Location);
        LuaAnnotationFormatter.WriteTypeAnnotation(sb, "enum", classFullName);

        sb.AppendLine($"local {csEnumType.Name} = {{");

        foreach (var field in csEnumType.Fields)
        {
            LuaAnnotationFormatter.WriteCommentAndLocation(sb, field.Comment, field.Location, 4);
            // 使用实际的枚举值，如果没有则默认为 0
            var enumValue = field.ConstantValue ?? 0;
            sb.AppendLine($"    {field.Name} = {enumValue},");
            sb.AppendLine();
        }

        sb.AppendLine("}");
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
        
        LuaAnnotationFormatter.WriteDelegateAlias(sb, csDelegate.Name, csDelegate.InvokeMethod);
    }

    private List<CSTypeMethod> GetCtorList(CSClassType csClassType)
    {
        return csClassType.Methods.FindAll(method => method.Name == ".ctor");
    }
}