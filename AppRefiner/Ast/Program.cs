using AppRefiner.Database;
using AppRefiner.PeopleCode; // For ProgramParser and generated ANTLR types
using static AppRefiner.PeopleCode.PeopleCodeParser; // Added static import
using System;
using System.Linq;
using Antlr4.Runtime.Tree;
using System.Collections.Generic; // Added for List

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents the top-level structure of a parsed PeopleCode program,
    /// focusing on identifying and containing an Application Class or Interface.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Gets the Application Class defined in this program, if any.
        /// </summary>
        public AppClass? ContainedAppClass { get; private set; }

        /// <summary>
        /// Gets the Interface defined in this program, if any.
        /// </summary>
        public Interface? ContainedInterface { get; private set; }

        /// <summary>
        /// Gets the list of import statements at the top of the program.
        /// </summary>
        public List<Import> Imports { get; private set; }

        // Private constructor to force use of Parse
        private Program() 
        { 
            // Initialize lists
            Imports = new List<Import>();
        }

        /// <summary>
        /// Parses a ProgramContext to create a Program AST instance.
        /// Determines if the program contains an AppClass or Interface and delegates parsing.
        /// </summary>
        /// <param name="context">The ANTLR context for the program.</param>
        /// <param name="fullPath">The full path of the program being parsed (used for AppClass/Interface identification).</param>
        /// <param name="dataManager">The data manager for resolving dependencies.</param>
        /// <param name="appClassParseResolver">Resolver function for Application Classes.</param>
        /// <param name="interfaceParseResolver">Resolver function for Interfaces.</param>
        /// <returns>A new Program instance containing the parsed AppClass or Interface, if applicable.</returns>
        public static Program Parse(
            ProgramContext context, // Type already correct
            string fullPath,
            IDataManager? dataManager)
        {
            var program = new Program();

            // --- Parse Imports --- Find the correct starting point based on grammar
            ImportsBlockContext? importsBlock = null;
            var appClassCtx = context.appClass(); // Get appClass context once

            if (appClassCtx != null)
            {
                // If it's an AppClass or Interface program, imports are child of appClassCtx
                 if (appClassCtx is AppClassProgramContext appProgCtx)
                 {
                    importsBlock = appProgCtx.importsBlock();
                 }
                 else if (appClassCtx is InterfaceProgramContext ifaceProgCtx)
                 {
                     importsBlock = ifaceProgCtx.importsBlock();
                 }
                 // else: Should not happen based on grammar for appClass
            }
            else
            {
                // If it's a standard program, imports are direct child of program context
                importsBlock = context.importsBlock();
            }

            if (importsBlock != null)
            {
                foreach (var importDecl in importsBlock.importDeclaration())
                {
                    try
                    {
                        program.Imports.Add(Import.Parse(importDecl));
                    }
                    catch (Exception ex) // Catch potential errors during import parsing
                    {
                        Console.Error.WriteLine($"Warning: Skipping invalid import in '{fullPath}': {importDecl.GetText()} ({ex.Message})");
                    }
                }
            }
            // --- End Parse Imports ---

            // --- Parse AppClass/Interface --- (Use the previously fetched appClassCtx)
            if (appClassCtx != null) // Check the specific type of appClass context
            {
                if (appClassCtx is AppClassProgramContext appClassProgramContext)
                {
                    program.ContainedAppClass = AppClass.Parse(appClassProgramContext, fullPath, dataManager);
                }
                else if (appClassCtx is InterfaceProgramContext interfaceProgramContext)
                {
                    program.ContainedInterface = Interface.Parse(interfaceProgramContext, fullPath, dataManager);
                }
            }
            else
            {
                 // This is a standard PeopleCode program (not an AppClass or Interface)
                // TODO: Parse other top-level elements like functions, variables if needed.
            }

            return program;
        }
    }
} 