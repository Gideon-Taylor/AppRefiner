# Extract Function/Method — Manual Test Script

Feature branch: `feat/extract-function-method`. All code is in `AppRefiner/Refactors/ExtractFunctionMethod.cs`. The refactor is auto-discovered; it appears in the command palette / refactor list as **"Extract Function/Method"**.

**How to run each case:** paste the input into an Application Designer PeopleCode editor, make the described selection, invoke the refactor, then compare against "Expected." The dialog lets you set the name, pick the Return value, and (in App Classes) the visibility. Unless a case says otherwise, accept the default name and default Return.

Byte-for-byte whitespace isn't the point — check the **structure**: correct parameters (value / `out`), correct return, correct call site, correct declaration placement, and that the result compiles.

---

## A. Function extraction (non-App-Class program — e.g. a Component/Record PeopleCode or a program with a `Function`)

### A1 — Inputs + void (no outputs)
```
Function DoWork()
   Local number &a = 1;
   Local number &b = 2;
   MessageBox(0, "", 0, 0, "sum=" | (&a + &b));
End-Function;
```
Select the `MessageBox(...)` line. **Expected:** a new `Function ExtractedFunction(&a As number, &b As number)` (no `Returns`) above `DoWork`, body = the MessageBox line; the selected line becomes `ExtractedFunction(&a, &b);`.

### A2 — Single Return, variable declared inside the selection
```
   Local number &a = 1;
   Local number &b = 2;
   Local number &c = &a + &b;
   MessageBox(0, "", 0, 0, "" | &c);
```
Select only `Local number &c = &a + &b;`. **Expected:** `Function ExtractedFunction(&a As number, &b As number) Returns number`, body keeps `Local number &c = &a + &b;` + `Return &c;`; call site becomes `Local number &c = ExtractedFunction(&a, &b);`.

### A3 — Single Return, variable declared before the selection
```
   Local number &a = 1;
   Local number &total;
   &total = &a * 10;
   MessageBox(0, "", 0, 0, "" | &total);
```
Select `&total = &a * 10;`. **Expected:** `Function ExtractedFunction(&a As number) Returns number`, body = `Local number &total;` + `&total = &a * 10;` + `Return &total;`; call site `&total = ExtractedFunction(&a);`. The original `Local number &total;` stays in the caller.

### A4 — Two outputs → one Return + one `out` param (both declared before)
```
   Local number &a = 1;
   Local number &sum;
   Local number &prod;
   &sum = &a + 10;
   &prod = &a * 10;
   MessageBox(0, "", 0, 0, "" | &sum | &prod);
```
Select the two assignment lines. **Expected in a FUNCTION (default Return = `&sum`):** because PeopleCode functions can't use `out` and pass by reference, the extra output `&prod` is renamed with an `Out` suffix: signature `Function ExtractedFunction(&a As number, &prodOut As number) Returns number`; the body assigns `&prodOut` (renamed) and `&sum`, `Return &sum;`; call site `&sum = ExtractedFunction(&a, &prod);` (the caller still passes the original `&prod`, by reference). Both keep their caller declarations.
**Then re-run and set Return to "(none — all via out params)":** both become renamed by-ref params — signature `(&a As number, &sumOut As number, &prodOut As number)`, void, call `ExtractedFunction(&a, &sum, &prod);`.
**In a METHOD (App Class), the same selection keeps `out` and the original names:** `method ExtractedMethod(&a As number, &prod As number out) returns number;`.

### A5 — Accumulator → in/out param  *(regression guard for the classification fix)*
```
   Local number &a = 5;
   Local number &total = 0;
   &total = &total + &a;
   MessageBox(0, "", 0, 0, "" | &total);
```
Select `&total = &total + &a;`. **Expected in a FUNCTION:** `&total` is passed **and** returned by reference — renamed `&totalOut`: signature `Function ExtractedFunction(&a As number, &totalOut As number)` (void), body `&totalOut = &totalOut + &a;`, call `ExtractedFunction(&a, &total);`. It must NOT drop `&total`'s incoming value. **In a METHOD:** `&total As number out` (name kept).

