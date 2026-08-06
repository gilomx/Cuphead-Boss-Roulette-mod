# Cuphead Boss Roulette - Project Handoff

Last updated: 2026-08-05
Current local version: 0.5.71

This file is the working context for the next agent. Read it before changing the
mod. The user has iterated on the layout by eye, so preserve all explicit
coordinates and avoid broad rewrites.

## Goal

Convert the boss roulette from the user's website into a BepInEx mod for
Cuphead. The roulette runs inside the game, chooses a boss/loadout/challenge,
equips the result, and loads the selected fight.

The target is not a generic mod UI. It must closely resemble Cuphead's original
Equip Card in layout, typography, input, animation, sound, and native prompts.

## Important locations

- Repository:
  `C:\Users\gilomx\Documents\dev\Cuphead-Boss-Roulette-mod`
- Git remote: `git@github.com:gilomx/Cuphead-Boss-Roulette-mod.git`
- Branch: `main`
- Cuphead: `E:\SteamLibrary\steamapps\common\Cuphead`
- Installed plugin:
  `E:\SteamLibrary\steamapps\common\Cuphead\BepInEx\plugins\GilomxBossRoulette`
- Runtime log:
  `E:\SteamLibrary\steamapps\common\Cuphead\BepInEx\LogOutput.log`
- BepInEx config:
  `E:\SteamLibrary\steamapps\common\Cuphead\BepInEx\config\mx.gilomx.cuphead.bossroulette.cfg`
- Original website archive: `C:\Users\mgtgi\dev\gilomx-website.zip`
- Website roulette source inside the archive: `src/app/ruleta`
- Card PSD supplied by the user:
  `C:\Users\mgtgi\OneDrive\Escritorio\cuphead_mod\originals\ch_equip_front_no_text.psd`

The PC already has GitHub SSH access. Do not install SSH tooling or unrelated
dependencies.

## Current Git state

This handoff documents the roulette implementation through version 0.5.71.
Always inspect `git status` before editing, and do not reset, restore, or
overwrite unrelated user changes.

Localization is intentionally deferred. No runtime text has been changed.
`LOCALIZATION_CATALOG.md` records every currently identified translatable
surface, Cuphead's 12 supported languages, native localization sources,
technical prerequisites, and the wording decisions reserved for the user.

Important correction from runtime diagnosis: the reported hotkey failure was
not caused by DLC filtering or by any keyboard backend. The Steam instance
running during the tests was stale and had been started during the direct
Doorstop launch diagnostics. The evidence indicates that it inherited
Doorstop's child-process disable marker, so every subsequent Cuphead process
started by that Steam session loaded the local `winhttp.dll` proxy but skipped
the BepInEx preloader. This made both the native prompt and F6 disappear. A full
normal Steam shutdown and restart fixed the issue: Cuphead launched through
Steam at 21:41 on 2026-08-02, BepInEx loaded Boss Roulette 0.5.43, and Rewired
found XInput in the same process. The old BepInEx log can otherwise make the
installation appear healthy even when its `LastWriteTime` predates the visible
Cuphead process.

All attempted keyboard hotkey workarounds from the diagnosis were removed at
the user's request: no `R` fallback, Win32 polling, duplicate keyboard paths,
extra input logging, or relaxed map gating remains. F6 still uses only the
original BepInEx `KeyboardShortcut.IsDown()` path, so each keyboard press
toggles exactly once. Version 0.5.44 introduces a separate, intentional Rewired
path only for the controller combo described below. DLC filtering and challenge
functionality remain intact.

## Build and install

No package installation is needed. Existing references point to the local game.

```powershell
dotnet build .\CupheadBossRoulette.csproj -c Release --no-restore `
  -p:CupheadDir="E:\SteamLibrary\steamapps\common\Cuphead"
```

Output:

`bin\Release\net35\Gilomx.CupheadBossRoulette.dll`

To test, close Cuphead, replace only the installed DLL, and restart Cuphead.
Keep the `assets` directory beside the DLL. Verify the version and errors in
`BepInEx\LogOutput.log`.

The following command was useful only as a diagnostic because it keeps BepInEx
in one process:

```powershell
$env:SteamAppId = "268910"
$env:SteamGameId = "268910"
Start-Process `
  -FilePath "E:\SteamLibrary\steamapps\common\Cuphead\Cuphead.exe" `
  -WorkingDirectory "E:\SteamLibrary\steamapps\common\Cuphead"
