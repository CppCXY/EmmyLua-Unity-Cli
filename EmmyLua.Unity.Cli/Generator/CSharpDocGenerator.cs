using EmmyLua.Unity.Generator.XLua;
using EmmyLua.Unity.Generator.ToLua;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

public class CSharpDocGenerator(GenerateOptions o)
{
    public async Task<int> Run()
    {
        // Validate options
        var validationErrors = o.Validate();
        if (validationErrors.Count > 0)
        {
            Console.WriteLine("Configuration validation failed:");
            foreach (var error in validationErrors) Console.WriteLine($"  - {error}");
            return 1;
        }

        var slnPath = o.Solution;
        var msbuildProperties = new Dictionary<string, string>();
        foreach (var property in o.Properties)
        {
            var parts = property.Split('=', 2);
            if (parts.Length == 2) msbuildProperties.Add(parts[0].Trim(), parts[1].Trim());
        }

        try
        {
            Console.WriteLine($"Opening solution: {slnPath}");
            var compilations = await CSharpWorkspace.OpenSolutionAsync(slnPath, msbuildProperties);
            var analyzer = new CSharpAnalyzer();
            Console.WriteLine("Analyzing types...");

            var symbolCount = 0;
            foreach (var compilation in compilations)
            {
                var symbols = CustomSymbolFinder.GetAllSymbols(compilation, o);
                var publicSymbols = symbols.Where(symbol =>
                    symbol is { DeclaredAccessibility: Accessibility.Public }).ToList();

                Console.WriteLine(
                    $"  Found {publicSymbols.Count} public types in compilation '{compilation.AssemblyName}'");

                foreach (var symbol in publicSymbols)
                {
                    analyzer.AnalyzeType(symbol);
                    symbolCount++;
                }
            }

            var csTypes = analyzer.GetCsTypes();
            Console.WriteLine(
                $"Successfully analyzed {symbolCount} symbols, produced {csTypes.Count} type definitions.");

            switch (o.BindingType)
            {
                case LuaBindingType.XLua:
                    Console.WriteLine("Generating XLua bindings...");
                    var xLuaDumper = new XLuaDumper();
                    xLuaDumper.Dump(csTypes, o.Output);
                    break;

                case LuaBindingType.ToLua:
                    Console.WriteLine("Generating ToLua bindings...");
                    var toLuaDumper = new ToLuaDumper();
                    toLuaDumper.Dump(csTypes, o.Output);
                    break;

                case LuaBindingType.Puerts:
                    Console.WriteLine("Puerts binding generation is not yet implemented.");
                    return 1;

                default:
                    Console.WriteLine("Error: No binding type specified.");
                    return 1;
            }

            Console.WriteLine("Generation completed successfully!");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Fatal error: {e.Message}");
            Console.WriteLine($"Stack trace:\n{e.StackTrace}");
            return 1;
        }
    }
}