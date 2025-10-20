using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSharpAnalyzer
{
    private List<CSType> CsTypes { get; } = [];

    private Dictionary<string, List<CSTypeMethod>> ExtendMethods { get; } = [];

    public void AnalyzeType(INamedTypeSymbol namedType)
    {
        try
        {
            if (namedType.IsNamespace) return;

            var csType = namedType.TypeKind switch
            {
                TypeKind.Class or TypeKind.Struct => AnalyzeClassType(namedType),
                TypeKind.Interface => AnalyzeInterfaceType(namedType),
                TypeKind.Enum => AnalyzeEnumType(namedType),
                TypeKind.Delegate => AnalyzeDelegateType(namedType),
                _ => null
            };

            if (csType != null) CsTypes.Add(csType);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error analyzing type '{namedType.Name}': {e.Message}");
        }
    }

    public List<CSType> GetCsTypes()
    {
        if (ExtendMethods.Count != 0)
        {
            foreach (var csType in CsTypes)
                if (ExtendMethods.TryGetValue(csType.Name, out var methods))
                    if (csType is IHasMethods hasMethods)
                        hasMethods.Methods.AddRange(methods);

            ExtendMethods.Clear();
        }

        return CsTypes;
    }

    private void AnalyzeTypeFields(ISymbol symbol, IHasFields classType)
    {
        // 跳过索引器 - 索引器需要特殊处理，暂时不支持
        if (symbol.Name == "this[]") return;

        // 对于属性，检查 getter/setter 的可访问性
        if (symbol is IPropertySymbol propertySymbol)
        {
            // 如果是索引器，跳过
            if (propertySymbol.IsIndexer) return;

            // 检查属性的可访问性
            // 如果 getter 和 setter 都不是 public，跳过此属性
            var getterAccessibility = propertySymbol.GetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable;
            var setterAccessibility = propertySymbol.SetMethod?.DeclaredAccessibility ?? Accessibility.NotApplicable;

            if (getterAccessibility != Accessibility.Public && setterAccessibility != Accessibility.Public) return;
        }

        var field = new CSTypeField
        {
            Comment = XmlDocumentationParser.GetSummary(symbol),
            TypeName = symbol switch
            {
                IFieldSymbol fieldSymbol => fieldSymbol.Type.ToDisplayString(),
                IPropertySymbol propSymbol => propSymbol.Type.ToDisplayString(),
                IEventSymbol eventSymbol => eventSymbol.Type.ToDisplayString(),
                _ => "any"
            }
        };

        FillBaseInfo(symbol, field);
        classType.Fields.Add(field);
    }

    private void AnalyzeTypeMethods(IMethodSymbol methodSymbol, IHasMethods csClassType)
    {
        if (methodSymbol.Name.StartsWith("get_") || methodSymbol.Name.StartsWith("set_")) return;

        var method = new CSTypeMethod
        {
            IsStatic = methodSymbol.IsStatic,
            ReturnTypeName = methodSymbol.ReturnType.ToDisplayString()
        };

        FillBaseInfo(methodSymbol, method);

        var xmlDictionary = XmlDocumentationParser.GetAllDocumentation(methodSymbol);
        if (xmlDictionary.TryGetValue("<summary>", out var summary)) method.Comment = summary;
        if (methodSymbol.IsExtensionMethod)
        {
            method.IsStatic = false;
            var parameters = methodSymbol.Parameters;
            var thisParameter = parameters.FirstOrDefault();
            var thisType = thisParameter?.Type;
            if (thisType is not INamedTypeSymbol namedTypeSymbol) return;
            method.Params = methodSymbol.Parameters
                .Skip(1)
                .Select(it => new CSParam
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString(),
                    Comment = xmlDictionary.GetValueOrDefault(it.Name, "")
                }).ToList();

            if (ExtendMethods.TryGetValue(namedTypeSymbol.Name, out var extendMethod))
                extendMethod.Add(method);
            else
                ExtendMethods.Add(namedTypeSymbol.Name, [method]);
        }
        else
        {
            method.Params = methodSymbol.Parameters
                .Select(it => new CSParam
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString(),
                    Comment = xmlDictionary.GetValueOrDefault(it.Name, "")
                }).ToList();
            csClassType.Methods.Add(method);
        }
    }

    private CSType AnalyzeClassType(INamedTypeSymbol symbol)
    {
        var csType = new CSClassType
        {
            BaseClass = symbol.BaseType?.ToString() ?? "",
            IsStatic = symbol.IsStatic,
            Comment = XmlDocumentationParser.GetSummary(symbol)
        };

        FillNamespace(symbol, csType);
        FillBaseInfo(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();

        if (symbol is { TypeArguments.Length: > 0 })
            csType.GenericTypes = symbol.TypeArguments.Select(it => it.ToDisplayString()).ToList();

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
                case IMethodSymbol methodSymbol:
                    AnalyzeTypeMethods(methodSymbol, csType);
                    break;
                case IPropertySymbol propertySymbol:
                    AnalyzeTypeFields(propertySymbol, csType);
                    break;
                case IEventSymbol eventSymbol:
                    AnalyzeTypeFields(eventSymbol, csType);
                    break;
            }

        return csType;
    }

    private CSType AnalyzeInterfaceType(INamedTypeSymbol symbol)
    {
        var csType = new CSInterface
        {
            Comment = XmlDocumentationParser.GetSummary(symbol)
        };

        FillNamespace(symbol, csType);
        FillBaseInfo(symbol, csType);

        if (!symbol.AllInterfaces.IsEmpty)
            csType.Interfaces = symbol.AllInterfaces.Select(it => it.ToDisplayString()).ToList();

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
                case IMethodSymbol methodSymbol:
                    AnalyzeTypeMethods(methodSymbol, csType);
                    break;
                case IPropertySymbol propertySymbol:
                    AnalyzeTypeFields(propertySymbol, csType);
                    break;
                case IEventSymbol eventSymbol:
                    AnalyzeTypeFields(eventSymbol, csType);
                    break;
            }

        return csType;
    }

    private CSType AnalyzeEnumType(INamedTypeSymbol symbol)
    {
        var csType = new CSEnumType
        {
            Comment = XmlDocumentationParser.GetSummary(symbol)
        };

        FillNamespace(symbol, csType);
        FillBaseInfo(symbol, csType);

        foreach (var member in symbol.GetMembers().Where(it => it is { DeclaredAccessibility: Accessibility.Public }))
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    AnalyzeTypeFields(fieldSymbol, csType);
                    break;
            }

        return csType;
    }

    private CSType AnalyzeDelegateType(INamedTypeSymbol symbol)
    {
        var csType = new CSDelegate
        {
            Comment = XmlDocumentationParser.GetSummary(symbol)
        };

        FillNamespace(symbol, csType);
        FillBaseInfo(symbol, csType);

        var invokeMethod = symbol.DelegateInvokeMethod;
        if (invokeMethod != null)
        {
            var method = new CSTypeMethod();
            method.ReturnTypeName = invokeMethod.ReturnType.ToDisplayString();
            method.Params = invokeMethod.Parameters
                .Select(it => new CSParam
                {
                    Name = it.Name,
                    Nullable = it.IsOptional,
                    Kind = it.RefKind,
                    TypeName = it.Type.ToDisplayString()
                }).ToList();
            csType.InvokeMethod = method;
        }

        return csType;
    }

    private void FillNamespace(INamedTypeSymbol symbol, IHasNamespace hasNamespace)
    {
        if (symbol.ContainingSymbol is INamespaceSymbol nsSymbol)
        {
            if (nsSymbol.IsGlobalNamespace)
            {
                hasNamespace.Namespace = string.Empty;
                return;
            }

            hasNamespace.Namespace = nsSymbol.ToString()!;
        }
        else if (symbol.ContainingSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            hasNamespace.Namespace = namedTypeSymbol.ToString()!;
        }
    }

    private void FillBaseInfo(ISymbol symbol, CSTypeBase typeBase)
    {
        typeBase.Name = symbol.Name;

        if (!symbol.Locations.IsEmpty)
        {
            var location = symbol.Locations.First();
            if (location.IsInMetadata)
            {
                typeBase.Location = location.MetadataModule?.ToString() ?? "";
            }
            else if (location.IsInSource)
            {
                var lineSpan = location.SourceTree.GetLineSpan(location.SourceSpan);

                typeBase.Location =
                    $"{new Uri(location.SourceTree.FilePath)}#{lineSpan.Span.Start.Line}:{lineSpan.Span.Start.Character}";
            }
        }
    }
}