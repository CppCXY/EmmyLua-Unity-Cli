using CommandLine;

namespace EmmyLua.Unity.Generator;

/// <summary>
/// Command line options for the EmmyLua Unity generator
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class GenerateOptions
{
    [Option('s', "solution", Required = true, HelpText = "The path to the solution file(.sln).")]
    public string Solution { get; set; } = string.Empty;

    [Option('p', "properties", Required = false, HelpText = "The MSBuild properties (format: key=value).")]
    public IEnumerable<string> Properties { get; set; } = new List<string>();

    [Option('b', "bind", Required = true, HelpText = "Generate XLua/ToLua binding.")]
    public LuaBindingType BindingType { get; set; } = LuaBindingType.None;

    [Option('o', "output", Required = true, HelpText = "The output path.")]
    public string Output { get; set; } = string.Empty;

    [Option('e', "export", Required = false, HelpText = "Export type (Json/Lua).")]
    public LuaExportType ExportType { get; set; } = LuaExportType.None;

    /// <summary>
    /// Validate the options
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Solution))
        {
            errors.Add("Solution path is required.");
        }
        else if (!File.Exists(Solution))
        {
            errors.Add($"Solution file not found: {Solution}");
        }
        else if (!Solution.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Solution path must point to a .sln file.");
        }

        if (string.IsNullOrWhiteSpace(Output))
        {
            errors.Add("Output path is required.");
        }

        if (BindingType == LuaBindingType.None)
        {
            errors.Add("Binding type must be specified.");
        }

        // Validate properties format
        foreach (var property in Properties)
        {
            if (!property.Contains('='))
            {
                errors.Add($"Invalid property format: {property}. Expected format: key=value");
            }
        }

        return errors;
    }
}

/// <summary>
/// Type of Lua binding framework
/// </summary>
public enum LuaBindingType
{
    None,
    XLua,
    ToLua,
    Puerts
}

/// <summary>
/// Export format for the generated definitions
/// </summary>
public enum LuaExportType
{
    None,
    Json,
    Lua
}