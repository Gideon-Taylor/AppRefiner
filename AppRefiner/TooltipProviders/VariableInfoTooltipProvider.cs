using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System.Text;

namespace AppRefiner.TooltipProviders
{
    /// <summary>
    /// Provides comprehensive tooltips for variables showing rich information including
    /// usage statistics, reference counts, safety classification, and scope information.
    /// This is the self-hosted equivalent of the ANTLR-based VariableInfoTooltipProvider
    /// with significantly enhanced capabilities.
    /// </summary>
    public class VariableInfoTooltipProvider : BaseTooltipProvider
    {
        // Fields to store variable/member information found at current position
        private string? _hoveredVariableName;
        private string? _hoveredMemberName;
        private SourceSpan? _hoveredVariableSpan;
        private SourceSpan? _hoveredMemberSpan;
        private ScopeContext? _scopeContext;
        /// <summary>
        /// Name of the tooltip provider.
        /// </summary>
        public override string Name => "Variable Info";

        /// <summary>
        /// Description of what the tooltip provider does.
        /// </summary>
        public override string Description => "Shows comprehensive information about variables including usage statistics and reference tracking";

        /// <summary>
        /// Medium priority
        /// </summary>
        public override int Priority => 50;

        /// <summary>
        /// Processes the AST to register tooltips for all variable references
        /// </summary>
        public override void ProcessProgram(ProgramNode program, int position, int lineNumber)
        {
            // Reset state for each processing
            _hoveredVariableName = null;
            _hoveredMemberName = null;
            _hoveredVariableSpan = null;
            _hoveredMemberSpan = null;
            
            base.ProcessProgram(program, position, lineNumber);
        }

        /// <summary>
        /// Override VisitProgram to register tooltips after full traversal is complete
        /// </summary>
        public override void VisitProgram(ProgramNode node)
        {
            // First, perform full AST traversal to populate all variable statistics
            base.VisitProgram(node);
            
            // After traversal is complete, register tooltips for any variables/members found at current position
            RegisterTooltip();
        }


        /// <summary>
        /// Override to check if identifier is at current position and store for deferred processing
        /// </summary>
        public override void VisitIdentifier(IdentifierNode node)
        {
            // Check if this identifier represents a variable and is at the current position
            if (IsVariableIdentifier(node) && node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                _hoveredVariableName = node.Name;
                _hoveredVariableSpan = node.SourceSpan;
                _scopeContext = GetCurrentScope();
            }

            base.VisitIdentifier(node);
        }

        public override void VisitProgramVariable(ProgramVariableNode node)
        {
            if (node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                _hoveredVariableName = node.Name;
                _hoveredVariableSpan = node.SourceSpan;
                _scopeContext = GetCurrentScope();
            }

            base.VisitProgramVariable(node);
        }

        /// <summary>
        /// Override to check if member access is at current position and store for deferred processing
        /// </summary>
        public override void VisitMemberAccess(MemberAccessNode node)
        {
            // Handle property access patterns like %This.PropertyName or &variable.PropertyName
            if (IsPropertyAccessPattern(node) && node.SourceSpan.ContainsPosition(CurrentPosition))
            {
                _hoveredMemberName = node.MemberName;
                _hoveredMemberSpan = node.SourceSpan;
                _scopeContext = GetCurrentScope();
            }

            base.VisitMemberAccess(node);
        }

        public override void VisitAssignment(AssignmentNode node)
        {
            if (node.Target is IdentifierNode identifier)
            {
                if (identifier.SourceSpan.ContainsPosition(CurrentPosition))
                {
                    _hoveredVariableName = identifier.Name;
                    _hoveredVariableSpan = identifier.SourceSpan;
                    _scopeContext = GetCurrentScope();
                }

            }
            base.VisitAssignment(node);
        }

