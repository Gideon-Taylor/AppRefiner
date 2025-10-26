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
        var arguments = argumentTypes.Select(t => ArgumentInfo.NonVariable(t)).ToArray();
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
        var arguments = argumentTypes.Select(t => ArgumentInfo.NonVariable(t)).ToArray();
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
        var arguments = argumentTypes.Select(t => ArgumentInfo.NonVariable(t)).ToArray();
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
        var arguments = argumentTypes.Select(t => ArgumentInfo.NonVariable(t)).ToArray();
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
        var arguments = argumentTypes.Select(t => ArgumentInfo.NonVariable(t)).ToArray();
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
}