### A6b — Loop iterator declared before, not used after → localized (NOT a parameter)
```
Function DoWork()
   Local integer &x;
   Local number &foo = 5;
   For &x = 1 To &foo
      MessageBox(0, "", 0, 0, "" | &x);
   End-For;
End-Function;
```
Select the whole `For … End-For;`. **Expected:** signature `Function ExtractedFunction(&foo As number)` — `&x` is NOT a parameter. The routine body **declares `&x` itself**: `Local integer &x;` at the top, then the `For` loop. (Before this fix the body referenced `&x` undeclared.) Compiles.

### A6c — Reused iterator read after the selection → becomes the return value (not localized)
```
Function DoWork()
   Local integer &x;
   Local number &foo = 5;
   For &x = 1 To &foo
      MessageBox(0, "", 0, 0, "first " | &x);
   End-For;
   For &x = 1 To &foo
      MessageBox(0, "", 0, 0, "second " | &x);
   End-For;
End-Function;
```
Select the **first** `For … End-For;` only. Because the *second* loop reads `&x` after the selection, `&x`'s value escapes the selection → it is treated as an **output**. Since it's the only output it becomes the **return value** (contrast A6b, where nothing reads `&x` afterward so it is localized). **Expected:**
```
Function ExtractedFunction(&foo As number) Returns integer
   Local integer &x;
   For &x = 1 To &foo
      MessageBox(0, "", 0, 0, "first " | &x);
   End-For;
   Return &x;
End-Function;
```
and the selected loop becomes `&x = ExtractedFunction(&foo);`. It is deliberately NOT localized (its post-selection value is used by the caller).

### A6 — Object mutation is NOT an output  *(regression guard)*
```
   Local array of string &names = CreateArrayRept("", 0);
   &names.Push("alice");
   &names.Push("bob");
   MessageBox(0, "", 0, 0, "count=" | &names.Len);
```
Select the two `&names.Push(...)` lines. **Expected:** `&names` is a **value** parameter (`&names As array of string`), NOT an `out` param — mutating an object is a read, and the caller already sees the change. Void function.

---

## B. Method extraction (App Class)

### B1 — Basic method with a return
```
class Sample
   method Run();
end-class;

method Run
   Local number &a = 1;
   Local number &b = 2;
   Local number &c = &a + &b;
   MessageBox(0, "", 0, 0, "" | &c);
end-method;
```
Select `Local number &c = &a + &b;`. The dialog shows a **Visibility** dropdown (default Private) and preview `Private method ExtractedMethod(&a As number, &b As number) Returns number`. Accept.
**Expected:**
- A declaration in the class header (private section — created if absent): `   method ExtractedMethod(&a As number, &b As number) returns number;`
- An implementation after `Run`:
  ```
  method ExtractedMethod
     /+ &a as number +/
     /+ &b as number +/
     /+ Returns number +/
     Local number &c = &a + &b;
     Return &c;
  end-method;
  ```
- Call site in `Run`: `Local number &c = %This.ExtractedMethod(&a, &b);`

### B2 — Instance variables / %This are NOT parameters
```
class Sample
   method Run();
private
   instance number &counter;
end-class;

method Run
   Local number &a = 1;
   &counter = &counter + &a;
   MessageBox(0, "", 0, 0, "" | &counter);
end-method;
```
Select `&counter = &counter + &a;`. **Expected:** only `&a` is a parameter (`&a As number`). `&counter` (an instance variable) is NOT a parameter and NOT an `out` param — it's reachable inside the extracted method directly. The extracted method reads/writes `&counter` as-is.

### B3 — Visibility picker
Repeat B1 but set the dropdown to **Protected**. **Expected:** the declaration lands in the `protected` section (created if absent); preview reads `Protected method ...`.

---

## C. Guards (must refuse cleanly with a message — never generate broken code)

### C1 — Contains a Return
```
Function DoWork() Returns number
   Local number &a = 1;
   If &a > 0 Then
      Return &a;
   End-If;
   Return 0;
End-Function;
```
Select the `If … End-If;` block. **Expected:** refusal — "…contains a Return…".

