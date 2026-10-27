# Codex task: redo 6 weak UI-03 overlay assets

Date: 27/10/2026. Assigned by: CPO, under the user's request "có những assets chưa đạt thì cần gen mới để thay".

## 1. Why

The CPO reviewed `Docs/Art/Production_A/MatchOverlay/Contact.png` (pack from `CODEX_TASK_UI03_Overlay.md`). 13 of 19 files pass. These 6 do not read correctly at their on-screen size and must be redrawn:

| File | Problem | New design (mandatory) |
| --- | --- | --- |
| `SliderHandle.png` (48×48 white mask) | Two vertical bars inside a square: reads as a **pause** button. | Vertical fader cap: solid rounded rectangle about 28 wide × 40 tall, 2 px stepped corners, one 2 px transparent horizontal groove across the middle. No vertical bars, no square outline-only look. Must still read at 20 px and 24 px. |
| `CauseZone.png` (64×64 icon) | Purple ring with 4 gold knobs reads as a **lifebuoy**. | Purple ring (2 native px thick) with 4 light chevrons pointing **inward** at N/E/S/W (the zone is shrinking), small gold dot in the centre (the player). No alternating segments on the ring. |
| `CauseLeft.png` (64×64 icon) | Almost identical to `IconExit.png`. | Must have a different silhouette from `IconExit`: a jade doorway with a simple gold walking figure stepping out of it. **No arrow.** |
| `CauseDisconnect.png` (64×64 icon) | Red lightning over a small arc: unreadable red squiggle at 28 px. | Signal icon: dot + 3 concentric arcs (jade, each 2 native px thick), crossed by one red diagonal slash 2 native px thick with the dark outline. Must read as "no connection" at 28 px. |
| `IconMouse.png` (64×64 icon) | Too small and low contrast (grey on grey) at 28 px. | Mouse body about 20 wide × 28 tall native px, light body (palette light grey/white), dark outline, left button filled jade, small wheel line. Content height ≈ 56/64. |
| `IconShake.png` (64×64 icon) | Phone-with-lightning look, cluttered at 28 px. | Screen frame (plain dark inner, no glyph) plus one offset ghost copy of the frame edge, and 2 short motion ticks on each side. No lightning. |

Keep everything else exactly as it is: the other 13 PNGs must stay byte-identical.

## 2. How

1. Edit only the recipes for these 6 in `Tools/Art/build_ui03_overlay_assets.py` (and the `note` text in their spec entries). Same pixel standard as section 4 of `CODEX_TASK_UI03_Overlay.md`: 32 native ×2 nearest for icons, binary alpha, outline `#0C1417`, palette only, ≤ 8 colours, ≥ 3 px padding, centred within 1 px; masks pure white.
2. Re-run the build. Confirm with a hash check that the 13 untouched PNGs did not change.
3. Re-run `Tools/Art/test_ui03_overlay_validation.py`; all checks must pass.
4. Make `Docs/Art/Production_A/MatchOverlay/Redo_Compare.png`: for each of the 6, old (take it from `git show 4be7cd97:<path>`) next to new, at 1× and at the on-screen size (28 px icons, 20/24 px handle), on the charcoal background. Put `CauseLeft` and `IconExit` side by side at 28 px so the difference is visible.
5. Regenerate `Contact.png`, update `asset_mapping.json` notes, update the `*_native32.png` previews, and add a "Lần làm lại 1" section to `Report.vi.md` (Vietnamese): what changed and why, validation result.
6. `asset_manifest.csv`: only edit the notes of these 6 rows if a note changed; keep UTF-8 without BOM and CRLF.

## 3. Rules (mandatory)

- **Do not** create, edit or stage any `.meta`, C#, prefab, scene, `.asset` or ProjectSettings file. The `.meta` files Unity generated next to the PNGs are untracked: leave them alone. Do not open or drive Unity: QA is using the Editor.
- Do not touch `Assets/Imported/`, the existing HUD/menu PNGs, or anyone else's area.
- Append one line to `Docs/Agent_Coordination.vi.md` section 4 when you start and one when you finish.
- Commit with explicit paths only (`git add -- <new files>` first), short English message, e.g. `art(ui-03): redraw six overlay sprites`. **Never** mention Claude, Codex or AI, no `Co-Authored-By`. Never stage `ProjectSettings/*` or `.codex/config.toml`. **Do not push.**
- At most 3 attempts per failing check; record anything still failing in the report.
- Final message: files changed, commit hashes, validation result, anything left undone.
