using AppRefiner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that identifies classes that might be missing required constructors.
/// This styler uses the database to check if the base class has constructors with parameters,
/// which would require the derived class to have its own constructor.
/// </summary>
public class MissingConstructor : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFFF; // Red color for missing constructor errors

    public override string Description => "Missing constructors";

    /// <summary>
    /// This styler requires a database connection to check base class constructors
    /// </summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

    #region AST Visitor Overrides

    /// <summary>
    /// Processes the entire program and resets state
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        
        // Visit the program
        base.VisitProgram(node);
    }

    /// <summary>
    /// Handles application class definitions
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        // Check if this class has a constructor
        var constructorMethod = node.Methods.FirstOrDefault(m => 
            string.Equals(m.Name, node.Name, StringComparison.OrdinalIgnoreCase));

        // If no constructor and class extends another class, check if base class requires one
        if (constructorMethod == null && node.BaseClass != null)
        {
            CheckIfBaseClassRequiresConstructor(node);
        }

        base.VisitAppClass(node);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses the base class source code to get its AST representation
    /// </summary>
    /// <param name="baseClassPath">The path to the base class</param>
    /// <returns>The parsed ProgramNode containing the base class, or null if parsing fails</returns>
    private ProgramNode? ParseBaseClassAst(string baseClassPath)
    {
        if (DataManager == null || string.IsNullOrEmpty(baseClassPath))
            return null;

        try
        {
            // Get the source code of the base class from the database
            string? baseClassSource = DataManager.GetAppClassSourceByPath(baseClassPath);
            
            if (string.IsNullOrEmpty(baseClassSource))
                return null; // Base class not found in database
            
            // Parse the base class using the self-hosted parser
            var lexer = new PeopleCodeParser.SelfHosted.Lexing.PeopleCodeLexer(baseClassSource);
            var tokens = lexer.TokenizeAll();
            
            var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
            return parser.ParseProgram();
        }
        catch (Exception)
        {
            // Silently handle database or parsing errors
            return null;
        }
    }

    /// <summary>
    /// Checks if the base class has parameterized constructors that would require
    /// the derived class to implement its own constructor
    /// </summary>
    private void CheckIfBaseClassRequiresConstructor(AppClassNode classNode)
    {
        if (DataManager == null || classNode.BaseClass == null)
            return;

        try
        {
            // Parse the base class AST
            ProgramNode? baseProgram = ParseBaseClassAst(classNode.BaseClass.TypeName);
            if (baseProgram == null)
                return; // Could not parse base class
            
            // Check both class and interface scenarios like AppRefiner
            MethodNode? baseClassConstructor = null;
            string? baseClassName = null;
            
            if (baseProgram.AppClass != null)
            {
                var baseClass = baseProgram.AppClass;
                baseClassName = baseClass.Name;
                var constructors = baseClass.Methods.Where(m => 
                    string.Equals(m.Name, baseClass.Name, StringComparison.OrdinalIgnoreCase));
                if (constructors.Any())
                {
                    baseClassConstructor = constructors.First();
                }
            }
            else if (baseProgram.Interface != null)
            {
                var baseInterface = baseProgram.Interface;
                baseClassName = baseInterface.Name;
                var constructors = baseInterface.Methods.Where(m => 
                    string.Equals(m.Name, baseInterface.Name, StringComparison.OrdinalIgnoreCase));
                if (constructors.Any())
                {
                    baseClassConstructor = constructors.First();
                }
            }
            
            // If base class/interface has parameterized constructor, flag the derived class
            if (baseClassConstructor?.Parameters.Count > 0)
            {
                string tooltip = $"Class '{classNode.Name}' is missing a constructor required by '{classNode.BaseClass.TypeName}'.";
                AddIndicator(classNode.NameToken.SourceSpan, IndicatorType.SQUIGGLE, ERROR_COLOR, tooltip);
            }
        }
        catch (Exception)
        {
            // Silently handle database or parsing errors
            // Don't add indicators if we can't determine the base class structure
        }
    }

    #endregion
}