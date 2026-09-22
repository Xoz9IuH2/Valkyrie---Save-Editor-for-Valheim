# Valheim Character Editor

Windows/.NET 8 editor for profile 46, player 33, inventory 109 and skills 2.
Launch `ValheimEditor/Editor.exe` from the workspace root.

Standalone single-file Windows x64 release:

```powershell
dotnet publish Editor/Editor.csproj -p:PublishProfile=Standalone -o artifacts/standalone
```

Distribute `artifacts/standalone/Editor.exe` alone. The .NET runtime is
included; catalog and icons are embedded resources. Native runtime libraries
may extract to the user's temporary directory. No downloads, administrator
privileges or separate runtime installation are required at launch.

- Russian/English interfaces, including item names, with a dark/light theme toggle.
- Existing skills: synchronized slider and precise numeric input, levels 0-100.
- Inventory: 8-wide grid that follows the save's `invrows` value (vanilla 4, expanded up to 8). Searchable Russian names and prefab IDs.
- Extracted item icons in the grid and search list. Drag an item onto an
  empty slot to move it, or an occupied slot to swap, preserving metadata.
- Category filters: weapons, armor, resources, food, other. Internal variants
  are hidden by default, with an optional toggle. This flag uses icon presence
  and technical-name heuristics; it is not an authoritative game classification.
- Ctrl+C copies a selected inventory slot; Ctrl+V pastes it. Click selects a
  slot, double-click opens the item picker.
- Restore backup opens a file picker for the currently opened local character.
  It validates format/checksum and character ID, confirms replacement, and
  backs up the current on-disk save before restoring. Close the game first.
- Add, replace, edit or clear a slot. Stack limits come from the catalog.
  Quality has three modes: catalog maximum, up to 99, and up to 999.
- Item picker shows a left-hand stats panel: description, weight, food
  health/stamina, and leveled damage/armor as `base (+bonus) = total`.
- Replacing a different item requires confirmation and resets its metadata.
  New items have full durability and the game's cheated-item flag cleared.
  Editing an existing item of the same type preserves its other metadata.
- Unknown items are retained without guessing their limits. They may be
  explicitly replaced or deleted.
- Clear item cheat flags clears the cheated bit on all inventory items,
  including unknown items, without changing other bits or metadata. Supports
  Ctrl+Z and requires saving to apply on disk. The separate profile-level
  cheat history and achievement eligibility are not changed.
- World tab scans local `.chunk` world files, lists cheated containers, and
  can clear those flags in place with backups. Old single-file `.db` worlds
  and cheated items in `_main.*.db2` are not cleaned.
- Some catalog entries are internal item variants. Check the prefab ID.
- New skill creation and map editing are not implemented.

Close Valheim before saving. Save to game writes directly to the local
`characters_local` folder using the opened filename, after confirmation.
Existing files are replaced atomically with the previous on-disk version
retained in `characters_local/Backups`. If the opened file has changed on
disk, saving is rejected. Files opened elsewhere cannot overwrite an
existing local character; open that local character directly first.
The editor never writes to Steam Cloud. In-game loading still needs testing.

Unedited saves round-trip byte-for-byte. Tests cover each editable field,
item insertion/replacement/deletion, repeated edits, item limits, corrupt
checksums and basic UI construction. In-game loading has not been verified.

```powershell
dotnet build Editor/Editor.csproj -c Release
dotnet Editor/bin/Release/net8.0-windows/Editor.dll --test "C:\path\character.fch"
dotnet publish Editor/Editor.csproj -c Release -o ValheimEditor
```

The extracted catalog is tied to this installed build. To regenerate it
after inspecting compatibility with a game update (requires UnityPy):

```powershell
python Editor/extract_catalog.py "D:\SteamLibrary\steamapps\common\Valheim" Editor/items.json
```

The extractor reads the current build's `c4210710` bundle and Russian
localization from `resources.assets`; bundle layout can change in updates.
Tests write `test-results.txt` next to the executable, not to the input save.
