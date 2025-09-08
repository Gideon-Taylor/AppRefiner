using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;
using System.Text;
using AppRefiner.Database;

namespace AppRefiner.Refactors.QuickFixes
{
    public static class MethodNodeExtensions
    {
        /// <summary>
        /// Generates a method header for insertion into a class declaration
        /// </summary>
        /// <param name="method">The method node to generate a header for</param>
        /// <param name="implementsComment">Optional comment indicating what this implements (e.g., "BaseClass.MethodName")</param>
        /// <returns>The formatted method header string</returns>
        public static string GenerateHeader(this MethodNode method, string? implementsComment = null)
        {
            var parameters = method.Parameters.Select(p =>
            {
                var paramType = p.Type?.ToString() ?? "any";
                var outModifier = p.IsOut ? " out" : "";
                return $"{p.Name} As {paramType}{outModifier}";
            });

            var parameterList = string.Join(", ", parameters);
            var returnType = method.ReturnType != null ? $" returns {method.ReturnType}" : "";
            var comment = !string.IsNullOrEmpty(implementsComment) ? $" /* Implements {implementsComment} */" : "";

            return $"   method {method.Name}({parameterList}){returnType};{comment}";
        }

        /// <summary>
        /// Generates a complete default method implementation
        /// </summary>
        /// <param name="method">The method node to generate an implementation for</param>
        /// <param name="options">Options controlling the implementation generation</param>
        /// <returns>The complete method implementation with proper spacing</returns>
        public static string GenerateDefaultImplementation(this MethodNode method, MethodImplementationOptions options)
        {
            var parameterAnnotations = GenerateParameterAnnotations(method);
            var returnTypeAnnotation = GenerateReturnTypeAnnotation(method);
            var overrideAnnotation = GenerateOverrideAnnotation(method, options);
            var methodBody = GenerateMethodBody(method, options);

            var implementation = $"method {method.Name}" + Environment.NewLine +
                                parameterAnnotations +
                                returnTypeAnnotation +
                                overrideAnnotation +
                                methodBody +
                                "end-method;";

            return Environment.NewLine + implementation + Environment.NewLine + Environment.NewLine;
        }

        /// <summary>
        /// Generates parameter annotations in the proper format with commas
        /// </summary>
        private static string GenerateParameterAnnotations(MethodNode method)
        {
            if (method.Parameters.Count == 0)
                return string.Empty;

            var annotations = new List<string>();
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var paramType = param.Type?.ToString() ?? "any";
                var outModifier = param.IsOut ? " out" : "";
                var comma = (i < method.Parameters.Count - 1) ? "," : ""; // Add comma except for last parameter
                annotations.Add($"   /+ {param.Name} as {paramType}{outModifier}{comma} +/");
            }

            return string.Join(Environment.NewLine, annotations) + Environment.NewLine;
        }

        /// <summary>
        /// Generates return type annotation if the method has a return type
        /// </summary>
        private static string GenerateReturnTypeAnnotation(MethodNode method)
        {
            if (method.ReturnType != null)
            {
                return $"   /+ Returns {method.ReturnType} +/" + Environment.NewLine;
            }

            return string.Empty;
        }

        /// <summary>
        /// Generates override/implements annotation if specified in options
        /// </summary>
        private static string GenerateOverrideAnnotation(MethodNode method, MethodImplementationOptions options)
        {
            if (!string.IsNullOrEmpty(options.ImplementsComment))
            {
                return $"   /+ Extends/implements {options.ImplementsComment} +/" + Environment.NewLine;
            }

            return string.Empty;
        }

        /// <summary>
        /// Generates the method body based on the implementation type
        /// </summary>
        private static string GenerateMethodBody(MethodNode method, MethodImplementationOptions options)
        {
            var indent = "   ";
            var methodBody = "";

            if (method.IsConstructor || options.Type == ImplementationType.Constructor)
            {
                // Constructor body - call super constructor
                if (!string.IsNullOrEmpty(options.BaseClassPath))
                {
                    var parameterList = string.Join(", ", method.Parameters.Select(p => p.Name));
                    methodBody = $"{indent}%Super = create {options.BaseClassPath}({parameterList});" + Environment.NewLine;
                }
            }
            else
            {
                // Regular method body - throw not implemented exception
                var declaringType = !string.IsNullOrEmpty(options.BaseClassPath) ? options.BaseClassPath : "base class";
                var className = !string.IsNullOrEmpty(options.TargetClassName) ? options.TargetClassName : "class";
                
                string errorMessage = options.Type switch
                {
                    ImplementationType.Abstract => $"{declaringType}.{method.Name} not implemented for {className}",
                    ImplementationType.Missing => $"Method '{method.Name}' not implemented.",
                    _ => $"Method '{method.Name}' not implemented."
                };

                methodBody = $"{indent}throw CreateException(0, 0, \"{errorMessage}\");" + Environment.NewLine;
            }

            // Add return statement if method has a return type
            if (method.ReturnType != null)
            {
                var defaultValue = GetDefaultValueForType(method.ReturnType.ToString());
                methodBody += $"{indent}Return {defaultValue};" + Environment.NewLine;
            }

            return methodBody;
        }

        /// <summary>
        /// Gets the default value for a PeopleCode type
        /// </summary>
        private static string GetDefaultValueForType(string typeName)
        {
            switch (typeName.ToLower())
            {
                case "boolean":
                    return "False";
                case "integer":
                case "number":
                case "float":
                    return "0";
                case "string":
                    return "\"\"";
                case "date":
                case "time":
                case "datetime":
                    return "Null";
                default:
                    return "Null";
            }
        }
    }

    /// <summary>
    /// Options for controlling method implementation generation
    /// </summary>
    public class MethodImplementationOptions
    {
        /// <summary>
        /// Comment indicating what this method implements (e.g., "BaseClass.MethodName")
        /// </summary>
        public string? ImplementsComment { get; set; }

        /// <summary>
        /// Base class path for constructor super calls or error messages
        /// </summary>
        public string? BaseClassPath { get; set; }

        /// <summary>
        /// Target class name for error messages
        /// </summary>
        public string? TargetClassName { get; set; }

        /// <summary>
        /// Type of implementation being generated
        /// </summary>
        public ImplementationType Type { get; set; } = ImplementationType.Abstract;
    }

    /// <summary>
    /// Types of method implementations that can be generated
    /// </summary>
    public enum ImplementationType
    {
        Abstract,       // Abstract method implementation
        Constructor,    // Constructor implementation
        Missing         // Missing method implementation
    }
}