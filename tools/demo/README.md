# Scripted demo

`demo.py` drives a live App Designer session for screen recordings. It types PeopleCode into an
empty editor with a human rhythm, picks from AppRefiner's suggestion lists, applies quick fixes and
a rename, and draws callout bubbles over the editor explaining what is happening. Scenes fade to
white between each other.

Nothing is ever saved: the script only types into an empty editor and clears it between scenes.

## Requirements

- Windows, Python 3.8+, and `pip install -r requirements.txt` (Pillow).
- App Designer with AppRefiner attached, connected to a database. The records, fields, component
  interface and definition-name scenes read delivered metadata (`PSOPRDEFN`, `PSROLEDEFN`,
  `PSPRCSRQST`, `USER_PROFILE`).

## Recording a take

1. Open an empty PeopleCode editor in App Designer.
2. Start the screen recording.
3. Run `python demo.py`, then click into the empty editor. The demo starts 2 seconds later.

Hold **F12** to stop. The script also stops if App Designer stops being the foreground window, so
it can't type into another program.

| Option | Effect |
| --- | --- |
| `--scenes 2,3` | Run only these scenes (numbers below). |
| `--speed 0.8` | Pause multiplier; below 1 is faster. |
| `--no-intro` | Skip the opening title bubble. |
| `--no-overlay` | Clean take with no bubbles. |
| `--seed N` | Typing-rhythm seed, so repeated takes match. |
| `--out DIR` | Where the cue sheet goes (default `out/`). |

Every run writes `out/cues.csv` and a PNG per bubble, with times relative to the start. Use them to
add the bubbles in a video editor instead, for example over a `--no-overlay` take.

## Scenes

1. Type checking
2. Suggestions
3. Quick fixes and rename
4. Records and fields
5. ApiObject refinement
6. Component interfaces
7. Definition name suggestions
8. Java objects and Ctrl+Space

## Adding a scene

A scene is a function taking the `Demo` object, registered in `SCENES`. The building blocks:

- `d.type(text)`: types text. `{rest}` waits for a suggestion list and accepts it with Tab.
- `d.pick(prefix, accept=VK_TAB)`: types a prefix into an open list, then accepts.
- `d.ctrl_space()`, `d.escape()`, `d.hover(needle)`, `d.quick_fix(needle)`, `d.rename(needle, new)`.
- `d.say(text, at=needle, title=...)` shows a bubble pointing at a piece of code; `d.unsay()`
  removes it. `every=True` outlines every occurrence; `center=True` centers it on the editor.

Anchors are matched against the editor text, which uses CRLF line endings, so they must never
contain a newline. Use only delivered metadata so the demo runs on any database.
