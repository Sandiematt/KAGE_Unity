# JS UI coverage (SAN-67 rework)

Every UI screen, panel, overlay and widget of the JS game (`E:/PROJECTS/KAGE/KAGE/src/js`, styles in `src/styles.css` and `src/wonder.css`), for the Prologue and Act I flow and everything around it, with the Unity class that draws it and where it stands.

Status words:

- **ported**: built before this rework (SAN-67, SAN-73, SAN-90, SAN-108) and still as it was.
- **ported now**: built or brought up to the JS (including its animation) in this rework.
- **not ported**: not built; the reason is in the last column. A later ticket owns it, or it is dead code in the JS.

Animation is part of "ported": a row says "ported now" when its motion was added in this rework, and the text names it. All motion plays from one clock (`UiClock`), is skipped when Windows' "Show animations" is off (the JS honours `prefers-reduced-motion`; the Unity UI reads the same Windows setting), and never holds up input: the controller changes screens at once and the wipe plays over the change.

The JS keeps the screens in `70-ui.js` (`UIManager`), the palette in `71-theme.js`, the opening in `65-splash.js`, the story in `68-story.js` and `52-story-art.js`, the scene tables in `12-scenes.js`, and the HUD markup in `70-ui.js` (`showHud`, `hud.update`).

## Boot, transitions and shared widgets

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Opening splash: moon scene fading up, K-A-G-E written stroke by stroke, slashes, final cut, subtitle, tagline, pulsing prompt | `65-splash.js` `Splash` | `SplashPanel`, `SplashTimeline` | ported now |
| Boot overlay: any key, click or button opens the title (fade .5 s) | `70-ui.js` `UIManager.init` (`start`), `index.html` `#boot` | `SplashPanel.Dismiss`, `GameFlow.Update` | ported now |
| Ink wipe between screens (brush stroke in .225 s, swap at 235 ms, out .225 s) | `70-ui.js` `UIManager.go`, `styles.css` `#wipe` | `ScreenWipe`, `UiWipe`, `GameFlow.Sync` | ported now |
| Toasts (drop in with a pop, stay 2.6 s, slip away) | `70-ui.js` `UIManager.toast`, `#toasts` | `ToastLayer`, `FlowController.Toasted` | ported now |
| Overlay card pop (`cardIn2`) | `styles.css` `.ov-card` | `OverlayKit.Card` | ported now |
| Back diamond, coin and jade pills, key hints, chips, pips, section titles | `70-ui.js` `wBack`, `wPills`, `wKeys`; `wonder.css` `.w-*` | `UiParts` | ported |
| Red brush button (Fight): hover glow, sheen sweep, press dip | `wonder.css` `.w-brush` | `UiParts.Brush`, `ButtonSheen` | ported now (hover tween, sheen, press) |
| Gold and ghost buttons (overlays): hover, sheen, press dip | `styles.css` `.btn-primary`, `.btn-ghost` | `UiParts.Plate`, `ButtonSheen` | ported now (hover tween, sheen, press) |
| Segmented control | `wonder.css` `.w-seg` | `UiParts.Seg` | ported |
| Keyboard, gamepad and mouse menu navigation (arrows, Enter, Esc, Q/E) | `70-ui.js` `UIManager.onKey`, `onPad` | `NavGroup`, `MenuInput`, `GameFlow.Send` | ported |
| Drifting sakura petals, drifting clouds | `70-ui.js` `wPetals`, `wClouds`; `wonder.css` `.w-petals`, `.w-cloud` | `PetalField`, `UiPetalView`, `UiCloudView` | ported now |
| Swaying maple leaves | `70-ui.js` `wLeaves`; `wonder.css` `.w-leaves` | `ArmoryScreen.Leaves` | ported now |
| Act palettes and the ink bloom between acts | `71-theme.js` `ActTheme` | none (`Ui` holds the crimson palette) | not ported: the JS pins every screen to Raikon's crimson (`MENU_THEME_PIN`), so the bloom and the other palettes never show |
| Canvas title scene (parallax key art, embers, scarf) | `55-title.js` `TitleScene` | none | not ported: dead code in the JS; `Game.setScene('title')` is never called, the Wonder home screen replaced it |
| Touch controls (stick, seven buttons) | `70-ui.js` `buildTouch`, `updateTouch` | none | not ported: touch is out of scope (Android and iOS come later) |
| Rotate-to-landscape hint | `index.html` `#rotate-hint` | none | not ported: desktop build, never portrait |

