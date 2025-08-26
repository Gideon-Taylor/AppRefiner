using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Nodes;
using System.Collections.Generic;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that identifies invalid application class references.
/// This is a self-hosted equivalent to the AppRefiner's InvalidAppClass styler.
/// </summary>
public class InvalidAppClass : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FF60; // Red color for invalid app classes
    
    // Dictionary cache to store app class path validity status (true = valid, false = invalid)
    private static Dictionary<string, bool> AppClassValidity = new();

    public override string Description => "Invalid app classes";

    /// <summary>
    /// This styler requires a database connection to validate app class references
    /// </summary>
    public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Required;

    /// <summary>
    /// Clear the cache of app class path validity status
    /// This should be called when the active editor changes or is set to null
    /// </summary>
    public static void ClearValidAppClassPathsCache()
    {
        AppClassValidity.Clear();
    }

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
    /// Handles application class type references and validates their existence
    /// </summary>
    public override void VisitAppClassType(AppClassTypeNode node)
    {
        if (DataManager == null)
        {
            base.VisitAppClassType(node);
            return;
        }

        string appClassPath = node.QualifiedName;
        bool isValid;
        
        // Check if this app class path is already in our cache
        if (AppClassValidity.TryGetValue(appClassPath, out isValid))
        {
            // We already know the validity status
        }
        else
        {
            // Check if app class exists
            isValid = DataManager.CheckAppClassExists(appClassPath);
            
            // Add to cache for future lookups
            AppClassValidity[appClassPath] = isValid;
        }
        
        // If the app class is invalid, highlight it with an error
        if (!isValid)
        {
            string tooltip = $"Application class '{appClassPath}' does not exist in the database.";
            AddIndicator(node.SourceSpan, IndicatorType.SQUIGGLE, ERROR_COLOR, tooltip);
        }

        base.VisitAppClassType(node);
    }

    /// <summary>
    /// Handle base class references in class declarations
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        // Check base class if present
        if (node.BaseClass is AppClassTypeNode baseClass)
        {
            VisitAppClassType(baseClass);
        }
        
        // Check implemented interface if present
        if (node.ImplementedInterface is AppClassTypeNode implementedInterface)
        {
            VisitAppClassType(implementedInterface);
        }

        base.VisitAppClass(node);
    }

    /// <summary>
    /// Handle interface base references
    /// </summary>
    public override void VisitInterface(InterfaceNode node)
    {
        // Check base interface if present
        if (node.BaseInterface is AppClassTypeNode baseInterface)
        {
            VisitAppClassType(baseInterface);
        }

        base.VisitInterface(node);
    }
}