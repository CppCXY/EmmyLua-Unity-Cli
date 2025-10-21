using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

/// <summary>
/// Base class for all C# type elements
/// </summary>
public abstract class CSTypeBase
{
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// Represents a field, property or event in a C# type
/// </summary>
public class CSTypeField : CSTypeBase
{
    public string TypeName { get; set; } = string.Empty;
    /// <summary>
    /// The constant value for enum fields
    /// </summary>
    public object? ConstantValue { get; set; }
    /// <summary>
    /// Indicates if this field is an event
    /// </summary>
    public bool IsEvent { get; set; }
}

/// <summary>
/// Represents a parameter in a method or delegate
/// </summary>
public class CSParam
{
    public string Name { get; set; } = string.Empty;
    public bool Nullable { get; set; }
    public RefKind Kind { get; set; } = RefKind.None;
    public string TypeName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// Represents a method in a C# type
/// </summary>
public class CSTypeMethod : CSTypeBase
{
    public string ReturnTypeName { get; set; } = string.Empty;
    public List<CSParam> Params { get; set; } = [];
    public bool IsStatic { get; set; }
}

/// <summary>
/// Interface for types that belong to a namespace
/// </summary>
public interface IHasNamespace
{
    string Namespace { get; set; }
}

/// <summary>
/// Interface for types that contain fields
/// </summary>
public interface IHasFields
{
    List<CSTypeField> Fields { get; }
}

/// <summary>
/// Interface for types that contain methods
/// </summary>
public interface IHasMethods
{
    List<CSTypeMethod> Methods { get; }
}

/// <summary>
/// Base class for all C# types (classes, interfaces, enums, delegates)
/// </summary>
public abstract class CSType : CSTypeBase, IHasNamespace
{
    public string Namespace { get; set; } = string.Empty;
}

/// <summary>
/// Represents a C# class or struct type
/// </summary>
public sealed class CSClassType : CSType, IHasFields, IHasMethods
{
    public string BaseClass { get; set; } = string.Empty;
    public List<string> GenericTypes { get; set; } = [];
    public List<string> Interfaces { get; set; } = [];
    public List<CSTypeField> Fields { get; } = [];
    public List<CSTypeMethod> Methods { get; } = [];
    public bool IsStatic { get; set; }
}

/// <summary>
/// Represents a C# enum type
/// </summary>
public sealed class CSEnumType : CSType, IHasFields
{
    public List<CSTypeField> Fields { get; } = [];
}

/// <summary>
/// Represents a C# interface type
/// </summary>
public sealed class CSInterface : CSType, IHasFields, IHasMethods
{
    public List<string> Interfaces { get; set; } = [];
    public List<CSTypeField> Fields { get; } = [];
    public List<CSTypeMethod> Methods { get; } = [];
}

/// <summary>
/// Represents a C# delegate type
/// </summary>
public sealed class CSDelegate : CSType
{
    public CSTypeMethod InvokeMethod { get; set; } = new();
}