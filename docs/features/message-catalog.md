# Message Catalog

AppRefiner brings the PeopleSoft Message Catalog into Application Designer.
It is read-only — creating catalog entries still happens in the browser — but
every lookup, search, and "which number is free?" question is answered in place.

## Hover tooltips

With a database connection, hover any call to `MsgGet`, `MsgGetText`,
`MsgGetExplainText`, `CreateException`, `MessageBox`, or `MsgBoxButtonOverride`
whose message set and number are literal values. The tooltip shows the set
description, severity, message text, and explain text. A reference with no
catalog entry shows "No catalog entry for set/num" — a quick typo check.

## The browser dialog

Open with **Browse Message Catalog** from the command palette (Ctrl+Shift+P)
or its keyboard shortcut. Message sets on the left (filter by number or
description), messages on the right with severity and text, and a preview pane
with the full explain text. Focus starts in the set filter, and Tab moves
straight to message search — type your set, Tab, type your search.

Message search is scoped to whatever the set filter has narrowed the list to:
an empty filter searches every set, a filter that matches your set searches
just that set (when it narrows to exactly one, that set loads and filters live
as you type). Press Enter to run the search across the listed sets — matching
message text plus explain text, capped at 200 results.

**Insert** puts a complete call at your cursor — pick the function from the
dropdown (your choice is remembered) — with the catalog text as the default
message string. **Copy set, num** copies just the numbers.

## Inserting into an existing call

Press **Ctrl+Space** inside the message set or number argument of any of the
six functions. The catalog dialog opens in insert mode: if you already typed
the set number, it is pre-selected and locked (click "Unlock set" if that was
the mistake). Picking a message inserts only what the call still needs —
numbers plus default text.

## Finding a free number

Expand **New message** under the set list. Free ranges are shown for
orientation ("48–99 (52 free) · 205+ (open)") — click one to pre-fill its
start, or type any number you like; sometimes you want to leave a buffer in a
gap. Validation is live: a taken number shows the colliding message. Add your
intended text and **Insert code** writes the call with your chosen number —
code only, ready to paste into the catalog page when you create the real
entry online.

Data is cached per connection; the **Refresh** button re-reads the catalog
after someone adds entries.
