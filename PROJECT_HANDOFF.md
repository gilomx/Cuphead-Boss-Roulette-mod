# Cuphead Boss Roulette - Project Handoff

Last updated: 2026-07-31
Current local version: 0.5.42

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

This handoff documents the challenge implementation through version 0.5.42.
Always inspect `git status` before editing, and do not reset, restore, or
overwrite unrelated user changes.

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

## User-facing behavior

- The roulette can only open after loading a save and while freely walking on a
  map.
- It must not open at the title screen or inside a fight.
- `F6` opens/closes the roulette.
- Arrow keys move/change options.
- `Enter` selects.
- `Esc` closes the roulette without also opening Cuphead's pause menu.
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
- `F7` shows a native `VOLVER A GIRAR` prompt and allows another spin.

Airplane bosses still roll and equip weapon A, weapon B, super, and charm.
Those ground loadout values do not affect airplane weapons, but leaving all
slots populated looks better and is the behavior requested by the user.

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
  valid. The same detection covers large-plane shots, bomb explosions, EX
  attacks, and supers. The active challenge is retained across the automatic
  restart.
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

- Version 0.5.42 builds with zero errors and zero warnings when
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
- Manual 0.5.40 test: every spin must select `No disparo Peashooter`; normal
  and Chalice players must start on bombs, weapon switching must not return to
  Peashooter before or after retry, and normal switching must return after
  win/exit.
- Manual 0.5.41 test: every spin must select `Solo mini avión`; changing size
  remained available and non-mini damage was suppressed. This test behavior
  was superseded by 0.5.42.
- Manual 0.5.42 test: every spin must select `Solo mini avión`; changing size
  must remain available and mini-plane shots must damage normally. A
  large-plane shot, bomb explosion, EX, or super must deal its normal damage
  and then restart the level with the challenge label still active. Also verify
  the projectile-time rule by changing size while a shot is in flight, then
  confirm normal unrestricted play returns after win/exit.
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