```

`doorstop_config.ini` was restored to its original
`ignore_disable_switch = false`; the temporary backup was removed. Do not ship
or recommend the environment-variable launcher: starting outside Steam bypassed
Steam Input, so the user's Switch Pro controller was not detected. Users should
launch Cuphead normally through Steam; no extra launcher is part of the mod. If
the same symptom returns after direct Doorstop diagnostics, fully exit Steam,
start Steam again from Windows, and launch Cuphead from the library. Compare the
surviving Cuphead process `StartTime` with `BepInEx\LogOutput.log`
`LastWriteTime`; the log must be newer than the process and confirm the current
Boss Roulette version.

## User-facing behavior

- The roulette can only open after loading a save and while freely walking on a
  map.
- It must not open at the title screen or inside a fight.
- `F6` opens/closes the roulette.
- A controller opens/closes it by holding the physical left trigger and
  pressing Cuphead's `EquipMenu` action: Switch `ZL + X`, Xbox `LT + Y`, or
  PlayStation `L2 + Triangle`. The trigger and Equip press must come from the
  same Rewired player. The native Equip Card is suppressed while the trigger
  is held, so the combo cannot open both interfaces.
- Arrow keys and the controller's D-pad/left stick move/change options.
  Controller navigation uses Cuphead's native Rewired `MenuUp`, `MenuDown`,
  `MenuLeft`, and `MenuRight` actions, so it follows the game's mappings.
- `Enter` or the controller's native `Accept` action changes the selected
  setting and confirms `GIRAR`/`JUGAR`.
- The controller's physical right trigger (`ZR`, `RT`, or `R2`) rerolls only
  when automatic load is disabled and a completed result is waiting.
- `Esc` closes the roulette without also opening Cuphead's pause menu.
  The controller's native `Cancel` action closes it through the same path.
- The mouse is not required.
- Forced selection from the website is intentionally excluded.
- The normal Equip Card must continue to open with `Shift` whenever the
  roulette is closed.
- While the roulette is open, native Equip Card input is temporarily disabled.
  Restore it on the next frame when the roulette closes.

## Roulette options and flow

Settings are persisted through BepInEx config:

- Difficulty: Simple / Normal / Expert
- Challenge (label is `RETO`, previously `MODO FEO`)
- Automatic load

If a setting changes after a completed spin, invalidate the old result and put
the main action back to `GIRAR`.

When automatic load is disabled:

- After a spin, the main action becomes `JUGAR`.
- `Enter` on `JUGAR` loads the result.
- `F7` or the controller's physical right trigger (`ZR`/`RT`/`R2`) allows
  another spin while the native `VOLVER A GIRAR` prompt is visible.

Airplane bosses still roll and equip weapon A, weapon B, super, and charm.
Those ground loadout values do not affect airplane weapons, but leaving all
slots populated looks better and is the behavior requested by the user.

The roulette intentionally uses the complete base/DLC equipment catalog even
when an item has not been purchased or unlocked in the current save. Treat the
result as a temporary loan: immediately before loading the selected boss,
snapshot both players' complete loadout fields and flags. Keep the roulette
loadout through losses, retries, challenge-triggered reloads, and every Rey
Dado subfight. Restore the snapshots before Cuphead saves a victory or a return
to the map. A map-side fallback restores and explicitly saves if a normal exit
hook is ever missed. Never add the loaned item to the player's inventory.

## Challenge lifecycle

Challenge names currently come from `RouletteData.Modifiers`.

Required behavior:

- When RETO is enabled, `Nada` must never appear during the animation or as the
  final result. `Nada` remains only as the empty visual state while RETO is
  disabled.
- Show `RETO: <name>` only during the exact fight loaded by the roulette.
- Do not show anything for `Nada`.
- A loss and retry keep the challenge.
- Winning clears it immediately.
- Leaving the level and returning to the map clears it.
- Entering another level clears a stale challenge.
- Rey Dado is a special multi-fight session. Every internal `DicePalace...`
  minion level and `DicePalaceMain` must match the same active challenge. A
  minion victory must not clear it; only winning `DicePalaceMain`, losing and
  choosing to leave, or returning to the map clears it.
- `No Dash` is enforced in ground fights by blocking
  `LevelPlayerMotor.HandleDash` while that exact active challenge is visible.
  The block persists through defeat/retry and is removed by the same win/exit
  lifecycle that clears the challenge.
- `No EX` is enforced for both ground and airplane fights. Ground fights force
  `PlayerStatsManager.CanUseEx` to false after the normal super decision;
  airplane fights block only `PlanePlayerWeaponManager.StartEx`. Normal shots
  and `StartSuper` are unaffected, and blocked EX attempts do not consume
  cards. The block persists through retry and clears on win/exit.
- `No mini avión` is enforced by skipping
  `PlanePlayerAnimationController.HandleShrunk()` while that exact challenge is
  active. The surrounding animation controller `FixedUpdate` still runs, so
  directional animation and other airplane behavior continue normally. Both
  players are covered, retry retains the block, and win/exit clears it.
- `Solo mini avión` leaves shrinking and expanding completely unrestricted.
  A postfix on `DamageDealer.DealDamage()` checks successful hits against an
  enemy `DamageReceiver` (including `DamageReceiverChild`). All attacks apply
  their normal damage, but an impact without Cuphead's native
  `DamageSource.SmallPlane` marker queues `SceneLoader.ReloadLevel()` for the
  next frame and invalidates that attempt. Because the marker belongs to the
  projectile, changing size after firing does not change whether its impact is
  valid. Large-plane shots, bomb explosions, and EX attacks still restart the
  attempt. Airplane supers are explicitly allowed to balance the challenge;
  Cuphead marks them with DamageSource.Super, separately from EX damage. The
  active challenge is retained across an automatic restart.
- `No disparo bombas` is enforced in two parts. An `OnLevelStart` postfix on
  `PlanePlayerWeaponManager` normalizes the starting weapon to
  `plane_weapon_peashot`, or `plane_chalice_weapon_3way` for Galletita Astral.
  This also protects against Reliquia Divina randomizing the initial airplane
  weapon. A prefix then skips `HandleWeaponSwitch()` while the challenge is
  active, preventing the player from switching to either normal or Chalice
  bombs. Shooting, EX, super, mini airplane, and movement remain unchanged.
- `No disparo Peashooter` uses the same starting-weapon/switch-lock pipeline in
  reverse. `OnLevelStart` forces `plane_weapon_bomb`, or
  `plane_chalice_weapon_bomb` for Galletita Astral, and the shared
  `HandleWeaponSwitch` prefix prevents returning to Peashooter/Chalice
  three-way fire. Reliquia Divina cannot randomize the starting weapon.
- `Blanco y negro` applies to ground and airplane fights. The
  `BlackAndWhiteSaturationEffect` loads a shader compiled with Unity 2017.4.9f1
  from `assets/shaders/gilomx-boss-roulette-shaders`. One explicit command
  buffer copies the final visible frame after Cuphead's image effects and
  continuously changes only its saturation. At full desaturation a Harmony
  postfix selects Cuphead's exact native BW filter; on exit it releases that
  pass while the bundled shader still displays a gray frame, then restores
  color smoothly. No white overlay, pre-image capture, or runtime shader lookup
  is used. Each attempt waits 1.5 seconds in color, fades over 1.25 seconds, and
  reverses over 0.9 seconds. It never mutates `PlayerData.Data.filter`; retry
  restarts the delay and fade, while win or abandonment initiates the exit fade.
  The temporary `BN` icon is
  `modifiers/blackandwhite.png`. The shader exposes `_FlipY`, fixed at `1` on
  Windows/Direct3D after the first manual test showed the frame vertically
  inverted. The forced test selector was cleared after validation. Transition
  timings are the
  `BlackAndWhiteEntryDelay`, `BlackAndWhiteFadeInDuration`, and
  `BlackAndWhiteFadeOutDuration` constants in `Plugin.cs`.

Implementation:

- `Plugin.cs` stores `activeChallenge` and `activeChallengeBoss`.
- A Harmony prefix on `Level._OnPreWin` clears the challenge on victory.
- Map/different-level detection clears abandoned state.
- `NativeMapPrompt.cs` clones the already-working native map prompt before the
  map scene unloads. It places the clone on a persistent Screen Space Overlay
  canvas, hides the key capsule, and displays the same native text styling in
  the bottom-right corner through retries.

Version 0.5.24 tried to find the map prompt again after entering the battle.
That failed because the map canvas had already been destroyed. Version 0.5.25
uses the persistent clone described above.

Version 0.5.26 removes the old 30% direct chance of `Nada` and excludes `Nada`
from `ValidModifierIndices`. This shared filtered list drives both the rolling
animation and the final random selection, so an enabled RETO always produces a
real challenge compatible with the selected boss.

Version 0.5.27 treats every `Levels` value whose name starts with
`DicePalace` as part of a Rey Dado challenge session. The `_OnPreWin` Harmony
prefix preserves the challenge after minion wins and clears it only when
`DicePalaceMain` wins. It also adds a Harmony prefix to the private
`LevelPlayerMotor.HandleDash()` method; when the active challenge is `No Dash`,
the prefix returns `false` without starting a dash. Normal gameplay and all
other challenges run the original method unchanged.

Version 0.5.28 adds a shared Harmony prefix to the ground and airplane
`StartEx()` methods. It skips those methods only while the active challenge is
`No EX`; this prevents the EX animation/projectile and stops the game from
spending a card. Supers use separate `StartSuper()` methods and remain usable.

Version 0.5.29 replaces the ground `StartEx()` prefix after a reported frozen
Fósforo Sombrío fight (dragon and clouds never advanced) with the earlier,
safer `PlayerStatsManager.CanUseEx` decision point. Ground super selection is
evaluated before `CanUseEx`, so supers remain available. Airplane
`CheckEx()` uses `CanUseEx` as a prerequisite for both EX and super, so airplane
fights retain the narrow `PlanePlayerWeaponManager.StartEx()` prefix. Rey Dado's
two airplane minions (`DicePalaceFlyingHorse` and
`DicePalaceFlyingMemory`) are explicitly treated as airplane controls.
No exception appeared in BepInEx's log for the reported freeze, so repeat the
exact Dragon/Galletita Astral/No EX scenario as a regression test.

Version 0.5.30 enables a temporary diagnostic override in
`Plugin.CreateRandomResult()` (`static readonly
ForceDragonNoExTestResult = true`). Every spin is forced to:

- Fósforo Sombrío (`Boss = 9`)
- Lanzaguisantes (`Weapon1 = 0`)
- Carga (`Weapon2 = 4`)
- Súper III (`Super = 2`)
- Galletita Astral (`Charm = 6`)
- No EX (`Modifier = 5`)

This is intentionally not production behavior. After the frozen-fight
regression test, remove the override and restore normal random selection before
continuing regular releases. While enabled, it also forces the in-memory RETO
setting on at startup/each spin so `No EX` is active, but it does not overwrite
the user's persisted RETO config value.

Version 0.5.31 is a temporary A/B diagnostic build. It preserves the forced
0.5.30 Dragon loadout and displays/retains the `No EX` challenge, but
`DisableNoExEnforcementForTest = true` makes `ShouldBlockEx()` return false.
Therefore EX attacks work normally in this build. If Dragon and the clouds
still freeze, neither the ground `CanUseEx` postfix nor the airplane `StartEx`
prefix is causing the freeze. If the fight runs normally, the enforcement path
is implicated. Remove this flag along with the forced result after diagnosis.

The 0.5.31 test still froze Dragon and the moving clouds even though EX attacks
were not blocked. This rules out both No EX enforcement patches as the direct
cause.

Version 0.5.32 is the next temporary A/B test. It keeps the same forced roulette
result and keeps No EX enforcement disabled, but
`DisableChallengeActivationForTest = true` prevents `SetActiveChallenge` from
activating the fight label/lifecycle. The roulette card still displays `No EX`;
the fight itself should not. If Dragon still freezes, active challenge state
and the persistent prompt are not the cause, and the next isolation target is
Galletita Astral/direct level loading.

The 0.5.32 test still froze Dragon and the clouds with no active challenge,
prompt, lifecycle, or enforcement. This rules out the complete active challenge
infrastructure.

Version 0.5.33 removes only Galletita Astral from the forced result by setting
`Charm` to the final `Nada` entry. It keeps Dragon, Peashooter, Charge,
Super III, and the non-active No EX result. If the fight starts normally, the
Ms. Chalice switch is the trigger. If it still freezes, isolate Super III next.

The 0.5.33 test still froze Dragon with no charm, ruling out Galletita Astral
and the Ms. Chalice switch.

Version 0.5.34 also removes Super III by setting `Super` to the final `Nada`
entry. It retains Dragon, Peashooter, Charge, no charm, and no active challenge
or enforcement. If it still freezes, the remaining isolation targets are the
weapon loadout and direct Dragon level loading.

The 0.5.34 test still froze Dragon with no super, ruling out Super III. The
persisted roulette difficulty was then confirmed as `Hard`; the BepInEx log
still contained no runtime exception.

Version 0.5.35 forces only the diagnostic Dragon load to `Level.Mode.Normal`
while leaving the persisted `Hard` config untouched. The forced weapons remain
Peashooter and Charge; super/charm/challenge/enforcement remain absent. If this
works, the freeze is tied to direct Expert-mode Dragon loading. If it still
freezes, test without applying the roulette loadout next.

The 0.5.35 Normal-mode test still froze Dragon. The actual cause was then found
outside this project: the installed root
`BepInEx\plugins\CupheadModdingTemplate.dll` hooks both
`DragonLevelCloudPlatform.move_cr` and `DragonLevel.nextPattern_cr` with custom
coroutines named `Clouds` and `DragonShoot`. Its cloud coroutine replaces
normal movement with Player Two input, and its dragon coroutine replaces the
normal attack pattern with Player Two button input. This exactly explains the
stationary clouds and idle dragon. A second copy existed under
`BepInEx\plugins\CupheadModdingTemplate\`.

Version 0.5.36 removes all temporary diagnostic flags/results, restores random
selection, persisted difficulty, loadout application, challenge activation,
and real No EX enforcement. Both `CupheadModdingTemplate` DLLs are moved
reversibly to `BepInEx\disabled`; `Blender` remains installed because Boss
Roulette does not depend on the template and Blender itself does not install
the Dragon-specific hooks.

Disabled template backup locations:

- `BepInEx\disabled\CupheadModdingTemplate-root.dll`
- `BepInEx\disabled\CupheadModdingTemplate-folder.dll`

Version 0.5.37 implements `No mini avión` and adds a reusable temporary
challenge-test selector near the top of `Plugin.cs`:

`ForcedTestChallenge = "No mini avión"`

While this string is non-empty, every spin selects that modifier and randomly
chooses only bosses compatible with it; equipment remains random. RETO is
forced on in memory without overwriting the persisted config. Change this one
string for the next challenge test, and clear it before a production build.

Version 0.5.38 implements `No disparo bombas` and changes the temporary selector
to:

`ForcedTestChallenge = "No disparo bombas"`

Every test spin therefore uses a compatible airplane boss and this challenge.
The normal airplane starts on Peashooter; Ms. Chalice starts on her three-way
shot. The weapon-switch input must not reach either bomb weapon.

Version 0.5.39 corrects `Los Perritos Pilotos` (`Levels.Airplane`) from
`IsPlane = true` to `false`. Although the encounter is visually airborne, the
player stands on a plane and uses the regular ground controller/loadout. It
must therefore receive ground challenges (`No Dash`, `No EX`) and must never be
selected for airplane-control challenges (`No mini avión`, `Solo mini avión`,
`No disparo bombas`, `No disparo Peashooter`).

Version 0.5.40 implements `No disparo Peashooter`. The shared airplane weapon
lock now covers both mutually exclusive shooting challenges, while the
`OnLevelStart` postfix chooses the allowed side. The temporary selector is:

`ForcedTestChallenge = "No disparo Peashooter"`

Every spin chooses a compatible airplane boss, starts on bombs (normal or
Chalice), and prevents switching back to the Peashooter family.

Version 0.5.41 initially implemented `Solo mini avión` by suppressing all
non-mini damage. This behavior was superseded in 0.5.42.

Version 0.5.42 implements `Solo mini avión` as an attempt validator instead of
forcing the player to remain shrunk. Cuphead already marks mini-plane
projectiles with `DamageDealer.DamageSource.SmallPlane` for its own
`SmallPlaneOnlyWin` achievement; the mod now uses the same per-projectile
marker after successful enemy hits. The temporary selector is:

`ForcedTestChallenge = "Solo mini avión"`

Every spin chooses a compatible airplane boss. Players may change size freely,
and every attack deals its normal damage. The first large-plane shot, bomb
explosion, EX attack, or super that damages an enemy automatically restarts the
level while preserving the challenge. A shot created in mini form remains
valid if the player expands before impact, while a large-plane shot remains
invalid if the player shrinks before impact.

Version 0.5.45 balances this rule by accepting both
DamageSource.SmallPlane and DamageSource.Super. Airplane supers may damage
the boss without restarting the attempt. Large-plane shots, bombs, and EX
attacks remain violations and still restart the level after dealing damage.

Version 0.5.46 makes every roulette loadout temporary. `LoadoutSnapshot`
captures both players before `ApplyLoadout`. The existing `Level._OnPreWin`
prefix restores regular victories, while a new `SceneLoader.LoadLastMap`
prefix restores abandonment before Cuphead's own save call. Rey Dado delays
victory restoration until `DicePalaceMain`; internal subfights and all retry or
reload paths retain the roulette equipment. The inventory is never modified,
so unpurchased equipment remains locked after the original loadout returns.

Version 0.5.49 prevents changing that temporary result from the defeat screen.
A Harmony prefix on `LevelGameOverGUI.ChangeEquipment()` skips the native
Equip Card action while `loanedLoadoutsActive` is true. This condition covers
every loss and retry in the same roulette session without affecting ordinary
fights. Victory or `SceneLoader.LoadLastMap()` restores the original loadout,
clears the flag, and therefore returns normal Equip Card behavior on the map.

Version 0.5.50 freezes map movement while the roulette is visible. A Harmony
postfix forces `MapPlayerController.CanMove()` to return false only when
`visible` is true. `MapPlayerMotor.Update()` then clears its axis, logical
velocity, and `Rigidbody2D` velocity in the same frame, while
`MapPlayerAnimationController` becomes stationary. The static check covers
both players and restores native movement immediately after closing the card.

Version 0.5.51 replaces the static challenge artwork with seven user-supplied
three-frame PNG sequences. All frames are 80x80 ARGB and live in
`assets/modifiers` using the source prefixes `nodash`, `nomini`, `mini`,
`nobombs`, `nopeashooter`, `noex`, and `blacknwhite`. `DrawModifierSlot()`
selects the current file through `AnimatedTexturePath()` at the shared Equip
Card rate of 12.5 FPS. The old static files remain available as legacy assets
and must not be removed without checking packaged builds that may reference them.
While field 5 is still rolling, the challenge slot also draws Cuphead's native
five-frame `equip_icon_sheen` overlay at the same 0.28 alpha as the other slots.

Version 0.5.52 replaces the separate persistent challenge label with
`BattleResultHud.cs`. During a roulette battle it displays the two weapons,
super, charm, and challenge in a right-anchored row at the bottom of the screen,
vertically aligned with Cuphead's health HUD. The row keeps a 26-unit right
margin. In the battle HUD, native equipment and challenge icons intentionally
use only their first frame; their looping Equip Card animation was distracting
during play. Ground battles show all five slots. Plane battles show only charm
and challenge because the first three roulette loadout values are not used by
plane controls. All icons and the challenge text use 0.70 alpha. Icons reveal
one by one at 0.15-second intervals and use the roulette's 0.38-second, 7.5%
sine pulse; the challenge label fades and settles from 1.12 scale after the
final icon. Long labels retain the fixed right edge, cap their width at 420
units, and reduce the font size down to 15.

`BossRouletteUiSaturation.shader` is shipped in the same AssetBundle as the
camera transition shader. `BattleResultHud` feeds it `1 - blackAndWhiteBlend`,
so the added icons and text crossfade to monochrome in sync with the scene.

The HUD is prepared from the map's native prompt before `LoadLevel()` and lives
initially in a `DontDestroyOnLoad` staging Canvas. The root stays hidden until
`LevelHUD.Current.Canvas` exists, then it is reparented into that native camera
Canvas. Cuphead's iris and phase transitions therefore cover the roulette row
exactly like health and super cards; the camera also supplies the gameplay
black-and-white pass without double desaturation. When `PauseManager.state` is
not zero it becomes the first child of `PauseGUI/Background`; on defeat it
becomes the first child of the active `LevelGameOverGUI` background. In both
cases the parent background draws the dark overlay first and later menu children
draw above the HUD. The custom UI saturation shader remains the fallback outside
the native LevelHUD Canvas. If a retry destroys the reparented root, it is
recreated from the staging Canvas.

Do not reset `battleHudWasVisible` or `battleHudRevealStartedAt` when the native
LevelHUD Canvas is only temporarily unavailable. Phase and iris transitions can
disable that Canvas; preserving both values prevents the entry animation from
replaying when it becomes active again. The normal `ShouldShowBattleResultHud`
false path still resets them for a genuinely new load/retry.

Roulette audio uses two separate `AudioSource` components. The continuous spin
source stays at priority 0 and volume 0.45. Transient UI sounds use priority 64;
the 0.209-second selection clip is played at volume 0.45 instead of 0.9 so it no
longer masks the spin loop by roughly 6 dB whenever a field settles. Do not route
selection sounds back through the spin source.

## HUD parry and knockout lifecycle (0.5.53-0.5.54)

Version 0.5.53 resolves the two runtime observations recorded after 0.5.52.
During normal gameplay `PlaceBattleHudOnGameplayLayer()` keeps the row on the
persistent Screen Space Overlay Canvas instead of the camera-rendered
`LevelHUD.Canvas`. This isolates it from Cuphead's parry camera flash. The
method still requires the native Canvas to exist, be enabled, and be active in
the hierarchy, so phase and iris transitions can hide the row normally. Pause
and game-over continue reparenting the same root below their menu content.

The visual lifetime no longer depends on `loanedLoadoutsActive` or
`activeChallenge`. `BeginBattleResultHudSession()` snapshots every roulette
result index plus the challenge name before `LoadLevel()`. The regular victory
hook can therefore restore the original equipment and clear all gameplay
restrictions immediately without erasing the displayed row.

Version 0.5.53 initially reparented the visible row under `LevelHUD` and hid it
as soon as `SceneLoader.CurrentlyLoading` became true. Runtime testing found a
brief transfer blink and confirmed that the loading flag becomes true before
the native HUD has finished darkening.

Version 0.5.54 replaces that transfer with
`TrySwapBattleHudToNativeVictoryLayer()`. It instantiates an inactive copy of
the fully revealed row directly under `LevelHUD.Canvas`, validates and captures
its five `RawImage` components and challenge `Text`, hides the overlay, then
activates the native copy in the same frame. The visible object is never moved,
so its screen position and alpha cannot jump. During this final-victory state,
`ShouldShowBattleResultHud()` deliberately tolerates
`SceneLoader.CurrentlyLoading` while the native root still exists. The native
Canvas therefore darkens and destroys the copy exactly with health and super;
the results scene has no battle `Level.Current`, so it cannot recreate there.

Dice Palace subfight victories do not enable this final-victory path, so the
snapshot remains available for the next internal battle. Returning to the map
through `SceneLoader.LoadLastMap()` explicitly ends the presentation session.

Version 0.5.54 also loads `assets/sounds/impact_01.wav` into
`battleHudImpactClip`. The supplied 32-bit float WAV is normalized for runtime
shipping as 16-bit stereo PCM at 44.1 kHz without changing its 0.834-second
duration. `UpdateBattleResultHudReveal()` tracks
`battleHudImpactPlayedCount` and calls `PlayOneShot` at 0.55 volume exactly once
for every icon whose reveal threshold has passed. Ground fights produce five
sounds and plane fights two. The challenge text has no sound. The counter
resets for a new attempt/session, but not when the HUD is temporarily hidden by
pause, phase, or iris layering.

Runtime testing found that the original waveform itself delayed its audible
impact: it remained below -25 dB for about 97.5 ms, even though
`PlayOneShot()` and the icon reveal occurred in the same frame. Version 0.5.55
rebuilds the shipping WAV from the supplied source with the first 85 ms removed
and a 5 ms fade-in. The resulting PCM file lasts 0.749 seconds and reaches the
same -25 dB threshold after about 12.5 ms. Do not add leading silence to fix
visual synchronization; that moves the perceived impact later. If further
tuning is requested, adjust the audio trim rather than `BattleHudRevealStep`.

## Compact HUD icon row and slower cadence (0.5.66)

The user felt the shot, super, charm, and challenge circles were visually too
far apart even though their rectangles had only a 4-unit gap; the artwork itself
contains transparent breathing room. `BattleHudIconGap` is now `-2f`, producing
a slight rectangle overlap that moves neighboring centers from 52 to 46 units.
Five ground icons therefore shrink from 256 to 232 units overall. Plane HUDs use
the same formula for their two visible icons. `BattleHudTextGap` intentionally
remains `10f`, so the visual space from the final circle to `RETO: ...` is
unchanged.

`BattleHudRevealStep` increases from `0.15f` to `0.45f`, adding exactly 300 ms
between consecutive circle reveals. The first circle still appears at the same
time. Impact playback derives from `revealedIconCount`, so every sound remains
on the same frame as its corresponding circle without further audio changes.
The challenge text delay continues to derive from the reveal step and therefore
waits until the slower icon sequence is complete.

## Louder HUD reveal impact (0.5.67)

`BattleHudImpactVolume` increases from `0.55f` to `0.70f` so the short reveal
impact remains perceptible when the player's game volumes are set relatively
low. This is only the clip's local `PlayOneShot` gain: `effectsAudioSource`
continues through Cuphead's native `sfx` mixer group, so both Principal/Master
and Efectos/SFX still control the final output and setting either to silence
still mutes the impact. No other mod sound level changes.

## HUD cadence and impact retune (0.5.68)

After gameplay testing, the 0.5.66 cadence felt too slow and the 0.5.67 impact
still too quiet. `BattleHudRevealStep` is now `0.35f` instead of `0.45f`, placing
the five ground reveals at 0, 350, 700, 1050, and 1400 ms; the plane pair appears
at 0 and 350 ms. `BattleHudImpactVolume` is now `0.85f`. The compact `-2f` icon
gap, native SFX mixer routing, per-icon impact counter, trimmed WAV, pulse
duration, and challenge-text timing formula are unchanged.

## Delayed HUD sequence start (0.5.69)

`BattleHudInitialRevealDelay = 1f` now holds the complete custom HUD invisible
for one second after its presentation begins. `BattleHudRevealStep` decreases
from `0.35f` to `0.30f`, so ground icons reveal at 1.0, 1.3, 1.6, 1.9, and 2.2
seconds and plane icons at 1.0 and 1.3 seconds. `localElapsed` subtracts both the
initial delay and each icon's step; the impact counter therefore remains silent
during the hold and still fires on each reveal frame. The challenge text delay
also adds `BattleHudInitialRevealDelay`, preventing it from fading in during the
new one-second pause.

## Final HUD reveal timing refinement (0.5.70)

The accepted 0.5.69 rhythm receives a small final adjustment:
`BattleHudInitialRevealDelay` is `1.1f` and `BattleHudRevealStep` is `0.28f`.
Ground reveal timestamps are 1.10, 1.38, 1.66, 1.94, and 2.22 seconds; plane
timestamps are 1.10 and 1.38 seconds. This delays the first visual by 100 ms
while shortening each subsequent gap by only 20 ms. Impact volume remains
`0.85f`; icon gap, pulse duration, text-delay formula, and audio synchronization
are unchanged.

## King Dice HUD session continuity (0.5.71)

King Dice must always be treated as one battle session even though Cuphead
loads a different `DicePalace*` scene for each board space. When
`ShouldShowBattleResultHud()` becomes false during one of those internal loads,
`UpdateBattleResultHud()` now hides the row without clearing
`battleHudWasVisible`, `battleHudRevealStartedAt`, or
`battleHudImpactPlayedCount`. The initial icon animation and its impact sounds
therefore run only after the real entry into King Dice; subsequent minijefes,
scene transitions, and retries restore the already revealed HUD immediately.
`BeginBattleResultHudSession()` remains the only reset at roulette launch, and
`EndBattleResultHudSession()` still clears the state when returning to the map.

`BattleHudUsesDicePalaceChain()` identifies this behavior from the snapshotted
roulette boss instead of the current scene. While that chain is active and the
final-victory flag is false, `PlaceBattleHudInsideMenuLayer()` keeps the row
under the persistent overlay canvas instead of adopting a scene-local pause,
defeat, or transition layer. This prevents later internal fights from inheriting
the native parry flash. Winning the real `DicePalaceMain` fight still calls
`KeepBattleResultHudThroughVictory()`, enables the native-victory path, and lets
the row darken and disappear with Cuphead's original HUD during knockout.

## Fullscreen Equip Card entrance stability (0.5.56)

At 1920x1080 the 1280x720 IMGUI design matrix uses a 1.5 scale. The previous
entrance multiplied `cardRoll` by `cardVisibility` while also moving the card by
an unsnapped fractional Y offset. Unity re-rasterized every IMGUI label, sprite,
and thin line at a slightly different rotated subpixel position each frame;
the paper background concealed this sampling change, but the contents appeared
to deform or move gelatinously until the card stopped.

`DrawRoulette()` now applies the selected `cardRoll` unchanged for the complete
entry/exit and snaps its design-space Y offset through
`Round(rawOffsetY * screenScale) / screenScale`. The whole composition therefore
moves as a rigid card in exact physical-pixel increments. `SetVisible(false)`
no longer clears `cardRoll`; the hidden value is harmless and a new random roll
is still selected at the next open. Preserve this rigid transform unless the
card is migrated away from IMGUI to a single precomposited texture.

## Spin cancellation when closing the card (0.5.57)

Previously, `SetVisible(false)` only animated the Equip Card out. The
frame-driven `UpdateSpin()` state remained active, so the roulette continued
advancing and playing audio while hidden and could finish with a valid result.

Closing the card while `running` now calls `CancelRouletteSpin()` before the
close sound is played. It clears `running`, `pendingLoad`, `resultReady`, the
timers, ticker, reveal count, pulse timers, and the partial `RouletteResult`.
It also stops both the dedicated looping spin `AudioSource` and transient
selection sounds on `effectsAudioSource`, then restores the prompt state to
`PULSA ENTER PARA GIRAR`. This applies equally to F6, the controller shortcut,
Escape, leaving the map, or any future close path routed through
`SetVisible(false)`. Reopening never resumes or accepts the cancelled result;
the player must start a new spin.

## Native audio settings integration (0.5.58)

Cuphead's audio options are not three independent multipliers applied by game
code. `SettingsData` stores `masterVolume`, `sFXVolume`, and `musicVolume`, then
applies them to the exposed `AudioManager` mixer properties `MasterVolume`,
`Options_SFXVolume`, and `Options_BGMVolume`. The master group is the parent of
both categories, so **Principal** affects music and effects; **Efectos** and
**Música** then control their respective child paths.

`RouteModAudioToGameSfxMixer()` assigns both persistent plugin `AudioSource`
instances to `AudioManagerMixer.GetGroups().sfx`, the same group used by
Cuphead's default-channel sound effects. This covers the roulette loop and all
clips played through `effectsAudioSource`: selection stops, local menu fallbacks,
and battle HUD impacts. Because routing occurs through the mixer, live option
changes propagate without polling or manually converting the mixer's decibel
values. Do not additionally multiply source volumes by `SettingsData` values;
that would apply the settings twice. Calls handled successfully by
`AudioManager.Play()` are already native SFX and keep their existing routing.

## Native fight-title localization (0.5.59)

The Equip Card originally localized only the large boss/character name through
`Localization.Find(boss.Level.ToString())`; its smaller fight title came from
the Spanish-only `BossEntry.Fight` field. Cuphead's localization catalog does
not expose these titles through a consistent `IntelMenu...` family. Inspection
of `MapDifficultySelectStartUI.In()` shows the actual native difficulty card
builds the title key by concatenating the level identifier with `Selection`
(and uses `<level>WorldMap` separately for the boss name).

`LocalizedFightName()` requests `<boss.Level>Selection` every time the card is
drawn and normalizes embedded `\\N` line breaks. This per-frame lookup
intentionally mirrors `LocalizedBossName()` and means changing Cuphead's
language updates both visible names without restarting the plugin. Version
0.5.59 originally fell back to `BossEntry.Fight`; 0.5.62 removes that visual
fallback so an unavailable translation leaves the subtitle empty.

## Native fight-title artwork and markup handling (0.5.60)

Runtime inspection of `FrogsSelection` confirmed the complete format split.
English, Korean, Japanese, and Simplified Chinese provide localized title art
through `spriteAtlasName` / `spriteAtlasImageName` (Japanese also carries text).
French, Italian, German, both Spanish variants, Russian, Polish, and Brazilian
Portuguese provide text decorated with TextMeshPro layout tags and transparent
punctuation used for the two-line native card.

`DrawLocalizedFightArtwork()` now mirrors `LocalizationHelper`: when the active
`Selection` translation reports `hasSpriteAtlasImage`, it obtains the atlas via
`AssetLoader<SpriteAtlas>.GetCachedAsset()`, retrieves the named sprite, fits it
without stretching inside the roulette subtitle area, and draws it through the
new direct-`Sprite` overload in `GameTheme`. A direct `translation.image` is
also supported. If no artwork is available, `PlainFightTitle()` removes fully
transparent TMP spans, remaining markup, hidden quote/semicolon kerning
characters, and line breaks before the IMGUI label is drawn. If atlas lookup
fails, the same active-language text path is attempted.

This means all twelve languages use Cuphead's own representation rather than a
mod-maintained translation table. Do not replace the atlas lookup with
`Resources.FindObjectsOfTypeAll<Sprite>()`: `LocalizationHelper` uses the same
`AssetLoader` cache because atlas sub-sprites are not guaranteed to appear in a
global resource scan.

## Uniform localized-title color (0.5.61)

Tinting the localized atlas sprite with `GUI.color` did not produce the same
cream as `equipFightStyle`: multiplication can darken white artwork but cannot
turn already-black pixels into a light color. This made several native titles
appear black while text-backed languages used `secondaryText` (`0.91, 0.86,
0.69`).

`GetTintedFightArtwork()` now copies the sprite's atlas rectangle through a
temporary `RenderTexture`, reads only that small region into a mod-owned
`Texture2D`, replaces its RGB with `equipFightStyle.normal.textColor`, and
preserves the original alpha (including antialiased edges). This GPU-copy first
step is required because Unity's imported atlas textures are not guaranteed to
be CPU-readable. Results are cached in the existing `textures` dictionary by
atlas, image, sprite, and target color, then destroyed by the established
`OnDestroy()` cleanup. Never perform `GetPixels32()` directly on the source
atlas and do not rebuild the tinted texture per frame.

## Empty missing-localization behavior (0.5.62)

The user explicitly prefers no fight subtitle over a title in the wrong
language. `LocalizedFightName()` therefore returns `string.Empty` when the
active `<level>Selection` entry has neither usable artwork nor usable text, or
when localization is temporarily unavailable. `BossEntry.Fight` remains in
`RouletteData` as internal Spanish reference data but is no longer a rendering
fallback. Preserve this distinction in future localization changes.

## Larger native fight-title artwork (0.5.63)

The initial atlas-art bounds (`x=67, y=303, width=461, height=34`) made several
native titles look materially smaller than the text-backed subtitle, especially
when the sprite itself included internal breathing room. Artwork now fits within
`x=54, y=305, width=487, height=46`: full subtitle width and about 35% more
maximum height. Aspect ratio and centering remain unchanged. The lower placement
keeps the larger art from covering the large boss name and still ends before the
equipment circles begin. Do not apply these bounds to `equipFightStyle`; the
text-backed path intentionally retains its original 487x24 rectangle.

## Spanish-only fight subtitle policy (0.5.64)

The user chose a simpler final presentation after seeing that Cuphead's
`Selection` atlas art contains both the boss name and the fight title, which
duplicated the already-localized large boss name on the roulette card. The
fight subtitle is now drawn only when `Localization.language` is
`SpanishSpain` or `SpanishAmerica`; both use the cleaned native
`<level>Selection` text. Every other language intentionally shows only the
large localized boss name and leaves the subtitle area empty.

Version 0.5.64 removes `DrawLocalizedFightArtwork()`, the atlas lookup, GPU
recoloring/cache, and the direct-`Sprite` `GameTheme.DrawSprite()` overload
introduced in 0.5.60-0.5.63. Those sections remain above as implementation
history, not current behavior. Preserve the Spanish-only rule unless the user
explicitly supplies or approves a text catalog for other languages.

## Spanish-Spain missing-title fallback (0.5.65)

At least one Spanish-Spain `Selection` entry may not expose usable text. The
user wants that isolated gap filled with the mod author's existing Spanish
title instead of leaving it blank. `LocalizedFightName()` records whether the
active language is `SpanishSpain`; if the native lookup, cleanup, or resource
availability produces no title, it returns `boss.Fight`. This fallback does not
apply to `SpanishAmerica` or any other language. Keep it narrowly scoped so the
general no-language-mixing policy from 0.5.64 remains intact.

Manual acceptance checks: parry repeatedly during a roulette fight and confirm
the row remains visually steady. Confirm each ground icon produces one impact
sound and the challenge text produces none; repeat in a plane fight for exactly
two impacts. Defeat the boss and confirm the row neither blinks nor moves, stays
fixed while the screen darkens, and disappears with the health HUD before the
results card appears. Also recheck iris, pause, defeat/retry, Blanco y negro,
and the final Dice Palace victory.

Version 0.5.43 adds automatic base-game/DLC compatibility. Each boss and
equipment entry now records whether it requires The Delicious Last Course.
Whenever the roulette opens or starts a spin, the plugin refreshes Cuphead's
own entitlement state through `DLCManager.RefreshDLC()` and reads
`DLCManager.DLCEnabled()`. If that check is false or throws, the safe fallback
is base-game-only content.

The DLC-only entries currently represented by the roulette are:

- Bosses: Las Alimañas, Esther Espuelas, Los Perritos Pilotos, Ángel y
  Demonio, Genovevo de Gelante, Granitoviejo el Gigante, and Chef Saleroso.
- Weapons: Tiro Certero, Convergencia, and Ciclónica.
- Charms: Galletita Astral, Reliquia Divina, and Anillo de Corazón.
- Supers: none. The three selected super slots exist in the base game; Ms.
  Chalice's variants are reached through Galletita Astral, which is already
  DLC-only.

Availability pools drive both `CreateRandomResult()` and the rolling card
animation, including the forced challenge boss selector. Therefore a base-only
player never sees or receives a DLC portrait, weapon, or charm, while a DLC
owner retains the full pool. `Nada` remains available for super/charm display
and probability exactly as before. The temporary challenge selector is empty
in 0.5.43, restoring normal boss and challenge selection after the completed
`Solo mini avión` test.

Manual end-to-end checks still recommended:

1. Spin with RETO enabled until a non-`Nada` challenge is selected.
2. Confirm the bottom-right label appears in the fight.
3. Lose and retry; confirm it remains.
4. Quit to map; confirm it disappears and F6 prompt returns.
5. Win; confirm it disappears before/at the victory flow.

## Native prompts and overlay

`NativeMapPrompt.cs` clones Cuphead's PauseGUI help prompt. It is used for:

- `ABRIR RULETA` + F6 on the map
- `VOLVER A GIRAR` + F7 when applicable
- The persistent informational challenge prompt during a fight

The F6/F7 capsule dimensions and text offset were tuned by the user. Preserve:

- Minimum key width: 30
- Key padding: 2.5
- Key text anchored offset: `(-10, -0.3)`
- Right edge constant: 1290
- Text-to-key gap: 4.5

The roulette dim layer must be behind the card and the native F7 prompt must be
in front of it.

Version 0.5.44 adds the controller open/close combo. `Plugin.cs` reads
Cuphead's `EquipMenu` action from each `Rewired.Player`, then scans only that
player's assigned joysticks for the physical left trigger (`Left Trigger`,
`L2`, or `ZL`). Axis and digital-button triggers are both supported. The combo
uses `GetButtonDown`, so one press toggles exactly once even while the trigger
remains held. `BlockMapPausePostfix` also rejects the native Equip Card while
the trigger is held, preventing the same Equip press from opening both cards.

Version 0.5.47 adds full controller navigation. It reads Cuphead's native
Rewired menu actions for both players: `MenuUp`, `MenuDown`, `MenuLeft`,
`MenuRight`, `Accept`, and `Cancel`. This supports D-pad and stick mappings
without hard-coding face-button numbers. Reroll scans assigned joysticks for
the physical right trigger labels (`Right Trigger`, `R2`, or `ZR`) and supports
both axes and digital buttons. A stored held-state turns it into a rising-edge
press, preventing repeated spins while the trigger remains held.

## Card layout invariants

These values were adjusted repeatedly by the user and are considered final:

- Shot A center: `(98.4, 399)`
- Shot B center: `(199.1, 399)`
- Super center: `(298.9, 399)`
- Charm center: `(397.7, 399)`
- Challenge center: `(497.1, 399)`

They are defined near the top of `EquipCardLayout.cs`. Do not change them
unless the user explicitly requests it.

The card background is:

`assets/card/roulette-card.png`

It was exported from the supplied PSD and includes the boss portrait circle.
Do not move other content when replacing the background.

## Native game assets

Weapon, super, charm, empty-slot, sheen, and cursor sprites are resolved from
Cuphead resources through `GameTheme`.

Selected equipment icons use three-frame animations. The original Equip Card
was measured live at about one frame every 0.08 seconds, or 12.5 FPS. The mod
uses `EquipIconFramesPerSecond = 12.5f`.

Do not change sheen/cursor speed when changing equipment icon speed; they are
separate animations.

Boss portraits and challenge artwork come from the user's web assets.

## Audio

Runtime audio uses WAV files because Unity 2017 reliably loads those in this
environment:

- `assets/sounds/spin.wav`
- `assets/sounds/selection.wav`
- `assets/sounds/abrir.wav`
- `assets/sounds/cerrar.wav`

These are decoded versions of the website audio. The original MP3 files may
also exist in the installed asset folder.

Spin audio and one-shot effects use separate `AudioSource` objects. This is
important: using one source caused the final stop sound to be cut off when the
looping spin audio stopped.

## Source file responsibilities

- `Plugin.cs`
  - BepInEx entry point
  - config and input
  - roulette state machine
  - loadout application and level loading
  - challenge state/lifecycle
  - sound loading
- `EquipCardLayout.cs`
  - current Equip Card-style roulette rendering
  - exact positions and native icon animation
- `NativeMapPrompt.cs`
  - native F6/F7 labels
  - dim overlay
  - persistent fight challenge label
- `RouletteData.cs`
  - bosses, weapons, supers, charms, modifiers
  - native sprite names and asset paths
- `GameTheme.cs`
  - fonts, native sprite lookup, paper/theme helpers

`DrawRouletteLegacy` still exists in `Plugin.cs` but is not the active card
renderer. Avoid editing it unless deliberately removing legacy code.

## Previous regressions to avoid

- Do not globally consume or disable Shift. It broke the original Equip Card.
- Do not leave `MapEquipUI` disabled after the roulette closes.
- Do not let Esc close the roulette and also open the pause menu.
- Do not open the roulette automatically when Cuphead starts.
- Do not show F6 outside the map.
- Do not clear the active challenge on defeat; retry must retain it.
- Do not show a challenge on the map after victory/exit.
- Do not modify the five finalized equipment coordinates casually.
- Preserve UTF-8 source encoding; earlier mojibake produced broken accents.

## Design preferences from the user

- Faithful Cuphead presentation is more important than a generic modern UI.
- Prefer actual game fonts, sprites, prompts, sounds, and animations.
- No large custom rectangles when native labels can be reused.
- Keyboard-only navigation should feel like the Equip Card.
- The card should enter/exit like Cuphead's card and should not use a heavy
  custom drop shadow.
- The boss image is smaller than Cuphead/Mugman on the original Equip Card.
- Boss and level names should use game localization where possible.
- Challenge restrictions are informational; they do not need to enforce input.

## Verification status at handoff

- Version 0.5.71 builds with zero errors and zero warnings and was installed
  and launched successfully on 2026-08-05. `BepInEx\LogOutput.log` confirms
  `Loading [Gilomx Boss Roulette 0.5.71]` and the normal ready message.
- The ready-to-paste artifact is
  `dist/Gilomx-Boss-Roulette-0.5.71-BepInEx-x64.zip` (10,575,732 bytes,
  SHA-256 `8DB50162FBDEF4E630C18543FEB3072471F4CCD26FE66207D27392BD6361F217`).
  Its 122 entries were inspected: it contains the x64 Doorstop bootstrap,
  18 BepInEx core files, the 0.5.71 DLL, the complete asset tree including
  `impact_01.wav`, and no config, log, cache, save, or unrelated plugin files.
- Manual 0.5.71 King Dice acceptance: confirm the icon/audio entry sequence
  runs once at the real start, never repeats during internal `DicePalace*`
  scene changes or retries, and remains steady during parries in later
  minijefes. The final `DicePalaceMain` knockout must still darken and remove
  the row with Cuphead's native health HUD.
- Version 0.5.48 builds with zero errors and zero warnings when
  `CupheadDir` points to the current installation on `E:`.
- The temporary version 0.5.30 was installed and reproduced the frozen Dragon
  fight even after the safer ground `CanUseEx` change.
- Version 0.5.31 was installed and launched successfully on 2026-07-30.
- `BepInEx\LogOutput.log` confirms:
  `Gilomx Boss Roulette 0.5.31 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.32 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.32 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.33 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.33 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.34 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.34 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.35 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.35 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.36 was installed and launched successfully on 2026-07-30.
