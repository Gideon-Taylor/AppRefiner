# SQL Formatting

App Refiner provides improved SQL formatting for better readability while editing SQL definitions in Application Designer.

## Overview

Application Designer enforces its own SQL formatting when saving SQL definitions, which is optimized for storage rather than readability. This can make SQL difficult to understand and edit while working with it.

App Refiner detects when a SQL definition is open and automatically reformats it to a more readable format for editing purposes. This reformatting is temporary and only affects the display during editing - it does not change the underlying format that Application Designer enforces when saving.

## How It Works

When you open a SQL definition in Application Designer:

1. App Refiner detects that a SQL definition is being edited
2. It automatically reformats the SQL to a more readable structure with proper indentation and alignment
3. You can edit the SQL in this readable format
4. When you save, Application Designer will apply its own formatting to the SQL for storage

## Benefits

- **Improved readability**: SQL is displayed with proper indentation, line breaks, and alignment
- **Easier editing**: Working with well-formatted SQL reduces errors and improves productivity
- **No conflicts**: The reformatting doesn't interfere with Application Designer's required format for storage
- **Seamless experience**: The reformatting happens automatically without requiring manual intervention

## Example

### Application Designer's Default Format (at save time)

Here is a made up example of complex SQL statement to demonstrate the benefits of App Refiner's SQL formatting:

```sql
SELECT  tc.CUSTOMER_ID
 ,  tc.CUSTOMER_NAME
 ,  tc.TOTAL_SALES
 ,  tc.NUM_ORDERS
 ,  o.ORDER_ID
 ,  o.ORDER_DATE
 ,  o.ORDER_AMOUNT
 ,  (  
 SELECT  COUNT(*)  
  FROM  (  
 SELECT  r.ORDER_ID  
  FROM  PS_RETURNS r  JOIN PS_RETURN_DETAILS rd ON r.RETURN_ID = rd.RETURN_ID  JOIN PS_ITEMS i ON rd.ITEM_ID = i.ITEM_ID  
 WHERE  r.ORDER_ID = o.ORDER_ID  ) NESTED_RETURNS  ) AS RETURN_COUNT,  (  
 SELECT  SUM(inv.INVOICE_AMOUNT)  
  FROM  PS_INVOICES inv  JOIN (  
 SELECT  oi_inner.INVOICE_ID  
  FROM  PS_ORDER_INVOICES oi_inner  JOIN PS_ORDERS o_inner ON oi_inner.ORDER_ID = o_inner.ORDER_ID  
 WHERE  o_inner.CUSTOMER_ID = tc.CUSTOMER_ID  ) ORDER_INV ON inv.INVOICE_ID = ORDER_INV.INVOICE_ID  ) AS TOTAL_INVOICES 
  FROM  PS_TOP_CUSTOMERS tc  JOIN PS_ORDERS o ON tc.CUSTOMER_ID = o.CUSTOMER_ID 
 WHERE  tc.RANK <= 10
```

### App Refiner's Improved Format (for editing)

```sql
SELECT
  tc.CUSTOMER_ID,
  tc.CUSTOMER_NAME,
  tc.TOTAL_SALES,
  tc.NUM_ORDERS,
  o.ORDER_ID,
  o.ORDER_DATE,
  o.ORDER_AMOUNT,
  (
    SELECT
      COUNT(*)
    FROM
      (
        SELECT
          r.ORDER_ID
        FROM
          PS_RETURNS r
          JOIN PS_RETURN_DETAILS rd ON r.RETURN_ID = rd.RETURN_ID
          JOIN PS_ITEMS i ON rd.ITEM_ID = i.ITEM_ID
        WHERE
          r.ORDER_ID = o.ORDER_ID
      ) NESTED_RETURNS
  ) AS RETURN_COUNT,
  (
    SELECT
      SUM(inv.INVOICE_AMOUNT)
    FROM
      PS_INVOICES inv
      JOIN (
        SELECT
          oi_inner.INVOICE_ID
        FROM
          PS_ORDER_INVOICES oi_inner
          JOIN PS_ORDERS o_inner ON oi_inner.ORDER_ID = o_inner.ORDER_ID
        WHERE
          o_inner.CUSTOMER_ID = tc.CUSTOMER_ID
      ) ORDER_INV ON inv.INVOICE_ID = ORDER_INV.INVOICE_ID
  ) AS TOTAL_INVOICES
FROM
  PS_TOP_CUSTOMERS tc
  JOIN PS_ORDERS o ON tc.CUSTOMER_ID = o.CUSTOMER_ID
WHERE
  tc.RANK <= 10
```