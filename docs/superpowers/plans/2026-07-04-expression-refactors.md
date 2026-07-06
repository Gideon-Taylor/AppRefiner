# Expression Refactors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship three new visible refactors — Extract Local Variable, Inline Variable, and Convert If ↔ Evaluate — plus the shared plumbing they need (selection range on BaseRefactor, opt-in type inference in RefactorManager).

**Architecture:** Each refactor is a `BaseRefactor` subclass in `AppRefiner/Refactors/`, auto-discovered by reflection (no registration step). Shared plumbing lands first: byte-safe source-text access and selection capture on `BaseRefactor`, then a `TypeInferenceRunner` service extracted from `StylerManager` so `RefactorManager` can run inference for refactors that opt in.

**Tech Stack:** C# / .NET 8, Windows Forms, self-hosted PeopleCode parser (`PeopleCodeParser.SelfHosted`). Spec: `docs/superpowers/specs/2026-07-04-expression-refactors-design.md`.

## Global Constraints

- **No automated test harness exists for refactors** — `BaseRefactor` requires a live cross-process `ScintillaEditor`. Verification per task = `dotnet build AppRefiner/AppRefiner.csproj` succeeds (~5 s). Behavioral verification is the manual test checklist at the end, executed by Tim in App Designer.
- Positions: Scintilla positions and `SourceSpan.Start/End.ByteIndex` are both **UTF-8 byte offsets**. Never index a C# string with a byte offset — always go through the UTF-8 byte array helpers added in Task 1 (see `MoveFunctionAbove.cs:74-85` for the established pattern).
- `SourceSpan.End.ByteIndex` is **exclusive** (see `RenameLocalVariable.GenerateRenameChanges`, which passes `Start.ByteIndex, End.ByteIndex` directly to `EditText`).
- Use AppRefiner's custom `Debug.Log()`, never `System.Diagnostics.Debug`.
- All three refactors: `IsHidden` stays default (visible), `RegisterKeyboardShortcut => false`, `RunOnIncompleteParse => false`.
- Edit-ordering rule in `BaseRefactor.ApplyEdits`: edits apply in descending `StartIndex` order via **stable** `OrderByDescending`. When a replacement and an insertion share the same `StartIndex`, the one added *first* applies first. Tasks below call out where this matters.
- Do not modify `StylerManager`'s skip-inference-when-no-resolver behavior (spec requirement).
- Commit after each task with the message given in the task.

---

### Task 1: Selection range + byte-safe source access on BaseRefactor

**Files:**
- Modify: `AppRefiner/ScintillaManager.cs` (near `GetSelectedText`, ~line 1348)
- Modify: `AppRefiner/Refactors/BaseRefactor.cs`

**Interfaces:**
- Produces (used by Tasks 3–7):
  - `ScintillaManager.GetSelectionRange(ScintillaEditor editor)` → `(int Start, int End)` byte positions
  - `BaseRefactor.SelectionStart` / `SelectionEnd` (`int`), `HasSelection` (`bool`)
  - `protected byte[] SourceBytes` — UTF-8 bytes of `originalSource`, valid after `Initialize`
  - `protected string GetSourceText(int startByteIndex, int endByteIndex)` and `protected string GetSourceText(SourceSpan span)`
  - `protected string NewLine` — `"\r\n"` if the document uses CRLF, else `"\n"`
  - `protected string GetLineIndent(int byteIndex)` — leading whitespace of the line containing `byteIndex`

- [ ] **Step 1: Add `GetSelectionRange` to ScintillaManager**

Place next to `GetSelectedText` (~line 1348). The `SCI_GETSELECTIONSTART`/`SCI_GETSELECTIONEND` constants already exist at lines 234/236.

```csharp
/// <summary>
/// Gets the current selection range as byte positions without fetching the text.
/// </summary>
public static (int Start, int End) GetSelectionRange(ScintillaEditor editor)
{
    var start = (int)editor.SendMessage(SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
    var end = (int)editor.SendMessage(SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);
    return (start, end);
}
```

- [ ] **Step 2: Add selection properties and byte helpers to BaseRefactor**

In `BaseRefactor.cs`, add `using System.Text;`. In the `IRefactor Properties` region add:

```csharp
/// <summary>
/// Gets the selection start byte position captured when the refactor was created
/// </summary>
public int SelectionStart { get; }

/// <summary>
/// Gets the selection end byte position (exclusive) captured when the refactor was created
/// </summary>
public int SelectionEnd { get; }

/// <summary>
/// Gets whether a non-empty selection existed when the refactor was created
/// </summary>
public bool HasSelection => SelectionEnd > SelectionStart;
```

In the constructor, after the `LineNumber` assignment:

```csharp
(SelectionStart, SelectionEnd) = ScintillaManager.GetSelectionRange(editor);
```

Add a private field `private byte[]? sourceBytes;` and set it in `Initialize` after `this.originalSource = source;`:

```csharp
sourceBytes = Encoding.UTF8.GetBytes(source);
```

Add to the `Text Editing Methods` region:

```csharp
/// <summary>
/// UTF-8 bytes of the original source. Spans and Scintilla positions are byte
/// offsets, so all source slicing must go through this array, never string indexes.
/// </summary>
protected byte[] SourceBytes => sourceBytes ?? throw new InvalidOperationException("Initialize has not been called");

/// <summary>
/// Extracts source text for a byte range (end exclusive)
/// </summary>
protected string GetSourceText(int startByteIndex, int endByteIndex)
{
    int start = Math.Max(0, startByteIndex);
    int end = Math.Min(SourceBytes.Length, endByteIndex);
    if (end <= start) return string.Empty;
    return Encoding.UTF8.GetString(SourceBytes, start, end - start);
}

/// <summary>
/// Extracts source text for a source span
/// </summary>
protected string GetSourceText(SourceSpan span)
    => GetSourceText(span.Start.ByteIndex, span.End.ByteIndex);

/// <summary>
/// Gets the document's line-ending convention
/// </summary>
protected string NewLine => (originalSource ?? "").Contains("\r\n") ? "\r\n" : "\n";

/// <summary>
/// Gets the leading whitespace of the line containing the given byte position
/// </summary>
protected string GetLineIndent(int byteIndex)
{
    int lineStart = Math.Min(byteIndex, SourceBytes.Length);
    while (lineStart > 0 && SourceBytes[lineStart - 1] != (byte)'\n')
        lineStart--;
    int i = lineStart;
    while (i < SourceBytes.Length && (SourceBytes[i] == (byte)' ' || SourceBytes[i] == (byte)'\t'))
        i++;
    return GetSourceText(lineStart, i);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/ScintillaManager.cs AppRefiner/Refactors/BaseRefactor.cs
git commit -m "feat: selection range and byte-safe source access on BaseRefactor"
```

---

### Task 2: TypeInferenceRunner service + RequiresTypeInference plumbing

**Files:**
- Create: `AppRefiner/Services/TypeInferenceRunner.cs`
- Modify: `AppRefiner/StylerManager.cs:354-465` (`RunTypeInferenceForProgram`, `DetermineQualifiedName`)
- Modify: `AppRefiner/Stylers/TypeErrorStyler.cs:81,119-134` (`RenderDeclaredType`)
- Modify: `AppRefiner/Refactors/BaseRefactor.cs` (new virtual property)
- Modify: `AppRefiner/RefactorManager.cs` (`ExecuteRefactor`, after the parse-error check ~line 295)

