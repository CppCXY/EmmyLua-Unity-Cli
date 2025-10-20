using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EmmyLua.Unity.Generator.XLua;

/// <summary>
/// XLua 类型查找器，支持三种配置方式：
/// 1. 直接在类型上标记 [LuaCallCSharp]
/// 2. 静态字段标记 [LuaCallCSharp]，类型为 List&lt;Type&gt; 或 IEnumerable&lt;Type&gt;
/// 3. 静态属性标记 [LuaCallCSharp]，返回类型为 List&lt;Type&gt; 或 IEnumerable&lt;Type&gt;（动态列表）
/// </summary>
public class XLuaClassFinder
{
    /// <summary>
    /// 获取所有标记为 LuaCallCSharp 的有效类型
    /// </summary>
    public List<INamedTypeSymbol> GetAllValidTypes(Compilation compilation)
    {
        var luaCallCSharpMembers = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // 方式1: 查找直接标记在类型上的 [LuaCallCSharp]
            var typeDeclarationSyntaxes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
            foreach (var typeDeclaration in typeDeclarationSyntaxes)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                if (typeSymbol != null && HasLuaCallCSharpAttribute(typeSymbol))
                {
                    luaCallCSharpMembers.Add(typeSymbol);
                }
            }

            // 方式2 & 3: 查找静态类中标记 [LuaCallCSharp] 的字段和属性
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDeclaration in classDeclarations)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (classSymbol == null || !classSymbol.IsStatic) continue;

                // 查找标记的字段和属性
                foreach (var member in classSymbol.GetMembers())
                {
                    if (member is IFieldSymbol fieldSymbol && 
                        fieldSymbol.IsStatic && 
                        HasLuaCallCSharpAttribute(fieldSymbol) &&
                        IsEnumerableOfType(fieldSymbol.Type))
                    {
                        var types = AnalyzeMemberForTypes(fieldSymbol, semanticModel);
                        foreach (var type in types)
                        {
                            luaCallCSharpMembers.Add(type);
                        }
                    }
                    else if (member is IPropertySymbol propertySymbol &&
                             propertySymbol.IsStatic &&
                             HasLuaCallCSharpAttribute(propertySymbol) &&
                             IsEnumerableOfType(propertySymbol.Type))
                    {
                        var types = AnalyzeMemberForTypes(propertySymbol, semanticModel);
                        foreach (var type in types)
                        {
                            luaCallCSharpMembers.Add(type);
                        }
                    }
                }
            }
        }

        return luaCallCSharpMembers
            .Where(type => IsValidType(type))
            .ToList();
    }

    /// <summary>
    /// 检查符号是否有 LuaCallCSharp 属性
    /// </summary>
    private bool HasLuaCallCSharpAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "LuaCallCSharpAttribute" ||
            attr.AttributeClass?.Name == "LuaCallCSharp");
    }

    /// <summary>
    /// 检查类型是否为 IEnumerable&lt;Type&gt; 或 List&lt;Type&gt;
    /// </summary>
    private bool IsEnumerableOfType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType) return false;

        // 检查是否是 List<Type> 或 IEnumerable<Type>
        var typeString = namedType.ToString();
        return typeString == "System.Collections.Generic.List<System.Type>" ||
               typeString == "System.Collections.Generic.IEnumerable<System.Type>";
    }

    /// <summary>
    /// 分析字段或属性以提取类型列表
    /// </summary>
    private List<INamedTypeSymbol> AnalyzeMemberForTypes(ISymbol member, SemanticModel semanticModel)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var syntaxRef in member.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();

            // 分析字段初始化器
            if (syntax is VariableDeclaratorSyntax variableDeclarator)
            {
                if (variableDeclarator.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation)
                {
                    result.AddRange(ExtractTypesFromInitializer(objectCreation, semanticModel));
                }
                else if (variableDeclarator.Initializer?.Value is ImplicitObjectCreationExpressionSyntax implicitCreation)
                {
                    result.AddRange(ExtractTypesFromInitializer(implicitCreation, semanticModel));
                }
            }
            // 分析属性初始化器
            else if (syntax is PropertyDeclarationSyntax propertyDeclaration)
            {
                // 尝试分析 getter 中的返回语句
                var getter = propertyDeclaration.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration);

                if (getter?.Body != null)
                {
                    var returnStatements = getter.Body.DescendantNodes().OfType<ReturnStatementSyntax>();
                    foreach (var returnStatement in returnStatements)
                    {
                        if (returnStatement.Expression is ObjectCreationExpressionSyntax objectCreation)
                        {
                            result.AddRange(ExtractTypesFromInitializer(objectCreation, semanticModel));
                        }
                    }
                }
                // 也支持表达式主体: public static List<Type> Types => new List<Type> { ... };
                else if (propertyDeclaration.ExpressionBody?.Expression is ObjectCreationExpressionSyntax exprBodyCreation)
                {
                    result.AddRange(ExtractTypesFromInitializer(exprBodyCreation, semanticModel));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 从对象创建表达式中提取类型（支持集合初始化器）
    /// </summary>
    private List<INamedTypeSymbol> ExtractTypesFromInitializer(SyntaxNode creationExpression, SemanticModel semanticModel)
    {
        var result = new List<INamedTypeSymbol>();

        // 查找所有 typeof(...) 表达式
        var typeofExpressions = creationExpression.DescendantNodes().OfType<TypeOfExpressionSyntax>();
        foreach (var typeofExpr in typeofExpressions)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeofExpr.Type);
            if (typeInfo.Type is INamedTypeSymbol namedType)
            {
                result.Add(namedType);
            }
        }

        return result;
    }

    /// <summary>
    /// 验证类型是否有效（必须是 public 且非泛型定义）
    /// </summary>
    private bool IsValidType(INamedTypeSymbol type)
    {
        // 必须是 public 类型
        if (type.DeclaredAccessibility != Accessibility.Public)
            return false;

        // 不能是未绑定的泛型类型定义（如 List<>），但可以是构造的泛型类型（如 List<int>）
        if (type.IsUnboundGenericType)
            return false;

        // 不能是编译器生成的类型
        if (type.Name.Contains("<") || type.Name.Contains(">"))
            return false;

        return true;
    }
}