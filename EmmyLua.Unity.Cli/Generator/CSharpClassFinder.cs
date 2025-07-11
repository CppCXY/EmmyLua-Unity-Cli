using System.Collections.Concurrent;
using EmmyLua.Unity.Generator.XLua;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CustomSymbolFinder
{
    public static List<INamedTypeSymbol> GetAllSymbols(Compilation compilation, GenerateOptions o)
    {
        switch (o.BindingType)
        {
            case LuaBindingType.XLua:
            {
                var finder = new XLuaClassFinder();
                return finder.GetAllValidTypes(compilation);
            }
            default:
                return [];
        }
    }
}