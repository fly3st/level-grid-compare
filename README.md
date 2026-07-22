# Level & Grid Compare — Revit Add-in (V1)

Compares **levels and grids** between the host model (architecture) and the
first linked model (structure). Reports MATCHED / MOVED / HOST ONLY / LINK ONLY
in a dialog and writes a full CSV to your Desktop (Hebrew-safe for Excel).

This is V1 of a larger arch-vs-structure comparison tool.

---

## 1. Prerequisites (all free)

- **Visual Studio 2022 Community** — during install, check the workload
  **".NET desktop development"**.
- **Revit 2027**. If your install is a different year,
  see "Other Revit versions" below.

## 2. Project setup

1. Create a folder, e.g. `C:\RevitDev\LevelGridCompare\`
2. Put `LevelGridCompare.csproj` and `Command.cs` in it.
3. Open the `.csproj` in Visual Studio (double-click it).
4. Check that the two `HintPath` lines in the `.csproj` point at your real
   Revit install folder (default: `C:\Program Files\Autodesk\Revit 2027\`).
5. Build: **Ctrl+Shift+B**. You should get
   `C:\RevitDev\LevelGridCompare\bin\Debug\LevelGridCompare.dll`

## 3. Register the add-in

1. Open `LevelGridCompare.addin` in Notepad and make sure the `<Assembly>`
   path matches where your DLL actually is.
2. Copy the `.addin` file to:

   ```
   %AppData%\Autodesk\Revit\Addins\2027\
   ```

   (Paste that into the Explorer address bar. Create the `2027` folder if
   it doesn't exist.)
3. Start Revit. Approve the "load add-in" security prompt (Always Load).
4. The command appears under **Add-Ins tab → External Tools →
   Level & Grid Compare**.

## 4. Build a test scene (10 minutes)

You need two models with deliberate mismatches:

1. New project (Architectural template) → add 4–5 levels with names, and
   6–8 grids (A, B, C… / 1, 2, 3…). Save as `TEST-ARCH.rvt`.
2. **Save As** → `TEST-ST.rvt`, and in it deliberately break things:
   - move one level up 30 mm
   - move one grid 50 mm
   - delete one grid
   - rename one level
3. Reopen `TEST-ARCH.rvt` → **Insert → Link Revit** → link `TEST-ST.rvt`
   (positioning: Auto – Origin to Origin).
4. Run the command. The dialog should catch every mismatch you planted,
   and `LevelGridCompare.csv` appears on your Desktop.

That moment — the tool catching the mistakes you planted — screenshot it.
That's portfolio material.

## 5. Other Revit versions

- **Revit 2027+:** works as-is (`net10.0-windows`).
- **Revit 2025/2026:** change `<TargetFramework>` to `net8.0-windows`, fix
  the year in the HintPaths and the Addins folder.
- **Revit 2021–2024:** change `<TargetFramework>` to `net48`, fix HintPaths
  and Addins folder year.

## 6. Troubleshooting

- **Command doesn't appear:** `.addin` not in the right folder, or the
  `<Assembly>` path inside it is wrong.
- **"Could not load file or assembly":** the year mismatch problem —
  TargetFramework must match your Revit generation (see §5).
- **Build errors about RevitAPI:** HintPath doesn't point at a real
  RevitAPI.dll.
- **DLL locked when rebuilding:** close Revit first (V2 of your dev setup
  can add hot-reload; not worth it today).

## 7. Roadmap (the bigger tool this feeds)

- **V2:** compare structural columns vs architectural columns
  (location within tolerance, missing, moved) — same CompareByName pattern.
- **V3:** walls and floors comparison.
- **V4:** auto-place your firm's mismatch annotations on comparison views.