## Title (Home)

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Sky, hills, castle with glowing windows, lake and trees | `70-ui.js` `renderTitle` (`.w-bd`) | `TitleScreen.Backdrop` | ported; castle windows and the sun's glow breathe (`wGlow`): ported now |
| Painted hero and the red moon behind him | `renderTitle` (`.hm-hero`, `.hm-moon`) | `TitleScreen.Backdrop` | ported; the hero fades in (.9 s) and the moon breathes (6 s): ported now |
| Drifting clouds far and near, falling petals | `renderTitle` (`wClouds`, `wPetals`) | `TitleScreen.Backdrop` | ported now |
| Kage wordmark and subtitle | `renderTitle` (`.hm-brand`) | `TitleScreen` | ported; rises in (.8 s): ported now |
| Seal, name, level, experience bar | `renderTitle` (`.hm-top`) | `TitleScreen` | ported |
| Coin and jade pills | `wPills` | `UiParts.Pills` | ported |
| Codex button (controls guide) | `renderTitle` (`data-a="help"`) | `TitleScreen.CodexButton` | ported now |
| Menu rows Home, Campaign, Armory, Settings: wedge grows on hover, label slides, rows rise in turn | `renderTitle` (`.hm-row`) | `TitleScreen.Row` | ported; hover tween and the staggered rise: ported now (the rows carry kanji in place of the JS SVG icons) |
| Menu rows Echo Trials and Boss Rush | `renderTitle` | none | not ported: Echo Trials and Boss Rush are out of scope in the Act I spec (they need the echoes of later bosses) |
| Continue card: scene, hills, petals, text, play button | `renderTitle` (`.hm-cont`) | `TitleScreen.ContinueCard` | ported; hills slide, moon and play button breathe, petals blow past, hover lift, rise: ported now |
| Mode tile Armory | `renderTitle` (`.hm-tile`) | `TitleScreen.ArmoryTile` | ported; rise and hover lift: ported now |
| Mode tiles Echo Trials and Boss Rush | `renderTitle` | none | not ported: out of scope, as the rows |
| Utility diamonds: Records, Trophies, Profile, sound toggle | `renderTitle` (`.hm-util`) | `TitleScreen.Utilities` | ported now |
| DEV badge | `renderTitle` | `TitleScreen` | ported |