- The log confirms `Gilomx Boss Roulette 0.5.36`, reports two plugins
  (`Blender` and Boss Roulette), and no longer loads `CupheadModdingTemplate`.
- Version 0.5.37 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.37 listo. F6 abre/cierra; F7 gira.`
- Manual 0.5.37 test: every spin must select an airplane boss plus
  `No mini avión`; shrink input must do nothing before and after defeat/retry,
  then work again after victory/exit in a non-challenge fight.
- Version 0.5.38 was installed and launched successfully on 2026-07-30.
- The log confirms:
  `Gilomx Boss Roulette 0.5.38 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.39 was installed and launched successfully on 2026-07-30; the
  log confirms it loaded. The forced `No disparo bombas` test must never select
  Los Perritos Pilotos.
- Version 0.5.40 was installed and launched successfully on 2026-07-30; the
  log confirms version 0.5.40.
- Version 0.5.41 was installed and launched successfully on 2026-07-31; the
  BepInEx log confirms:
  `Gilomx Boss Roulette 0.5.41 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.42 was installed and launched successfully on 2026-07-31; the
  BepInEx log confirms:
  `Gilomx Boss Roulette 0.5.42 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.43 was installed and launched successfully on 2026-08-02; the
  BepInEx log confirms:
  `Gilomx Boss Roulette 0.5.43 listo. F6 abre/cierra; F7 gira.`
