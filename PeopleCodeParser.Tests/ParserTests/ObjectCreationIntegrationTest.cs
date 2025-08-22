using FluentAssertions;
using PeopleCodeParser.Tests.Utilities;

namespace PeopleCodeParser.Tests.ParserTests;

public class ObjectCreationIntegrationTest
{
    [Fact]
    public void Should_Parse_Complex_Expressions_Including_Object_Creation()
    {
        // Arrange - Complex expressions including: LOCAL any &result4 = CREATE MyPackage:MyClass(&param1, &param2);
        var complexCode = @"
            LOCAL number &result1 = (1 + 2) * 3 ** 2 / 4 - 5;
            LOCAL boolean &result2 = &a AND (&b OR NOT &c) AND (&d >= &e);
            LOCAL string &result3 = &str1 | "" - "" | &str2 | "" (concatenated)"";
            LOCAL any &result4 = CREATE MyPackage:MyClass(&param1, &param2);
            LOCAL MyPackage:MyClass &result5 = &obj AS MyPackage:MyClass();
            LOCAL number &result6 = &array[&index1][&index2].Property.Method(&arg);
        ";
        
        // Act
        var result = TestHelper.ParseAndAssertSuccess(complexCode);
        
        // Assert
        result.Should().NotBeNull();
        // The test is mainly that it doesn't throw an exception and parses successfully
        // The specific CREATE statement with package path should work correctly
    }
}