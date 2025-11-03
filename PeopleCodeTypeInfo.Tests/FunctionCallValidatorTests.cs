using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Functions;
using PeopleCodeTypeInfo.Types;
using PeopleCodeTypeInfo.Validation;
using Xunit;

namespace PeopleCodeTypeInfo.Tests;

/// <summary>
/// Tests for FunctionCallValidator backtracking and complex parameter matching
/// </summary>
public class FunctionCallValidatorTests
{
    /// <summary>
    /// Tests the backtracking fix for nested VariableParameter inside ParameterGroup.
    ///
    /// Signature: GetPageField(page: string, scrollpath: ((recname: @Record, row_num: number){0-2}, target_rec: @Record)?, target_row: number?, fieldname: string) -> field
    ///
    /// Test case: string, @RECORD, number, string
    /// Expected: Valid - the {0-2} repetition should try 0 iterations, allowing target_rec to consume the @RECORD
    /// </summary>
    [Fact]
    public void ValidateGetPageField_WithScrollpathTargetRecOnly_ShouldSucceed()
    {
        // Build the GetPageField signature
        var functionInfo = new FunctionInfo
        {
            Name = "GetPageField",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Field),
            Parameters = new List<Parameter>
            {
                // [0] page: string
                new SingleParameter
                {
                    Name = "page",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.String)
                },

                // [1] scrollpath?: ((recname: @Record, row_num: number){0-2}, target_rec: @Record)
                new VariableParameter
                {
                    Name = "scrollpath",
                    MinCount = 0,
                    MaxCount = 1,
                    InnerParameter = new ParameterGroup
                    {
                        Name = "scrollpath",
                        Parameters = new List<Parameter>
                        {
                            // Inner repetition: (recname: @Record, row_num: number){0-2}
                            new VariableParameter
                            {
                                MinCount = 0,
                                MaxCount = 2,
                                InnerParameter = new ParameterGroup
                                {
                                    Parameters = new List<Parameter>
                                    {
                                        new ReferenceParameter(PeopleCodeType.Record)
                                        {
                                            Name = "recname"
                                        },
                                        new SingleParameter
                                        {
                                            Name = "row_num",
                                            ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                                        }
                                    }
                                }
                            },
                            // target_rec: @Record
                            new ReferenceParameter(PeopleCodeType.Record)
                            {
                                Name = "target_rec"
                            }
                        }
                    }
                },

                // [2] target_row?: number
                new VariableParameter
                {
                    Name = "target_row",
                    MinCount = 0,
                    MaxCount = 1,
                    InnerParameter = new SingleParameter
                    {
                        ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                    }
                },

                // [3] fieldname: string
                new SingleParameter
                {
                    Name = "fieldname",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.String)
                }
            }
        };

        // Test case: string, @RECORD, number, string
        // This should match:
        // - page = string
        // - scrollpath: {0-2} takes 0 iterations, target_rec takes @RECORD
        // - target_row = number
        // - fieldname = string
        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String)
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        // Assert: Should be valid
        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests GetPageField with 1 repetition of the scrollpath inner group
    /// Test case: string, @RECORD, number, @RECORD, number, string
    /// </summary>
    [Fact]
    public void ValidateGetPageField_WithOneScrollpathRepetition_ShouldSucceed()
    {
        var functionInfo = BuildGetPageFieldSignature();

        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String)
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests GetPageField with 2 repetitions of the scrollpath inner group
    /// Test case: string, @RECORD, number, @RECORD, number, @RECORD, number, string
    /// </summary>
    [Fact]
    public void ValidateGetPageField_WithTwoScrollpathRepetitions_ShouldSucceed()
    {
        var functionInfo = BuildGetPageFieldSignature();

        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String)
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests GetPageField with scrollpath omitted entirely
    /// Test case: string, string
    /// </summary>
    [Fact]
    public void ValidateGetPageField_WithoutScrollpath_ShouldSucceed()
    {
        var functionInfo = BuildGetPageFieldSignature();

        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String)
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests GetPageField with scrollpath but without target_row
    /// Test case: string, @RECORD, string
    /// </summary>
    [Fact]
    public void ValidateGetPageField_WithScrollpathWithoutTargetRow_ShouldSucceed()
    {
        var functionInfo = BuildGetPageFieldSignature();

        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.String)
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    private static FunctionInfo BuildGetPageFieldSignature()
    {
        return new FunctionInfo
        {
            Name = "GetPageField",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Field),
            Parameters = new List<Parameter>
            {
                new SingleParameter
                {
                    Name = "page",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.String)
                },
                new VariableParameter
                {
                    Name = "scrollpath",
                    MinCount = 0,
                    MaxCount = 1,
                    InnerParameter = new ParameterGroup
                    {
                        Name = "scrollpath",
                        Parameters = new List<Parameter>
                        {
                            new VariableParameter
                            {
                                MinCount = 0,
                                MaxCount = 2,
                                InnerParameter = new ParameterGroup
                                {
                                    Parameters = new List<Parameter>
                                    {
                                        new ReferenceParameter(PeopleCodeType.Record)
                                        {
                                            Name = "recname"
                                        },
                                        new SingleParameter
                                        {
                                            Name = "row_num",
                                            ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                                        }
                                    }
                                }
                            },
                            new ReferenceParameter(PeopleCodeType.Record)
                            {
                                Name = "target_rec"
                            }
                        }
                    }
                },
                new VariableParameter
                {
                    Name = "target_row",
                    MinCount = 0,
                    MaxCount = 1,
                    InnerParameter = new SingleParameter
                    {
                        ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                    }
                },
                new SingleParameter
                {
                    Name = "fieldname",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.String)
                }
            }
        };
    }

    /// <summary>
    /// Tests backtracking for FetchValue signature where scrollpath is a required ParameterGroup
    /// containing a variable parameter.
    ///
    /// Signature: FetchValue(scrollpath: ((recname: @Record|field, row_num: number){0-2}, target_rec_or_field: @Record|field), target_row: number, fieldname: field|string?) -> any
    ///
    /// Test case: @RECORD, number, field
    /// Expected: Valid - the {0-2} should match 0 times, target_rec_or_field should consume @RECORD,
    ///           target_row should consume number, fieldname should consume field
    /// </summary>
    [Fact]
    public void ValidateFetchValue_WithZeroScrollpathRepetitions_ShouldSucceed()
    {
        var functionInfo = BuildFetchValueSignature();

        var argumentTypes = new TypeInfo[]
        {
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Field)
        };

        var arguments = argumentTypes;
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests FetchValue with 1 repetition of the scrollpath inner group
    /// Test case: @RECORD, number, @RECORD, number, field
    /// </summary>
    [Fact]
    public void ValidateFetchValue_WithOneScrollpathRepetition_ShouldSucceed()
    {
        var functionInfo = BuildFetchValueSignature();

        var argumentTypes = new TypeInfo[]
        {
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Field)
        };

        var arguments = argumentTypes;
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests FetchValue with 2 repetitions of the scrollpath inner group
    /// Test case: @RECORD, number, @RECORD, number, @RECORD, number, field
    /// </summary>
    [Fact]
    public void ValidateFetchValue_WithTwoScrollpathRepetitions_ShouldSucceed()
    {
        var functionInfo = BuildFetchValueSignature();

        var argumentTypes = new TypeInfo[]
        {
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Field)
        };

        var arguments = argumentTypes;
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    /// <summary>
    /// Tests FetchValue with field type for recname (testing the union @Record|field)
    /// Test case: field, number, @RECORD, number, field
    /// </summary>
    [Fact]
    public void ValidateFetchValue_WithFieldInScrollpath_ShouldSucceed()
    {
        var functionInfo = BuildFetchValueSignature();

        var argumentTypes = new TypeInfo[]
        {
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Field),  // recname can be field
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            new ReferenceTypeInfo(PeopleCodeType.Record, "test", "test"),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Number),
            TypeInfo.FromPeopleCodeType(PeopleCodeType.Field)
        };

        var arguments = argumentTypes;
        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var result = validator.Validate(functionInfo, arguments);

        Assert.True(result.IsValid, $"Validation failed: {result.GetDetailedError()}");
    }

    private static FunctionInfo BuildFetchValueSignature()
    {
        return new FunctionInfo
        {
            Name = "FetchValue",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Any),
            Parameters = new List<Parameter>
            {
                // scrollpath is a REQUIRED ParameterGroup (not wrapped in VariableParameter like GetPageField)
                new ParameterGroup
                {
                    Name = "scrollpath",
                    Parameters = new List<Parameter>
                    {
                        // Variable parameter: (recname: @Record|field, row_num: number){0-2}
                        new VariableParameter
                        {
                            MinCount = 0,
                            MaxCount = 2,
                            InnerParameter = new ParameterGroup
                            {
                                Parameters = new List<Parameter>
                                {
                                    new UnionParameter
                                    {
                                        Name = "recname",
                                        AllowedTypes = new List<TypeWithDimensionality>
                                        {
                                            new TypeWithDimensionality(PeopleCodeType.Record, isReference: true),
                                            TypeWithDimensionality.CreateBuiltIn(PeopleCodeType.Field)
                                        }
                                    },
                                    new SingleParameter
                                    {
                                        Name = "row_num",
                                        ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                                    }
                                }
                            }
                        },
                        // target_rec_or_field: @Record|field (required within the group)
                        new UnionParameter
                        {
                            Name = "target_rec_or_field",
                            AllowedTypes = new List<TypeWithDimensionality>
                            {
                                new TypeWithDimensionality(PeopleCodeType.Record, isReference: true),
                                TypeWithDimensionality.CreateBuiltIn(PeopleCodeType.Field)
                            }
                        }
                    }
                },
                // target_row: number (REQUIRED - not optional)
                new SingleParameter
                {
                    Name = "target_row",
                    ParameterType = new TypeWithDimensionality(PeopleCodeType.Number)
                },
                // fieldname: field|string? (OPTIONAL)
                new VariableParameter
                {
                    Name = "fieldname",
                    MinCount = 0,
                    MaxCount = 1,
                    InnerParameter = new UnionParameter
                    {
                        AllowedTypes = new List<TypeWithDimensionality>
                        {
                            TypeWithDimensionality.CreateBuiltIn(PeopleCodeType.Field),
                            TypeWithDimensionality.CreateBuiltIn(PeopleCodeType.String)
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Tests that passing 'object' to an AppClass parameter issues a warning.
    /// Should pass validation but generate ImplicitNarrowingToAppClass warning.
    /// </summary>
    [Fact]
    public void ValidateObjectToAppClass_ShouldPassWithWarning()
    {
        // Create a function that accepts an AppClass parameter
        var functionInfo = new FunctionInfo
        {
            Name = "acceptVisitor",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = new List<Parameter>
            {
                new SingleParameter
                {
                    Name = "oVisitor",
                    ParameterType = new TypeWithDimensionality(
                        PeopleCodeType.AppClass,
                        0,
                        "AUC_TREE:AbstractVisitor")
                }
            }
        };

        // Pass an 'object' type argument
        var argumentTypes = new TypeInfo[]
        {
            ObjectTypeInfo.Instance
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        // Should be valid
        Assert.True(result.IsValid, $"Validation should pass but failed: {result.GetDetailedError()}");

        // Should have exactly one warning
        Assert.Single(result.Warnings);

        // Check warning details
        var warning = result.Warnings[0];
        Assert.Equal(TypeWarningKind.ImplicitNarrowingToAppClass, warning.Kind);
        Assert.Equal(0, warning.ArgumentIndex);
        Assert.Equal("AUC_TREE:AbstractVisitor", warning.ExpectedType);
        Assert.Equal("object", warning.FoundType);
        Assert.Equal("acceptVisitor", warning.FunctionName);

        // Check default message contains useful info
        var message = warning.GetDefaultMessage();
        Assert.Contains("object", message);
        Assert.Contains("AUC_TREE:AbstractVisitor", message);
        Assert.Contains("cast", message);
    }

    /// <summary>
    /// Tests that passing 'any' to an AppClass parameter issues a warning.
    /// Should pass validation but generate ImplicitNarrowingToAppClass warning.
    /// </summary>
    [Fact]
    public void ValidateAnyToAppClass_ShouldPassWithWarning()
    {
        // Create a function that accepts an AppClass parameter
        var functionInfo = new FunctionInfo
        {
            Name = "processNode",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = new List<Parameter>
            {
                new SingleParameter
                {
                    Name = "node",
                    ParameterType = new TypeWithDimensionality(
                        PeopleCodeType.AppClass,
                        0,
                        "MyPackage:TreeNode")
                }
            }
        };

        // Pass an 'any' type argument
        var argumentTypes = new TypeInfo[]
        {
            AnyTypeInfo.Instance
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        // Should be valid
        Assert.True(result.IsValid, $"Validation should pass but failed: {result.GetDetailedError()}");

        // Should have exactly one warning
        Assert.Single(result.Warnings);

        // Check warning details
        var warning = result.Warnings[0];
        Assert.Equal(TypeWarningKind.ImplicitNarrowingToAppClass, warning.Kind);
        Assert.Equal(0, warning.ArgumentIndex);
        Assert.Equal("MyPackage:TreeNode", warning.ExpectedType);
        Assert.Equal("any", warning.FoundType);
        Assert.Equal("processNode", warning.FunctionName);
    }

    /// <summary>
    /// Tests that passing a matching AppClass type does NOT generate a warning.
    /// </summary>
    [Fact]
    public void ValidateMatchingAppClass_ShouldPassWithoutWarning()
    {
        var functionInfo = new FunctionInfo
        {
            Name = "acceptVisitor",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Void),
            Parameters = new List<Parameter>
            {
                new SingleParameter
                {
                    Name = "oVisitor",
                    ParameterType = new TypeWithDimensionality(
                        PeopleCodeType.AppClass,
                        0,
                        "AUC_TREE:AbstractVisitor")
                }
            }
        };

        // Pass a matching AppClass type
        var argumentTypes = new TypeInfo[]
        {
            new AppClassTypeInfo("AUC_TREE:AbstractVisitor")
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        // Should be valid
        Assert.True(result.IsValid);

        // Should have NO warnings
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Tests that passing object to multiple AppClass parameters generates multiple warnings.
    /// </summary>
    [Fact]
    public void ValidateMultipleObjectToAppClass_ShouldGenerateMultipleWarnings()
    {
        var functionInfo = new FunctionInfo
        {
            Name = "compareNodes",
            ReturnType = new TypeWithDimensionality(PeopleCodeType.Boolean),
            Parameters = new List<Parameter>
            {
                new SingleParameter
                {
                    Name = "node1",
                    ParameterType = new TypeWithDimensionality(
                        PeopleCodeType.AppClass,
                        0,
                        "TreeNode")
                },
                new SingleParameter
                {
                    Name = "node2",
                    ParameterType = new TypeWithDimensionality(
                        PeopleCodeType.AppClass,
                        0,
                        "TreeNode")
                }
            }
        };

        // Pass two 'any' arguments
        var argumentTypes = new TypeInfo[]
        {
            AnyTypeInfo.Instance,
            AnyTypeInfo.Instance
        };

        var validator = new FunctionCallValidator(NullTypeMetadataResolver.Instance);
        var arguments = argumentTypes;
        var result = validator.Validate(functionInfo, arguments);

        // Should be valid
        Assert.True(result.IsValid);

        // Should have TWO warnings
        Assert.Equal(2, result.Warnings.Count);

        // Check first warning
        Assert.Equal(0, result.Warnings[0].ArgumentIndex);
        Assert.Equal("TreeNode", result.Warnings[0].ExpectedType);

        // Check second warning
        Assert.Equal(1, result.Warnings[1].ArgumentIndex);
        Assert.Equal("TreeNode", result.Warnings[1].ExpectedType);
    }
}