- Version 0.5.44 was installed and launched through Steam successfully on
  2026-08-03. The BepInEx log is newer than the Cuphead process and confirms:
  `Gilomx Boss Roulette 0.5.44 listo. F6 o gatillo izquierdo + Equip abre/cierra; F7 gira.`
- Manual 0.5.44 controller test: on the map, hold physical `ZL` and press
  physical `X` on the Switch Pro. Confirm the roulette opens once and the
  native Equip Card does not open. Repeat to close it, then release `ZL` and
  confirm `X` still opens the native Equip Card normally.
- Manual 0.5.40 test: every spin must select `No disparo Peashooter`; normal
  and Chalice players must start on bombs, weapon switching must not return to
  Peashooter before or after retry, and normal switching must return after
  win/exit.
- Manual 0.5.45 test: mini-plane shots and airplane supers must deal damage
  without restarting. A large-plane shot, bomb, or EX must deal its normal
  damage and then restart the level while retaining the challenge.
- Manual 0.5.46 test: equip a known loadout, spin equipment that includes at
  least one unpurchased item, and enter the fight. Lose/retry and confirm the
  roulette equipment remains. Win and confirm the original equipment returns
  on the map and in the Equip Card. Repeat by abandoning to the map. Restart
  Cuphead and confirm the restored loadout persisted while the loaned item is
  still locked. Finally test Rey Dado: roulette equipment must remain through
  every internal subfight and restore only after winning `DicePalaceMain` or
  abandoning the run.
