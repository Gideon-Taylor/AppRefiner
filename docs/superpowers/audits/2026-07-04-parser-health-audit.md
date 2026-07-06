# PeopleCodeParser.SelfHosted — Health Audit

**Date:** 2026-07-04
**Scope:** Full audit of `PeopleCodeParser.SelfHosted` (~19.5k lines): parser core + directive preprocessing, lexer, visitor/type-analysis layer, AST node layer. Four independent specialist reviews, top findings spot-verified against source.

**How to use this doc:** Each finding has an ID, severity, location, and a status checkbox. Check items off as they're fixed (add a `— fixed in <commit>` note). Line numbers are as of commit `69d0632`; they will drift as fixes land.

**Overall verdict:** Architecture is fundamentally sound. The UTF-8 byte/char index mapping (historically the risky area) verified completely clean; scope push/pop is exception-safe; case-insensitivity discipline is consistent; directive preprocessing preserves source spans correctly. The real problems: one process-crash vector, a cluster of bugs that misparse *valid* PeopleCode, and AST-wiring bugs specific to property getters.

---

## Suggested priority order

1. **CR-1, CR-2** — depth guards (the only process-crash vector)
2. **LX-1, LX-2, PC-1** — valid-code lexer bugs + NOT precedence (corrupt correct programs)
3. **PC-2, PC-3** — property-getter wiring pair (one-line fixes), then **AST-1** to prevent recurrence
4. **ER-1, ER-2** — recovery-budget reset + class-body resync (biggest live-editing wins)
5. **VT-1..VT-5** — visitor traversal gaps
6. Medium/low tail opportunistically; **DC-*** cleanup pass

**Structural recommendation:** build a small fuzz harness (random token deletion/truncation over a corpus of real PeopleCode, asserting no-throw + forward progress + AST retains valid siblings of broken constructs). It would have caught most high-severity findings here and guards all fixes going forward.

---

## CRITICAL — crash vectors

### CR-1 — No recursion depth guard in the parser ✅verified
- [x] Fixed in `a4cdac6` — 2026-07-04: `EnsureStackDepth()` (via `RuntimeHelpers.EnsureSufficientExecutionStack`) at `ParseStatement`/`ParsePrimaryExpression`/`ParseUnaryExpression`; all `catch (Exception)` recovery sites filtered to not swallow it; `ParseProgram` catches it, reports "Code is nested too deeply to parse", returns partial program. Tests: `PeopleCodeParser.SelfHosted.Tests/ParserStackGuardTests.cs`
- **Where:** `PeopleCodeParser.cs:4794` (`ParsePrimaryExpression` → `ParseOrExpression` at 4843); statement recursion `ParseStatement` → `ParseIfStatement` → `ParseStatementList` → `ParseStatement`
- Each `(` costs ~12 stack frames (Or→And→Equality→Relational→TypeCast→Concat→Not→Additive→Multiplicative→Exponential→Unary→Postfix→Primary). Nested `If`/`For` recurse similarly. `StackOverflowException` is uncatchable in .NET — the parser's try/catch blocks cannot intercept it; the background parse thread overflow kills the whole AppRefiner process.
- **Trigger:** paste a file with thousands of nested `(` or generated deeply-nested `If`s.
- **Fix:** depth counter in `ParsePrimaryExpression`/`ParseStatement`; report a parse error and return null past ~500 levels.

### CR-2 — `FindDescendants<T>` is recursive (stack-depth risk)
- [x] Fixed in `a4cdac6` — 2026-07-04: rewritten with explicit `Stack<AstNode>` mirroring `FindNodes`, same pre-order/excludes-self semantics. Tests: `PeopleCodeParser.SelfHosted.Tests/FindDescendantsTests.cs`
- **Where:** `AstNode.cs:174-184`
- Nested `yield`-based enumerator frame per AST level; deeply nested expressions (long `|` concat chains, generated code) risk `StackOverflowException`. Sibling `FindNodes` (`AstNode.cs:199-219`) already solved this with an explicit `Stack<AstNode>` — mirror that pattern.

---

## Misparses of VALID PeopleCode

### LX-1 — `&y-1` lexed as negative literal, fails to parse ✅verified
- [x] Fixed in `71a1b97` — 2026-07-04: deleted both lexer arms (normal + interpolation mode); `-1` now lexes as Minus + literal, folded by `ParseUnaryExpression`. `ParseConstantDeclaration` folds a leading Minus into the literal (constants require literals, not expressions). Tests: `NegativeLiteralLexingTests.cs`
- **Where:** `PeopleCodeLexer.cs:400` (duplicate in interpolation mode at `:1043`)
- `'-' when char.IsDigit(PeekChar()) => ScanNumber()` — no context check; unary-vs-binary decided purely by adjacency. `&x = &y-1;` → `-1` becomes one `IntegerLiteral`; `ParseAdditiveExpression` (`PeopleCodeParser.cs:4496`) has no negative-literal compensation → parse error on legal code. Whitespace-sensitive (`&y - 1` works, `&y-1` fails) → fires constantly during live editing.
- **Fix:** delete the lexer arm; the parser already folds unary minus (`PeopleCodeParser.cs:4585`).

