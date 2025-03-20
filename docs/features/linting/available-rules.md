# Available Lint Rules

AppRefiner includes a comprehensive set of lint rules designed specifically for PeopleCode development. This document provides a detailed reference of all available lint rules.

## Code Structure Rules

### Empty Catch Block (EMPTY_CATCH-1)

**Severity**: Warning

Detects empty catch blocks that silently swallow exceptions.

```peoplecode
/* Incorrect */
try
   &result = DoSomething();
catch Exception &e
   /* Empty catch block silently swallows the exception */
end-try;

/* Correct */
try
   &result = DoSomething();
catch Exception &e
   WriteToLog(0, "Error: " | &e.ToString());
   throw;
end-try;
```

### Flowerbox Header (FLOWERBOX-1)

**Severity**: Warning

Validates that files start with a flowerbox header comment.

```peoplecode
/* Incorrect */
Local string &name = "John";
Local number &age = 30;

/* Correct */
/* ===== My PeopleCode Program ===== */
Local string &name = "John";
Local number &age = 30;
```

### Class Method Parameter Count (FUNC_PARAM_COUNT-1)

**Severity**: Warning

Detects methods with too many parameters (more than 5).

```peoplecode
/* Incorrect */
class MyClass
   method MyMethod(&param1, &param2, &param3, &param4, &param5, &param6);
end-class;

/* Correct */
class MyClass
   method MyMethod(&param1, &param2, &param3, &param4, &param5);
end-class;
```

### Function Parameter Count (FUNC_PARAM_COUNT-2)

**Severity**: Warning

Detects functions with too many parameters (more than 5).

```peoplecode
/* Incorrect */
Function ProcessData(&id, &name, &email, &phone, &address, &city)
   /* Function implementation */
end-function;

/* Correct */
Function ProcessData(&id, &name, &email, &phone, &address)
   /* Function implementation */
end-function;

/* Even Better - Using a compound parameter object */
Function ProcessData(&customerData)
   /* Function implementation using &customerData.id, &customerData.name, etc. */
end-function;
```

### Long Expression (LONG_EXPR-1)

**Severity**: Warning

Detects expressions that are too long (more than 200 characters).

```peoplecode
/* Incorrect */
&result = &value1 + &value2 + &value3 + &value4 + &value5 + &value6 + &value7 + &value8 + &value9 + &value10 + &value11 + &value12 + &value13 + &value14 + &value15 + &value16 + &value17 + &value18 + &value19 + &value20;

/* Correct */
&subtotal1 = &value1 + &value2 + &value3 + &value4 + &value5;
&subtotal2 = &value6 + &value7 + &value8 + &value9 + &value10;
&subtotal3 = &value11 + &value12 + &value13 + &value14 + &value15;
&subtotal4 = &value16 + &value17 + &value18 + &value19 + &value20;
&result = &subtotal1 + &subtotal2 + &subtotal3 + &subtotal4;
```

### Long Expression (LONG_EXPR-2)

**Severity**: Warning

Detects expressions that are too complex (more than 5 operators).

```peoplecode
/* Incorrect */
&result = (&value1 * &value2) + (&value3 / &value4) - (&value5 * &value6) + (&value7 - &value8);

/* Correct */
&part1 = &value1 * &value2;
&part2 = &value3 / &value4;
&part3 = &value5 * &value6;
&part4 = &value7 - &value8;
&result = &part1 + &part2 - &part3 + &part4;
```

### Nested If Statements (NESTED_IF-1)

**Severity**: Warning

Identifies deeply nested If/Else blocks (more than 3 levels deep).

```peoplecode
/* Incorrect */
If &condition1 Then
   If &condition2 Then
      If &condition3 Then
         If &condition4 Then
            /* Too deeply nested */
         End-If;
      End-If;
   End-If;
End-If;

/* Correct */
If Not &condition1 Then
   Return;
End-If;

If Not &condition2 Then
   Return;
End-If;

If Not &condition3 Then
   Return;
End-If;

If &condition4 Then
   /* Process when all conditions are met */
End-If;
```

### If-Else-If Chains (NESTED_IF-2)

**Severity**: Information

Detects multiple IF-ELSE-IF chains that could be replaced with Evaluate.

```peoplecode
/* Incorrect */
If &status = "New" Then
   &result = "Process new request";
Else
   If &status = "Pending" Then
      &result = "Process pending request";
   Else
      If &status = "Approved" Then
         &result = "Process approved request";
      Else
         &result = "Unknown status";
      End-If;
   End-If;
End-If;

/* Correct */
Evaluate &status
When = "New"
   &result = "Process new request";
When = "Pending"
   &result = "Process pending request";
When = "Approved"
   &result = "Process approved request";
When-Other
   &result = "Unknown status";
End-Evaluate;
```

### Recursive Function (RECURSIVE_FUNC-1)

**Severity**: Warning

Detects potentially unsafe recursive functions without proper termination conditions.

```peoplecode
/* Incorrect */
Function ProcessNode(&node)
   /* No termination condition */
   ProcessNode(&node.GetChildNode());
End-Function;

/* Correct */
Function ProcessNode(&node)
   If None(&node) Then
      Return;
   End-If;
   
   /* Process the node */
   ProcessNode(&node.GetChildNode());
End-Function;
```

