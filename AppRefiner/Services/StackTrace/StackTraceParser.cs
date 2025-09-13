using System.Text;
using System.Text.RegularExpressions;
using AppRefiner.Database;
using AppRefiner.Database.Models;
using AppRefiner.Models;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Lexing;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;

namespace AppRefiner.Services.StackTrace
{
    /// <summary>
    /// Parses PeopleCode stack traces into navigable entries
    /// </summary>
    public static class StackTraceParser
    {
        /// <summary>
        /// Parses a stack trace string into a list of StackTraceEntry objects
        /// </summary>
        /// <param name="stackTrace">The full stack trace text</param>
        /// <returns>List of parsed stack trace entries</returns>
        public static List<StackTraceEntry> ParseStackTrace(string stackTrace)
        {
            var entries = new List<StackTraceEntry>();
            
            if (string.IsNullOrWhiteSpace(stackTrace))
                return entries;

            var lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                var entry = ParseStackTraceLine(line, i);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }


            return entries;
        }

        /// <summary>
        /// Comprehensively parses and processes a stack trace string into fully resolved StackTraceEntry objects
        /// This method front-loads all processing including database validation, program parsing, and position calculation
        /// </summary>
        /// <param name="stackTrace">The full stack trace text</param>
        /// <param name="dataManager">Data manager for database access and program loading</param>
        /// <returns>List of fully processed stack trace entries ready for navigation</returns>
        public static async Task<List<StackTraceEntry>> ParseAndProcessStackTraceAsync(string stackTrace, IDataManager dataManager)
        {
            var entries = new List<StackTraceEntry>();
            
            if (string.IsNullOrWhiteSpace(stackTrace))
                return entries;

            // Step 1: Parse stack trace lines
            var lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                var entry = ParseStackTraceLine(line, i);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            // Step 2: Check for error patterns
            var errorPattern = ErrorPatternMatcherRegistry.MatchPattern(stackTrace);

            // Step 3: Process entries in background thread
            await Task.Run(() => ProcessEntries(entries, dataManager, errorPattern));

            return entries;
        }

        /// <summary>
        /// Parses a single line from a stack trace
        /// </summary>
        /// <param name="line">The stack trace line to parse</param>
        /// <param name="lineNumber">The line number in the stack trace</param>
        /// <returns>Parsed StackTraceEntry or null if line doesn't match expected patterns</returns>
        private static StackTraceEntry? ParseStackTraceLine(string line, int lineNumber)
        {
            // Regex pattern to extract program path and statement number
            // Handles both direct calls and "Called from:" prefixes
            // Examples:
            // - "PTPPB_PAGELET.DataSource.URLDataSource.OnExecute Name:execute PCPC:4027 Statement:70"
            // - "Called from:PTPPB_PAGELET.Pagelet.OnExecute Name:Execute Statement:715"
            // - "IS_CV_KB_LST.Activate PCPC:64 Statement:2"
            // - "IS_CV_KN_DFN.GBL.IS_CV_KN_DFN.IS_CV_KN_GP_TYPE.FieldChange PCPC:64 Statement:2 (0,0)"

            var programPathPattern = @"(?:Called from:)?\s*([A-Z_][A-Z0-9_]*(?:\.[A-Z0-9_]+)+)";
            var statementPattern = @"Statement:(\d+)";

            var programMatch = Regex.Match(line, programPathPattern, RegexOptions.IgnoreCase);
            var statementMatch = Regex.Match(line, statementPattern, RegexOptions.IgnoreCase);

            if (!programMatch.Success)
            {
                // If we can't extract a program path, skip this line
                return null;
            }

            var programPath = programMatch.Groups[1].Value;
            var objectValues = programPath.Split('.');

            // Extract statement number if present
            int? statementNumber = null;
            if (statementMatch.Success && int.TryParse(statementMatch.Groups[1].Value, out int stmt))
            {
                statementNumber = stmt;
            }

            var entry = new StackTraceEntry(line, lineNumber)
            {
                DisplayName = programPath + (statementNumber.HasValue ? $" (Statement: {statementNumber})" : ""),
                ObjectValues = objectValues,
                StatementNumber = statementNumber
            };

            return entry;
        }