**Interfaces:**
- Consumes: existing `NullTypeMetadataResolver` (namespace `AppRefiner`, in `AppDesignerProcess.cs:19`), `TypeMetadataBuilder.ExtractMetadata`, `TypeInferenceVisitor.Run`, `OpenTargetBuilder`, `TypeExtensionManager.HandleUndefinedVariable`, `mainForm.TypeExtensionManager` (already used at `StylerManager.cs:406`).
- Produces (used by Tasks 3–4):
  - `TypeInferenceRunner.Run(ProgramNode program, ScintillaEditor editor, ITypeMetadataResolver typeResolver, TypeExtensionManager? typeExtensionManager)` → `void`
  - `TypeInferenceRunner.DetermineQualifiedName(ProgramNode node, ScintillaEditor editor)` → `string`
  - `TypeInferenceRunner.RenderDeclaredType(PeopleCodeTypeInfo.Types.TypeInfo? typeInfo)` → `string` (returns `"any"` for null/unrenderable types)
  - `BaseRefactor.RequiresTypeInference` — `public virtual bool`, default `false`

- [ ] **Step 1: Create TypeInferenceRunner**

Create `AppRefiner/Services/TypeInferenceRunner.cs`. The `Run` body and `DetermineQualifiedName` are **moved verbatim** from `StylerManager.RunTypeInferenceForProgram` (lines 379-408, minus the resolver null-check which stays in StylerManager) and `StylerManager.DetermineQualifiedName` (lines 424-465). `RenderDeclaredType` is **moved verbatim** from `TypeErrorStyler` (lines 122-134). Carry over the `using` directives those bodies need from `StylerManager.cs` / `TypeErrorStyler.cs` (`PeopleCodeParser.SelfHosted.Nodes`, `PeopleCodeParser.SelfHosted.Visitors`, `PeopleCodeTypeInfo.Contracts`, `PeopleCodeTypeInfo.Inference`, `AppRefiner.TypeExtensions`).

```csharp
using AppRefiner.TypeExtensions;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeTypeInfo.Contracts;
using PeopleCodeTypeInfo.Inference;

namespace AppRefiner.Services
{
    /// <summary>
    /// Shared type-inference pipeline used by StylerManager (stylers) and
    /// RefactorManager (refactors with RequiresTypeInference). Populates
    /// node.Attributes["TypeInfo"] throughout the AST.
    /// </summary>
    public static class TypeInferenceRunner
    {
        public static void Run(ProgramNode program, ScintillaEditor editor,
            ITypeMetadataResolver typeResolver, TypeExtensionManager? typeExtensionManager)
        {
            string qualifiedName = DetermineQualifiedName(program, editor);
            var programMetadata = TypeMetadataBuilder.ExtractMetadata(program, qualifiedName);

            string? defaultRecord = null;
            string? defaultField = null;
            if (editor.Caption?.EndsWith("(Record PeopleCode)") == true)
            {
                var parts = qualifiedName.Split('.');
                if (parts.Length >= 2)
                {
                    defaultRecord = parts[0];
                    defaultField = parts[1];
                }
            }

            TypeInferenceVisitor.Run(
                program,
                programMetadata,
                typeResolver,
                defaultRecord,
                defaultField,
                inferAutoDeclaredTypes: false,
                onUndefinedVariable: typeExtensionManager != null ? typeExtensionManager.HandleUndefinedVariable : null);
        }

        public static string DetermineQualifiedName(ProgramNode node, ScintillaEditor editor)
        {
            // moved verbatim from StylerManager.DetermineQualifiedName (StylerManager.cs:424-465)
        }

        /// <summary>
        /// Renders a TypeInfo as declared-type syntax; anything that doesn't look like
        /// a legal type token conservatively declares as "any".
        /// </summary>
        public static string RenderDeclaredType(PeopleCodeTypeInfo.Types.TypeInfo? typeInfo)
        {
            // moved verbatim from TypeErrorStyler.RenderDeclaredType (TypeErrorStyler.cs:122-134)
        }
    }
}
```

(The two "moved verbatim" comments above are instructions to you, the implementer — paste the existing method bodies there, then delete the originals in the next steps. Do not leave the comments in.)

- [ ] **Step 2: Slim down StylerManager**

`RunTypeInferenceForProgram` keeps its null-checks and try/catch but delegates the pipeline:

```csharp
private void RunTypeInferenceForProgram(ProgramNode program, ScintillaEditor editor)
{
    try
    {
        var appDesignerProcess = editor?.AppDesignerProcess;
        if (appDesignerProcess == null)
        {
            Debug.Log("StylerManager: No AppDesigner process available for type inference");
            return;
        }

        var typeResolver = appDesignerProcess.TypeResolver;
        if (typeResolver == null)
        {
            Debug.Log("StylerManager: TypeResolver is null (database not connected?), skipping type inference");
            return;
        }

        Services.TypeInferenceRunner.Run(program, editor!, typeResolver, mainForm.TypeExtensionManager);
        Debug.Log("StylerManager: Type inference completed");
    }
    catch (Exception ex)
    {
        Debug.LogException(ex, "StylerManager: Error during type inference");
    }
}
```

Delete `StylerManager.DetermineQualifiedName` (verify with grep that its only StylerManager caller was line 380; if `TypeMetadataBuilder`-related usings become unused, remove them).

- [ ] **Step 3: Delegate TypeErrorStyler.RenderDeclaredType**

Delete the private `RenderDeclaredType` method body in `TypeErrorStyler.cs` and replace the method with a delegation (keeps the call site at line 81 unchanged):

```csharp
private static string RenderDeclaredType(PeopleCodeTypeInfo.Types.TypeInfo? typeInfo)
    => Services.TypeInferenceRunner.RenderDeclaredType(typeInfo);
```

- [ ] **Step 4: Add RequiresTypeInference to BaseRefactor**

In the `IRefactor Properties` region:

```csharp
/// <summary>
/// Gets whether type inference should be run on the freshly parsed program
/// before this refactor's visitor executes. Inference is best-effort: without
/// a database connection, builtins and literals still resolve but app class
/// and record metadata lookups return null.
/// </summary>
public virtual bool RequiresTypeInference => false;
```

- [ ] **Step 5: Run inference in ExecuteRefactor**

In `RefactorManager.ExecuteRefactor`, immediately after the parse-error check block (the `if (program == null || ...)` that returns, ~line 295) and **before** `refactorClass.Initialize(...)`:

```csharp
// Run type inference when the refactor needs it. Best-effort: with no DB
// connection we substitute NullTypeMetadataResolver so builtins/literals
// still resolve; failures are non-fatal (refactor sees Unknown/Any types).
if (refactorClass.RequiresTypeInference)
{
    try
    {
        var resolver = activeEditor.AppDesignerProcess?.TypeResolver
            ?? (PeopleCodeTypeInfo.Contracts.ITypeMetadataResolver)new NullTypeMetadataResolver();
        Services.TypeInferenceRunner.Run(program, activeEditor, resolver, mainForm.TypeExtensionManager);
    }
    catch (Exception ex)
    {
        Debug.LogException(ex, "RefactorManager: type inference failed; proceeding with best-effort types");
    }
}
```

Note: `NullTypeMetadataResolver` here is the one in namespace `AppRefiner` (`AppDesignerProcess.cs:19`), not the one in `PeopleCodeTypeInfo.Contracts`.