### LX-2 — `+/` and `/+` annotation operators recognized context-free ✅verified
- [x] Fixed (targeted) in `71a1b97` — 2026-07-04: `+/` no longer recognized when followed by `*` (i.e. `+/* comment */` lexes as Plus + comment) — this was the only sequence corrupting *valid* code. `/+` recognition stays context-free: PeopleCode has no unary plus, so `/ +` adjacency cannot occur in valid code; misfires only affect already-invalid input. Tests: `AnnotationOperatorLexingTests.cs`
- **Where:** `PeopleCodeLexer.cs:392,395`
- `&x = &y +/* comment */ 1;` → `+/` becomes `PlusSlash`, shredding the rest of the statement. `&a /+2` becomes `SlashPlus`. These markers only belong in App Designer method-header annotations.
- **Fix:** make recognition contextual (annotation mode) or let the parser assemble them.

### PC-1 — NOT precedence inverted vs PeopleBooks ✅verified
- [x] Fixed in `71a1b97` — 2026-07-04: NOT moved between AND and equality (`ParseAnd` → `ParseNot` → `ParseEquality`; concat now calls additive directly); `ParseNotExpression` recurses so `Not Not &a` parses. Also fixed the relational/equality consume-then-drop half of PM-2: `Not` is only consumed as an operator modifier when the matching operator actually follows (Peek-guarded). Tests: `NotPrecedenceTests.cs`
- **Where:** `PeopleCodeParser.cs:4457` (`ParseNotExpression`), called from `ParseConcatenationExpression:4432`
- NOT binds tighter than comparison; PeopleBooks order is: unary −, `**`, `* /`, `+ −`, relational/=, NOT, AND, OR. `If Not &str = "x" Then` parses as `(Not &str) = "x"` — wrong AST poisons type inference. Also `Not Not &a` fails to parse entirely (single-NOT handling, operand parse starts below NOT level).

### PC-2 — Property getter attached via raw setter, breaking parent link ✅verified
- [x] Fixed — 2026-07-04: uses `SetGetterImplementation()`; getter bodies now reachable via FindDescendants/GetRoot/FindAncestor. Tests: `PropertyGetterWiringTests.cs`
- **Where:** `PeopleCodeParser.cs:1797` — `matchingProperty.Getter = propImplNode;` (setter branch at `:1801` correctly uses `SetSetterImplementation`)
- Bypasses `SetGetterImplementation()` → getter `PropertyImplNode` has `Parent == null`, absent from `Children`. For **every property getter in every class**: `FindAncestor<T>()` from inside the getter body returns null; `FindDescendants<T>()` from the root silently skips getter contents; `GetRoot()` never reaches `ProgramNode`. Plain visitor traversal still works (`IAstVisitor.cs:247` dispatches explicitly) — which is why linting looks fine while span/navigation-based tools are broken.
- **Fix:** one line — use `SetGetterImplementation(propImplNode)`.

### PC-3 — `PropertyImplNode.SetImplementationType` missing `AddChild`
- [x] Fixed — 2026-07-04: `AddChild(type)` added. Tests: `PropertyGetterWiringTests.cs`
- **Where:** `DeclarationNodes.cs:332-339`; exercised from `PeopleCodeParser.cs:2442` on every property-getter with EXTENDS/IMPLEMENTS annotation
- The one `SetX` method in the file that forgets `AddChild(type)`. The `AppClassTypeNode` in `ImplementedInterface` gets no parent, invisible to `FindDescendants` — e.g., a class rename would miss updating a getter's `IMPLEMENTS OldClass.Get` annotation.

### PC-4 — `ParseGenericId` unconditionally consumes any token ✅verified
- [x] Fixed — 2026-07-04: deny-list added (EOF, `;`, `,`, `.`, `:`, parens/brackets, operators, literals) while staying keyword-permissive; downstream null-checks are now live. `ParseMethodHeader` additionally refuses member-start keywords (`property`, `method`, etc.) as method names **when not followed by `(`** — keywords ARE legal member names (`method Property();` compiles), so the lookahead distinguishes a real name from a half-typed `method` line swallowing the next declaration. Tests: `GenericIdAndMemberMatchingTests.cs`
- **Where:** `PeopleCodeParser.cs:1752-1772` — body is `if (true) { take Current.Text; _position++; }`
- Can never return null → every downstream `if (name == null)` is dead code. Consumes `;`, `)`, operators, keywords, EOF as "identifiers."
- **Traced:** `method ` typed above `property string Foo;` → `property` consumed as method name, both declarations corrupted. `create;` → `;` becomes the class path. `ParseAppClassPath`'s break at `:1723-1735` is unreachable.
- **Fix:** deny-list at minimum (EOF, semicolon, operators, parens/brackets), even if staying permissive about keywords-as-identifiers.

### PC-5 — Case-sensitive member matching ✅verified
- [x] Fixed — 2026-07-04: both matching sites use `OrdinalIgnoreCase`. Tests: `GenericIdAndMemberMatchingTests.cs`
- **Where:** `PeopleCodeParser.cs:1778` (methods), `:1792` (properties) — `p.Name == propImplNode.Name`
- PeopleCode identifiers are case-insensitive; App Designer normalizes on save, but during live typing `method foo` implementing declared `Foo` → spurious "no matching declaration" + orphaned implementation. Use `OrdinalIgnoreCase`.

---

## Error recovery quality (live-editing experience)

### ER-1 — Recovery budget never replenishes; can be burned in place
- [x] Fixed — 2026-07-04: `_errorRecoveryCount` resets to 0 after every successfully parsed statement (ParseStatementList + ParseProgram main loop) and class member; ParseStatementList forces ≥1 token of progress when recovery returns with the position unchanged, so the budget can no longer be burned in place. Tests: `ErrorRecoveryTests.cs`
- **Where:** `_errorRecoveryCount` (`PeopleCodeParser.cs:24`, incremented `:322/:358/:401`), `SmartStatementRecover:406-427`
- Cap of 10 for the *entire file*, reset only at `ParseProgram` start. `SmartStatementRecover` returns `true` with zero tokens consumed when already at a sync token. Traced: `While &x Else End-While` loops 10 identical recovery attempts, then recovery is disabled for the rest of the file; every subsequent capped call adds another "Too many parse errors" error → error-per-token noise on large half-broken files.
- **Fix:** reset counter after each successfully parsed statement; force ≥1 token progress when recovery stops at start position.

