using System;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Ast
{
    /// <summary>
    /// Represents a PeopleCode IMPORT statement in the AST.
    /// </summary>
    public class Import
    {
        /// <summary>
        /// Gets the path being imported (e.g., "PKG:SUB:Class" or "PKG:SUB").
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this is a wildcard import (importing all classes in a package).
        /// </summary>
        public bool IsWildcard { get; private set; }

        /// <summary>
        /// Gets the full original text of the import path (e.g., "PKG:SUB:Class" or "PKG:SUB:*").
        /// </summary>
        public string FullImportText { get; private set; }

        // Private constructor
        private Import(string path, bool isWildcard, string fullImportText)
        {
            Path = path;
            IsWildcard = isWildcard;
            FullImportText = fullImportText;
        }

        /// <summary>
        /// Parses an ImportDeclarationContext to create an Import AST node.
        /// </summary>
        /// <param name="context">The ANTLR ImportDeclarationContext.</param>
        /// <returns>A new Import instance.</returns>
        public static Import Parse(ImportDeclarationContext context)
        {
            var appPkgAllCtx = context.appPackageAll();
            var appClassPathCtx = context.appClassPath();

            if (appPkgAllCtx != null)
            {
                // Wildcard import: PKG:SUB:*
                var packagePath = appPkgAllCtx.appPackagePath().GetText();
                return new Import(packagePath, true, appPkgAllCtx.GetText());
            }
            else if (appClassPathCtx != null)
            {
                 // Specific class import: PKG:SUB:Class
                var classPath = appClassPathCtx.GetText();
                 return new Import(classPath, false, classPath);
            }
            else
            {
                 // Should not happen if grammar is correct
                throw new ArgumentException("Invalid import declaration context.", nameof(context));
            }
        }

        public override string ToString()
        {
            return $"IMPORT {FullImportText}";
        }
    }
} 