- [ ] **Step 6: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add AppRefiner/Services/TypeInferenceRunner.cs AppRefiner/StylerManager.cs AppRefiner/Stylers/TypeErrorStyler.cs AppRefiner/Refactors/BaseRefactor.cs AppRefiner/RefactorManager.cs
git commit -m "feat: shared TypeInferenceRunner and opt-in inference for refactors"
```

---

### Task 3: ExtractLocalVariable — matching, guards, transformation

**Files:**
- Create: `AppRefiner/Refactors/ExtractLocalVariable.cs`

**Interfaces:**
- Consumes: Task 1 (`HasSelection`, `SelectionStart/End`, `SourceBytes`, `GetSourceText`, `NewLine`, `GetLineIndent`), Task 2 (`RequiresTypeInference`, `TypeInferenceRunner.RenderDeclaredType`), `node.GetInferredType()` (extension in namespace `PeopleCodeParser.SelfHosted`), `VariableRegistry.FindVariableInScope(name, scope)`, `GetCurrentScope()`.
- Produces: fields `targetExpression`, `containingStatement`, `containingScope`, `occurrences`, and methods `SuggestName()`, `IsNameVisibleInScope(string)` that Task 4's dialog uses. Task 4 replaces the placeholder `ShowRefactorDialog` written here.

This task delivers a working refactor end-to-end with an auto-generated name and single-occurrence replacement; Task 4 adds the real dialog and replace-all.

- [ ] **Step 1: Create the refactor skeleton with selection matching and guards**

```csharp
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Extracts the selected expression into a new local variable declared
    /// immediately before the containing statement. Requires a selection that
    /// covers exactly one complete expression — cursor position alone is
    /// ambiguous for nested expressions.
    /// </summary>
    public class ExtractLocalVariable : BaseRefactor
    {
        public new static string RefactorName => "Extract Local Variable";
        public new static string RefactorDescription => "Extracts the selected expression into a new local variable";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RequiresUserInputDialog => true;
        public override bool DeferDialogUntilAfterVisitor => true;
        public override bool RequiresTypeInference => true;
        public override bool RunOnIncompleteParse => false;

        private ExpressionNode? targetExpression;
        private StatementNode? containingStatement;
        private ScopeContext? containingScope;
        private readonly List<ExpressionNode> occurrences = new();

        public ExtractLocalVariable(ScintillaEditor editor) : base(editor) { }

        public override void VisitBlock(BlockNode node)
        {
            // Deepest block containing the selection wins. Blocks don't introduce
            // scopes, so GetCurrentScope() is the enclosing method/function/getter/
            // setter scope — captured here because scope contexts are gone by the
            // time OnExitGlobalScope runs the location logic.
            if (HasSelection && node.SourceSpan.ContainsPosition(SelectionStart))
            {
                containingScope = GetCurrentScope();
            }
            base.VisitBlock(node);
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            LocateTargetExpression(node);
        }

        protected override void OnReset()
        {
            targetExpression = null;
            containingStatement = null;
            containingScope = null;
            occurrences.Clear();
        }
    }
}
```

- [ ] **Step 2: Implement LocateTargetExpression**

Add to the class:

```csharp
private void LocateTargetExpression(ProgramNode program)
{
    if (!HasSelection)
    {
        SetFailure("Select the expression to extract. Extract Local Variable needs a selection because the cursor alone cannot identify which enclosing expression you mean.");
        return;
    }

    int selStart = SelectionStart;
    int selEnd = SelectionEnd;
    TrimWhitespace(ref selStart, ref selEnd);

    // Exact-span match; if none, strip one paren layer and retry
    while (true)
    {
        // FindDescendants is a pre-order walk, so the first exact-span match is
        // the outermost node when wrappers share the span
        targetExpression = program.FindDescendants<ExpressionNode>()
            .FirstOrDefault(e => e.SourceSpan.Start.ByteIndex == selStart
                              && e.SourceSpan.End.ByteIndex == selEnd);
        if (targetExpression != null)
            break;

        if (selEnd - selStart >= 2
            && SourceBytes[selStart] == (byte)'('
            && SourceBytes[selEnd - 1] == (byte)')')
        {
            selStart++;
            selEnd--;
            TrimWhitespace(ref selStart, ref selEnd);
            continue;
        }

        SetFailure("Selection must cover exactly one complete expression.");
        return;
    }

    if (targetExpression is IdentifierNode)
    {
        SetFailure("The selection is already a single identifier — there is nothing to extract.");
        return;
    }

    if (targetExpression.Parent is AssignmentNode assignment
        && ReferenceEquals(assignment.Target, targetExpression))
    {
        SetFailure("Cannot extract an assignment target.");
        return;
    }

    containingStatement = FindContainingStatement(targetExpression);
    if (containingStatement == null || containingScope == null)
    {
        SetFailure("Extract Local Variable only works inside a code block (method, function, getter/setter, or event body).");
        return;
    }

    // Re-evaluated contexts: hoisting out of these changes semantics
    for (AstNode? cur = targetExpression; cur != null && !ReferenceEquals(cur, containingStatement); cur = cur.Parent)
    {
        switch (cur.Parent)
        {
            case WhileStatementNode w when ReferenceEquals(cur, w.Condition):
                SetFailure("Cannot extract from a While condition — it is re-evaluated every iteration.");
                return;
            case RepeatStatementNode r when ReferenceEquals(cur, r.Condition):
                SetFailure("Cannot extract from a Repeat-Until condition — it is re-evaluated every iteration.");
                return;
            case ForStatementNode f when ReferenceEquals(cur, f.FromValue)
                                      || ReferenceEquals(cur, f.ToValue)
                                      || (f.StepValue != null && ReferenceEquals(cur, f.StepValue)):
                SetFailure("Cannot extract from a For loop header.");
                return;
            case EvaluateStatementNode ev when ev.WhenClauses.Any(wc => ReferenceEquals(cur, wc.Condition)):
                SetFailure("Cannot extract from a When condition.");
                return;
        }
    }

    CollectOccurrences();
}

private void TrimWhitespace(ref int start, ref int end)
{
    while (start < end && IsWhitespaceByte(SourceBytes[start])) start++;
    while (end > start && IsWhitespaceByte(SourceBytes[end - 1])) end--;
}

private static bool IsWhitespaceByte(byte b)
    => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

private static StatementNode? FindContainingStatement(AstNode node)
{
    for (AstNode? cur = node.Parent; cur != null; cur = cur.Parent)
    {
        if (cur is StatementNode stmt && stmt.Parent is BlockNode)
            return stmt;
    }
    return null;
}
```

- [ ] **Step 3: Implement occurrence collection and name suggestion**

```csharp
/// <summary>
/// Finds expressions identical to the target (normalized text) within the
/// containing block subtree, at or after the insertion point, so the new
/// declaration dominates every replacement. The target itself is included.
/// </summary>
private void CollectOccurrences()
{
    occurrences.Clear();
    var block = (BlockNode)containingStatement!.Parent!;
    string normTarget = Normalize(GetSourceText(targetExpression!.SourceSpan));

    foreach (var expr in block.FindDescendants<ExpressionNode>())
    {
        if (expr.SourceSpan.Start.ByteIndex < containingStatement.SourceSpan.Start.ByteIndex)
            continue;
        if (expr.Parent is AssignmentNode a && ReferenceEquals(a.Target, expr))
            continue;
        if (Normalize(GetSourceText(expr.SourceSpan)) == normTarget)
            occurrences.Add(expr);
    }

    if (!occurrences.Contains(targetExpression))
        occurrences.Add(targetExpression);
}

/// <summary>
/// Whitespace-collapsed comparison text. Case-insensitive except when a string
/// literal is present anywhere in the text — PeopleCode identifiers are
/// case-insensitive but string literal contents are not, so mixed text is
/// compared case-sensitively (conservative: may miss a match, never wrong).
/// </summary>
private static string Normalize(string text)
{
    var collapsed = System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    return collapsed.Contains('"') ? collapsed : collapsed.ToLowerInvariant();
}

private string SuggestName()
{
    string baseName = targetExpression switch
    {
        FunctionCallNode fc when fc.Function is IdentifierNode fn => fn.Name,
        FunctionCallNode fc when fc.Function is MemberAccessNode ma => ma.MemberName,
        MemberAccessNode member => member.MemberName,
        _ => "value"
    };

    baseName = new string(baseName.Where(char.IsLetterOrDigit).ToArray());
    if (baseName.Length == 0 || !char.IsLetter(baseName[0]))
        baseName = "value";
    baseName = "&" + char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);

    string candidate = baseName;
    int suffix = 2;
    while (IsNameVisibleInScope(candidate))
        candidate = baseName + suffix++;
    return candidate;
}