        public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
        {
            /* Todo handle the name of the variable since we dont have Identifier nodes here. */
            foreach (var nameInfo in node.VariableNameInfos)
            {
                if (nameInfo.SourceSpan.ContainsPosition(CurrentPosition))
                {
                    _hoveredVariableName = nameInfo.Name;
                    _hoveredVariableSpan = nameInfo.SourceSpan;
                    _scopeContext = GetCurrentScope();
                    break;
                }
            }
            base.VisitLocalVariableDeclaration(node);
        }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            if (node.VariableNameInfo.SourceSpan.ContainsPosition(CurrentPosition))
            {
                _hoveredVariableName = node.VariableNameInfo.Name;
                _hoveredVariableSpan = node.VariableNameInfo.SourceSpan;
                _scopeContext = GetCurrentScope();
            }

            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        /// <summary>
        /// Registers tooltips for variables/members found at the current position after full traversal
        /// </summary>
        private void RegisterTooltip()
        {
            // Register tooltip for hovered variable if found
            if (!string.IsNullOrEmpty(_hoveredVariableName) && _hoveredVariableSpan.HasValue)
            {
                ProcessVariableReference(_hoveredVariableName, _hoveredVariableSpan.Value);
            }
            
            // Register tooltip for hovered member if found
            if (!string.IsNullOrEmpty(_hoveredMemberName) && _hoveredMemberSpan.HasValue)
            {
                ProcessMemberAccess(_hoveredMemberName, _hoveredMemberSpan.Value);
            }
        }

        /// <summary>
        /// Checks if an identifier represents a variable that should have tooltips
        /// </summary>
        private bool IsVariableIdentifier(IdentifierNode node)
        {
            return node.IdentifierType == IdentifierType.UserVariable ||
                   node.IdentifierType == IdentifierType.Generic;
        }

        /// <summary>
        /// Checks if a member access represents a property access pattern
        /// </summary>
        private bool IsPropertyAccessPattern(MemberAccessNode node)
        {
            // Check for patterns like %This.Property or &variable.Property
            return node.Target is IdentifierNode target &&
                   (target.Name.Equals("%This", StringComparison.OrdinalIgnoreCase) ||
                    target.Name.StartsWith("&"));
        }

        /// <summary>
        /// Processes a variable reference and registers a rich tooltip with complete statistics
        /// </summary>
        private void ProcessVariableReference(string variableName, SourceSpan span)
        {
            if (_scopeContext == null) return;
            try
            {
                // Try to get the variable information from the scoped visitor (now with complete statistics)
                var variable = GetVariablesInScope(_scopeContext)
                    .FirstOrDefault(v => v.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase) &&
                                        v.VariableNameInfo.SourceSpan.IsValid);

                if (variable != null)
                {
                    string tooltipText = GenerateRichVariableTooltip(variable);
                    RegisterTooltip(span, tooltipText);
                }
            }
            catch (Exception)
            {
                // Silently handle errors and fall back to basic tooltip
                string tooltipText = GenerateBasicVariableTooltip(variableName);
                RegisterTooltip(span, tooltipText);
            }
        }

