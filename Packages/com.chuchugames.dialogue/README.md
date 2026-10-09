# Chuchu Games – Dialogue

Visual-novel style dialogue in two parts. Depends on `com.chuchugames.ui`.

**Runtime (engine-free):**
- **`DialogueScript.Parse(text, defaultSpeaker)`**: a plain-text format that's easy to write by hand or by an AI.
  ```
  Bartholomew[happy]: Welcome!          ← speaker + optional [expression]
  You look tired.                       ← no name = same speaker
  Captain Morrow: Ahoy.
  > Welcome aboard | Go away            ← replies for the line above
  # comment
  ```
  A colon inside a sentence is not read as a speaker tag.
- **`DialogueRunner`**: `Start`, `Advance` (blocked while a choice is pending), `Choose`, `Skip`, a `Log`, and `Choices` (the history of picks).

**UI (`ChuchuGames.Dialogue.UI`):**
- **`DialogueView.Play(parent, lines, style, onFinished)`**: a uGUI overlay with a portrait, name plate, typewriter text and voice blips every N characters. Tap once to complete the line and again to advance; it also has 2-choice replies, Log, Auto and Skip. `DialogueStyle` supplies colours, fonts, `Portrait(speaker, expression)` and `Voice(speaker)`.
