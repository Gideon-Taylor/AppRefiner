using System.Text;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Refactoring operation that optimizes and organizes import statements in PeopleCode
    /// </summary>
    public class OptimizeImports(ScintillaEditor editor) : BaseRefactor(editor)
    {
        private class ImportEntry
        {
            public string FullPackage { get; }
            public string RootPackage { get; }
            public bool IsWildcard { get; }
            public bool Used { get; set; }
            public int StartIndex { get; }
            public int StopIndex { get; }

            public ImportEntry(string package, bool isWildcard, int start, int stop)
            {
                FullPackage = package;
                RootPackage = package.Split(':')[0];
                IsWildcard = isWildcard;
                Used = true;
                StartIndex = start;
                StopIndex = stop;
            }
        }

        private readonly Dictionary<string, List<ImportEntry>> importsByPackage = new();
        private bool trackUsage = false;
        private ImportsBlockContext? importsBlockContext;
        public override void EnterImportDeclaration(ImportDeclarationContext context)
        {
            string packageName;
            bool isWildcard = false;

            var appPackageAll = context.appPackageAll();
            if (appPackageAll != null)
            {
                packageName = appPackageAll.GetText().TrimEnd('*');
                isWildcard = true;
            }
            else
            {
                packageName = context.appClassPath().GetText();
            }

            var entry = new ImportEntry(
                packageName,
                isWildcard,
                context.Start.StartIndex,
                context.Stop.StopIndex
            );

            string basePackage = packageName.Substring(0, packageName.LastIndexOf(':'));
            if (!importsByPackage.ContainsKey(basePackage))
            {
                importsByPackage[basePackage] = new List<ImportEntry>();
            }
            importsByPackage[basePackage].Add(entry);
        }

        public override void EnterAppClassPath(AppClassPathContext context)
        {
            if (!trackUsage) return;

            string packageName = context.GetText();
            string basePackage = packageName.Substring(0, packageName.LastIndexOf(':'));

            if (importsByPackage.ContainsKey(basePackage))
            {
                var imports = importsByPackage[basePackage];
                var exactMatch = imports.FirstOrDefault(i => i.FullPackage == packageName);
                if (exactMatch != null)
                {
                    exactMatch.Used = true;
                }
                else
                {
                    var wildcardMatch = imports.FirstOrDefault(i => i.IsWildcard);
                    if (wildcardMatch != null)
                    {
                        wildcardMatch.Used = true;
                    }
                }
            }
        }

        public override void ExitImportsBlock(ImportsBlockContext context)
        {
            trackUsage = true;
            importsBlockContext = context;
        }

        public override void ExitProgram(ProgramContext context)
        {
            if (importsByPackage.Count == 0) return;

            // Find the start and end of the imports block
            var firstImport = importsByPackage.SelectMany(kvp => kvp.Value)
                .OrderBy(i => i.StartIndex)
                .First();
            var lastImport = importsByPackage.SelectMany(kvp => kvp.Value)
                .OrderBy(i => i.StartIndex)
                .Last();

            // Build the new optimized imports block
            var newImports = new StringBuilder();
            var rootPackages = importsByPackage.Keys
                .Select(p => p.Split(':')[0])
                .Distinct()
                .OrderBy(p => p);

            foreach (var rootPackage in rootPackages)
            {
                var packagesInRoot = importsByPackage
                    .Where(kvp => kvp.Key.Equals(rootPackage) || kvp.Key.StartsWith($"{rootPackage}:"))
                    .OrderBy(kvp => kvp.Key);

                foreach (var package in packagesInRoot)
                {
                    var usedImports = package.Value.Where(i => i.Used).ToList();
                    if (usedImports.Count == 0) continue;

                    // If we have more than 2 imports from the same package, use wildcard
                    if (usedImports.Count > 2 && !usedImports.Any(i => i.IsWildcard))
                    {
                        newImports.AppendLine($"import {package.Key}:*;");
                    }
                    else
                    {
                        // Keep existing imports (both explicit and wildcards)
                        foreach (var import in usedImports.OrderBy(i => i.FullPackage))
                        {
                            newImports.AppendLine($"import {import.FullPackage}{(import.IsWildcard ? "*" : "")};");
                        }
                    }
                }
            }
            if (importsBlockContext != null)
            {
                // Replace the entire imports block
                ReplaceNode(importsBlockContext,
                         newImports.ToString().TrimEnd(),
                         "Optimize imports");
            }
        }
    }
}