### ER-2 — One stray token drops the entire class body
- [x] Fixed — 2026-07-04: `ParseClassBody` rewritten to loop to EOF, resynchronizing to the next `method`/`get`/`set` on stray tokens (missing `;` between members is now a reported error, not a silent body drop); `ParseVisibilitySection`'s unknown-token `break` replaced with report + resync to the next member keyword or section boundary. Tests: `ErrorRecoveryTests.cs`
- **Where:** `ParseClassBody` `PeopleCodeParser.cs:1815-1858` + `ParseClassMember:1877-1906`; same weakness in `ParseVisibilitySection:976-980`
- No resynchronization scanning forward for the next `method`/`get`/`set`. A typo before the first member implementation (`mthod Foo`) discards all subsequent *valid* implementations — stylers see an empty class the moment a typo appears.

### ER-3 — `Repeat` above existing code swallows it
- [x] Fixed — 2026-07-04: Repeat (and While, for consistency) keep their parsed body with a placeholder `true` condition, matching If recovery. Tests: `RecoveryTailTests.cs`
- **Where:** `PeopleCodeParser.cs:3616-3621`
- Missing `Until` condition returns null *after* the body was parsed — all consumed statements vanish from the AST. Inconsistent with If/While/For/Evaluate (partial nodes). `While` has same null-return (`:3570-3575`) but before body consumption — lower impact.
- **Fix:** return the node with a placeholder condition.

### ER-4 — Unterminated comments at EOF produce no error
- [x] Fixed — 2026-07-04: block/nested/REM scanners report "Unterminated ... comment" at the comment start. Tests: `RecoveryTailTests.cs`
- **Where:** `ScanBlockComment` (`PeopleCodeLexer.cs:605-614`), `ScanNestedComment` (`:626-645`, depth also unchecked at exit), `ScanRemComment` (`:576-579`)
- All terminate cleanly but silently — no squiggle when a runaway comment eats the rest of the program. (Contrast `ScanStringLiteral:685`, which does report.)

### ER-5 — Plain string literals never recover at EOL; error reported at wrong position
- [x] Fixed — 2026-07-04: plain strings recover at end-of-line (single-line per App Designer, confirmed by Tim) and the error is reported at the opening quote. Tests: `RecoveryTailTests.cs`
- **Where:** `ScanStringLiteral` `PeopleCodeLexer.cs:658-681`, error at `:685`
- One stray `"` re-tokenizes to next quote/EOF; the "Unterminated string literal" error is reported at **EOF**, not the opening quote (`start` is in scope — pass it). Interpolated strings deliberately recover at EOL (`:717-722`); consider similar heuristic for plain strings, or at least fix the error position.

### ER-6 — Function parse-failure recovery stops at first semicolon
- [x] Fixed — 2026-07-04: recovery syncs to End-Function/Function/Declare only (semicolons inside the broken body no longer end recovery). Exception-path; verified by inspection — not organically reachable from source text.
- **Where:** `ParseFunction` catch `PeopleCodeParser.cs:2941-2951`
- Syncs to `EndFunction` *or* `Semicolon`; a semicolon inside the broken body wins and remaining body statements leak into the main program block.

### ER-7 — Setter recovery targets wrong end token
- [x] Fixed — 2026-07-04: recovery accepts End-Get or End-Set and consumes the terminator. Exception-path; verified by inspection.
- **Where:** `ParsePropertyImplementation` catch `PeopleCodeParser.cs:2102` — `PanicRecover({EndGet})` only; exception in a SET block skips up to 100 tokens hunting END-GET.

### ER-8 — Unprotected top-level parse paths
- [x] Fixed — 2026-07-04: ParseProgram outer try gained a general catch (after the CR-1 stack-guard catch) that reports and returns the partial program — no exception escapes to the host.
- **Where:** `ParseProgram` app-class/interface branch `:462-499` (try/finally, no catch); `ParseInterface:2571` same
- Unanticipated exception (node-ctor ArgumentException etc.) escapes to the host.

---

## Visitor / type-analysis traversal gaps

### VT-1 — `VisitProperty` never visits `PropertyNode.Type`
- [x] Fixed — 2026-07-04: `ScopedAstVisitor.VisitProperty` now accepts `node.Type`. Tests: `VisitorTraversalTests.cs`
- **Where:** `ScopedAstVisitor.cs:492-499` — registers the variable but no `base.VisitProperty` / `node.Type.Accept(this)`
- Property declared types get no inferred TypeInfo (not even the Any fallback); no type-node visitor fires for them → "unknown app class in property declaration"-style checks can never run.

### VT-2 — Method-declaration parameter annotations unreachable
- [x] Fixed — 2026-07-04: `annotation.Type.Accept(this)`. Note: end-to-end testing showed real-world annotations attach to `MethodImplNode` (whose loop was already correct), so this was a latent path; fixed anyway with a regression test. Tests: `VisitorTraversalTests.cs`
- **Where:** `IAstVisitor.cs:304-308` — `annotation.Accept(this)` where `ParameterNode.Accept` is intentionally empty (`DeclarationNodes.cs:673-676`)
- Should be `annotation.Type.Accept(this)` (real parameters do this at `:301`; `VisitMethodImpl` does it right at `:321`). Every `/+ +/` annotation type on a method **declaration** is invisible to all visitors.

