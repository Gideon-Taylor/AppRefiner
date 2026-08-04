# Evaluate/When clause grouping in completion analysis

Fixes GitHub issue #79 — a false `Not all paths return a value` on a method whose
every `Evaluate` arm returns.

This refines the `Evaluate` handling specified in
`2026-07-09-completion-analysis-exit-modes-design.md`. Everything that spec says
about union-of-arms, `Break` absorption, and the missing-`When-Other` path stays
true; only the treatment of a **bodyless `When` clause** changes.

## Background

The reported method (abridged):

```peoplecode
method FieldTypeNameConvertToString
   /+ Returns String +/
   Evaluate &fieldTypeName
   When "NUMBER"
   When "SIGNEDNUMBER"
      Return (String(&anyValue));
      Break;
   ...
   When-Other
      Return (String(&anyValue));
      Break;
   End-Evaluate;
end-method;
```

Every arm returns, yet `NotAllPathsReturnCheck` reports the method. The reporter had
to append a dead `Return "";` after `End-Evaluate` to silence it.

**Cause.** The parser gives `When "NUMBER"` an empty `BlockNode`
(`PeopleCodeParser.cs:4431`) because its statements belong to the following clause.
`CompletionAnalyzer.AnalyzeEvaluate` (`CompletionAnalyzer.cs:85`) analyzes each arm in
isolation, an empty block completes normally, and that `Normal` is unioned into the
`Evaluate` — so the body looks like it can fall off the end of the method.

## PeopleCode Evaluate semantics (empirically confirmed)

Verified against App Designer with this program, varying `&x`:

```peoplecode
Evaluate &x
When = 0;
When = 1;
   %Response.Write("1");
   Break;
When = 2
When-Other
   %Response.Write("When-other");
End-Evaluate;
%Response.Write("Done");
```

| `&x` | output | shows |
|---|---|---|
| 0 | `Done` | `When = 0;` is its own arm with an empty body |
| 0, with the `;` removed | `1Done` | a bodyless `When` shares the next clause's block |
| 1 | `1Done` | `Break` ends the whole evaluation |
| 2 | `Done` | grouping does **not** reach into `When-Other`; a matched `When` also suppresses `When-Other` |

The governing rules:

1. Clauses are tested **top to bottom, all of them** — every `When` whose condition
   matches runs its body. Arms are not mutually exclusive.
2. `Break` stops the entire `Evaluate`; no further conditions are tested.
3. `When-Other` runs only when no `When` matched.
4. A `When` whose condition is **not** followed by a `;` and which has no statements
   of its own shares the *next clause's* block — the stacked-`When` idiom.
5. A trailing `;` after the condition closes the clause. `When = 1;` shows the
   terminator is inert once statements follow, so it only carries meaning when the
   resulting body is empty.

Rule 1 means the existing union-of-arms model is correct: any arm that can complete
normally makes the whole `Evaluate` able to complete normally, because nothing forces
a later clause to match. Only rule 4 is missing from the analyzer.

## Design

### Effective body of a `When` clause

| clause | effective body |
|---|---|
| has statements | its own |
| empty, `;`-terminated | empty → `Normal` |
| empty, no terminator, another `When` follows | that clause's effective body (chains) |
| empty, no terminator, `When-Other` or `End-Evaluate` follows | empty → `Normal` |

Chaining walks to the immediately following `When` clause and stops there: a
`;`-terminated empty clause ends the chain with an empty body, matching the reading
that a bodyless clause shares the *next clause's block* rather than searching for the
next non-empty one.

### 1. Parser — keep the terminator

`WhenClause` gains a `Token? ConditionTerminator` (null when absent), populated from
the semicolons already consumed and discarded at `PeopleCodeParser.cs:4428`. Capture
the first one matched. `WhenClause` is constructed in exactly one place
(`PeopleCodeParser.cs:4432`).

### 2. `CompletionAnalyzer.AnalyzeEvaluate`

Resolve each clause's effective modes per the table, then union, `Absorb(Break)`, and
add `Normal` when `WhenOtherBlock` is null — all unchanged from today.

Annotate an empty grouped block with its **group's** modes, not its own `Normal`.
`NotAllPathsReturnCheck` walks `WhenClauses` bodies looking for the "best incomplete
arm" to host its secondary diagnostic (`NotAllPathsReturnCheck.cs:181-269`,
`350-391`); left annotated as `Normal`, an empty grouped clause would win that search
on genuinely broken `Evaluate`s and put `This path can complete without returning a
value.` on an innocent `When` line.

Each block is still analyzed exactly once, so the annotation stays deterministic:
analyze all clause bodies first, then resolve groups and re-annotate the empty ones.

### 3. `ConvertIfEvaluate`

`ConvertEvaluateToIf` already implements grouping (`ConvertIfEvaluate.cs:363-388`,
Or-joining pending empty `When`s into the next non-empty body) but cannot see the
terminator, so it folds a closed `When = "one";` into the next group and emits an If
chain that runs the body for `"one"` — a semantic change.

Fix: a `;`-terminated empty clause is not foldable. `SetFailure` with a message naming
the clause rather than emitting an empty `If` branch.

## Tests

`PeopleCodeParser.SelfHosted.Tests`:

- Parser: `When = 1;` records a terminator, `When = 1` does not; `When = 1;` followed
  by statements still binds them as its body.
- `CompletionAnalyzerTests`: each row of the effective-body table; the four rows of
  the empirical table above as end-to-end exit-mode assertions; an empty grouped block
  is annotated with its group's modes.
- `NotAllPathsReturnCheckTests`: the issue #79 method verbatim reports nothing; an
  `Evaluate` that *is* incomplete because of a `;`-terminated empty arm still reports,
  with the secondary on that arm's `When` line.

Existing tests stay green, including
`EvaluateOneWhenFallsThrough_WithWhenOther_KeepsNormal` — correct under rule 1.

## Out of scope

A diagnostic for a bodyless `When` immediately before `When-Other` or `End-Evaluate`.
It silently swallows the match and skips `When-Other`, so it is nearly always a
mistake, and the grouping data makes it cheap to detect — but it is a new styler, not
part of this fix.

## Files

- `PeopleCodeParser.SelfHosted/Nodes/StatementNodes.cs` — `WhenClause.ConditionTerminator`
- `PeopleCodeParser.SelfHosted/PeopleCodeParser.cs` — capture it in `ParseEvaluateStatement`
- `PeopleCodeParser.SelfHosted/Analysis/CompletionAnalyzer.cs` — grouping in `AnalyzeEvaluate`
- `AppRefiner/Refactors/ConvertIfEvaluate.cs` — refuse to fold a terminated empty clause
- `PeopleCodeParser.SelfHosted.Tests/EvaluateWhenTerminatorTests.cs` (new — parser level)
- `PeopleCodeParser.SelfHosted.Tests/Analysis/CompletionAnalyzerTests.cs`
- `PeopleCodeParser.SelfHosted.Tests/Compilation/NotAllPathsReturnCheckTests.cs`
