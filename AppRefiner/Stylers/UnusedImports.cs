using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedImportsListener : BaseStyler
    {
        private const uint HIGHLIGHT_COLOR = 0xBBBBBB00; // Light gray text (no alpha)
        class ImportInfo
        {
            public string Name { get; }
            public bool Used { get; set; }

            public int Line { get; }
            public int StartIndex { get; }
            public int StopIndex { get; }

            public ImportInfo(string name, int line, int start, int stop)
            {
                Name = name;
                Used = false;
                Line = line;
                StartIndex = start;
                StopIndex = stop;
            }
        }

        // A stack of scopes. Each scope maps a variable name to its information.
        Dictionary<string, ImportInfo> importsUsed = new();
        private bool trackUsage = false;

        public UnusedImportsListener()
        {
            // Start with a global scope.
            Description = "Grays out unused imports.";
            Active = true;
        }

        public override void EnterImportDeclaration(ImportDeclarationContext context)
        {
            if (context == null) return;
            
            var appPackageAll = context.appPackageAll();
            var appClassPath = context.appClassPath();
            
            // Ensure that at least one of the paths is valid
            if (appPackageAll == null && appClassPath == null) return;
            
            string packageName = appPackageAll != null ? appPackageAll.GetText()?.TrimEnd('*') : appClassPath?.GetText();
            
            // Skip if we couldn't get a valid package name
            if (string.IsNullOrEmpty(packageName)) return;

            var importInfo = new ImportInfo(packageName, context.Start.Line, context.Start.StartIndex, context.Stop.StopIndex);
            importsUsed[packageName] = importInfo;
        }

        public override void EnterAppClassPath(AppClassPathContext context)
        {
            if (!trackUsage || context == null) return;

            string packageName = context.GetText();
            if (string.IsNullOrEmpty(packageName)) return;
            
            if (importsUsed.ContainsKey(packageName))
            {
                /* Explicit import found */
                importsUsed[packageName].Used = true;
            }
            else
            {
                /* Class wasn't covered by an explicit import, check if it's covered by a wildcard import */
                int lastColonIndex = packageName.LastIndexOf(':');
                if (lastColonIndex < 0) return;
                
                string subPackage = packageName.Substring(0, lastColonIndex + 1);
                if (string.IsNullOrEmpty(subPackage)) return;
                
                if (importsUsed.ContainsKey(subPackage))
                {
                    importsUsed[subPackage].Used = true;
                }
            }
        }

        public override void ExitImportsBlock(ImportsBlockContext context)
        {
            trackUsage = true;
        }

        public override void ExitProgram(ProgramContext context)
        {
            foreach (var import in importsUsed)
            {
                if (import.Key == null || import.Value == null) continue;
                
                if (!import.Value.Used)
                {
                    Indicators?.Add(new Indicator
                    {
                        Start = import.Value.StartIndex,
                        Length = import.Value.StopIndex - import.Value.StartIndex + 1,
                        Color = HIGHLIGHT_COLOR,
                        Tooltip = "Unused import",
                        Type = IndicatorType.TEXTCOLOR
                    });
                }
            }
        }

        public override void Reset()
        {
            importsUsed.Clear();
            trackUsage = false;
        }
    }
}