- Manual 0.5.47 controller test: open with the existing left-trigger + Equip
  combo, navigate all four rows with D-pad and left stick, change every setting
  with left/right and `Accept`, then use `Accept` on `GIRAR` and `JUGAR`.
  With automatic load disabled and a completed result waiting, press physical
  `ZR`/`RT`/`R2` once and confirm exactly one new spin begins. Confirm the right
  trigger does nothing during a spin and while automatic load is enabled.
- Manual 0.5.48 test: select `Blanco y negro` in one ground fight and one
  airplane fight. Confirm each attempt remains in color for 1.5 seconds, fades
  continuously into the native black-and-white look and back after victory
  or abandonment. After defeat/retry, confirm the next attempt repeats the
  delay and fade. Repeat while the player's saved filter is `Two-Strip` and
  confirm that setting returns unchanged on the map and after restarting
  Cuphead.
- Manual 0.5.49 test: start any fight through the roulette, lose, and press the
  native Equip Card button on the defeat screen. Confirm that the card does not
  open and retry uses the exact roulette loadout. Then exit to the map and
  confirm the normal Equip Card opens again with the restored original loadout.
- Manual 0.5.50 test: start walking and open the roulette before releasing the
  direction. Confirm both the movement and walking animation stop immediately.
  Try every direction and, in multiplayer, both players. Close the card and
  confirm normal movement returns without opening the native Equip Card.