/// <summary>
/// The registry stores names both with and without the leading ampersand
/// depending on declaration kind, so check both forms (mirrors AssignToNewVariable).
/// </summary>
private bool IsNameVisibleInScope(string name)
{
    return VariableRegistry.FindVariableInScope(name, containingScope!) != null
        || VariableRegistry.FindVariableInScope(name.Substring(1), containingScope!) != null;
}
```

- [ ] **Step 4: Implement the transformation and a temporary no-dialog ShowRefactorDialog**

```csharp
public override bool ShowRefactorDialog()
{
    // Task 4 replaces this with the real dialog (name input, type display,
    // replace-all checkbox). Until then: suggested name, single occurrence.
    if (targetExpression == null)
        return false;
    GenerateChanges(SuggestName(), replaceAll: false);
    return true;
}

private void GenerateChanges(string variableName, bool replaceAll)
{
    string exprText = GetSourceText(targetExpression!.SourceSpan);
    string typeName = Services.TypeInferenceRunner.RenderDeclaredType(targetExpression.GetInferredType());
    string indent = GetLineIndent(containingStatement!.SourceSpan.Start.ByteIndex);

    // Replacements MUST be added before the insertion: when an occurrence starts
    // at the same byte as the insertion point (extracting a whole expression
    // statement), ApplyEdits' stable descending sort applies same-position edits
    // in add order, and the replacement must consume the original span first.
    var targets = replaceAll ? occurrences : new List<ExpressionNode> { targetExpression };
    foreach (var expr in targets)
    {
        EditText(expr.SourceSpan.Start.ByteIndex, expr.SourceSpan.End.ByteIndex,
            variableName, $"Replace expression with {variableName}");
    }

    InsertText(containingStatement.SourceSpan.Start.ByteIndex,
        $"Local {typeName} {variableName} = {exprText};{NewLine}{indent}",
        $"Declare {variableName}");
}
```

Note: `GetInferredType()` requires `using PeopleCodeParser.SelfHosted;` (already in the file's usings from Step 1).

- [ ] **Step 5: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded. "Extract Local Variable" will now appear in the refactor list (reflection discovery — no registration).

- [ ] **Step 6: Commit**

```bash
git add AppRefiner/Refactors/ExtractLocalVariable.cs
git commit -m "feat: Extract Local Variable refactor (selection matching, guards, transformation)"
```

---

### Task 4: ExtractLocalVariable — name dialog with replace-all checkbox

**Files:**
- Modify: `AppRefiner/Refactors/ExtractLocalVariable.cs`

**Interfaces:**
- Consumes from Task 3: `targetExpression`, `occurrences`, `SuggestName()`, `IsNameVisibleInScope(string)`, `GenerateChanges(string, bool)`, and Task 2's `RenderDeclaredType`.

- [ ] **Step 1: Add the dialog as a nested class**

Model on `RenameLocalVariable.RenameVariableDialog` (`RenameLocalVariable.cs:65-213`): borderless, dark header panel `Color.FromArgb(50, 50, 60)`, Segoe UI, `StartPosition = CenterParent`, Escape cancels via `ProcessCmdKey`, Enter accepts. Layout: header (30px) → prompt label → name TextBox → type label → optional checkbox → error label → OK/Cancel.

```csharp
private class ExtractVariableDialog : Form
{
    private readonly TextBox txtName = new();
    private readonly Button btnOk = new();
    private readonly Button btnCancel = new();
    private readonly Label lblPrompt = new();
    private readonly Label lblType = new();
    private readonly CheckBox chkReplaceAll = new();
    private readonly Label lblError = new();
    private readonly Panel headerPanel = new();
    private readonly Label headerLabel = new();

    private readonly Func<string, bool> isNameTaken;

    public string VariableName { get; private set; }
    public bool ReplaceAll => chkReplaceAll.Visible && chkReplaceAll.Checked;

    public ExtractVariableDialog(string suggestedName, string typeName,
        int occurrenceCount, Func<string, bool> isNameTaken)
    {
        this.isNameTaken = isNameTaken;
        VariableName = suggestedName;
        InitializeComponent(typeName, occurrenceCount);
        txtName.Text = suggestedName.StartsWith('&') ? suggestedName.Substring(1) : suggestedName;
        ActiveControl = txtName;
        txtName.SelectAll();
    }

    private void InitializeComponent(string typeName, int occurrenceCount)
    {
        SuspendLayout();

        headerPanel.BackColor = Color.FromArgb(50, 50, 60);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 30;
        headerPanel.Controls.Add(headerLabel);

        headerLabel.Text = "Extract Local Variable";
        headerLabel.ForeColor = Color.White;
        headerLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        headerLabel.Dock = DockStyle.Fill;
        headerLabel.TextAlign = ContentAlignment.MiddleCenter;

        lblPrompt.AutoSize = true;
        lblPrompt.Location = new Point(12, 40);
        lblPrompt.Text = "Enter variable name:";

        txtName.BorderStyle = BorderStyle.FixedSingle;
        txtName.Location = new Point(12, 60);
        txtName.Size = new Size(260, 23);
        txtName.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
        txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnOk_Click(s, e); };

        lblType.AutoSize = true;
        lblType.Location = new Point(12, 90);
        lblType.ForeColor = Color.FromArgb(90, 90, 100);
        lblType.Text = $"Type: {typeName}";

        chkReplaceAll.AutoSize = true;
        chkReplaceAll.Location = new Point(12, 112);
        chkReplaceAll.Text = $"Replace all {occurrenceCount} identical occurrences";
        chkReplaceAll.Visible = occurrenceCount > 1;

        lblError.AutoSize = true;
        lblError.Location = new Point(12, 136);
        lblError.ForeColor = Color.Firebrick;
        lblError.Text = "";

        btnOk.DialogResult = DialogResult.None;
        btnOk.Location = new Point(116, 158);
        btnOk.Size = new Size(75, 28);
        btnOk.Text = "&OK";
        btnOk.UseVisualStyleBackColor = true;
        btnOk.Click += BtnOk_Click;

        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(197, 158);
        btnCancel.Size = new Size(75, 28);
        btnCancel.Text = "&Cancel";
        btnCancel.UseVisualStyleBackColor = true;

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ClientSize = new Size(284, 198);
        Controls.AddRange(new Control[] { btnCancel, btnOk, lblError, chkReplaceAll, lblType, txtName, lblPrompt, headerPanel });
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ExtractVariableDialog";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Extract Local Variable";
        ShowInTaskbar = false;
        ResumeLayout(false);
        PerformLayout();
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var name = txtName.Text.Trim();
        if (!name.StartsWith("&"))
            name = "&" + name;

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^&[A-Za-z][A-Za-z0-9_]*$"))
        {
            lblError.Text = "Not a valid variable name.";
            return;
        }
        if (isNameTaken(name))
        {
            lblError.Text = $"{name} is already in use in this scope.";
            return;
        }

