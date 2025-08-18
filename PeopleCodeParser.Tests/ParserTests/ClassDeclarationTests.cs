using FluentAssertions;
using Xunit;
using PeopleCodeParser.SelfHosted;

namespace PeopleCodeParser.Tests.ParserTests;

/// <summary>
/// Tests for class and interface declaration parsing
/// </summary>
public class ClassDeclarationTests
{
    [Theory]
    [InlineData("CLASS MyClass END-CLASS;", "Simple class declaration")]
    [InlineData("CLASS MyClass EXTENDS BaseClass END-CLASS;", "Class with inheritance")]
    [InlineData("CLASS MyClass IMPLEMENTS IMyInterface END-CLASS;", "Class with interface implementation")]
    public void Should_Parse_Basic_Class_Declarations(string classDeclaration, string description)
    {
        var parseTree = ProgramParser.Parse(classDeclaration);
        parseTree.Should().NotBeNull(description);
    }

    [Theory]
    [InlineData("INTERFACE IMyInterface END-INTERFACE;", "Simple interface declaration")]
    [InlineData("INTERFACE IMyInterface EXTENDS IBaseInterface END-INTERFACE;", "Interface with inheritance")]
    public void Should_Parse_Interface_Declarations(string interfaceDeclaration, string description)
    {
        var parseTree = ProgramParser.Parse(interfaceDeclaration);
        parseTree.Should().NotBeNull(description);
    }

    [Fact]
    public void Should_Parse_Class_With_Visibility_Sections()
    {
        var sourceCode = @"
        CLASS MyClass
            METHOD GetName() RETURNS string;
            PROPERTY string Name GET SET;
            
        PROTECTED
            METHOD Initialize();
            
        PRIVATE
            INSTANCE string &privateName;
            CONSTANT PI = 3.14159;
        END-CLASS;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Class with visibility sections should parse");
    }

    [Fact]
    public void Should_Parse_Class_With_Method_Implementations()
    {
        var sourceCode = @"
        CLASS Calculator
            METHOD Add(&a AS number, &b AS number) RETURNS number;
            METHOD Subtract(&a AS number, &b AS number) RETURNS number;
        END-CLASS;
        
        METHOD Calculator.Add
        /+ &a AS number, &b AS number +/
        /+ RETURNS number +/
            RETURN &a + &b;
        END-METHOD;
        
        METHOD Calculator.Subtract
        /+ &a AS number, &b AS number +/
        /+ RETURNS number +/
            RETURN &a - &b;
        END-METHOD;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Class with method implementations should parse");
    }

    [Fact]
    public void Should_Parse_Class_With_Properties()
    {
        var sourceCode = @"
        CLASS Person
            PROPERTY string Name GET SET;
            PROPERTY number Age GET SET;
            PROPERTY string Email READONLY;
            
        PRIVATE
            INSTANCE string &name;
            INSTANCE number &age;
            INSTANCE string &email;
        END-CLASS;
        
        GET Person.Name
        /+ RETURNS string +/
            RETURN &name;
        END-GET;
        
        SET Person.Name
        /+ &value AS string +/
            &name = &value;
        END-SET;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Class with properties should parse");
    }

    [Fact]
    public void Should_Parse_Class_With_Constants_And_Instances()
    {
        var sourceCode = @"
        CLASS Configuration
            PROPERTY string DatabaseUrl GET;
            
        PRIVATE
            CONSTANT DEFAULT_TIMEOUT = 30;
            CONSTANT MAX_RETRIES = 3;
            
            INSTANCE string &databaseUrl;
            INSTANCE number &timeout;
            INSTANCE boolean &isConnected;
        END-CLASS;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Class with constants and instances should parse");
    }

    [Fact]
    public void Should_Parse_Interface_With_Method_Signatures()
    {
        var sourceCode = @"
        INTERFACE IRepository
            METHOD Save(&entity AS any) RETURNS boolean;
            METHOD FindById(&id AS string) RETURNS any;
            METHOD Delete(&id AS string) RETURNS boolean;
            METHOD GetAll() RETURNS array of any;
        END-INTERFACE;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Interface with method signatures should parse");
    }

    [Fact]
    public void Should_Parse_Class_With_Package_References()
    {
        var sourceCode = @"
        IMPORT MyPackage:BaseClasses:*;
        IMPORT MyPackage:Interfaces:ILogger;
        
        CLASS MyService EXTENDS MyPackage:BaseClasses:BaseService 
                       IMPLEMENTS MyPackage:Interfaces:ILogger
            METHOD DoWork();
        END-CLASS;
        ";

        var parseTree = ProgramParser.Parse(sourceCode);
        parseTree.Should().NotBeNull("Class with package references should parse");
    }

    [Theory]
    [InlineData("CLASS MyClass EXTENDS Exception END-CLASS;", "Class extending Exception")]
    [InlineData("CLASS MyClass EXTENDS string END-CLASS;", "Class extending built-in type")]
    [InlineData("CLASS MyClass EXTENDS MyPackage:MyBaseClass END-CLASS;", "Class extending package class")]
    public void Should_Parse_Different_Superclass_Types(string classDeclaration, string description)
    {
        var parseTree = ProgramParser.Parse(classDeclaration);
        parseTree.Should().NotBeNull(description);
    }
}