- Manual 0.5.41 test: every spin must select `Solo mini avión`; changing size
  remained available and non-mini damage was suppressed. This test behavior
  was superseded by 0.5.42.
- Manual 0.5.42 test: every spin must select `Solo mini avión`; changing size
  must remain available and mini-plane shots must damage normally. A
  large-plane shot, bomb explosion, EX, or super must deal its normal damage
  and then restart the level with the challenge label still active. Also verify
  the projectile-time rule by changing size while a shot is in flight, then
  confirm normal unrestricted play returns after win/exit.
- Manual 0.5.43 test with DLC enabled: the BepInEx log must report that base and
  DLC content are in use, and DLC bosses/equipment must continue appearing.
- Manual 0.5.43 test without DLC: the log must report base-only mode; no DLC
  boss, weapon, or charm may appear during the animation or as the final
  result, and the selected base boss/loadout must load successfully.
- Manual 0.5.38 test: every spin must select `No disparo bombas`; normal
  Peashooter/Chalice three-way fire must work, weapon switch must not select
  bombs before or after retry, and switching must return after win/exit.
- Other unrelated BepInEx plugins are also installed; verify that exactly one
  `Gilomx Boss Roulette` instance loads.
- The persistent challenge label requires the manual five-step gameplay test
  listed above.
- Version 0.5.27 still needs manual gameplay verification:
  complete a Rey Dado minion and confirm the label persists through the next
  fight and final boss; test `No Dash` before and after defeat/retry, then
  confirm dash returns after victory or exiting to the map.
- Version 0.5.28 needs manual `No EX` verification in one ground and one
  airplane fight: normal fire and supers should work, EX should neither fire
  nor spend a card, retry should retain the block, and win/exit should remove
  it.