### C2 — Partial statement selection
From A2, select only `&a + &b` (inside the statement). **Expected:** refusal — "Selection must cover whole statements…".

### C3 — Break/Continue crossing the selection boundary
```
Function DoWork()
   Local number &i;
   For &i = 1 To 10
      If &i = 5 Then
         Break;
      End-If;
      MessageBox(0, "", 0, 0, "" | &i);
   End-For;
End-Function;
```
Select the `If … End-If;` (the one containing `Break;`) plus the `MessageBox` line, but NOT the `For`. **Expected:** refusal about Break targeting a loop outside the selection. *(Selecting the whole `For … End-For;` instead should be allowed — the loop and its Break move together.)*

### C4 — Combined declaration refusal (shared decl **inside** the selection)
```
Function DoWork()
   Local number &a = 1;
   Local number &x, &y;
   &x = &a + 1;
   &y = &a + 2;
   MessageBox(0, "", 0, 0, "" | &x | &y);
End-Function;
```
Select the combined declaration and both assignments (`Local number &x, &y;` through `&y = &a + 2;`). Both `&x` and `&y` are read after the selection, so both are outputs; set the Return to `&x` (or "(none)") so `&y` must become an `out`/`Out` parameter. Because `&y` is declared **inside** the selection as part of a combined `Local number &x, &y;`, it can't be cleanly relocated. **Expected:** refusal — *"…output variable '&y' shares a combined Local declaration. Split it first."* (No mangled output.)

**Contrast — a combined decl *before* the selection is fine (no refusal).** If `Local number &x, &y;` is above the selection and only the assignments are selected, a variable that isn't read afterward is simply re-declared as a fresh `Local` inside the extracted routine (it's localized, not returned) — valid code, no refusal. Example: with `Local number &x, &y;` then selecting `&x = 1;` / `&y = &x + 1;` where only `&x` is read later, extracting with `&x` as the Return yields a valid function that declares `Local number &y;` and `Local number &x;` and returns `&x`.

---

## D. Edge cases

### D1 — Single-statement selection
From A1, select just the one `MessageBox` line → void function, call replaces the line. (Already A1; confirm a one-statement selection works.)

### D2 — No inputs and no outputs
```
Function DoWork()
   MessageBox(0, "", 0, 0, "hello");
   MessageBox(0, "", 0, 0, "world");
End-Function;
```
Select both lines → `Function ExtractedFunction()` (empty params, void), call `ExtractedFunction();`.

### D3 — Bare (no-initializer) out-param declared inside  *(regression guard for the final-review fix)*
```
   Local number &a = 3;
   Local number &sum;
   Local number &result;
   &sum = &a + 1;
   &result = &a * 2;
   MessageBox(0, "", 0, 0, "" | &sum | &result);
```
Select the two assignment lines. In the dialog, set Return to **"(none)"** (so both become out params). **Expected:** the body must NOT contain a bare `&sum;` or `&result;` statement; the caller declares `Local number &sum;` and `Local number &result;` before the call; signature has `&sum As number out, &result As number out`. Confirm the generated function compiles.

---

## E. Things to eyeball (known cosmetic items, not blockers)

- **Blank line between methods (App Class):** the new method implementation is inserted right after the previous `end-method;` — check spacing looks acceptable on a **CRLF** file (there's a known risk of tight spacing / a stray line-ending between the two methods on CRLF documents; verify it's not visibly broken).
- **Preview vs. generated order:** with 2+ `out` params, glance that the dialog's signature preview lists params in the same order as the finally-generated signature.
- **Indentation:** extracted body is re-indented to one level (3 spaces) under the routine header; check nested blocks inside the selection still look right.
- **Empty visibility section creation:** when the target visibility section didn't exist, confirm the created `private`/`protected` header reads cleanly in the class.

---

## Regression sanity (unchanged sibling refactor)
Run **Extract Local Variable** once on any expression to confirm this branch didn't disturb it (both share `BaseRefactor` and the borderless-dialog pattern).