        /// <summary>
        /// Builds a basic OpenTarget string using the cached target
        /// </summary>
        /// <param name="entry">The stack trace entry with cached ResolvedTarget</param>
        /// <returns>Basic OpenTarget string for Application Designer navigation</returns>
        public static string BuildOpenTargetString(StackTraceEntry entry)
        {
            if (entry?.ResolvedTarget == null)
                return string.Empty;

            var sb = new StringBuilder();
            var target = entry.ResolvedTarget;
            
            for (var x = 0; x < target.ObjectIDs.Length; x++)
            {
                if (target.ObjectIDs[x] == PSCLASSID.NONE) 
                    break;
                    
                if (x > 0)
                {
                    sb.Append('.');
                }
                sb.Append(Enum.GetName(typeof(PSCLASSID), target.ObjectIDs[x]));
                sb.Append('.');
                sb.Append(target.ObjectValues[x]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Processes entries by validating them and calculating navigation data
        /// This method does the heavy lifting that was previously done on-click
        /// </summary>
        /// <param name="entries">The entries to process</param>
        /// <param name="dataManager">Data manager for database access</param>
        /// <param name="errorPattern">Detected error pattern if any</param>
        private static void ProcessEntries(List<StackTraceEntry> entries, IDataManager dataManager, ErrorPatternMatch? errorPattern)
        {
            if (dataManager == null || !dataManager.IsConnected)
            {
                // Mark all entries as invalid due to no database connection
                foreach (var entry in entries)
                {
                    entry.IsValid = false;
                    entry.ErrorMessage = "Unable to validate - no database connection";
                }
                return;
            }

            bool isFirstValidEntry = true;
            
            foreach (var entry in entries)
            {
                try
                {
                    // Step 1: Validate entry against database
                    if (!ValidateEntry(entry, dataManager))
                        continue; // Skip invalid entries

                    // Step 2: Build basic OpenTarget string
                    string baseTarget = BuildOpenTargetString(entry);
                    if (string.IsNullOrEmpty(baseTarget))
                        continue;

                    // Step 3: Calculate navigation data
                    if (entry.StatementNumber.HasValue && entry.ResolvedTarget != null)
                    {
                        ProcessStatementNavigation(entry, dataManager, baseTarget, errorPattern, isFirstValidEntry);
                    }
                    else
                    {
                        // No statement number - just set basic target
                        entry.OpenTargetString = baseTarget;
                    }
                    
                    // Mark that we've processed the first valid entry
                    isFirstValidEntry = false;
                }
                catch (Exception ex)
                {
                    entry.IsValid = false;
                    entry.ErrorMessage = $"Processing error: {ex.Message}";
                    Debug.Log($"Error processing stack trace entry: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Processes statement-level navigation data including program parsing and position calculation
        /// </summary>
        /// <param name="entry">The entry to process</param>
        /// <param name="dataManager">Data manager for program loading</param>
        /// <param name="baseTarget">Base OpenTarget string</param>
        /// <param name="errorPattern">Detected error pattern if any</param>
        /// <param name="isFirstValidEntry">True if this is the first valid entry being processed</param>
        private static void ProcessStatementNavigation(StackTraceEntry entry, IDataManager dataManager, string baseTarget, ErrorPatternMatch? errorPattern, bool isFirstValidEntry)
        {
            try
            {
                // Load program text from database
                string? programText = dataManager.GetPeopleCodeProgram(entry.ResolvedTarget!);
                
                if (string.IsNullOrEmpty(programText))
                {
                    entry.IsValid = false;
                    entry.ErrorMessage = "Program text not available";
                    return;
                }

                // Parse program
                var lexer = new PeopleCodeLexer(programText);
                var tokens = lexer.TokenizeAll();
                PeopleCodeParser.SelfHosted.PeopleCodeParser.ToolsRelease = new ToolsVersion(dataManager.GetToolsVersion());
                var parser = new PeopleCodeParser.SelfHosted.PeopleCodeParser(tokens);
                var program = parser.ParseProgram();
                
                if (program == null)
                {
                    entry.IsValid = false;
                    entry.ErrorMessage = "Failed to parse program";
                    return;
                }
                
                entry.IsProgramParsed = true;
                
                // Stack traces are 1-based, but statement numbers are 0-based
                int lineNumber = program.GetLineForStatement(entry.StatementNumber!.Value - 1);
                
                // Get the statement AST node and use its SourceSpan as default selection
                var statementNode = program.GetStatementAtLine(lineNumber);
                if (statementNode != null)
                {
                    entry.SelectionSpan = statementNode.SourceSpan;
                    entry.ByteOffset = statementNode.SourceSpan.Start.ByteIndex;
                }
                else
                {
                    // Fallback to first token on the line if statement node not found
                    var lineTokens = tokens.Where(t => t.SourceSpan.Start.Line == lineNumber).ToList();
                    if (lineTokens.Any())
                    {
                        var firstToken = lineTokens.First();
                        entry.SelectionSpan = firstToken.SourceSpan;
                        entry.ByteOffset = firstToken.SourceSpan.Start.ByteIndex;
                    }
                }
                
                // Use basic OpenTarget string (no SOURCETOKEN needed since we use PendingSelection)
                entry.OpenTargetString = baseTarget;
                
                // Apply error pattern if this is the first valid entry and we have a pattern match
                if (errorPattern != null && isFirstValidEntry)
                {
                    entry.ErrorPattern = errorPattern;
                    var enhancedSelection = ErrorPatternMatcherRegistry.GetEnhancedSelection(errorPattern, program, lineNumber);
                    if (enhancedSelection != null)
                    {
                        entry.SelectionSpan = enhancedSelection;
                        errorPattern.EnhancedSelection = enhancedSelection;
                    }
                }
            }
            catch (Exception ex)
            {
                entry.IsValid = false;
                entry.ErrorMessage = $"Navigation processing error: {ex.Message}";
                Debug.Log($"Error processing statement navigation: {ex.Message}");
            }
        }

        /// <summary>
        /// Legacy method - use pre-calculated OpenTargetString and SelectionSpan properties from processed entries
        /// </summary>
        /// <param name="entry">The stack trace entry with cached ResolvedTarget</param>
        /// <param name="dataManager">Data manager for getting program text</param>
        /// <returns>OpenTarget string with SOURCETOKEN for precise navigation</returns>
        [Obsolete("Use pre-calculated OpenTargetString and SelectionSpan properties from processed entries")]
        public static (string,SourceSpan? selection) GetOpenTargetStringAndSelection(StackTraceEntry entry, IDataManager dataManager)
        {
            // Return pre-calculated values if available (from processed entries)
            if (!string.IsNullOrEmpty(entry.OpenTargetString))
            {
                return (entry.OpenTargetString, entry.SelectionSpan);
            }
            
            // Fallback to basic target for backward compatibility
            if (entry?.ResolvedTarget == null)
                return (string.Empty, null);

            string baseTarget = BuildOpenTargetString(entry);
            return (baseTarget, null);
        }

        /// <summary>
        /// Validates whether a stack trace entry represents a target that exists in the database
        /// </summary>
        /// <param name="entry">The stack trace entry to validate</param>
        /// <param name="dataManager">Data manager for database validation</param>
        /// <returns>True if the entry represents a valid, existing target</returns>
        public static bool ValidateEntry(StackTraceEntry entry, IDataManager dataManager)
        {
            if (entry == null || dataManager == null || !dataManager.IsConnected)
            {
                if (entry != null)
                {
                    entry.IsValid = false;
                    entry.ErrorMessage = "Unable to validate - no database connection";
                }
                return false;
            }

            try
            {
                // Query database once and cache result
                var programMatches = dataManager.GetProgramObjectIds(entry.ObjectValues);
                
                if (programMatches.Count > 0)
                {
                    var (objectIds, objectValues) = programMatches[0];
                    var objectPairs = BuildObjectPairs(objectIds, objectValues);
                    
                    // Cache the resolved OpenTarget
                    entry.ResolvedTarget = new OpenTarget(
                        OpenTargetType.UNKNOWN, 
                        entry.DisplayName, 
                        "Stack Trace Entry", 
                        objectPairs, 
                        entry.StatementNumber
                    );
                    
                    entry.IsValid = true;
                    return true;
                }
                
                entry.IsValid = false;
                entry.ErrorMessage = "Program not found in database";
                return false;
            }
            catch (Exception ex)
            {
                entry.IsValid = false;
                entry.ErrorMessage = $"Validation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Builds object pairs from the database query results
        /// </summary>
        /// <param name="objectIds">Array of PSCLASSID values</param>
        /// <param name="objectValues">Array of object values</param>
        /// <returns>Enumerable of object ID/value pairs</returns>
        private static IEnumerable<(PSCLASSID ObjectID, string ObjectValue)> BuildObjectPairs(PSCLASSID[] objectIds, string[] objectValues)
        {
            for (int i = 0; i < objectIds.Length && objectIds[i] != PSCLASSID.NONE; i++)
            {
                yield return (objectIds[i], objectValues[i]);
            }
        }

        /// <summary>
        /// Asynchronously validates a list of stack trace entries
        /// </summary>
        /// <param name="entries">The entries to validate</param>
        /// <param name="dataManager">Data manager for database validation</param>
        /// <returns>Task that completes when validation is done</returns>
        public static async Task ValidateEntriesAsync(List<StackTraceEntry> entries, IDataManager dataManager)
        {
            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    entry.IsValid = ValidateEntry(entry, dataManager);
                }
            });
        }

    }

   
}