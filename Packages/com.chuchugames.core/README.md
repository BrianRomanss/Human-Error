# Chuchu Games – Core

Engine-free game plumbing.

- `CommandStack` + `ICommand` / `DelegateCommand`: undo and redo (Command pattern). Optional capacity, and a `Changed` event for refreshing buttons. Running a new command clears the redo branch.

- `StateMachine<TEnum>`: the current state, `OnEnter`/`OnExit` callbacks, a `Changed(from, to)` event, and optional `Allow(from, to...)` transition rules.
- `EventBus`: typed publish/subscribe. `Subscribe<T>` returns an `IDisposable` token.
- `SeededRandom`: deterministic xorshift128+ RNG (same sequence everywhere), `SeedFrom(text)` for daily seeds, `Fork(salt)` for independent sub-streams, `Range`, `Chance`, `Pick`, `PickWeighted`, `Shuffle`.
- `SaveSlots<T>`: numbered save files. Writes are atomic (temp file, then replace, keeping a `.bak`), and a corrupt save falls back to the backup. You inject the serializer, so it stays engine-free. In Unity:
  `new SaveSlots<MySave>(Path.Combine(Application.persistentDataPath, "saves"), s => JsonUtility.ToJson(s), JsonUtility.FromJson<MySave>)`