        /// <summary>
        /// Processes a property access and registers a rich tooltip with complete statistics
        /// </summary>
        private void ProcessMemberAccess(string memberName, SourceSpan span)
        {
            if (_scopeContext == null) return;
            try
            {
                // Try to find the property in the current class (now with complete statistics)
                var property = GetVariablesInScope(_scopeContext)
                    .Where(v => v.Kind == VariableKind.Property &&
                               v.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(v => v.VariableNameInfo.SourceSpan.IsValid);

                if (property != null)
                {
                    string tooltipText = GenerateRichVariableTooltip(property);
                    RegisterTooltip(span, tooltipText);
                }
                else
                {
                    // Fallback for unknown properties
                    string tooltipText = GenerateBasicPropertyTooltip(memberName);
                    RegisterTooltip(span, tooltipText);
                }
            }
            catch (Exception)
            {
                // Silently handle errors and fall back to basic tooltip
                string tooltipText = GenerateBasicPropertyTooltip(memberName);
                RegisterTooltip(span, tooltipText);
            }
        }

        /// <summary>
        /// Generates a rich tooltip with comprehensive variable information
        /// </summary>
        private string GenerateRichVariableTooltip(VariableInfo variable)
        {
            var sb = new StringBuilder();

            // Variable header with type and name
            sb.AppendLine($"**{variable.Name}** ({variable.Type})");

            // Variable kind with emoji
            string kindIcon = GetVariableKindIcon(variable.Kind);
            string kindDescription = GetVariableKindDescription(variable.Kind);
            sb.AppendLine($"{kindIcon} {kindDescription}");

            // Declaration scope information
            if (variable.DeclarationScope != null)
            {
                sb.AppendLine($"📍 Declared in: {variable.DeclarationScope.Name}");
                sb.AppendLine($"📍 Line: {variable.DeclarationLine}");
            }

            // Safety classification
            string safetyIcon = variable.IsSafeToRefactor ? "✅" : "⚠️";
            string safetyText = variable.IsSafeToRefactor ? "Safe to refactor" : "Unsafe to refactor";
            sb.AppendLine($"{safetyIcon} {safetyText}");

            // Usage statistics with detailed breakdown
            var usageSummary = variable.GetUsageSummary();
            sb.AppendLine("");
            sb.AppendLine("📊 **Usage Statistics:**");

            if (variable.IsUsed)
            {
                sb.AppendLine($"   • Total references: {usageSummary.TotalReferences}");

                if (usageSummary.ReadCount > 0)
                    sb.AppendLine($"   • Read operations: {usageSummary.ReadCount}");

                if (usageSummary.WriteCount > 0)
                    sb.AppendLine($"   • Write operations: {usageSummary.WriteCount}");

                if (usageSummary.ParameterAnnotationCount > 0)
                    sb.AppendLine($"   • Parameter annotations: {usageSummary.ParameterAnnotationCount}");

                // Show read/write ratio if both exist
                if (usageSummary.ReadCount > 0 && usageSummary.WriteCount > 0)
                {
                    double readRatio = (double)usageSummary.ReadCount / (usageSummary.ReadCount + usageSummary.WriteCount);
                    double writeRatio = (double)usageSummary.WriteCount / (usageSummary.ReadCount + usageSummary.WriteCount);
                    sb.AppendLine($"   • Read/Write ratio: {readRatio:P0} / {writeRatio:P0}");
                }
            }
            else
            {
                sb.AppendLine("   • *Unused variable*");
            }

            // Reference locations (show first few and indicate if there are more)
            var sortedRefs = variable.GetReferencesSortedByLocation().ToList();
            if (sortedRefs.Any())
            {
                sb.AppendLine("");
                sb.AppendLine("📍 **Reference Locations:**");

                int maxRefsToShow = 5;
                var refsToShow = sortedRefs.Take(maxRefsToShow).ToList();

                foreach (var reference in refsToShow)
                {
                    string refIcon = GetReferenceTypeIcon(reference.ReferenceType);
                    string context = string.IsNullOrEmpty(reference.Context) ? "" : $" ({reference.Context})";
                    sb.AppendLine($"   {refIcon} Line {reference.Line}: {reference.ReferenceType}{context}");
                }

                if (sortedRefs.Count > maxRefsToShow)
                {
                    int remaining = sortedRefs.Count - maxRefsToShow;
                    sb.AppendLine($"   ... and {remaining} more reference{(remaining != 1 ? "s" : "")}");
                }
            }

            // Shadowing information
            var shadowedVars = GetAllVariables()
                .Where(v => v.Name.Equals(variable.Name, StringComparison.OrdinalIgnoreCase) &&
                           v.DeclarationScope.Id != variable.DeclarationScope.Id)
                .ToList();

            if (shadowedVars.Any())
            {
                sb.AppendLine("");
                sb.AppendLine("⚠️ **Variable Shadowing:**");

                var shadowing = shadowedVars.FirstOrDefault(v => v.Shadows(variable));
                if (shadowing != null)
                {
                    sb.AppendLine($"   • Shadows variable in {shadowing.DeclarationScope.Name}");
                }

                var shadowed = shadowedVars.FirstOrDefault(v => variable.Shadows(v));
                if (shadowed != null)
                {
                    sb.AppendLine($"   • Shadowed by variable in {shadowed.DeclarationScope.Name}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a basic tooltip for variables not found in scope analysis
        /// </summary>
        private string GenerateBasicVariableTooltip(string variableName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"**{variableName}**");

            // Try to infer variable type from name patterns
            string inferredType = InferVariableType(variableName);
            if (!string.IsNullOrEmpty(inferredType))
            {
                sb.AppendLine($"🔍 Type: {inferredType}");
            }

            sb.AppendLine("❓ Variable not found in scope analysis");
            sb.AppendLine("   (May be a system variable, global, or defined elsewhere)");

            return sb.ToString();
        }

        /// <summary>
        /// Generates a basic tooltip for properties
        /// </summary>
        private string GenerateBasicPropertyTooltip(string propertyName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"**{propertyName}**");
            sb.AppendLine("🏠 Property");
            sb.AppendLine("❓ Property definition not found in scope analysis");

            return sb.ToString();
        }

        /// <summary>
        /// Gets an appropriate icon for variable kind
        /// </summary>
        private string GetVariableKindIcon(VariableKind kind)
        {
            return kind switch
            {
                VariableKind.Local => "🔸",
                VariableKind.Instance => "🏠",
                VariableKind.Global => "🌍",
                VariableKind.Component => "📦",
                VariableKind.Parameter => "📥",
                VariableKind.Constant => "🔒",
                VariableKind.Property => "🏷️",
                _ => "❓"
            };
        }

        /// <summary>
        /// Gets a human-readable description for variable kind
        /// </summary>
        private string GetVariableKindDescription(VariableKind kind)
        {
            return kind switch
            {
                VariableKind.Local => "Local variable",
                VariableKind.Instance => "Instance variable",
                VariableKind.Global => "Global variable",
                VariableKind.Component => "Component variable",
                VariableKind.Parameter => "Method parameter",
                VariableKind.Constant => "Constant",
                VariableKind.Property => "Property",
                _ => "Variable"
            };
        }

        /// <summary>
        /// Gets an appropriate icon for reference type
        /// </summary>
        private string GetReferenceTypeIcon(ReferenceType referenceType)
        {
            return referenceType switch
            {
                ReferenceType.Declaration => "📝",
                ReferenceType.Read => "👁️",
                ReferenceType.Write => "✏️",
                ReferenceType.ParameterAnnotation => "🏷️",
                _ => "🔗"
            };
        }

        /// <summary>
        /// Attempts to infer variable type from naming patterns
        /// </summary>
        private string InferVariableType(string variableName)
        {
            if (variableName.StartsWith("&"))
            {
                return "User variable";
            }
            else if (variableName.StartsWith("%"))
            {
                // Common PeopleSoft system variables
                return variableName.ToUpperInvariant() switch
                {
                    "%THIS" => "Current object reference",
                    "%SESSION" => "Session object",
                    "%COMPONENT" => "Component object",
                    "%PAGE" => "Page object",
                    "%ROW" => "Row object",
                    "%SQL" => "SQL object",
                    "%FILE" => "File object",
                    _ => "System variable"
                };
            }
            else
            {
                // Try to infer from naming conventions
                if (variableName.Contains("array", StringComparison.OrdinalIgnoreCase))
                    return "Array";
                if (variableName.Contains("list", StringComparison.OrdinalIgnoreCase))
                    return "List";
                if (variableName.Contains("string", StringComparison.OrdinalIgnoreCase))
                    return "String";
                if (variableName.Contains("number", StringComparison.OrdinalIgnoreCase) ||
                    variableName.Contains("num", StringComparison.OrdinalIgnoreCase))
                    return "Number";
                if (variableName.Contains("date", StringComparison.OrdinalIgnoreCase))
                    return "Date";
                if (variableName.Contains("time", StringComparison.OrdinalIgnoreCase))
                    return "Time";

                return "Unknown type";
            }
        }
    }
}