## Campaign map

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Top bar: back, title, chapter pips, pills | `renderCampaignW` (`.w-top`) | `CampaignScreen` | ported |
| Chapter rail: locked, open, cleared, selected | `renderCampaignW` (`.cp-chap`) | `CampaignScreen.ChapterRow` | ported; rows rise in turn and slide 4 px on hover: ported now |
| Map panel: skyline, window lamps, title, progress bar | `renderCampaignW` (`.cp-map`) | `CampaignScreen.MapPanel` | ported; the panel fades in, the lamps glow: ported now |
| Stage nodes (stretch, sub-boss, boss) with lock, number, name, type | `renderCampaignW` (`.cp-node`) | `CampaignScreen.Node` | ported; nodes pop in 40 ms apart, hover grows the diamond: ported now |
| Current node's breathing ring | `wonder.css` `cpHalo` | `CampaignScreen.Node` | ported now |
| Road between nodes: solid when cleared, marching dashes when not | `renderCampaignW` (`.lines`), `cpDash` | `CampaignScreen.MapPanel`, `UiDashFlow` | ported now (the dashes march; the JS has no stroke-drawing animation) |
| Stage card: strip, kind line, title, description, threat pips, reward, Fight | `renderCampaignW` (`.cp-card`) | `CampaignScreen.StageCardPanel` | ported; the card rises (.45 s) whenever the stage changes and the strip's sun glows: ported now |
| Act boss reveal card (Raikon) | `renderCampaignW` (`.cp-reveal`) | `CampaignScreen.StageCardPanel` | ported; rises and opens from the bottom, light sweeps over it: ported now |
| Act boss card for a boss with no reveal art | `renderCampaignW` (`.cp-boss`) | none | not ported: Acts II and III and the Core have no reveal art in Unity yet (Act I's boss has) |
| New Game+ badge | `renderCampaignW` (`.cp-ng`) | none | not ported: New Game+ is out of scope |
| Scene stage (Epilogue) plays its scene over the map | `playScene`, `sceneStageDone` | none | not ported: the Epilogue and the story player are out of scope |
| "Stage locked" and "Chapter locked" toasts | `action` (`stage`, `chapter`) | none | not ported: Unity lets a locked stage be looked at (its card says Locked); locked chapters cannot be focused |

## Pre-fight loadout

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Live tableau of both fighters | `renderPrefight` (`Game.setTableau`) | `StageWorld` (tableau phase) | ported |
| Top bar: back, "Prepare for battle", pills | `renderPrefight` | `LoadoutScreen` | ported |
| Kage block: name, level, health, helm, armour, mode cells | `renderPrefight` (`.pf-me`) | `LoadoutScreen` | ported; rises in (.6 s): ported now |
| Change gear button | `renderPrefight` (`.pf-gear`) | `LoadoutScreen.ChangeGear` | ported |
| Foe block: pips, eyebrow, name, title, chips | `renderPrefight` (`.pf-foe`) | `LoadoutScreen` | ported; rises in after .08 s: ported now |
| VS diamond with 対 | `renderPrefight` (`.pf-vs`) | `LoadoutScreen` | ported; drops in large and turned (.7 s): ported now |
| Water rings under both fighters | `renderPrefight` (`.pf-ripple`) | `LoadoutScreen.Ripples` | ported now (breathing, placed under the live fighters) |
| Foe name plate over the foe | `renderPrefight` (`.pf-plate`) | none | not ported: the plate follows the JS canvas fighter; Unity's fighters are 3D (a later polish) |
| Weapon row with arrows, cards, hover lift | `renderPrefight` (`.pf-wrow`, `.w-wcard`) | `LoadoutScreen.Dock`, `Card` | ported; hover lift tween: ported now |
| Fight button | `renderPrefight` (`.pf-fight`) | `UiParts.Brush` | ported now (sheen, press) |
| Echo Trials pre-fight, Interlude spar variant | `renderPrefight` | none | not ported: Echo Trials and the Interlude are out of scope |

## Armory

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Tabs Weapons, Helmets, Armor with the brush mark | `renderArmory` (`.ar-tabs`) | `ArmoryScreen.Header` | ported; focus colour tween, the open tab fades in: ported now |
| Backdrop: sun, beam, hills, branch with swaying leaves | `renderArmory` (`.w-bd`) | `ArmoryScreen.Backdrop` | ported; halo ring and leaves move: ported now |
| Weapon preview: the blade illustration turning in front of the sun over a faint warrior | `startPreview` | `ArmoryScreen.Preview`, `ArmoryBladeSway`, `ArmoryPreviewMotion` | ported now (the JS drawing of every weapon, sway, bob, turn and shimmer) |
| Helmet preview: Kage's head wearing the helmet | `startPreview` | `ArmoryWorld` | ported (live Kage) |
| Armor tab: a row of tall cards, Kage in each armour | `armorScreenHTML`, `startArmorCards` | `ArmoryScreen.Card`, `ArmoryWorld` | ported; cards rise in turn: ported now |
| Weapon tiles: the sword | `renderArmory` (`.ar-tile`, `drawWeaponIcon`) | `ArmoryScreen.Tile`, `GearArt` | ported now (the design's renders and the JS drawing of the boss sword) |
| Helmet tiles: the helmet | `paintIcon` | `ArmoryScreen.Tile`, `GearArt` | ported now (the JS drawing of the 14 helmets the Armory lists, in place of the 兜 glyph) |
| Armour cards and chips: the armour | `paintIcon`, `startArmorCards` | `ArmoryScreen.Card`, `GearArt` | ported (live Kage on the cards); the JS drawing is also on the results chip and the profile: ported now |
| Detail panel: rarity, name, lore, description, level, stats with the next-level preview, perks or rage art, lock box | `renderArmory` (`.ar-det`) | `ArmoryScreen.Detail`, `ArmoryView` | ported; fades in when the item changes: ported now |
| Buy, Upgrade, Equip buttons | `renderArmory` (`.ar-acts`) | `ArmoryScreen.Button`, `ArmoryController` | ported |
| Armor line and Trophies chips | `armorScreenHTML` (`.al-chips`) | none | removed on purpose (SAN-85, owner: "remove the trophies section completely"): the Armor tab is one list, with an owned Act I reward armour after the shop armours |
| Items the Armory lists | `renderArmory` | `ArmoryListing`, `ArmoryView.Items` | listed only when implemented in Unity (SAN-108): the table of every weapon, helmet and armour with its class is `docs/armory-items.md` |
| Pick on click, not on hover | `renderArmory` (`.ar-tile` click) | `ArmoryTilePick`, `ArmoryScreen.Tile`, `Card` | changed (SAN-108): hover, arrows and the stick only highlight a tile (lift and wash); a click, or Enter, Space, J or A on the focused tile, selects it |
| Tile rise (`wRise`, 25 ms apart), hover lift | `renderArmory` | `ArmoryScreen.Tile` | ported now; hover and focus no longer change the selection (SAN-108) |
| "Acquired" and "tempered" toasts | `buy`, `upgrade` | `ArmoryController` toast, `ToastLayer` | ported now |
| Rim-light outline on the armour cards' fighters | `startArmorCards` (`rim`) | none | not ported: it is part of how the character is drawn (fights unchanged) |

## Results

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Victory or defeat stage, heading, line | `showResult` | `ResultsScreen`, `ResultsView` | ported; the heading rises (1 s): ported now |
| Seals (Victory, Under 60s, Flawless) | `showResult` (`.seal-b`) | `ResultsScreen` | ported; they pop in 120 ms apart: ported now |
| Rating letter | `showResult` (`.rating`) | `ResultsScreen` | ported; drops in large and turned (.6 s): ported now |
| Coin counter | `showResult` (`data-count`) | `ResultsScreen.Tick` | ported |
| Gem count, item chips | `showResult` (`.rewards`) | `ResultsScreen` | ported; they pop in; the item chip shows the item drawn: ported now |
| Loss: recap and tip | `showResult` (`.recap`, `.tip`) | `ResultsScreen` | ported |
| Experience bar | `showResult` (`.xp`) | `ResultsScreen.Tick` | ported |
| Continue, Replay / Retry, Upgrade, Map buttons | `showResult` (`.res-actions`) | `UiParts.Plate` | ported now (sheen, press) |
| Victory and defeat tableau behind | `showResult` (`Game.setTableau`) | `StageWorld.FrameResults` | ported |
| Unlock, new chapter and level-up toasts | `showResult` (`afterShow`) | `ResultsScreen.Tick`, `ToastLayer` | ported now |
| Echo Trials and Boss Rush results, the spar's "Interrupted" | `showResult`, `showBossRushResult` | none | not ported: out of scope |

## Settings

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Backdrop: sunset over the torii, swaying leaves | `renderSettings` (`.w-bd`) | `SettingsScreen` | ported; leaves added: ported now |
| Panel with the Feel & Sound rows (shake, flashing, sound, music) | `renderSettings` (`.sg-panel`) | `SettingsScreen` | ported; the panel rises (.5 s): ported now |
| Challenge row (difficulty) and the new-game mode picker | `settingsRows`, `openModePicker` | none (a Dev Mode row stands in) | not ported: difficulties other than Normal are out of scope; the JS row is replaced by the dev-mode switch (ADR-0009) |
| Touch controls and touch stick rows | `renderSettings` | none | not ported: touch is out of scope |
| New Game+ card | `renderSettings` (`.sg-ng`) | none | not ported: New Game+ is out of scope |
| Help card: controls guide | `renderSettings` (`.sg-help`) | `SettingsScreen` | ported; rises after .16 s: ported now |
| Erase all progress | `renderSettings` (`data-a="reset"`) | none | not ported: Unity's fresh start is turning dev mode off (ADR-0009) |
| Key hints | `renderSettings` (`.sg-hints`) | `UiParts.Keys` | ported |

## Overlays

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Pause: Resume, Restart, Controls, Quit, settings rows | `openPause` | `PauseOverlay` | ported; card pop: ported now; Controls button: ported now |
| Controls guide | `openHelp` | `ControlsOverlay` | ported; reachable from the title's Codex button, the settings and the pause menu: ported now |
| Confirm question | `confirm` | `ConfirmOverlay` | ported |
| Records (duels won, defeats, bosses broken, flawless wins, cycle, experience) | `openProfile` | `RecordsOverlay` | ported now |
| Trophies (fallen foes) | `openTrophies` | `TrophiesOverlay` | ported now |
| Profile (level, experience, gear, health, Open Armory) | `openLoadout` | `ProfileOverlay` | ported now (the gear drawn in a row; the JS draws Kage himself in a canvas) |
| Mode picker (new game difficulty) | `openModePicker` | none | not ported: difficulties other than Normal are out of scope |
| Story player: dialogue panel with portrait medallion and typewriter text, skip button, video placeholder card, full-screen illustrations | `68-story.js` `StoryPlayer`, `52-story-art.js`, `12-scenes.js` | none | not ported: the Act I spec leaves the story out (scenes, dialogue, the story player); it is a ticket of its own (the scene tables, 926 lines of canvas illustrations and the stage hooks) |

## Fight HUD (SAN-108)

| JS element | JS file and function | Unity class | Status |
| --- | --- | --- | --- |
| Player panel: medallion, name, level, health bar, rage bar, Rage ready, kunai count | `showHud` (`.fp.left`) | `Hud`, `HudModel` | ported |
| Foe panel | `showHud` (`.fp.right`) | `Hud` | ported |
| Boss bar: art card, phases, name, track, phase marks, hurt shake | `showHud` (`.boss-bar`), `bossArtHTML` | `Hud.Boss` | ported (no 3D turn or light sweep on the card) |
| Clock and round pips | `showHud` (`.clock`) | `Hud` | ported |
| Road strip: track, encounter marks, Kage marker | `showHud` (`.hud-road`) | `Hud`, `HudLayoutRects` | ported (no clue count: clues are not played); the label is centred under the strip, between the two health numbers (SAN-108: left-aligned it ran over Kage's health number; the JS CSS has no fix for this) |
| Pause button | `showHud` (`.hud-pause`) | `Hud.PauseRequested` | ported |
| Combo counter | `showHud` (`.combo`) | `Hud` | ported |
| Stage title card, tutorial prompt slip | Unity addition (spec story 81) | `Hud.StageCard`, `StageHudPanel` | ported |
| Wave label (Echo Trials, Boss Rush) | `showHud` (`.hud-wave`) | none | not ported: out of scope |
| Witness plate, modifiers, glitching HUD | `showHud` (`.wit-plate`, `.wit-mods`) | none | not ported: the Witness is the final boss |
| Story dialogue line and skip hint | `showHud` (`.dialogue`, `.skip-hint`) | none | not ported: the story is out of scope (see the story player above) |