### VT-3 — Getter/setter parameter annotations not visited at all
- [x] Fixed — 2026-07-04: `IAstVisitor.VisitPropertyImpl` visits `annotation.Type` for each parameter annotation. Tests: `VisitorTraversalTests.cs`
- **Where:** `IAstVisitor.cs:626-630` (`VisitPropertyImpl` visits only `ImplementedInterface` + `Body`); `ScopedAstVisitor.cs:794-845` adds textual references but never `.Type.Accept()`.

### VT-4 — `VisitAppClass` double-visits `BaseType`
- [x] Fixed — 2026-07-04: fallback child loop skips `BaseType`. Tests: `VisitorTraversalTests.cs`
- **Where:** `IAstVisitor.cs:212-266` — explicit visit at `:218`, then the "not already visited" fallback loop (`:251-265`) doesn't exclude `BaseType`
- Any lint rule reacting to base-type nodes with `AddReport()` double-reports for every class/interface with EXTENDS/IMPLEMENTS.

### VT-5 — Sibling catch blocks reusing `&Ex` collide in the registry
- [x] Fixed — 2026-07-04: `VisitCatch` merges a same-scope Exception re-declaration into the existing `VariableInfo` (extra Declaration reference) instead of letting the registry overwrite it — rename now sees both catches' usages. Along the way: catch exception variables get real `FirstToken`/`LastToken` (the SP-1 catch case — spans were (0,0), which also defeated reference dedupe), and the declaration identifier is no longer visited as a spurious Read (exception vars previously always looked "used"). Tests: `VisitorTraversalTests.cs`
- **Where:** `ScopedAstVisitor.cs:732-747` (`VisitCatch` registers into enclosing scope); `VariableRegistry.cs:66-88` plain indexer assignment keyed `(name, scopeId)`
- `catch Exception &Ex ... catch SQL.SQLException &Ex` — after traversal only the last catch's `VariableInfo` is reachable via `FindVariable`; a rename invoked from catch #1's `&Ex` would resolve to and rename catch #2's usages.

### VT-6 — Ampersand-stripping heuristic couples unrelated variables
- [ ] Fixed
- **Where:** `ScopedAstVisitor.cs:574-578`, `:908-915`, `:1019-1026`
- Every `&Foo` reference *unconditionally* also records a reference to bare `Foo` if any variable/property has that name — not just as a fallback. A local `&Total` marks an unrelated property `Total` as "used" → false negatives in unused-variable detection.

### VT-7 — `VariableInfo.IsSafeToRefactor` excludes exception variables
- [x] Fixed — 2026-07-04: `VariableKind.Exception` included. Tests: `VisitorTraversalTests.cs`
- **Where:** `VariableInfo.cs:99` — `Kind is Local or Instance or Parameter`; `VariableKind.Exception` omitted, so rename UIs gating on it refuse catch variables that are exactly as local as a `Local`.

### VT-8 — `AppClassNode.MethodImplementations` is dead but excluded from the fallback visit
- [ ] Fixed
- **Where:** `ProgramNodes.cs:378` (populated only via `AddMember` when `IsImplementation` already true — never in the real parser flow, see `PeopleCodeParser.cs:984/1781`); excluded from fallback at `IAstVisitor.cs:259`
- If ever populated (hand-built AST), those methods become silently unreachable by traversal.

---

## Parser correctness — medium

### PM-1 — Chained assignment `&a = &b = &c` should be a PARSE ERROR
- [x] Fixed — 2026-07-04: bare AssignmentNode on the RHS reports "wrap it in parentheses to compare values" while still building the AST (hook for a future quick fix). Parenthesized comparisons unaffected. Tests: `ParserMediumTests.cs`
- **Where:** `PeopleCodeParser.cs:4217` — `right = ParseAssignmentExpression()` ("Right associative") produces nested `AssignmentNode`s
- **Source of truth (PeopleCode compiler):** chained assignment is *invalid syntax*. The compiler requires explicit parens — `&a = (&b = &c)` — to resolve the `=` ambiguity; a bare `&b = &c` on the RHS of an assignment does not become a comparison, it fails to compile.
- **Fix:** parser should report an error like "assignment cannot appear on the right-hand side of an assignment; wrap it in parentheses to compare" instead of silently building nested AssignmentNodes.
- **Follow-up idea:** offer a "wrap in parens" quick fix that rewrites `&a = &b = &c` → `&a = (&b = &c)`.

### PM-2 — Consumed-then-dropped `Not` tokens
- [x] Fixed — 2026-07-04: equality/relational Peek-guard before consuming `Not` (with PC-1); EVALUATE now rewinds pre-consumed `Not` tokens when no comparison operator follows, letting expression parsing handle prefix NOT at its correct precedence. Tests: `ParserMediumTests.cs`
- **Where:** `ParseEqualityExpression:4301-4306`, `ParseRelationalExpression:4340-4345`; EVALUATE `When Not &flag` at `:4053-4088`
- `Not` consumed before operator lookup; if no operator follows (or `=` excluded via `allowAssignmentEqual`), negation vanishes silently. Traced: `&x Not = 5;` → AST is plain `&x = 5`, no error. EVALUATE's `notCount` applied only when explicit relop follows.

