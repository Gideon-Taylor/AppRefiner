using Antlr4.Runtime.Tree;
using AppRefiner.Linters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AppRefiner.PeopleCode.PeopleCodeParser;

namespace AppRefiner.Stylers
{
    public class UnusedImportsListener : BaseStyler
    {
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
            string packageName = "";
            IRuleNode ruleNode;

            var appPackageAll = context.appPackageAll();
            if (appPackageAll != null)
            {
                packageName = appPackageAll.GetText().TrimEnd('*');
            }
            else
            {
                packageName = context.appClassPath().GetText();
            }

            var importInfo = new ImportInfo(packageName, context.Start.Line, context.Start.StartIndex, context.Stop.StopIndex);

            importsUsed[packageName] = importInfo;
        }

        public override void EnterAppClassPath(AppClassPathContext context)
        {
            if (!trackUsage)
            {
                return;
            }

            string packageName = context.GetText();
            if (importsUsed.ContainsKey(packageName))
            {
                /* Explicit import found */
                importsUsed[packageName].Used = true;
            }
            else
            {
                /* Class wasn't covered by an explicit import, check if it's covered by a wildcard import */
                string subPackage = packageName.Substring(0, packageName.LastIndexOf(':') + 1);
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
                if (!import.Value.Used)
                {
                    string suffix = import.Value.Name.EndsWith(":") ? "*" : "";

                    CodeHighlight highlight = new CodeHighlight()
                    {
                        Start = import.Value.StartIndex,
                        Length = import.Value.StopIndex - import.Value.StartIndex + 1,
                        Color = HighlightColor.Gray
                    };
                    Highlights?.Add(highlight);


                    /* CodeColor color = new CodeColor()
                    {
                        Start = import.Value.StartIndex,
                        Length = import.Value.StopIndex - import.Value.StartIndex + 1,
                        Color = FontColor.Gray
                    };
                    Colors?.Add(color); */
                }
            }
        }

        public override void Reset()
        {
            importsUsed.Clear();
        }
    }
}