### Multiline REM Comment (MULTILINE_REM-1)

**Severity**: Warning

Detects REM comments that span multiple lines, which may indicate missing semicolon termination.

```peoplecode
/* Incorrect */
rem This is a comment that spans
Local integer &i = 3;

/* Correct */
rem This is a comment that is properly terminated;
Local integer &i = 3;
```

## Variable and Type Rules

### Object Type Usage (OBJECT_TYPE-1)

**Severity**: Warning

Checks for variables declared as 'object' that are assigned specific types.

```peoplecode
/* Incorrect */
Local object &myGrid = create MY:TestClass();

/* Correct */
Local MY:TestClass &myGrid = create MY:TestClass();
```

## SQL Rules
**Note**: Database connection is required to lint SQL methods using SQL objects instead of strings. 
### SQL Wildcard (SQL_WILDCARD-1)

**Severity**: Warning

Reports any SQL using * wildcards.

```peoplecode
/* Incorrect */
&sql = CreateSQL("SELECT * FROM PS_JOB");

/* Correct */
&sql = CreateSQL("SELECT EMPLID, EMPL_RCD, EFFDT, EFFSEQ, JOBCODE FROM PS_JOB");
```

### SQL Long String (SQL_LONG-1)

**Severity**: Warning

Reports SQL strings longer than 120 characters.

```peoplecode
/* Incorrect */
&sql = CreateSQL("SELECT EMPLID, NAME, EMAIL, PHONE, ADDRESS1, ADDRESS2, CITY, STATE, POSTAL, COUNTRY FROM PS_PERSONAL_DATA WHERE EMPLID = :1 AND EFFDT = (SELECT MAX(EFFDT) FROM PS_PERSONAL_DATA WHERE EMPLID = :2)", &emplid, &emplid);

/* Correct */
/* Use a SQL definition in App Designer instead */
&sql = CreateSQL(SQL.PERSONAL_DATA_CURRENT);
```

### SQLExec With String Concatenation (SQL_EXEC-2)

**Severity**: Error

Detects SQL using string concatenation.

```peoplecode
/* Incorrect */
&whereClause = " WHERE EMPLID = '" | &emplid | "'";
SQLExec("SELECT NAME FROM PS_PERSONAL_DATA" | &whereClause, &name);

/* Correct */
SQLExec("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1", &emplid, &name);
```

### SQLExec Variable Count (SQL_EXEC-3)

**Severity**: Error

Validates that SQLExec calls have the correct number of input and output parameters.

```peoplecode
/* Incorrect - Missing output parameter */
SQLExec("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1", &emplid);

/* Incorrect - Too many parameters */
SQLExec("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1", &emplid, &name, &email);

/* Correct */
SQLExec("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1", &emplid, &name);
```

### Invalid SQL Defintion (CREATE_SQL-1)

**Severity**: Error

Reports invalid SQL definitions.

```peoplecode
/* Incorrect */
&sql = CreateSQL(SQL.INVALID_DEFINITION);

/* Correct */
&sql = CreateSQL(SQL.VALID_DEFINITION);
```

### CreateSQL Variable Count (CREATE_SQL-3)

**Severity**: Information

Cannot validate SQL using certain MetaSQL constructs.

```peoplecode
/* Cannot validate due to MetaSQL */
&sql = CreateSQL("%SelectAll(:1) WHERE EMPLID = :2", Record.JOB, &emplid);
```

### CreateSQL Variable Count (CREATE_SQL-4)

**Severity**: Error

Validates that CreateSQL calls have the correct number of input parameters.

```peoplecode
/* Incorrect - Too few parameters */
&sql = CreateSQL("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1 AND DEPTID = :2", &emplid);

/* Incorrect - Too many parameters */
&sql = CreateSQL("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1", &emplid, &deptid);

/* Correct */
&sql = CreateSQL("SELECT NAME FROM PS_PERSONAL_DATA WHERE EMPLID = :1 AND DEPTID = :2", &emplid, &deptid);
```

## HTML Rules
**Note**: Database connection is required to lint HTML objects. 

### Invalid HTML Defintion (HTML_VAR_COUNT-1)
**Severity**: Error

Reports invalid HTML definitions.

```peoplecode
/* Incorrect */
&html = GetHTMLText(HTML.INVALID_DEFINITION);

/* Correct */
&html = GetHTMLText(HTML.VALID_DEFINITION);
```

### HTML Variable Count  (HTML_VAR_COUNT-2)
**Severity**: Error

Validates that GetHTMLText calls have enough bind parameters.

```peoplecode
/* Incorrect - Missing parameters */
&html = GetHTMLText(HTML.MY_TEMPLATE, &param1); /* Template requires 2 parameters */

/* Correct */
&html = GetHTMLText(HTML.MY_TEMPLATE, &param1, &param2);
```

### HTML Variable Count (HTML_VAR_COUNT-3)
**Severity**: Warning

Validates that GetHTMLText calls don't have too many bind parameters.

```peoplecode
/* Incorrect - Too many parameters */
&html = GetHTMLText(HTML.MY_TEMPLATE, &param1, &param2, &param3); /* Template only requires 2 parameters */

/* Correct */
&html = GetHTMLText(HTML.MY_TEMPLATE, &param1, &param2);
```
