using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EmmyLua.Unity.Generator.ToLua;

/// <summary>
/// Finds classes marked with ToLua binding in CustomSettings
/// ToLua uses _GT() method to add types to customTypeList
/// </summary>
public class ToLuaClassFinder
{
    public List<INamedTypeSymbol> GetAllValidTypes(Compilation compilation)
    {
        var toLuaBindMembers = new List<INamedTypeSymbol>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // 查找 CustomSettings 类
            var classDeclarations = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.Text == "CustomSettings");

            foreach (var classDeclaration in classDeclarations)
            {
                // 查找 customTypeList 字段
                var fieldDeclarations = classDeclaration.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>();

                foreach (var fieldDeclaration in fieldDeclarations)
                {
                    var variable = fieldDeclaration.Declaration.Variables.FirstOrDefault();
                    if (variable?.Identifier.Text == "customTypeList")
                    {
                        toLuaBindMembers.AddRange(AnalyzeCustomTypeList(variable, semanticModel));
                    }
                }
            }
        }

        return toLuaBindMembers;
    }

    /// <summary>
    /// 分析 customTypeList 中的 _GT(typeof(Type)) 调用
    /// </summary>
    private List<INamedTypeSymbol> AnalyzeCustomTypeList(VariableDeclaratorSyntax variable, SemanticModel semanticModel)
    {
        var types = new List<INamedTypeSymbol>();

        if (variable.Initializer?.Value is not InitializerExpressionSyntax initializer)
        {
            return types;
        }

        // 遍历数组初始化器中的每个表达式
        foreach (var expression in initializer.Expressions)
        {
            // 查找 _GT(typeof(...)) 调用
            if (expression is InvocationExpressionSyntax invocation)
            {
                var identifierName = invocation.Expression as IdentifierNameSyntax;
                if (identifierName?.Identifier.Text == "_GT")
                {
                    // 提取 typeof(...) 参数
                    var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
                    if (argument?.Expression is TypeOfExpressionSyntax typeOfExpr)
                    {
                        var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                        if (typeInfo.Type is INamedTypeSymbol namedType)
                        {
                            types.Add(namedType);
                        }
                    }
                }
            }
        }

        return types;
    }
}