        VariableName = name;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
```

- [ ] **Step 2: Replace the temporary ShowRefactorDialog**

```csharp
public override bool ShowRefactorDialog()
{
    if (targetExpression == null)
    {
        // LocateTargetExpression already called SetFailure with the reason
        return false;
    }

    string typeName = Services.TypeInferenceRunner.RenderDeclaredType(targetExpression.GetInferredType());
    using var dialog = new ExtractVariableDialog(SuggestName(), typeName, occurrences.Count, IsNameVisibleInScope);
    var wrapper = new WindowWrapper(GetEditorMainWindowHandle());

    if (dialog.ShowDialog(wrapper) != DialogResult.OK)
        return false;

    GenerateChanges(dialog.VariableName, dialog.ReplaceAll);
    return true;
}
```

- [ ] **Step 3: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/Refactors/ExtractLocalVariable.cs
git commit -m "feat: Extract Local Variable dialog with type display and replace-all"
```

---

### Task 5: InlineVariable

**Files:**
- Create: `AppRefiner/Refactors/InlineVariable.cs`

**Interfaces:**
- Consumes: Task 1 helpers (`GetSourceText`, `SourceBytes`), `ScopedAstVisitor` members `GetAllScopes()`, `GetVariablesInScope(scope)`, `VariableInfo` (`Kind`, `Name`, `References`), `ReferenceType` (Declaration/Read/Write), `LocalVariableDeclarationWithAssignmentNode` (`VariableNameInfo`, `InitialValue`).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Create the refactor**

```csharp
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Inlines a single-assignment local variable: replaces every read with the
    /// initializer expression and deletes the declaration. Refuses when inlining
    /// would duplicate side effects or observe a stale value. Known limitation
    /// (accepted in the design spec): instance/global variables mutated
    /// indirectly by calls between the declaration and a read are not detected.
    /// </summary>
    public class InlineVariable : BaseRefactor
    {
        public new static string RefactorName => "Inline Variable";
        public new static string RefactorDescription => "Replaces reads of a single-assignment local variable with its initializer and removes the declaration";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RunOnIncompleteParse => false;

        private readonly List<LocalVariableDeclarationWithAssignmentNode> declarationNodes = new();

        public InlineVariable(ScintillaEditor editor) : base(editor) { }

        public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
        {
            declarationNodes.Add(node);
            base.VisitLocalVariableDeclarationWithAssignment(node);
        }

        protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, object> customData)
        {
            GenerateInline();
        }

        protected override void OnReset()
        {
            declarationNodes.Clear();
        }
    }
}
```

- [ ] **Step 2: Implement target resolution and eligibility/safety checks**

```csharp
private void GenerateInline()
{
    // Same cursor-resolution approach as RenameLocalVariable
    VariableInfo? variable = null;
    foreach (var scope in GetAllScopes())
    {
        foreach (var candidate in GetVariablesInScope(scope))
        {
            if (candidate.References.Any(r => r.SourceSpan.ContainsPosition(CurrentPosition)))
            {
                variable = candidate;
                break;
            }
        }
        if (variable != null) break;
    }

    if (variable == null)
    {
        SetFailure("No variable found at the cursor position.");
        return;
    }

    if (variable.Kind != VariableKind.Local)
    {
        SetFailure($"{variable.Name} is not a local variable — only locals can be inlined.");
        return;
    }

    var declNode = declarationNodes.FirstOrDefault(d =>
        string.Equals(d.VariableName, variable.Name, StringComparison.OrdinalIgnoreCase)
        && variable.References.Any(r => r.ReferenceType == ReferenceType.Declaration
            && d.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex)));

    if (declNode == null)
    {
        SetFailure($"{variable.Name} is not declared with an initializer (Local <type> {variable.Name} = <value>;) — only such declarations can be inlined.");
        return;
    }

    // Exactly one write: the initializer. Writes inside the declaration span are
    // the initializer itself regardless of how the visitor classified it.
    var externalWrites = variable.References
        .Where(r => r.ReferenceType == ReferenceType.Write
                 && !declNode.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex))
        .ToList();
    if (externalWrites.Count > 0)
    {
        SetFailure($"{variable.Name} is assigned {externalWrites.Count} more time(s) after its declaration — only single-assignment variables can be inlined.");
        return;
    }

    var reads = variable.References
        .Where(r => r.ReferenceType == ReferenceType.Read
                 && !declNode.SourceSpan.ContainsPosition(r.SourceSpan.Start.ByteIndex))
        .OrderBy(r => r.SourceSpan.Start.ByteIndex)
        .ToList();
    if (reads.Count == 0)
    {
        SetFailure($"{variable.Name} is never read — use the Delete Unused Variable quick fix instead.");
        return;
    }

    // Safety 1: side-effect duplication
    bool initializerHasCalls = declNode.InitialValue.HasSideEffects
        || declNode.InitialValue is FunctionCallNode or ObjectCreationNode or ObjectCreateShortHand
        || declNode.InitialValue.FindDescendants<FunctionCallNode>().Any()
        || declNode.InitialValue.FindDescendants<ObjectCreationNode>().Any()
        || declNode.InitialValue.FindDescendants<ObjectCreateShortHand>().Any();
    if (initializerHasCalls && reads.Count > 1)
    {
        SetFailure($"The initializer of {variable.Name} calls a function and the variable is read {reads.Count} times — inlining would evaluate it {reads.Count} times.");
        return;
    }

    // Safety 2: stale value — variables referenced in the initializer must not be
    // written between the declaration and the last read
    int lastReadStart = reads[^1].SourceSpan.Start.ByteIndex;
    var initIdentifiers = declNode.InitialValue.FindDescendants<IdentifierNode>().ToList();
    if (declNode.InitialValue is IdentifierNode selfIdentifier)
        initIdentifiers.Add(selfIdentifier);

    foreach (var ident in initIdentifiers)
    {
        if (ident.IdentifierType != IdentifierType.UserVariable)
            continue;

        var referenced = FindVariableByReferencePosition(ident.SourceSpan.Start.ByteIndex);
        if (referenced == null)
            continue;

        bool mutated = referenced.References.Any(r => r.ReferenceType == ReferenceType.Write
            && r.SourceSpan.Start.ByteIndex >= declNode.SourceSpan.End.ByteIndex
            && r.SourceSpan.Start.ByteIndex < lastReadStart);
        if (mutated)
        {
            SetFailure($"{referenced.Name} is reassigned between the declaration and a use of {variable.Name} — inlining would change the value observed.");
            return;
        }
    }

    GenerateEdits(declNode, reads);
}

private VariableInfo? FindVariableByReferencePosition(int byteIndex)
{
    foreach (var scope in GetAllScopes())
        foreach (var v in GetVariablesInScope(scope))
            if (v.References.Any(r => r.SourceSpan.Start.ByteIndex == byteIndex))
                return v;
    return null;
}
```

- [ ] **Step 3: Implement the edits**

```csharp
private void GenerateEdits(LocalVariableDeclarationWithAssignmentNode declNode, List<VariableReference> reads)
{
    string initText = GetSourceText(declNode.InitialValue.SourceSpan);
    if (NeedsParentheses(declNode.InitialValue))
        initText = "(" + initText + ")";

    foreach (var read in reads)
    {
        EditText(read.SourceSpan.Start.ByteIndex, read.SourceSpan.End.ByteIndex,
            initText, $"Inline {declNode.VariableName}");
    }

    DeleteDeclaration(declNode);
}

/// <summary>
/// Atomic expressions never need wrapping; anything with an operator does,
/// so precedence in the surrounding context is preserved.
/// </summary>
private static bool NeedsParentheses(ExpressionNode initializer) => initializer switch
{
    LiteralNode or IdentifierNode or MemberAccessNode or PropertyAccessNode
        or ArrayAccessNode or FunctionCallNode or ParenthesizedExpressionNode
        or ObjectCreationNode or ObjectCreateShortHand or ClassConstantNode
        or MetadataExpressionNode => false,
    _ => true
};

/// <summary>
/// Deletes the declaration statement plus its semicolon; when the statement is
/// alone on its line, the whole line goes (including the line break).
/// </summary>
private void DeleteDeclaration(LocalVariableDeclarationWithAssignmentNode declNode)
{
    int delStart = declNode.SourceSpan.Start.ByteIndex;
    int delEnd = declNode.SourceSpan.End.ByteIndex;

    // Consume trailing whitespace + semicolon
    while (delEnd < SourceBytes.Length
        && (SourceBytes[delEnd] == (byte)' ' || SourceBytes[delEnd] == (byte)'\t'))
        delEnd++;
    if (delEnd < SourceBytes.Length && SourceBytes[delEnd] == (byte)';')
        delEnd++;

    int lineStart = delStart;
    while (lineStart > 0 && SourceBytes[lineStart - 1] != (byte)'\n')
        lineStart--;

    bool onlyIndentBefore = true;
    for (int i = lineStart; i < delStart; i++)
    {
        if (SourceBytes[i] != (byte)' ' && SourceBytes[i] != (byte)'\t')
        {
            onlyIndentBefore = false;
            break;
        }
    }

    int afterEnd = delEnd;
    while (afterEnd < SourceBytes.Length
        && (SourceBytes[afterEnd] == (byte)' ' || SourceBytes[afterEnd] == (byte)'\t'))
        afterEnd++;
    bool nothingAfter = afterEnd >= SourceBytes.Length
        || SourceBytes[afterEnd] == (byte)'\r' || SourceBytes[afterEnd] == (byte)'\n';

    if (onlyIndentBefore && nothingAfter)
    {
        // Whole line: include trailing EOL
        int lineEnd = afterEnd;
        if (lineEnd < SourceBytes.Length && SourceBytes[lineEnd] == (byte)'\r') lineEnd++;
        if (lineEnd < SourceBytes.Length && SourceBytes[lineEnd] == (byte)'\n') lineEnd++;
        DeleteText(lineStart, lineEnd, $"Remove declaration of {declNode.VariableName}");
    }
    else
    {
        DeleteText(delStart, delEnd, $"Remove declaration of {declNode.VariableName}");
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/Refactors/InlineVariable.cs
git commit -m "feat: Inline Variable refactor with side-effect and stale-value guards"
```

---

### Task 6: ConvertIfEvaluate — detection and If→Evaluate

**Files:**
- Create: `AppRefiner/Refactors/ConvertIfEvaluate.cs`

**Interfaces:**
- Consumes: Task 1 helpers, `IfStatementNode` (`Condition`, `ThenBlock`, `ElseBlock`), `EvaluateStatementNode` (`Expression`, `WhenClauses`, `WhenOtherBlock`), `WhenClause` (`Operator`, `Condition`, `Body`), `BinaryOperationNode` (`Left`, `Operator`, `NotFlag`, `Right`), `BinaryOperator` + `GetSymbol()`, `BlockNode.Statements`, `BreakStatementNode`.
- Produces (used by Task 7 in the same file): `RenderBody(BlockNode, string, bool)`, `ContainsEvaluateBoundBreak(BlockNode, bool)`, `DetectIndentUnit()`, `ReplaceStatement(StatementNode, string)`, field `evaluateNode`.

- [ ] **Step 1: Create the refactor with direction detection**

```csharp
using System.Text;
using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Refactors
{
    /// <summary>
    /// Converts between an If/Else-If chain and an Evaluate statement, whichever
    /// direction applies at the cursor. Semantics note: PeopleCode Evaluate falls
    /// through after a matching When unless Break, so generated When bodies always
    /// end with Break; and only Break-terminated Evaluates convert back to If.
    /// </summary>
    public class ConvertIfEvaluate : BaseRefactor
    {
        public new static string RefactorName => "Convert If ↔ Evaluate";
        public new static string RefactorDescription => "Converts an If/Else-If chain to an Evaluate statement, or back";
        public new static bool RegisterKeyboardShortcut => false;

        public override bool RunOnIncompleteParse => false;

        private static readonly BinaryOperator[] ComparisonOperators =
        {
            BinaryOperator.Equal, BinaryOperator.NotEqual,
            BinaryOperator.LessThan, BinaryOperator.LessThanOrEqual,
            BinaryOperator.GreaterThan, BinaryOperator.GreaterThanOrEqual
        };

        private IfStatementNode? topIfNode;
        private EvaluateStatementNode? evaluateNode;

        public ConvertIfEvaluate(ScintillaEditor editor) : base(editor) { }

        public override void VisitProgram(ProgramNode node)
        {
            base.VisitProgram(node);
            Analyze(node);
        }

        private void Analyze(ProgramNode program)
        {
            var statement = program.FindDescendants<StatementNode>()
                .Where(s => s.SourceSpan.ContainsPosition(CurrentPosition))
                .OrderByDescending(s => s.SourceSpan.Start.ByteIndex)
                .FirstOrDefault();

            AstNode? cur = statement;
            while (cur != null && cur is not IfStatementNode && cur is not EvaluateStatementNode)
                cur = cur.Parent;

            switch (cur)
            {
                case IfStatementNode ifNode:
                    topIfNode = ifNode;
                    ConvertIfToEvaluate();
                    break;
                case EvaluateStatementNode evalNode:
                    evaluateNode = evalNode;
                    ConvertEvaluateToIf();
                    break;
                default:
                    SetFailure("Place the cursor inside an If/Else-If chain or an Evaluate statement.");
                    break;
            }
        }
    }
}
```

- [ ] **Step 2: Implement chain collection and validation for If→Evaluate**

```csharp
private void ConvertIfToEvaluate()
{
    // Climb to the topmost chain member: a chain link's Else block contains
    // exactly one statement, which is the next If (PeopleCode has no ElseIf)
    var ifNode = topIfNode!;
    while (ifNode.Parent is BlockNode parentBlock
        && parentBlock.Statements.Count == 1
        && parentBlock.Parent is IfStatementNode parentIf
        && ReferenceEquals(parentIf.ElseBlock, parentBlock))
    {
        ifNode = parentIf;
    }

    var links = new List<IfStatementNode>();
    BlockNode? finalElse = null;
    var current = ifNode;
    while (true)
    {
        links.Add(current);
        if (current.ElseBlock is { } elseBlock
            && elseBlock.Statements.Count == 1
            && elseBlock.Statements[0] is IfStatementNode nextIf)
        {
            current = nextIf;
            continue;
        }
        finalElse = current.ElseBlock;
        break;
    }

    if (links.Count < 2)
    {
        SetFailure("Convert to Evaluate needs an If/Else-If chain with at least two conditions.");
        return;
    }

    var comparisons = new List<BinaryOperationNode>();
    foreach (var link in links)
    {
        if (link.Condition is not BinaryOperationNode cmp
            || cmp.NotFlag
            || !ComparisonOperators.Contains(cmp.Operator))
        {
            SetFailure("Every condition in the chain must be a simple comparison (=, <>, <, <=, >, >=) to convert to Evaluate.");
            return;
        }
        comparisons.Add(cmp);
    }

    // Scrutinee: the expression common to all comparisons, on either side
    var whens = MatchScrutinee(comparisons, useLeftOfFirst: true)
             ?? MatchScrutinee(comparisons, useLeftOfFirst: false);
    if (whens == null)
    {
        SetFailure("The conditions do not all compare the same expression, so the chain cannot become an Evaluate.");
        return;
    }

    foreach (var body in links.Select(l => l.ThenBlock).Concat(finalElse != null ? new[] { finalElse } : Array.Empty<BlockNode>()))
    {
        if (ContainsEvaluateBoundBreak(body, ignoreTrailing: false))
        {
            SetFailure("A body in this chain contains a Break that targets an enclosing loop — wrapping it in an Evaluate would silently retarget the Break.");
            return;
        }
    }

    BuildEvaluateText(ifNode, links, whens, finalElse);
}

/// <summary>
/// Tries one side of the first comparison as the scrutinee and checks every
/// comparison against it. Returns per-link (operator symbol, value expression),
/// with the operator mirrored when the scrutinee is on the right (5 &lt; &amp;x
/// becomes When &gt; 5). Null when any comparison doesn't involve the scrutinee.
/// </summary>
private List<(string OpSymbol, ExpressionNode Value)>? MatchScrutinee(
    List<BinaryOperationNode> comparisons, bool useLeftOfFirst)
{
    var first = comparisons[0];
    var scrutineeExpr = useLeftOfFirst ? first.Left : first.Right;
    string scrutNorm = NormalizeExpressionText(scrutineeExpr);

    var result = new List<(string, ExpressionNode)>();
    foreach (var cmp in comparisons)
    {
        if (NormalizeExpressionText(cmp.Left) == scrutNorm)
            result.Add((cmp.Operator.GetSymbol(), cmp.Right));
        else if (NormalizeExpressionText(cmp.Right) == scrutNorm)
            result.Add((MirrorOperator(cmp.Operator).GetSymbol(), cmp.Left));
        else
            return null;
    }
    ScrutineeText = GetSourceText(scrutineeExpr.SourceSpan);
    return result;
}

private string? ScrutineeText;

private string NormalizeExpressionText(ExpressionNode expr)
{
    var collapsed = System.Text.RegularExpressions.Regex.Replace(
        GetSourceText(expr.SourceSpan).Trim(), @"\s+", " ");
    return collapsed.Contains('"') ? collapsed : collapsed.ToLowerInvariant();
}

private static BinaryOperator MirrorOperator(BinaryOperator op) => op switch
{
    BinaryOperator.LessThan => BinaryOperator.GreaterThan,
    BinaryOperator.LessThanOrEqual => BinaryOperator.GreaterThanOrEqual,
    BinaryOperator.GreaterThan => BinaryOperator.LessThan,
    BinaryOperator.GreaterThanOrEqual => BinaryOperator.LessThanOrEqual,
    _ => op // = and <> are symmetric
};

/// <summary>
/// True when the block contains a Break that would bind to an Evaluate/loop at
/// this block's level — i.e., not nested inside an inner For/While/Repeat/Evaluate.
/// With ignoreTrailing, a Break that is the block's own last top-level statement
/// is skipped (used by Evaluate→If, where that Break is expected and dropped).
/// </summary>
private static bool ContainsEvaluateBoundBreak(BlockNode body, bool ignoreTrailing)
{
    foreach (var breakNode in body.FindDescendants<BreakStatementNode>())
    {
        if (ignoreTrailing
            && body.Statements.Count > 0
            && ReferenceEquals(body.Statements[^1], breakNode))
            continue;

        bool bound = true;
        for (AstNode? cur = breakNode.Parent; cur != null && !ReferenceEquals(cur, body); cur = cur.Parent)
        {
            if (cur is ForStatementNode or WhileStatementNode or RepeatStatementNode or EvaluateStatementNode)
            {
                bound = false;
                break;
            }
        }
        if (bound) return true;
    }
    return false;
}
```

- [ ] **Step 3: Implement text generation for If→Evaluate**

```csharp
private void BuildEvaluateText(IfStatementNode topIf, List<IfStatementNode> links,
    List<(string OpSymbol, ExpressionNode Value)> whens, BlockNode? finalElse)
{
    string indent = GetLineIndent(topIf.SourceSpan.Start.ByteIndex);
    string unit = DetectIndentUnit(links[0], indent);
    var sb = new StringBuilder();

    sb.Append($"Evaluate {ScrutineeText}{NewLine}");
    for (int i = 0; i < links.Count; i++)
    {
        sb.Append($"{indent}When {whens[i].OpSymbol} {GetSourceText(whens[i].Value.SourceSpan)}{NewLine}");
        sb.Append(RenderBody(links[i].ThenBlock, indent + unit, dropTrailingBreak: false));
        // Evaluate falls through: every When body must Break to preserve
        // if/else-if semantics (even an empty body)
        sb.Append($"{indent + unit}Break;{NewLine}");
    }
    if (finalElse != null)
    {
        sb.Append($"{indent}When-Other{NewLine}");
        sb.Append(RenderBody(finalElse, indent + unit, dropTrailingBreak: false));
    }
    sb.Append($"{indent}End-Evaluate");

    ReplaceStatement(topIf, sb.ToString());
}

/// <summary>
/// Indent unit inferred from the delta between the statement's line and its Then
/// block's first line; App Designer's three-space convention as fallback.
/// </summary>
private string DetectIndentUnit(IfStatementNode firstLink, string baseIndent)
{
    if (firstLink.ThenBlock.Statements.Count > 0)
    {
        string bodyIndent = GetLineIndent(firstLink.ThenBlock.Statements[0].SourceSpan.Start.ByteIndex);
        if (bodyIndent.Length > baseIndent.Length && bodyIndent.StartsWith(baseIndent))
            return bodyIndent.Substring(baseIndent.Length);
    }
    return "   ";
}

/// <summary>
/// Renders a block's statements re-indented at newIndent. Captures from the line
/// start of the first statement so every line carries its real indentation, then
/// strips the common leading whitespace and prepends newIndent.
/// </summary>
private string RenderBody(BlockNode block, string newIndent, bool dropTrailingBreak)
{
    var statements = block.Statements;
    int count = statements.Count;
    if (dropTrailingBreak && count > 0 && statements[^1] is BreakStatementNode)
        count--;
    if (count == 0)
        return string.Empty;

    int start = statements[0].SourceSpan.Start.ByteIndex;
    while (start > 0 && SourceBytes[start - 1] != (byte)'\n'
        && (SourceBytes[start - 1] == (byte)' ' || SourceBytes[start - 1] == (byte)'\t'))
        start--;

    int end = statements[count - 1].SourceSpan.End.ByteIndex;
    while (end < SourceBytes.Length
        && (SourceBytes[end] == (byte)' ' || SourceBytes[end] == (byte)'\t'))
        end++;
    if (end < SourceBytes.Length && SourceBytes[end] == (byte)';')
        end++;

    string text = GetSourceText(start, end);
    var lines = text.Replace("\r\n", "\n").Split('\n');

    int minIndent = int.MaxValue;
    foreach (var line in lines)
    {
        if (line.Trim().Length == 0) continue;
        int ws = 0;
        while (ws < line.Length && (line[ws] == ' ' || line[ws] == '\t')) ws++;
        minIndent = Math.Min(minIndent, ws);
    }
    if (minIndent == int.MaxValue) minIndent = 0;

    var sb = new StringBuilder();
    foreach (var line in lines)
    {
        if (line.Trim().Length == 0)
            sb.Append(NewLine);
        else
            sb.Append(newIndent + line.Substring(Math.Min(minIndent, line.Length)) + NewLine);
    }
    return sb.ToString();
}

/// <summary>
/// Replaces the statement's span with new text, carrying over a trailing
/// semicolon if the original had one after its span.
/// </summary>
private void ReplaceStatement(StatementNode statement, string newText)
{
    int end = statement.SourceSpan.End.ByteIndex;
    if (end < SourceBytes.Length && SourceBytes[end] == (byte)';')
    {
        end++;
        newText += ";";
    }
    EditText(statement.SourceSpan.Start.ByteIndex, end, newText, "Convert If ↔ Evaluate");
}
```

Add a placeholder for Task 7 so this task compiles:

```csharp
private void ConvertEvaluateToIf()
{
    SetFailure("Converting Evaluate to If is not implemented yet.");
}
```

- [ ] **Step 4: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add AppRefiner/Refactors/ConvertIfEvaluate.cs
git commit -m "feat: Convert If to Evaluate with Break preservation and scrutinee matching"
```

---

### Task 7: ConvertIfEvaluate — Evaluate→If

**Files:**
- Modify: `AppRefiner/Refactors/ConvertIfEvaluate.cs` (replace the `ConvertEvaluateToIf` placeholder)

**Interfaces:**
- Consumes from Task 6: `evaluateNode`, `RenderBody(BlockNode, string, bool)`, `ContainsEvaluateBoundBreak(BlockNode, bool)`, `ReplaceStatement(StatementNode, string)`, `NormalizeExpressionText`.

- [ ] **Step 1: Implement grouping and validation**

Replace the placeholder:

```csharp
private void ConvertEvaluateToIf()
{
    var ev = evaluateNode!;
    if (ev.WhenClauses.Count == 0)
    {
        SetFailure("This Evaluate has no When clauses to convert.");
        return;
    }

    // Consecutive empty-bodied Whens group with the next non-empty one as
    // Or-joined conditions (the standard PeopleCode stacked-When idiom)
    var groups = new List<(List<WhenClause> Whens, BlockNode Body)>();
    var pending = new List<WhenClause>();
    foreach (var whenClause in ev.WhenClauses)
    {
        pending.Add(whenClause);
        if (whenClause.Body.Statements.Count == 0)
            continue;

        if (whenClause.Body.Statements[^1] is not BreakStatementNode)
        {
            SetFailure("A When clause falls through (its body does not end with Break) — intentional fall-through cannot be expressed as If/Else and will not be converted.");
            return;
        }
        if (ContainsEvaluateBoundBreak(whenClause.Body, ignoreTrailing: true))
        {
            SetFailure("A When body contains a Break in the middle of its logic — converting to If/Else would change where execution resumes.");
            return;
        }
        groups.Add((new List<WhenClause>(pending), whenClause.Body));
        pending.Clear();
    }

    if (pending.Count > 0)
    {
        SetFailure("The last When clause(s) have no body — there is nothing for their conditions to execute, so the Evaluate cannot be converted.");
        return;
    }

    if (ev.WhenOtherBlock != null
        && ContainsEvaluateBoundBreak(ev.WhenOtherBlock, ignoreTrailing: true))
    {
        SetFailure("The When-Other body contains a Break in the middle of its logic — converting to If/Else would change where execution resumes.");
        return;
    }

    BuildIfChainText(ev, groups);
}
```

- [ ] **Step 2: Implement nested If/Else generation**

```csharp
private void BuildIfChainText(EvaluateStatementNode ev,
    List<(List<WhenClause> Whens, BlockNode Body)> groups)
{
    string scrutinee = GetSourceText(ev.Expression.SourceSpan);
    string baseIndent = GetLineIndent(ev.SourceSpan.Start.ByteIndex);
    string unit = groups.Count > 0 && groups[0].Body.Statements.Count > 0
        ? InferUnitFromBody(groups[0].Body, baseIndent)
        : "   ";

    string Condition(List<WhenClause> whens) => string.Join(" Or ",
        whens.Select(w =>
            $"{scrutinee} {(w.Operator ?? BinaryOperator.Equal).GetSymbol()} {GetSourceText(w.Condition.SourceSpan)}"));

    // PeopleCode has no ElseIf: each subsequent group is a nested If inside Else,
    // one indent level deeper, each closing its own End-If
    string Build(int groupIndex, string indent)
    {
        var (whens, body) = groups[groupIndex];
        var sb = new StringBuilder();
        sb.Append($"If {Condition(whens)} Then{NewLine}");
        sb.Append(RenderBody(body, indent + unit, dropTrailingBreak: true));

        bool hasMoreGroups = groupIndex + 1 < groups.Count;
        if (hasMoreGroups)
        {
            sb.Append($"{indent}Else{NewLine}");
            sb.Append(indent + unit);
            sb.Append(Build(groupIndex + 1, indent + unit));
            sb.Append(NewLine);
        }
        else if (ev.WhenOtherBlock != null)
        {
            sb.Append($"{indent}Else{NewLine}");
            sb.Append(RenderBody(ev.WhenOtherBlock, indent + unit, dropTrailingBreak: true));
        }

        // Inner End-Ifs need statement separators; the outermost semicolon is
        // handled by ReplaceStatement
        sb.Append(groupIndex == 0 ? $"{indent}End-If" : $"{indent}End-If;");
        return sb.ToString();
    }

    ReplaceStatement(ev, Build(0, baseIndent));
}

private string InferUnitFromBody(BlockNode body, string baseIndent)
{
    string bodyIndent = GetLineIndent(body.Statements[0].SourceSpan.Start.ByteIndex);
    return bodyIndent.Length > baseIndent.Length && bodyIndent.StartsWith(baseIndent)
        ? bodyIndent.Substring(baseIndent.Length)
        : "   ";
}
```

- [ ] **Step 3: Build**

Run: `dotnet build AppRefiner/AppRefiner.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add AppRefiner/Refactors/ConvertIfEvaluate.cs
git commit -m "feat: Convert Evaluate to If with stacked-When Or-merging and fall-through refusal"
```

---

### Task 8: Manual test checklist handoff

No code. Present the checklist below to Tim (builds and App Designer testing are his step — do not launch anything). Note for the reviewer: one open semantics question worth Tim's eye during testing — the Evaluate→If stacked-When grouping assumes the standard PeopleCode idiom where consecutive empty Whens act as Or; verify with a live Evaluate that matches on the *first* of two stacked Whens.

**Extract Local Variable**
- [ ] Select `6 * 2` inside `DoAThing(3 + (4 - (6 * 2)))` → extracts exactly that; repeat for `4 - (6 * 2)` and the full argument.
- [ ] Select `(4 - (6 * 2))` including parens → paren-stripping matches the inner expression.
- [ ] Select `3, 4 + 6` inside an argument list → refused: "exactly one complete expression".
- [ ] Selection spanning two statements → refused.
- [ ] No selection → refused with the "select the expression" message.
- [ ] Select the condition inside `While ... End-While` → refused (re-evaluation).
- [ ] Extract with DB connected: `Len(&s)` shows `Type: number` in dialog; an app class expression shows its qualified name.
- [ ] Extract with **no DB connection**: `Len(&s)` still shows `number` (builtin, no resolver needed); an app class method call degrades to `any`.
- [ ] Expression appearing 3× in the block: checkbox shows "Replace all 3 identical occurrences"; both checked and unchecked paths.
- [ ] Extract a whole expression statement (e.g. select all of `CreateRecord(Record.FOO)` in `CreateRecord(Record.FOO);`) → declaration inserted above, statement becomes `&rec;`-style read (verify edit ordering produced valid output).
- [ ] Name collision typed into dialog → inline error, dialog stays open.
- [ ] Undo (Ctrl+Z) reverts declaration + replacement as one action.

**Inline Variable**
- [ ] `Local number &x = 5;` read twice → both reads become `5`, declaration line removed.
- [ ] Initializer `&a + 1` inlined into a comparison → parenthesized `(&a + 1)`.
- [ ] Initializer calls a function, 1 read → allowed; 2+ reads → refused with count in message.
- [ ] `Local number &y = &a; &a = 10; &z = &y;` → refused (stale value).
- [ ] Variable assigned again later → refused (single-assignment).
- [ ] Never-read variable → refused, message points at Delete Unused Variable quick fix.
- [ ] Cursor on an instance variable → refused (locals only).

**Convert If ↔ Evaluate**
- [ ] 3-way `If &status = "A" ... Else If &status = "B" ... Else ... End-If` chain → Evaluate with two Whens + When-Other, every When body ends `Break;`.
- [ ] Chain with scrutinee on the right (`If 5 < &x`) → operator mirrored (`When > 5`).
- [ ] Chain where one condition uses `And` → refused.
- [ ] Chain whose body contains a `Break` targeting an enclosing While → refused (both directions).
- [ ] Round-trip: convert If→Evaluate, then cursor inside result, convert back → behaviorally identical chain.
- [ ] Evaluate with stacked `When = "A"` `When = "B"` sharing a body → If condition `... = "A" Or ... = "B"`.
- [ ] Evaluate with a non-Break-terminated When body → refused with fall-through message.
- [ ] Cursor on neither construct → "place the cursor" message.
- [ ] Indentation of generated code matches surrounding file style.

---

## Self-review notes (already applied)

- Edit-ordering hazard (insert + replace at the same byte position) is handled by adding replacements before the insertion in `GenerateChanges` and documented in Global Constraints.
- `NullTypeMetadataResolver` name collision (two classes) called out in Task 2 Step 5.
- `RenderBody` is shared by both conversion directions with `dropTrailingBreak` parameterization; `ContainsEvaluateBoundBreak` likewise with `ignoreTrailing`.
- Spec coverage: shared plumbing (Tasks 1–2), Extract incl. dialog/replace-all/type fallback (Tasks 3–4), Inline incl. both safety refusals (Task 5), If→Evaluate incl. Break semantics and mirrored operators (Task 6), Evaluate→If incl. Or-merging and refusals (Task 7), manual test plan (Task 8). Deferred items from the spec's "Out of scope" section have no tasks, as intended.
