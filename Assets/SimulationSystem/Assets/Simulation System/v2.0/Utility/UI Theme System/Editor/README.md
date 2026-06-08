# UI Theme System — Unity 6
## Editor-only. Zero runtime overhead.

---

## Folder structure

```
Assets/
└── UIThemeSystem/
    ├── Runtime/
    │   ├── ColorRole.cs          ← enum (shared data)
    │   ├── UIThemeAsset.cs       ← ScriptableObject (pure data)
    │   ├── ThemedImage.cs        ← component for UI Images
    │   └── ThemedText.cs         ← component for TMP / legacy Text
    └── Editor/
        ├── UIThemeAssetEditor.cs ← custom inspector + context menus on the asset
        ├── ThemedImageEditor.cs  ← custom inspector for ThemedImage
        └── ThemedTextEditor.cs   ← custom inspector for ThemedText
```

> **Important:** The `Editor/` folder name is special in Unity.
> Scripts inside it are stripped from builds automatically — no editor code ships.

---

## Quick start

### 1. Create a Theme Asset
Right-click in the Project window:
`Create → UI Theme System → Theme Asset`

Name it (e.g. `DarkTheme`, `LightTheme`). Set your colors in the Inspector.

### 2. Tag your UI objects

On any **Image** GameObject → Add Component → `ThemedImage`
- Assign the theme asset
- Pick a `ColorRole`

On any **Text / TMP** GameObject → Add Component → `ThemedText`
- Assign the theme asset
- Pick a `ColorRole`

The color is applied immediately when you set the fields (via `OnValidate`).

### 3. Apply to the whole scene at once

**Option A — Inspector button:**
Select your theme asset → click **"Apply to Entire Scene"** in the Inspector.

**Option B — Context menu on the asset:**
Right-click the asset in the Project window → `UI Theme System → Apply to Entire Scene`

**Option C — Context menu on the asset Inspector:**
Click the three-dot menu (⋮) on the Inspector header → `Apply to Entire Scene`

### 4. Preview a selection only
Select some GameObjects in the Hierarchy → select the asset →
click **"Preview Selection"** (or use the context menu).

---

## How the editor-only design works

| Concern | Where it lives | Runtime? |
|---|---|---|
| Color data | `UIThemeAsset` fields | ✅ (it's just a SO asset) |
| Role lookup | `UIThemeAsset.Resolve()` | ✅ (pure method, no side-effects) |
| Writing color to Image/Text | `ApplyTheme()` in each component | Called from editor only |
| Undo / SetDirty | Inside `#if UNITY_EDITOR` blocks | ✗ stripped from build |
| Inspector buttons | `Editor/` scripts | ✗ stripped from build |
| Auto-apply on field change | `OnValidate()` in components | ✗ editor-only callback |

At runtime, `Image.color` and `TMP_Text.color` simply hold whatever value was
last written in the editor — the same as if you had set it by hand. The
`ThemedImage` and `ThemedText` MonoBehaviours are present on the GameObject
but contain no `Start/Update/Awake` hooks, so they have **zero runtime cost**
beyond their serialized field memory (two object references + one int).

If you want, you can strip the components from builds entirely by wrapping them
with an Assembly Definition that excludes non-editor platforms. But even without
that, they cost effectively nothing.

---

## Swapping themes

1. Create a second theme asset (e.g. `LightTheme`).
2. Select it and click **"Apply to Entire Scene"**.
   All components whose `theme` field pointed at the old asset *or* was blank
   will be updated and their colors baked in.
3. Save the scene. Done.

---

## Adding more color roles

1. Add a new entry to `ColorRole.cs` (e.g. `Disabled`).
2. Add a matching field to `UIThemeAsset.cs` (e.g. `public Color disabled`).
3. Add a case to `UIThemeAsset.Resolve()`.
4. The new role appears in all `ThemedImage` / `ThemedText` dropdowns automatically.