### PM-3 — `Global`/`Component` declaration with initializer loses tokens
- [x] Fixed — 2026-07-04: declaration stays in the AST; "cannot have an initializer" reported; initializer consumed so following statements parse. Tests: `ParserMediumTests.cs`
- **Where:** `PeopleCodeParser.cs:3204-3208` — on `=`, returns null *without restoring `_position`* (Local path at `:619-630` resets correctly). `Global number &n = 5;` → declaration silently swallowed + cascade errors.

### PM-4 — Directive skipped-span garbage for `#If`-false with no `#Else`
- [x] Fixed — 2026-07-04: `ProcessDirectiveEndIf` sets `IfBlockEnd` when no `#Else` was seen. Tests: `ParserMediumTests.cs`
- **Where:** `DirectivePreprocessor.cs:77` — records `SourceSpan(IfBlockStart, IfBlockEnd)` but `IfBlockEnd` only assigned in `ProcessDirectiveElse` (`:160`); `ProcessDirectiveEndIf` sets only `ElseBlockEnd` (`:188`)
- `#If #ToolsRel < "8.50" #Then ... #End-If` (false) → skipped span ends at position 0 (inverted). Anything graying out inactive regions renders the wrong range.

### PM-5 — IndexOutOfRange reachable from parser constructor via `#Else` at end of token list
- [x] Fixed — 2026-07-04: bounds-checked. Tests: `ParserMediumTests.cs`
- **Where:** `DirectivePreprocessor.cs:171-172` — `position++` then unguarded index; no try/catch up through the `PeopleCodeParser` constructor (`:69-74`)
- Safe today only because `TokenizeAll` always appends EOF; the constructor accepts arbitrary `IEnumerable<Token>`. (`:133`'s analogous index IS inside a catch.) Bounds-check it.

---

## Lexer correctness — medium

### LM-1 — Numeric overflow silently yields `Value = 0`
- [x] Fixed — 2026-07-04: widening chain int → long → decimal → raw source text (never 0); `NegateLiteralValue` handles the string form. Tests: `LexerMediumTests.cs`
- **Where:** `ScanNumber` `PeopleCodeLexer.cs:1162-1169`
- `int.TryParse`/`decimal` overflow (`9999999999` — legal, PeopleCode Number is arbitrary-precision) → literal with `Value = 0`, no LexError. Flows into `LiteralNode` (`PeopleCodeParser.cs:5015`) and is stringified by transforms (`ArrayExtensions.cs:732`) — would emit `0`.
- **Fix:** fall back to wider parse or keep raw text as value; error if truly unparseable.

### LM-2 — Line/column base inconsistency (0-based line one, 1-based after)
- [x] Fixed — 2026-07-04: standardized on ZERO-based lines and columns (matching Scintilla and all existing AppRefiner consumers — one-based would have shifted every annotation/outline/suppression); `Advance` resets column to 0, `SourcePosition` docs corrected. Tests: `LexerMediumTests.cs`
- **Where:** constructor `PeopleCodeLexer.cs:227-229` (`_line = 0; _column = 0`) vs `Advance()` `\n` branch `:264-268` (`_column = 1`)
- `"ab\ncd"`: token `ab` at line 0 col 0; `cd` at line 1 col **1**. Contradicts `SourcePosition` documented "One-based" (`AstNode.cs:373,378`). Pick one convention.

### LM-3 — Mid-line `REM` not recognized after statement keywords
- [x] Fixed — 2026-07-04: comment context also opens after `Then`/`Else` (word-boundary, case-insensitive) and after `*/` / `*>`; `REC.REM` stays protected. Tests: `LexerMediumTests.cs`
- **Where:** `IsInCommentContext` `PeopleCodeLexer.cs:512-530` — requires line start or preceding `;`
- `If True Then Rem note; End-If;` and `/* hdr */ REM old;` mis-lex as identifiers → parse error on valid code. Heuristic exists for good reason (`REC.REM` field access) but under-approximates: `Then`/`Else` also open REM-valid positions.

### LM-4 — Consecutive trailing comments at EOF: all but last dropped
- [x] Fixed — 2026-07-04: EOF path chains earlier comments as leading trivia; `TokenizeAll` attaches trailing comments to the EOF token so they survive the parser's trivia filter into `ProgramNode.Comments`. Tests: `LexerMediumTests.cs`
- **Where:** `NextToken` EOF-trivia path `PeopleCodeLexer.cs:371-383`; `TokenizeAll:311-317` only flattens `LeadingTrivia` of returned tokens
- `&x = 1; /* first */ /* second */` at EOF → only `/* second */` survives. Comment-aware consumers (folding, comment preservation in refactors) lose data.

---

## Latent API traps (verified NOT firing today; waiting for the next AST-building consumer)

### AST-1 — Parent/child invariant enforced by convention only (systemic)
- [x] Fixed — 2026-07-04: all ~20 single-child slots now `{ get; private set; }` routed through `SetX()` (back-reference `MethodImplNode.Declaration` is `internal set`); all 24 structural `List<T>` collections converted to `IReadOnlyList<T>` over private backing lists mutated only by `AddX()`. Enforced by reflection tests (`AstNodeInvariantTests`) so new node types can't regress; plus a parse-and-verify parent-integrity test that follows property links, not just Children. Known remaining gap: `AppClassNode.VisibilitySections` (Dictionary of `List<AstNode>`) is still mutable. Downstream fix-ups: 4 AppRefiner files (incl. removing a synthetic-ProgramNode wrapper in MethodParametersTooltipProvider that would now reparent the real AST).
- **Where:** pervasive — `DeclarationNodes.cs`, `StatementNodes.cs`, `TypeNodes.cs`, `ProgramNodes.cs`
- Nearly every child slot has both a correct `SetX()` and a raw public setter (`MethodNode.ReturnType:58`, `ArrayTypeNode.ElementType` (`TypeNodes.cs:71` — has NO SetX at all), `IfStatementNode.ElseBlock`, `ForStatementNode.StepValue`, `PropertyNode.Getter/Setter`, `ProgramNode.AppClass/MainBlock`, etc.). All structural `List<T>` collections are mutable with companion `AddX()` methods. PC-2/PC-3 prove the convention fails even for its authors.
- **Fix:** `{ get; private set; }` + `IReadOnlyList<T>` backed by private lists; the `ParameterNode.Type` custom setter (`DeclarationNodes.cs:643-654`) is the correct in-file model. This one systemic fix prevents the whole bug class.

### AST-2 — `SourcePosition` 3-arg constructor silently misbinds
- [x] Fixed — 2026-07-04: collapsed to 1-arg (index only) and full 4-arg constructors; partial calls no longer compile. One caller (AddFlowerBox) updated. Tests: `AstApiTrapTests.cs`
- **Where:** `AstNode.cs:380-394` — `(int index, int line = 1, int column = 1)` vs `(int index, int byteIndex, int line = 0, int column = 0)`
- 2-arg call is CS0121 (safe); **3-arg call binds to the first ctor**: `new SourcePosition(charIdx, byteIdx, line)` silently sets `Line = byteIdx` and forces `ByteIndex = Index` — recreating the exact byte/char confusion the type exists to prevent. Collapse to one constructor or require named args.

### AST-3 — `SourceSpan` getter drops position when only one token set
- [x] Fixed — 2026-07-04: getter tolerates a single missing token (uses the available one for both ends). Tests: `AstApiTrapTests.cs`
- **Where:** `AstNode.cs:35-51` — requires both `FirstToken` AND `LastToken`; otherwise falls to `_explicitSourceSpan` (default = position 0)
- Hand-built or error-recovery node with only `FirstToken` → span-based edit lands at document offset 0. Silent wrong edit. Fall back to `FirstToken.SourceSpan` when `LastToken` is null.

### AST-4 — `ImportNode(IEnumerable<string>, string?)` wrong wildcard `FullPath`
- [x] Fixed — 2026-07-04: FullPath decided by the className parameter instead of the not-yet-assigned IsWildcard. Tests: `AstApiTrapTests.cs`
- **Where:** `ProgramNodes.cs:245-269` — `FullPath` computed reading `IsWildcard` before `ImportedType` is assigned (always false in ctor) → wildcard import yields `"MyPackage:"` not `"MyPackage:*"`. Unreachable today (both call sites use the string-path ctor); a future "add wildcard import" quick fix hits it silently.

### AST-5 — No cycle protection in parent-chain walkers
- [x] Fixed — 2026-07-04: depth cap (100k) in FindAncestor/GetRoot/GetScopeContext throws InvalidOperationException instead of looping forever (RED run literally hung the test host). Tests: `AstApiTrapTests.cs`
- **Where:** `AstNode.cs:159-197` (`FindAncestor`, `GetRoot`), `AstNodeScopeExtensions.cs:29-39` (`GetScopeContext`)
- Raw setters (AST-1) make accidental cycles constructible; result is an infinite loop, not a fail-fast. Cheap: depth cap or visited check.

### AST-6 — `ForStatementNode.GetIteratorName()` unchecked cast
- [x] Fixed — 2026-07-04: pattern-matched; non-identifier targets fall back to ToString(). Tests: `AstApiTrapTests.cs`
- **Where:** `StatementNodes.cs:253-261` — `((IdentifierNode)ma.Target)` unvalidated; parser only builds single-level `record.field` today, but a hand-built nested member-access iterator throws `InvalidCastException` from `ToString()`.

---

## Token-span quality (LOW)

### SP-1 — Token-less nodes → span (0,0)
- [x] Fixed — 2026-07-04: FOR iterators (both forms), catch `BuiltInTypeNode(Exception)`, and all placeholder literals now carry real tokens (catch variable was fixed earlier with VT-5). Tests: `TokenSpanTests.cs`
- FOR iterator `IdentifierNode` (`PeopleCodeParser.cs:3454`, `:3466` — affects **valid** code), CATCH exception variable (`:3680` — **fixed 2026-07-04 with VT-5**), `BuiltInTypeNode(Exception)` (`:3659`), placeholder `LiteralNode`s (`:3824, :3846, :3865, :4037, :4969, :4990`). Refactors/stylers using these spans target offset 0.

### SP-2 — `ExitStatement` span excludes its argument
- [x] Fixed — 2026-07-04: `LastToken = exitCode?.LastToken ?? token`. Tests: `TokenSpanTests.cs`
- `PeopleCodeParser.cs:3803-3807` sets `LastToken` = EXIT keyword even with an exit code parsed; `:3328`'s `??` fixup can't repair non-null.

### SP-3 — Astral-plane chars outside strings → overlapping zero-width invalid tokens
- [x] Fixed — 2026-07-04: `ScanInvalidCharacter` consumes the full surrogate pair as one Invalid token. Tests: `TokenSpanTests.cs`
- `ScanInvalidCharacter` (`PeopleCodeLexer.cs:1385-1391`) consumes one UTF-16 unit; surrogate pair → two `Invalid` tokens, first with empty byte span. Consume a full code point.

---

## Lexer misc (LOW)

- [ ] **LL-1** — U+FEFF (BOM surviving into source string) → spurious "Invalid character" at 0:0 (`char.IsWhiteSpace('﻿')` is false). Skip/trivia-ize it. — **Won't fix** (2026-07-04, per Tim)
- [ ] **LL-2** — Lone `\r` line endings never increment `_line` (`PeopleCodeLexer.cs:264-272`). Unlikely via Scintilla; trivial hardening. — **Won't fix** (2026-07-04, per Tim)
- [x] **LL-3** — `ScanEndKeyword` (`:1322-1341`) accepts `End Class` / `End -Class` as `End-Class` (over-lenient); multi-space `End  -  Class` fails via single-pass `Replace("--","-")`. Backtrack state restore verified correct; no corruption. — Fixed 2026-07-04: strict `End-Xxx` form only (single hyphen, no spaces); backtrack also restores the shared StringBuilder. Tests: `EndKeywordLexingTests.cs`
- [x] **LL-4** — `Token.cs:502`: `EndSet => "snd-set"` typo in `GetText()`. — Fixed 2026-07-04. Tests: `EndKeywordLexingTests.cs`

## Parser misc (LOW)

- [x] **PL-1** — `PartialShortHandAssignmentNode` never parses a RHS (`:4207-4215`): complete `&x += 1;` becomes partial node + orphan literal statement. Intentional for AppRefiner's expansion-on-entry, but guard by parsing the RHS when present. — Fixed 2026-07-04: complete shorthand parses as a real `AssignmentNode` (with new `OperatorToken` property); partial node still produced when the RHS is absent; `ConcatAutoComplete` expansion handles both shapes. Tests: `ParserMediumTests.cs`
- [ ] **PL-2** — Static mutable `ToolsRelease` (`:27`) mutated by *instance* `SetToolsRelease` (`:5396`) — races across concurrent background parses. Also `PreProcessDirectives` culls prior errors by `Contains("directive")` substring (`:82`) — can delete unrelated messages. — **Won't fix for now** (2026-07-04, per Tim)
- [ ] **PL-3** — Unclosed `#If` (false condition) silently drops all remaining tokens incl. EOF, no skipped-span recorded (`DirectivePreprocessor.cs:96`); `DirectiveExpressionParser.ContainsParentheses` (`:230`) is dead code. — **Deferred** (2026-07-04, per Tim; no clear fix)
- [x] **PL-4** — Perf: `Token.CreateEof` allocated on every `Current`/`Peek` at EOF; `Check(params TokenType[])` allocates per call in hot loops; `ReportError` builds rule-stack join per error (painful combined with ER-1's flood). — Fixed 2026-07-04: EOF tokens cached (invalidated on directive reprocessing); non-allocating 2/3/4-arg `Check` overloads; error context built by shared helper that suppresses detail past 500 errors.

---

## Dead code / cleanup (DC)

- [x] **DC-1** — `VariableUsageTracker.cs`, `IVariableUsageTracker.cs`, `Models/ScopeInfo.cs`: complete parallel scope/usage implementation, zero consumers (superseded by `VariableRegistry`/`ScopeContext`). — Trackers deleted 2026-07-04. **`ScopeInfo.cs` KEPT** — the audit was wrong about it: it has live consumers in AppRefiner (RedeclaredVariables, PropertyAsVariable, ScopeTooltipProvider).
- [ ] **DC-2** — `Visitors/ExampleUsage.cs` (`VariableAnalysisVisitor`): example code shipping in production assembly; only referenced by a README. — **Kept** (2026-07-04, per Tim)
- [x] **DC-3** — `ExpressionNode.InferredType : TypeNode?` (`ExpressionNodes.cs:8-13`): never read or written; name-collides with the real `GetInferredType() : TypeInfo?` mechanism — one autocomplete away from a silent-null bug. Delete. — deleted 2026-07-04 (with AST-1; verified zero usages repo-wide)
- [x] **DC-4** — `PropertyAccessNode`: never constructed (parser emits `MemberAccessNode`); dead branches persist in `ScopedAstVisitor.VisitExpressionAsRead/Write`, `AstVisitorBase.VisitPropertyAccess`, `TypeCheckerVisitor.IsVariableReference`. — Deleted 2026-07-04 (node class, visitor members, all dead branches, and two AppRefiner pattern-match disjuncts).
- [x] **DC-5** — `IAstVisitor<TResult>` (`IAstVisitor.cs:78-144`): zero implementers anywhere; must be hand-maintained per new node type. — Deleted 2026-07-04 (interface + abstract `Accept<TResult>` + ~60 node overrides; verified zero consumers in AppRefiner/PluginSample).
- [x] **DC-6** — Dead `TokenType` members: `True`, `False`, `Add`, `GenericIdLimited` (booleans emit as `BooleanLiteral`). — Deleted 2026-07-04 (incl. GetText arms and two dead parser conditions on GenericIdLimited; none were range endpoints for the Is* checks).
- [x] **DC-7** — Parser dead code: `if (true)` in `ParseGenericId` (see PC-4), empty `if` bodies at `:1073-1075`, `:3033-3035`, `:3075-3077`, empty foreach `:576-579`; unused usings (`System.Net.Http.Headers`, `System.Reflection.Metadata`); duplicated annotation parsers (MethodNode vs PropertyImplNode overloads). — Done 2026-07-04 except the annotation-parser dedup (a behavior-bearing refactor, deliberately left; `if (true)` had already died with PC-4).
- [x] **DC-8** — `ScopedAstVisitor.Reset()` (`ScopedAstVisitor.cs:868-879`) doesn't reset `skipChildTraversal` — harmless today (all mutation sites use try/finally) but a latent trap for instance reuse. — Fixed 2026-07-04.

---

## Addendum: recovery-opportunity pass (2026-07-04, post-audit)

A second agent pass hunted remaining recovery gaps after all the above landed. Findings R-1..R-12 plus one valid-code bug — **all fixed same day** except where noted. Tests: `RecoveryHardeningTests.cs`, `RecoveryMediumTests.cs`, `RecoveryLowTests.cs`.

- [x] **R-1** — half-typed `import` swallowed the next `import`/`class` keyword, dropping the whole program out of app-class mode. Program-structure keywords now refused as package names.
- [x] **R-2** — statement lists/recovery walked through `End-Method`/`End-Function`/`End-Get`/`End-Set`; a missing `End-If` merged all following methods. BlockSyncTokens are now universal hard boundaries.
- [x] **R-3** — `SynchronizeToToken(Then)` hunted across statement boundaries and stole a later If's `Then` (statements between silently deleted). Now bounded (`SynchronizeToTokenBounded`); half-typed If keeps following statements in its body.
- [x] **R-4** — stray `Library` at preamble position was an **infinite loop** (falsified the "no infinite loops" entry above). Consumed with a clear error + forced-progress guard in `ParseProgramPreambles`.
- [x] **R-5** — the unfixed siblings of PC-4: `ParseGenericId` now denies `End-*`/`&var`/`%var` (a missing property name used to consume `End-Class` and shred the file; `&rec.` + Enter silently absorbed the next statement); method-impl/getter/setter/function/property name positions refuse a member-start keyword that clearly begins the next construct.
- [x] **R-6** — FOR bounds/LOCAL initializers/RETURN values parsed top-level assignments (clean parse, wrong AST — next statement swallowed with zero errors). `ParseExpressionNoAssignment` reports, rewinds, and the statement re-parses.
- [x] **R-7** — bare `create` deleted the next statement; now returns a placeholder ObjectCreationNode.
- [x] **R-8** — lone `/+` ate the rest of the file; annotation skip bounded (closer/block terminator/100 tokens), error at the `/+`.
- [x] **R-9** — half-typed `#If` bound to a LATER directive's `#Then` and deleted the region between; condition extraction now line-bounded, directive recovery re-emits skipped tokens, and all preprocessor errors carry real spans (were pinned at 0:0). Also fixed the PL-2 error-culling half (by context, not message substring).
- [x] **R-10** — broken interpolation holes leaked in-string tokens and surfaced "Unexpected literal type" exception text; now resynchronized within the string.
- [x] **R-11** — half-typed `Local`/`Constant` declarations dropped the named node; name+type now kept with placeholder/omitted values.
- [x] **R-12** — orphaned `End-*` tokens get "'End-If' has no matching 'If'" instead of raw enum names; the "Finished parsing... Got: X == Y" dev message reworded.
- [x] **Bonus** — `/+ Extends/Implements Pkg:Intf.Name +/` annotations stored `"+/"` as `ImplementedMethodName`/`ImplementedPropertyName` (assigned `Current.Text` after the name was already consumed); now uses the parsed name.

---

## Design notes (not defects — behavior to be aware of)

- Trailing trivia is never populated by the lexer; an end-of-line comment attaches as *leading* trivia of the next token, possibly on a distant line. Comment-association tooling must account for this.
- `RecordEvents` token classification is context-free: any identifier named `Workflow`, `RowInit`, etc. becomes `TokenType.RecordEvent` — the parser must accept `RecordEvent` wherever `GenericId` is valid.
- Directive tokens (`#If` etc.) are created via `CreateTrivia` but emitted as primary tokens; `IsTrivia()` returns true for them — a consumer filtering on `IsTrivia()` drops directives.

---

## Verified healthy (explicitly checked — don't re-audit)

- **UTF-8 byte map** (`PeopleCodeLexer.cs:157-225`): correct for ASCII, 2/3-byte BMP, surrogate pairs (both halves → pair start, +4), lone surrogates (U+FFFD = 3 bytes matches), sentinel entry. Bytes derived from the actual encoded buffer — char/byte positions cannot drift.
- **No infinite loops** in lexer, parser, or preprocessor: every loop provably consumes or breaks (ER-1's budget-burn stall terminates via the cap).
- Empty token stream / EOF mid-construct / `Peek` past end all safe via synthesized EOF token.
- String `""` escaping, empty strings, span start-inclusive/end-exclusive convention, two-char operator peeks.
- Unary minus vs `**` precedence (`(-2)**2 = 4`), `**` right-associativity.
- Directive nesting (stack-based), directives mid-statement, retained-token source positions.
- `%Super`/`%Metadata` boundary logic; keyword case-insensitivity throughout.
- `ScopedAstVisitor` scope push/pop exception-safe (try/finally at every site); getter/setter `PropertyImplNode`s always distinct.
- Case-insensitivity across `VariableRegistry` (3 dicts), `VariableKey`, `VariableInfo`, `VariableReference`, `TypeInferenceVisitor._importMap`, inheritance-walk sets, `TypeMetadataBuilder`.
- `TypeCheckerVisitor.AreTypesCompatible` treats `UnknownTypeInfo` like `AnyTypeInfo` (`TypeCheckerVisitor.cs:205`) — the pervasive `UnknownTypeInfo.Instance` fallbacks do NOT cascade into false type errors.
- `ParseMethodAnnotations` three-way backtracking (`:2114-2162`) always progresses, including empty `/+ +/`.
