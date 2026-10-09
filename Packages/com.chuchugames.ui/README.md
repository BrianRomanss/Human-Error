# Chuchu Games – UI

uGUI helpers.

- **Drag & drop**: put a `DragDropController` on a Canvas, `Draggable` on items and `DropTarget` on slots, each with any `Payload` you like. Works with mouse and touch through the EventSystem, and supports tap-to-select then tap-to-place. Events: `DragStarted`, `HoverChanged`, `Dropped(item, target-or-null)` and `SelectionChanged`. The controller only reports intent: the dragged visual returns home before `Dropped` fires, and your view re-lays itself out from your model.
- **UiBuild**: build uGUI from code (`Canvas`, `Panel`, `Label`, `Button`, plus the `Fill`, `PlaceTopLeft`, `Centre` and `Band` layout helpers). Good for grey-boxing and for editor builder scripts.

- **Juice:**
  - `Tween`: scale, move, fade, punch (squash and stretch), delay and generic `Run`, with eases. Uses unscaled time, and there is only one tween per target and channel.
  - `Bob`: idle float. `Pulse`: breathing scale. `Shake`: screen shake.
  - **Accessibility:** `Tween.ReduceMotion` turns off motion and shake (fades still play).
- **`AudioPlayer`**: pooled SFX with pitch and random variation, plus one looping music track. Master, SFX and music volumes.
- **`Spotlight.Show(root, target, tip, onDone, handSprite)`**: a tutorial spotlight. It dims everything except the target, outlines it, points a bobbing hand at it and shows a tip, placing the tip below, above or beside the target so it stays on screen.
- **`UiBuild.Slider`**, **`UiBuild.Picture`** (an aspect-preserving sprite), and optional `PanelSprite`/`ButtonSprite` (9-slice rounded corners) and `ClickSound`.
- **`HoldButton.Attach(button, fill, seconds, onConfirmed)`**: hold-to-confirm with a growing fill bar; releasing or sliding off cancels.
- **`LaunchScreenshot`**: launch a player with `--screenshot out.png [--screenshot-frames N]` to save a screenshot and quit. Good for CI and for reviewing UI without a person.

Planned: a generic screen router.
