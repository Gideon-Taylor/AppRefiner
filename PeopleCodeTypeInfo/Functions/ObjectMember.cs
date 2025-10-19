using PeopleCodeTypeInfo.Types;

namespace PeopleCodeTypeInfo.Functions;

/// <summary>
/// Base class for object members (methods and properties)
/// </summary>
public abstract class ObjectMember
{
    /// <summary>
    /// Name of the member
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Type of member (method or property)
    /// </summary>
    public abstract ObjectMemberType MemberType { get; }
}

/// <summary>
/// Enumeration of object member types
/// </summary>
public enum ObjectMemberType
{
    Method,
    Property
}

/// <summary>
/// Represents a method member of an object
/// </summary>
public class MethodMember : ObjectMember
{
    public override ObjectMemberType MemberType => ObjectMemberType.Method;

    /// <summary>
    /// Method information
    /// </summary>
    public FunctionInfo Method { get; set; } = new();

    public MethodMember() { }

    public MethodMember(string name, FunctionInfo method)
    {
        Name = name;
        Method = method;
        Method.Name = name; // Ensure consistency
    }

    /// <summary>
    /// Whether this is a default method
    /// </summary>
    public bool IsDefault => Method.IsDefaultMethod;

    /// <summary>
    /// Return type of the method
    /// </summary>
    public PeopleCodeType ReturnType => Method.ReturnType.Type;

    /// <summary>
    /// Parameters of the method
    /// </summary>
    public List<Parameter> Parameters => Method.Parameters;
}

/// <summary>
/// Represents a property member of an object
/// </summary>
public class PropertyMember : ObjectMember
{
    public override ObjectMemberType MemberType => ObjectMemberType.Property;

    /// <summary>
    /// Property information
    /// </summary>
    public PropertyInfo Property { get; set; }

    public PropertyMember() { }

    public PropertyMember(string name, PropertyInfo property)
    {
        Name = name;
        Property = property;
    }

    /// <summary>
    /// Type of the property
    /// </summary>
    public PeopleCodeType Type => Property.Type;

    /// <summary>
    /// Whether the property is an array
    /// </summary>
    public bool IsArray => Property.IsArray;

    /// <summary>
    /// Array dimensionality
    /// </summary>
    public byte ArrayDimensionality => Property.ArrayDimensionality;
}
