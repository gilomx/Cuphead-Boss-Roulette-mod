# Cuphead Boss Roulette - Project Handoff

## Ink Rain tuning, lifecycle, and rotating Dogfight support (2026-08-12)

The experimental `InkRain` challenge remains enabled and forced from
`ExperimentalFeatures.cs` while arena-wide acceptance continues. The temporary
boss selector used for Dogfight validation has been disabled again, so spins
choose bosses normally. The roulette now keeps a session-only history of the
last three boss results and excludes them when the compatible pool permits it;
small pools progressively relax the oldest exclusions rather than failing.

Accepted tuning at this checkpoint:

- Easy: 4 visible drops, waves every 1.00-1.25 seconds, 85% single and 15%
  double waves.
- Normal: 12 visible drops, waves every 0.65-0.90 seconds, 60% single, 34%
  double and 6% triple waves.
- Expert: 20 visible drops, waves every 0.40-0.75 seconds, 53% single, 36%
  double, 9% triple and 2% four-drop waves.
- Regular drops use random downward gravity of 0.22-0.32 camera-heights per
  second squared. Ink holds for 2.0 / 2.2 / 2.5 seconds on Easy / Normal /
  Expert and restores over 3 seconds.
- Multi-drop vertical delay is 20%-40%; Normal applies the same variation to
  every wave. The horizontal spawn strip is symmetric from -5% to 105% of the
  visible world span, including evenly partitioned multi-drop waves.

Victory now holds rain and darkness through Cuphead's knockout/loading fade and
clears them only once the battle transition has actually left the scene. Defeat
keeps the natural ink restoration instead of abruptly clearing it. Results and
map scenes receive no new rain. King Dice sublevels restart regular rain without
replaying the squid introduction; internal minion knockouts preserve the same
challenge session until `DicePalaceMain`. The Dice Palace guards must continue
to test `Levels.DicePalaceMain` specifically.

`Los Perritos Pilotos` needed special handling because its final phase rotates
the camera counter-clockwise while world gravity and the visual sky rotate
around the monitor. Spawn bounds are derived from all four camera viewport
corners, but drops continue to move in world space. Therefore their apparent
source follows the correct sky side in every orientation: screen top, left,
bottom, right, then top. Radius projection now uses the full 2D screen-space
distance, preventing sprites from shrinking at 90/270 degrees. Regular drops
are removed against the world-space exit edge captured when they spawn, rather
than viewport X/Y limits; this prevents existing drops from being culled during
a rotation and preserves the perceived density. A seven-second lifetime remains
the safety fallback. The final symmetric -5%-105% spawn strip fixes the visible
left/right bias in the first and third rotations. Manual testing accepted this
behavior provisionally.

Angel and Demon now resolves its return target through the DLC
`MapGraveyardHandler`, so victory or abandonment returns to the graveyard rather
than the previously visited boss door.

### Required follow-up after this checkpoint

1. Perform another complete `Los Perritos Pilotos` run and inspect every
   transition, especially spawn coverage at both lateral edges, density during
   rotation, world-sky direction, sprite size, collision and cleanup.
2. Run a broad regression pass across ground bosses, ordinary plane bosses,
   King Dice sublevels, retry, pause, knockout, results, map return and co-op.
   Confirm the four-corner bounds and world-edge lifetime introduced for
   Dogfight do not change ordinary arenas or leak rain into later scenes.
3. Keep the Ink Rain challenge experimental and forced only for testing until
   that review passes; then disable `ForceInkRainChallengeForTesting` before a
   public build.
## Experimental Ink Rain challenge (2026-08-11 handoff)

`ModifierId.InkRain` is a first playable ground-and-plane prototype named
`LLUVIA DE TINTA` in Spanish and `INK RAIN` in the other localization tables.
`RouletteData` currently points to `modifiers/inkrain_01.png`. The three prepared
icon frames now show three native ink blobs in separate, deliberately
non-collinear lanes. Each travels down-left with its own trail leaning up-right,
so the group reads as simultaneous diagonal rain rather than three poses of one
projectile; only frame 01 is
currently referenced by the roulette/HUD. The feature
and its forced test selector are deliberately still enabled in
`ExperimentalFeatures.cs` so the next session always rolls this challenge.
Disable `ForceInkRainChallengeForTesting` after the remaining acceptance work,
and keep the challenge experimental until every arena has been checked.

Runtime implementation lives in `InkRainChallenge.cs`. `Plugin` installs its
Level-init Harmony hook, updates the runtime every frame and clears it through
the normal active-challenge lifecycle. The runtime is created only when the
roulette-selected challenge is `InkRain`; battle end, scene loading, victory,
map return and plugin destruction reset drops, screen ink and loaded textures.
This lifecycle previously required several fixes because leaked runtime state
could keep spawning drops on results, the map or later non-roulette battles.
Do not weaken the `activeChallenge == ModifierId.InkRain` and battle-session
guards while adjusting visuals.

### Native behavior recovered from Cuphead

The original classes were inspected directly in `Assembly-CSharp.dll` with
Mono.Cecil. `PirateLevelSquidProjectile` stores velocity and gravity, advances
its position every frame and subtracts gravity from vertical velocity. Its
`OnTriggerEnter2D` calls `PirateLevelSquidInkOverlay.Hit()` for `PlayerId.One`
or `PlayerId.Two`; for a collider named exactly `Level_Ground` it calls
`Die()`. `Die()` disables the projectile collider and sends the `OnDeath`
trigger to the same Animator. It does not use `Pirate_Squid_Splash` (that is a
separate white water effect).

`PirateLevelSquidInkOverlay.Hit()` raises the target darkness by 0.4, clamps it
to 1.0, rises over 0.4 seconds, holds for 2.8 / 3.3 / 4.0 seconds on Easy /
Normal / Expert and fades over 5 seconds. The mod mirrors those timings and
chooses one of the three recovered native screen-splat layouts. The five native
screen animation families `a` through `e` run at 12 fps with their individual
completion durations.

### Integrated native assets and rendering

The committed `assets/inkrain` folder contains:

- `projectiles`: 36 exact `pirate_squid_inkblob` Sprite exports used at 24 fps;
- `screen`: the full-screen ink veil plus the normalized `a`-`e` screen splats;
- `impacts`: four `pirate_squid_ink_death_[a-d]` variants, seven frames each at
  24 fps. Their native pivots were normalized to a 214 x 60 transparent canvas
  anchored at the lower center, preserving one particle that extends one pixel
  beyond the nominal 212-pixel width;
- `squid`: 18 native entrance frames, 19 numbered frames split by the game into
  a 3-frame attack opening plus a 16-frame attack loop, and 22 leave frames.
  The runtime reconstructs the native 29-frame exit from numbered frames 4-10
  followed by leave frames 1-22. All 59 exported PNGs use Cuphead's fixed
  `620 x 620` canvas and original lower-center pivot.

Assets were exported from the installed game with AssetStudio 2.4.1 using
`Sprite:Both`, not rectangular atlas crops. Rectangular extraction caused
neighboring pirate/ship artwork to leak into transparent frames and must not be
used again.

The squid introduction sprites were extracted from the same installed
`atlas_piratelevel` bundle with UnityPy 1.25.3. They are original Cuphead Sprite
exports rather than screenshots or recreated art.

The full-screen veil and animated splats are now composed by
`InkRainPreFilmRenderer`, using a command buffer at
`CameraEvent.BeforeImageEffects`. This matches the native pirate ordering: the
ink enters the camera image first, then Cuphead's animated film effects add their
grain, dust, scratches, chromatic aberration and selected filter. Previously the
same sprites were drawn in `OnGUI`, after post-processing, which made maximum
ink look unnaturally flat. The first integration moved only the screen ink and
left drops/ground impacts in late `OnGUI`; manual testing exposed that those
elements then appeared in front of the blackout and changed the perceived size
of the hit splats. Drops, ground impacts and the full-screen veil now use the
pre-film command buffer. `OnGUI` draws their complete fallback only when that
renderer is unavailable.
The renderer uses
Cuphead's `Sprites/Default` shader with `Unlit/Transparent` as a secondary
choice; if neither is supported, the old `OnGUI` screen-ink path is retained as
a runtime fallback. Drawing each animated hit-splat sprite directly through the
command buffer was rejected in manual testing: the unusually tall source canvases
were visibly flattened and material-property reuse made frames look repeated.
The runtime now rasterizes the whole group first into a screen-sized transparent
`RenderTexture` during the repaint event using `Graphics.DrawTexture`, the exact
sprite UVs, the original `0.65` horizontal / `0.115` vertical scale, positions,
frame rate and stagger. On the following frame, the command buffer composites
that one flat texture before the full-screen veil and film effects. The deliberate
one-frame latency avoids per-sprite command-buffer distortion while retaining the
accepted GUI footprint and correct layer order. Resolution changes recreate the
temporary surface; reset/scene exit releases it. The literal old
`GUI.DrawTextureWithTexCoords` implementation remains in the all-GUI fallback.
This arrangement compiles cleanly and awaits manual visual validation.

Screen-splat scale accepted manually is `SplatVisualScaleX = 0.65` and
`SplatVisualScaleY = 0.115`. The full-screen darkness must render after the
falling drops, ground impacts and screen splats. Rendering it before the splats
made their translucent light fringe appear as a white halo; drawing the veil
last removed the halo and was manually approved.

The current test trajectory enters near the upper-right, moves left with
horizontal velocity between `-0.20` and `-0.14` camera-heights per second,
starts downward between `0.15` and `0.22`, and applies downward gravity between
`0.22` and `0.28`. This produces the requested curved diagonal fall instead of
a nearly vertical line. These numbers are provisional and need gameplay tuning.

### Native squid introduction (implemented, awaiting gameplay validation)

The introduction must not modify Cuphead's gameplay timing. There is no patch on
`Level.LevelIntroTime`; the original one-second pre-Ready window remains intact.
`PlayerStatsManager.LevelInit()` now starts the sprites immediately after it
configures the Ink Rain runtime, with an explicit 1.0-second visual/audio delay.
At that point Cuphead has created the battle level but still covers it with its
loading presentation. The sequence keeps its complete duration and starts 1.0
seconds later than the immediate-`LevelInit` test; the first sprite and native
entrance sound both wait for the same scheduled time. This does not change any
gameplay clock. `Level._OnTransitionInComplete()`
remains a no-op-safe fallback for special scenes where the early call cannot
start. The sprites run at their
native 24 fps. Fitting all 72 displayed source frames into one second would
require an unwanted `10/3` speed-up, which manual testing rejected. The runtime
therefore plays the complete source sequence concurrently with Cuphead's normal
startup: 18 entrance frames (0.75 s), 3 attack-opening frames (0.125 s), 22
displayed frames from the native 16-frame loop (0.917 s), and the reconstructed
29-frame exit (1.208 s). `Ready/Wallop` begins on Cuphead's own unchanged
schedule while the squid continues. The only transform movement is the native
20-unit sinusoidal bob from `PirateLevelSquid.Update()`; the exit artwork carries
the squid out of view at the same 24 fps as its entry.
No additional squid artwork was missing; the problem was the earlier clip split
and projectile-origin synchronization. After the first visual test,
the lower-center anchor moved from 82% to 50% of screen width and its scale
doubled from 55% to 110% of the exported sprite size relative to a 720p frame.
The vertical anchor is now 4% below the viewport (`-0.04`) so the naturally
cropped tentacle ends continue below the screen instead of making the sprite's
lower boundary visible.

The direct command-buffer sprite draw was invisible during Cuphead's camera
transition. A temporary `OnGUI` path proved the animation but placed it above
the film effects and therefore made it look unusually clean. A subsequent
full-screen RenderTexture attempt was visible before the film effects, but
double-composited the alpha and presented the previous frame, causing a pale
appearance and visible trembling. The current path uses a real `SpriteRenderer`
in front of the gameplay scene. Its 59 sprites are created with their original
lower-center pivot, and its world scale is calculated from the camera viewport
to preserve the requested screen size. The original exports were tightly
cropped to different dimensions, even though Cuphead defines every frame on the
same 620 x 620 canvas. All 59 PNGs were therefore rebuilt on that native canvas
using each Sprite's `textureRectOffset`; this preserves the real per-frame
alignment. The actor is also parented to the gameplay camera, eliminating
relative motion caused by Cuphead finishing its camera transition after
`Update`. Cuphead now applies its film effects to the actor once, without a
frame of latency. Drops, impacts, splats and the veil retain their approved
ordering.

`PlayerStatsManager.LevelInit()` records the level instance, configures the
runtime and calls `BeginInkRainSquidIntroOnce()`. That call schedules the actual
start for `Time.time + 1.0`; the rain window remains relative to the delayed
animation start. The guard is now session-based rather than derived from
`Level.Current.GetInstanceID()`: Cuphead can replace or temporarily omit that
object while constructing one battle, which previously made a second
`LevelInit` look like a new session. The first `LevelInit` configures the
session; later calls and the transition-complete fallback cannot reset or replay
the squid. Defeat/retry, exit or a genuinely new battle clears both booleans.
While `SceneLoader.CurrentlyLoading`
remains true for that last fade, the plugin preserves the session and updates
only the squid, its harmless intro drops and the pre-film compositor. It does
not scan players or advance screen ink.

Retry required a separate reset path. `ClearInkRainChallengeSession()` is tied
to clearing the active roulette challenge and is not called by Cuphead's normal
pause-menu/defeat reload, so the first session guard initially survived retry
and blocked both squid and rain. `ResetChallengeVisualsForReload()` now also
calls `ResetInkRainChallengeForRetry()`: it disables and clears the runtime
behind the existing opaque restart fade, resets both session guards and lets the
next `PlayerStatsManager.LevelInit()` create exactly one fresh attempt. Multiple
`LevelInit` calls inside that reload still share the newly configured guard.

The opening rain now follows the native animation events rather than a manually
tuned window. The 18-frame entrance lasts 0.75 seconds, but its
`OnEnterAnimationComplete` event occurs on frame 17 at exactly `16 / 24`, or
0.6667 seconds. At that event the attack-loop sound starts and the first blob is
created immediately from the initial native `InkOrigin` local position `(46,
368)`. The visual attack clip begins at 0.75 seconds, plays the pop sound on its
first frame and reaches the 16-frame attack loop at 0.875 seconds. That loop
contains a compressed streamed Transform curve which was easy to miss because
`m_PositionCurves` itself is empty. Its path CRC `2960652783` is exactly the
CRC32 of `InkOrigin`, and it moves the child through all 16 nozzle positions.
The mod now evaluates Cuphead's original cubic coefficients for X/Y on every
spawn, including interpolation between the 24-fps keys. Therefore the first
blobs precede the opened bottle from `(46, 368)`, then the emission point jumps
to the sprayer and travels with it exactly as drawn. There is one animated
origin, not a second projectile source.

Subsequent intro blobs use Cuphead's exact per-difficulty delays: 0.21 seconds
on Easy and 0.12 seconds on Normal or Expert. Overdue ticks are processed in
order so frame-rate changes do not alter the stream. The temporary ceiling of
20 remains only as a safety limit and is not normally reached with the native
cadence. Emission stops when the shortened introduction enters its exit at
`0.75 + 0.125 + 0.9167 = 1.7917` seconds, matching the original rule that the
attack coroutine stops when the squid changes to its Exit state. The complete
visual still lasts 3 seconds instead of retaining the real boss enemy for its
native 5.5/7.5-second attack, so the approved non-blocking level introduction is
not extended. At that cutoff
the active ceiling immediately returns to the difficulty's `3/4/13`; no regular
wave is added until enough existing drops have left the screen or hit the
floor. They use the horizontal/vertical velocity
ranges and gravity that
`LevelProperties.Pirate` assigns to Easy, Normal or Hard. After the squid
leaves, `SpawnWave()` automatically resumes the regular approved top-right
origin, wave-size probabilities and `NextSpawnDelay()` cadence. These drops
move and collide with the floor normally but cannot ink a player while Cuphead
still has player control locked. Player ink effects
remain disabled beyond the intro until exactly one scaled second after
`Level.PlayAnnouncerBegin()` starts Cuphead's `Wallop` announcement. Drops remain
visible and continue their normal movement/floor collisions during this grace
period, but pass through players without adding darkness, splats or blackout
audio. `_OnLevelStart()` starts the same one-second grace only as a fallback for
Dice Palace, Tower of Power or another special scene that omits the announcer
call; it never extends a grace period already started by `Wallop`. Native audio
keys `level_pirate_squid_enter`,
`level_pirate_squid_attack_pop`, `level_pirate_squid_attack_loop` and
`level_pirate_squid_exit` are used, so the game's effects/master volume settings
remain authoritative. Reset and scene exit always stop the loop.

The squid actor also reproduces the native root motion from
`PirateLevelSquid.Update()`: it moves 20 source units downward and back with
`easeInOutSine(PingPong(t, 1))`, a complete two-second cycle. The offset is
converted through the approved 2x visual scale and current camera resolution,
so the sprite and its animated `InkOrigin` move together without changing the
projectile trajectories after creation. The root bob and the 16-key child curve
are applied independently, just as in the native prefab plus Animator.

Cuphead pauses gameplay by setting `CupheadTime.GlobalSpeed` to zero rather than
relying on Unity's `Time.timeScale`, so checking only `Time.deltaTime` allowed
the virtual rain to keep moving. The runtime now returns immediately whenever
`GlobalSpeed <= 0`. On unpause it shifts every absolute gameplay clock by the
paused duration: next rain spawn, squid sequence, damage grace, player scan,
ground-impact animation and screen-splat animation. Drop age, velocity, gravity,
ink interpolation and hold time were already delta-driven and therefore remain
unchanged while the update is suspended. The paused frame stays rendered in
place instead of disappearing or catching up afterward.

This does not instantiate `PirateLevelSquid`: no enemy HP, collider, damage
receiver, Pirate `LevelProperties`, physical projectile prefab or boss event is
created. Fitting the visual inside Cuphead's existing pre-battle window avoids
changing `LevelIntroTime`, `Time.timeScale` or the start of player/boss control.
If any of the 59 PNGs
is missing, the runtime logs a warning, skips the visual and releases regular
rain instead of leaving spawning disabled.

### Ground-impact trigger fix (validated in Beppi)

The mod does not instantiate physical projectile GameObjects. It linecasts from
each virtual drop's previous position to its new position and looks for a
collider named `Level_Ground`; a match removes the drop and plays a random
native `ink_death` sequence at the hit point. The project references
Cuphead's existing `UnityEngine.Physics2DModule.dll`; no extra runtime package is
required.

The first Beppi test did not fire any ground impact because the implementation
rejected `collider.isTrigger`, while the original projectile deliberately handles
the floor in `OnTriggerEnter2D`. That rejection is now removed. The runtime also
logs linecast hits at most once every two seconds, including hierarchy path,
collider type, layer, tag, `isTrigger` and hit point. It logs the first accepted
`Level_Ground` separately. The trigger fix was manually validated in Beppi. If
another arena fails, use those diagnostics to verify whether it names the floor
differently. Ground-impact sprites render at 60% of their exported PNG size so
the splash remains proportional to the challenge's virtual drops.
If virtual
linecasts remain unreliable, the higher-fidelity fallback is to instantiate or
clone the native projectile/prefab flow instead of guessing a fixed floor Y.
Plane levels or levels without a valid ground should allow drops to leave the
screen without a fake impact.

### Required follow-up tests and tasks

1. Decide the final amount of ink rain by difficulty: maximum simultaneous
   drops, wave size probabilities and spawn delays. Current values are only a
   current prototype (`3/4/13` visible on Easy/Normal/Expert). Spawn intervals
   and double-wave probabilities were intentionally left unchanged when these
   caps and the gravity were raised.
2. Tune horizontal speed, initial fall speed and gravity through gameplay so the
   diagonal arcs are readable and fair in both ground and plane fights.
3. Validate ground collision beyond Beppi. Confirm the original four-way OnDeath
   animation appears on real floors/platforms and never on walls, enemies,
   results, the map or later non-roulette levels.
4. Validate the new squid introduction in ground, plane, retry and co-op fights.
   Specifically confirm the first drops cannot ink a locked player, the sequence
   does not delay Ready/Wallop and the native attack loop always stops.
5. King Dice is a known pending compatibility case, not a normal single-scene
   boss. Reproduce and document the current Ink Rain errors across several
   `DicePalace*` minions and `DicePalaceMain`, then verify that every internal
   scene keeps the same challenge session without replaying the squid intro,
   duplicating rain/compositors, losing the damage grace, or clearing the
   challenge before the final boss. Camera and HUD replacement during those
   transitions also needs explicit coverage. Do not treat an internal minion
   victory as the end of Ink Rain.
6. Decide the intended behavior when the roulette selects Ink Rain for Captain
   Brineybeard (`Pirate`). His native fight already owns the same squid, ink
   projectiles and full-screen overlay. The current experimental implementation
   can coexist with those systems, but that may duplicate the introductory
   squid, stack two rain sources and make the overlay/difficulty confusing.
   Before public activation, choose and test one policy: exclude Ink Rain from
   this boss, keep the extra rain but suppress only the mod's squid intro, or
   deliberately allow both complete systems. This is a design decision, not a
   resolved bug; do not silently disable the native boss attack.
7. After the above, test defeat/retry, victory, abandon-to-map, results screens,
   ground bosses, plane bosses, DLC bosses, pause and two-player sessions.
   Finally disable the forced selector and replace the provisional challenge
   icon with the user's finished animation.

## Completed dormant HP.1 challenge (0.5.129, awaiting final animated icon)

`ModifierId.HpOne` implements the ground-and-plane `HP.1` challenge. The
roulette and battle HUD use `assets/modifiers/hp1.png`, an 80 x 80 temporary
single-frame icon with a padlock and the joined `HP.1` label. All localization
dictionaries currently use `HP.1` as the challenge name.

The runtime rule is a real health lock, not a cosmetic HUD override.
`HpOneChallenge.cs` patches the `PlayerStatsManager.Health` and `HealthMax`
setters: values above one are clamped to one while zero and damage/death values
are preserved. Therefore every player starts with one current/max HP and any
valid hit still kills normally. Native health sources continue executing their
other behavior but cannot raise health: Heart and Twin Heart keep their damage
penalties, Heart Ring is intentionally useless, Cursed/Divine Relic keep their
non-healing behavior, and King Dice hearts, Djimmi wishes and other native
heals cannot exceed one HP. Astral Cookie still selects Ms. Chalice.

The guard applies only while the roulette's matching boss battle is active.
It uses `Level.Current` plus `ActiveChallengeMatches()` and a narrow LevelInit
fallback tied to the active battle HUD/loaned-loadout session. It does not
change map health or saved profile data, and normal behavior returns after the
battle lifecycle clears the challenge.

Co-op patches force `PartnerCanSteal` true so P2 can join a one-HP run and skip
the donor-health subtraction in `OnPartnerStealHealth`; both players remain at
one HP independently. Native revive health is also clamped to one.

Ms. Chalice Super II is rejected instead of granting a shield. Calls to
`SetChaliceShield(true)` are forced false, the player is made vulnerable, and
the spawned heart remains for a 1.15-second rejection effect before being
destroyed. `HpOneRejectedHeartEffect.cs` renders it grayscale at roughly 50%
opacity with deterministic horizontal/vertical jitter, flicker, scanlines and
a final fade. It deliberately avoids Unity's random generator so the visual
cannot alter gameplay RNG. The shader is
`tools/unity-shader/Assets/BossRouletteRejectedHeart.shader`; the rebuilt
`assets/shaders/gilomx-boss-roulette-shaders` bundle now contains three
shaders. A narrowly scoped `PlayerDamageReceiver.OnRevive` guard suppresses
only the same-frame native revive/invulnerability cleanup caused by destroying
this rejected heart.

Heart Ring and both effective states of `Charm.charm_curse` (Cursed and Divine
Relic) share Cuphead's native `PlayerStatsManager.HealerCharm()` path. When its
parry interval produces a heal, the game creates five
`HealerCharmParticleEffect` instances under a visible
`HealerCharmSparkEffect` root. The first runtime test proved that decorating
only the particles left the dominant root animation in color. The final hooks
decorate the returned root in `Effect.Create(Vector3, Vector3)` and each
particle in its `Awake()` postfix, before its first rendered frame. Both receive
the same `HpOneRejectedHeartEffect` and shader while HP.1 is active. The
attempted heal still executes its native secondary behavior but
the health setter clamps it to one; visually, its particles are grayscale,
roughly 50% opaque, deterministic-jittered, flickered, scanlined and faded.
The patch is shared by ground and plane because both animation controllers use
the same particle class.

The second runtime test found one heart layer still pink and left the player as
a permanent white silhouette. `HealerCharmSparkEffect` owns the delayed native
`player_flash_cr()` cleanup; destroying its root on the visual effect's
1.15-second timer could interrupt `SetOldMaterial()` after the five particles
arrived. The root now fades to zero without destroying its GameObject and lets
Cuphead finish that coroutine and destroy it natively. Particle objects retain
the original timed destruction. While active, the component also reasserts its
shader material every frame so an Animator material swap cannot expose a pink
heart layer.

One pink heart layer still remained after that lifecycle fix. The visual
component had only enumerated `SpriteRenderer`; it now captures every child
`Renderer`, including particle-system or mesh renderers, and copies the source
material's main texture into the rejection shader. Sprite tint is still forced
white where applicable. This remains object-scoped and does not apply a global
grayscale pass or alter the persistent health HUD. The renderer-wide coverage
was manually accepted. A faint pink contribution from the native rotation can
remain, but the rejected-heal result is clear, the player returns from the
native flash correctly, and this visual is approved.

The rejected heal also has its own approved cue:
`assets/sounds/hp_one_rejected_parry.wav`. The Harmony audio hook is scoped to
the same-frame real heal attempt from `HealerCharm()` and works in ground and
plane fights. It replaces `player_parry_power_up`; for
`player_parry_power_up_full` it adds the rejected cue while preserving the
native full-meter sound.

`RouletteDjimmiGuard.cs` is intentionally broader than HP.1. While
`loanedLoadoutsActive` marks any roulette battle session, its postfix makes
`PlayerData.DjimmiActivatedCurrentRegion()` return false. It does not consume,
clear or save over the wish. Manual validation in Normal produced 3 HP in a
roulette fight and restored the native wish (9 HP with the player's restored
loadout) in a manually entered fight. Cuphead's own `DjimmiInUse()` excludes
Expert (`Level.Mode` value 2), so Normal is the meaningful regression case.

### Dormant state and completed validation

- `ExperimentalFeatures.EnableHpOneChallenge = false`.
- `ExperimentalFeatures.ForceHpOneChallengeForTesting = false`.
- Every HP.1 loadout/boss test selector and the generic boss selector are
  `false`; normal builds do not select HP.1.
- The only release gate is replacing the temporary single-frame `hp1.png` with
  the final animated challenge icon. Re-enable the challenge only after that
  asset is integrated; never re-enable a force selector for a public build.

Manual validation completed for ground and plane, Cuphead/Mugman and Ms.
Chalice-specific behavior, Heart, Twin Heart, Heart Ring, Cursed/Divine Relic,
King Dice hearts, Djimmi, Chalice Super II, retry, exit/restoration and normal
non-roulette entry. Heart/Twin Heart retained their native damage multipliers
(0.95/0.90). The rejected-heal visual and custom sound were approved.

Co-op validation also passed: two players joined from the map both started at
1 HP; a revived ghost returned at 1 HP; retry preserved one HP for both; a P2
joining after battle start entered at 1 HP; retry again preserved the rule;
and leaving the roulette session restored normal health in a manually entered
level.
## Completed dormant flat 180-degree challenge (0.5.129)

`ModifierId.UpsideDown` is a new experimental ground-and-plane challenge.
`ExperimentalFeatures.EnableUpsideDownChallenge` and
`ForceUpsideDownChallengeForTesting` are both `false` after completed manual
validation. The implementation remains compiled but dormant, following the
same release-gate pattern as RGB, so normal spins cannot select it.

The selected design is a literal flat rotation around screen center, not a
horizontal mirror or a card flip. Each fresh attempt stays normal for only a
challenge-specific 0.25-second delay, then uses a 0.45-second smoothstep to
rotate from 0 to 180 degrees. The orientation therefore settles near the start
of the fight instead of changing after the player is already moving, and it
matches the accelerated defeat return without changing Black and White/RGB
timing. A minimum angle-aware overscan experiment removed black
wedges but cropped almost half the fight and was rejected. The current pass
instead draws a full-screen quad with inverse-rotated UVs and clamp sampling:
the central captured frame remains at 1:1 scale while only its outermost pixels
extend into otherwise empty intermediate-angle corners.

`FlatRotationRenderEffect` is appended to each battle camera and receives the
final frame after Cuphead's image effects. It draws that texture as an
aspect-correct rotated GL quad using the already bundled saturation shader at
full color. It never changes the camera transform, input, world coordinates,
physics, culling decisions or hitboxes. `UpsideDownChallenge.cs` owns effect
discovery, lifecycle, transition state and cleanup.

The first runtime test exposed a double-transform bug: setting the reused
shader's `_FlipY` to 1 canceled the rotated quad's vertical inversion, so the
180-degree endpoint looked like a horizontal mirror. The rotation pass now
sets `_FlipY` to 0. The geometry alone performs both axis inversions and the
endpoint is genuinely upside down.

The intermediate design added a horizontal reflection so characters would keep
their original screen X position. Runtime testing ultimately rejected that
behavior because the result looked mirrored. The final implementation removes
`rotationProgress`, `horizontalMirrorScale` and the midpoint sign change
entirely. `DrawVertex()` now applies only the inverse rotation. At 180 degrees
both screen axes reverse, so left and right exchange places naturally as they
would on a physical image rotated half a turn; no reflection occurs at any
point in the transition.

UpsideDown now remains fully inverted throughout the defeat card.
`LevelGameOverGUI.Retry()` and `LevelGameOverGUI.ExitToMap()` capture the old
level and hold the frame through the native fade. `LevelPauseGUI.Restart()` and
`LevelPauseGUI.Exit()` use the same covered-transition path.
`SceneLoader.OnFadeInEndEvent` resets the visual controllers only after the
fader is completely opaque, so a retry starts from the normal frame and either
map-exit route arrives upright without exposing the return. RGB and Black and
White retain their existing defeat unwind behavior. Tower of Power's
confirmation-only pause Restart branch remains excluded.

Victory intentionally keeps its separate presentation: the inverted K.O. frame
holds for 1 second and then visibly returns to normal in 0.45 seconds before
grading.

Both generated sound directions were rejected: the 0.38-second noise whoosh and
the 0.41-second hollow object/cartoon whistle. The active replacement is
`assets/sounds/upside_down_turn.wav`, derived from the user-provided cartoon
violin MP3. FFmpeg takes its first 1.90 active seconds, compresses them to exactly
0.450 seconds with Rubber Band tempo 4.222222/pitch 0.96, high-passes at 90 Hz,
compresses dynamics, applies 8 ms and 45 ms fades, and normalizes to -5
LUFS/-0.2 dBTP after three requested loudness lifts (2 dB, 2.5 dB, then about
1.5 dB with stronger compression), followed by final 0.75 dB and 0.5 dB
post-gain passes with a 0.988 ceiling limiter. `LoadAudio()` loads it and every actual UpsideDown transition
queues it at volume 1.0 when its delay expires; Cuphead's Master and SFX settings
still control the result. Verify the source MP3's redistribution license before
a public package.

The roulette HUD row is reparented to `LevelHUD.Canvas` only for RGB and
UpsideDown, so lives, cards and the mod HUD rotate together. Pause/game-over
layers remain upright. A retry of the same level instance sequence starts from
0 degrees again. During a Dice Palace chain, a new internal scene with a
different `Levels` value preserves the completed 180-degree state and attaches
it to the new camera without replaying the entry.

The temporary display name is `180°` in all 12 language dictionaries. The
single-frame `assets/modifiers/upside_down.png` is now an 80 × 80 transparent
text-free icon: a cream arrow with black vintage ink, tilted as an elliptical
ring in perspective so its wide front arc and narrow rear arc imply a flat
180-degree turn. The merely vertically reversed second draft was rejected.
The current third draft narrows into the upper/rear arc, then grows into a large
foreshortened arrowhead that emerges from the back toward the viewer. Runtime
feedback found the first export too horizontal, so the active asset rotates the
same high-resolution transparent art clockwise by 28 degrees before its final
80 Ã— 80 Lanczos downscale. It was
generated with the built-in image tool on chroma
green, keyed locally with the image skill helper, tightly padded and downscaled
with Lanczos. Final three-frame art and localized copy remain pending.

The source MP3 is 2.377 seconds and its useful gesture ends near 1.90 seconds.
The earlier -17 LUFS preview was judged too quiet; the active WAV uses the
stronger processing and runtime volume described above.

Manual acceptance passed for the core ground-fight flow: pure rotation without
mirroring, turn direction, edge extension, native/mod HUD rotation, defeat
hold, defeat Retry, defeat Exit to Map, pause Restart, pause Exit to Map, K.O.
return, synchronized SFX and hidden cleanup. Before public activation, complete
the remaining plane, co-op, repeated-parry and full Dice Palace matrix. Final
art remains pending; both experimental switches are disabled.

## Cagney native fuzzy suppression during RGB (0.5.129)

When a roulette fight combines `Levels.Flower` with `ModifierId.RgbShift`, a
Harmony prefix now skips `CupheadRenderer.TouchFuzzy(float, float, float)`.
Direct IL inspection confirmed that this method only calls
`ChromaticAberrationFilmGrain.PsychedelicEffect(...)` and starts
`CupheadRenderer.change_blur_cr(...)`; it does not own hit detection, damage or
audio. Suppressing the whole method therefore prevents the two native visual
coroutines from running behind the permanent challenge without changing the
attack itself.

The prefix returns normally for every other boss, every non-RGB fight and all
gameplay outside the active roulette challenge. It also requires
`ShouldShowActiveChallenge()`, so stale roulette state during map/scene loading
cannot suppress a normal Cuphead effect.

Manual acceptance passed with `ForcedTestBossSequence` restricted to
`Levels.Flower`: receiving the pollen hit after the RGB transition caused no
visual jump or additional blur, while damage remained normal. After validation,
`ExperimentalFeatures.EnableRgbShiftChallenge`,
`ForceRgbShiftChallengeForTesting` and `Plugin.ForceTestBoss` were restored to
`false`. The guard remains compiled and ready for the future RGB release, but
neither the challenge nor the Cagney selector is active in the public build.
Final verification: 0 errors, 0 warnings. DLL SHA-256
`38CAB27C384B19713B24575FDB1BC5BDB1D2345850F7520F1593D1ADFEBE8469`.

## Native weapon-switch notification suppressed (0.5.129)

Roulette fights no longer show Cuphead's native `E`/weapon-switch tutorial,
regardless of whether Weapon B is equipped. The root cause was in
`Plugin.ApplyLoadout()`: it always set `MustNotifySwitchRegularWeapon = true`.
`LevelHUDPlayer.Init()` then activated `weaponSwitchNotification`, and native
`OnWeaponChanged()` was the only path that cleared and faded it. With Weapon B
set to `Weapon.None`, the player could never generate that event, so the prompt
remained for the entire fight.

`ApplyLoadout()` now sets both `MustNotifySwitchRegularWeapon` and
`MustNotifySwitchSHMUPWeapon` to `false` for each temporary roulette loadout.
This covers ground and plane fights, one or two players, Weapon B present or
empty, retries and the Dice Palace chain. It does not patch `LevelHUDPlayer` or
alter normal Cuphead fights. `LoadoutSnapshot` already captures both original
flags and restores them on win/exit, so a tutorial legitimately pending before
the roulette is preserved after returning to the map.

Manual acceptance: test one roulette fight with Weapon B `Nada` and one with
two weapons; neither should show the switch notice. Retry both once, then leave
to the map and confirm ordinary non-roulette behavior is unchanged. Build
verification: 0 errors, 0 warnings. DLL SHA-256
`DBA0BD2444B85F0651DE2B6C2E32DD9B2DD13194DD2CCFA9B4E13EBA3A9559D6`.

## Completed dormant RGB challenge (0.5.129)

`ModifierId.RgbShift` is now an experimental ground-and-plane challenge. It
uses Cuphead's persistent `ChromaticAberrationFilmGrain` component rather than
shipping another shader. Reverse engineering confirmed Cagney's pollen calls
`CupheadRenderer.TouchFuzzy(15f, 8f, 1.2f)`: the native coroutine drives red
up, green up at half amplitude and blue down using `sin(time * 8) * 15`.

`RgbShiftChallenge.cs` owns those three vectors and the camera's `BlurGamma`
directly instead of repeatedly calling `TouchFuzzy`, which would accumulate
psychedelic and blur coroutines. Reverse engineering the native blur coroutine
confirmed that it jumps to baseline +1, rises at one unit per second for 0.6 s
to baseline +1.6, then falls at the same rate for 1.6 s. A complete assembly
reference scan also confirmed that the pollen coroutine writes only vertical
RGB vectors; its apparent horizontal movement comes from `BlurGamma` sampling
diagonally. The full repeated blur was rejected visually as too strong.

The current deliberately exaggerated test returns to smooth sinusoidal motion
after irregular target bounces were rejected visually. Base amplitude is 32
and vertical speed is 10. Horizontal motion runs at 73% speed (7.3), 70%
amplitude and a quarter-cycle phase offset, creating a continuous 2D path. Red
uses 120% strength, green 60% and blue 90% in the opposite direction; their
maximum vertical displacements are 38.4, 19.2 and 28.8. The native 2.2-second
blur timing runs at 70% strength (baseline +0.7 initially, peaking at +1.12).
This is a difficulty-boundary test, not a claim that Cagney's native coroutine
has this 2D amplitude. The challenge does not reproduce Cagney's `AudioWarble`
because it is visual.

Neither effect changes the camera transform, world positions, controls or
hitboxes. The perceived screen motion comes only from RGB samples being drawn
at different offsets and `BlurGamma` sampling the rendered image diagonally.

During active RGB gameplay, `PlaceBattleHudOnGameplayLayer()` reparents the
roulette row to `LevelHUD.Canvas`. The normal persistent HUD is a
`ScreenSpaceOverlay` rendered after camera postprocessing, so it could not
receive RGB or blur. This exception is restricted to `ModifierId.RgbShift`;
every other challenge retains the accepted independent overlay and its parry
isolation. Pause, game over, victory and scene-transition paths remain
unchanged. Manual acceptance must parry repeatedly in ground, plane and King
Dice fights to ensure the RGB-only native-Canvas route behaves like Cuphead's
health/super HUD without reviving the old custom-HUD blink.

It captures the native RGB vectors and `blurSize`, waits the accepted
`BlackAndWhiteEntryDelay` (1.5 s), fades both effects in over
`BlackAndWhiteFadeInDuration` (1.25 s), applies them in `LateUpdate`, fades out
over 0.9 s and restores every captured value. `LateUpdate` also prevents
Cagney's own pollen coroutines from fighting the owned values. Transient scene
loading preserves the current state, matching the black-and-white lifecycle;
once a genuinely new battle `Level` instance is available, its normal opening
starts again. Retry keeps the challenge but also starts its opening again.
`OnDestroy` performs an immediate final restore.

The user accepted the challenge behavior, then requested that it remain dormant
for ongoing development. `ExperimentalFeatures.EnableRgbShiftChallenge` and
`ForceRgbShiftChallengeForTesting` are therefore both `false`. RGB is absent
from `ValidModifierIndices`, cannot be selected or forced, and its runtime
ownership stays disabled; the completed implementation and assets remain in
the project. To publish it later, set Enable true and keep Force false. Set both
true only for a new forced local test.

The placeholder `assets/modifiers/rgb.png` is one transparent 80 × 80 frame
generated from the existing challenge-icon visual language. `ModifierEntry`
now carries `FrameCount` (default 3); RGB passes 1 so the Equip Card does not
request missing `_02`/`_03` files or blink. The HUD already consumes the same
first texture. The temporary displayed name is `RGB` in all 12 language tables.
The functionality is complete. Final localized copy and the user's final
three-frame art are still pending presentation assets, not code blockers.

Build verification: 0 errors, 0 warnings. DLL SHA-256
`DBA0BD2444B85F0651DE2B6C2E32DD9B2DD13194DD2CCFA9B4E13EBA3A9559D6`;
placeholder SHA-256
`F5D81433B50BA114FBCF69F23A67F7FF7D42E0C111947781CA08A5BBF8D5218A`.

## Uniform localized equipment labels (0.5.128)

The five labels below the equipment icons now share one fitted font size per
language. Their rectangles remain at the accepted 98 × 23 layout coordinates,
with 94 units reserved as the safe text width. `FitEquipSlotLabels()` measures
the active Cuphead font with `GUIStyle.CalcSize`, tries sizes 14 through 11 and
applies the first size that fits every label. Word wrapping is disabled so no
translation can spill into the settings rows. The result is cached and is
invalidated when the language changes or the GUI style is rebuilt.

Russian was manually accepted with the full approved `СПЕЦАТАКА` and
`ИСПЫТАНИЕ` strings. Do not shorten translations or move the five explicitly
tuned icon coordinates to solve future language issues; extend the shared-fit
rule only if another real font needs it. The temporary `Ctrl+F8` language
cycle is disabled in this release. The release build completed with zero errors and zero warnings; its DLL SHA-256 is `71318EB76146ABF4C4963EAA2A6B64CB5232CA6F285B64F87A880E4410910298`.

## Public package preparation (0.5.127)

The temporary 0.5.126 roulette-open diagnostic confirmed that BepInEx loaded,
F6 opened the roulette and spins ran without any rejected-map log. Version
0.5.127 removes that diagnostic completely and restores the original single
`KeyboardShortcut.IsDown()` input path. The screenshot-only `Ctrl+F8` language
cycle is disabled again. The English `Angel and Demon` correction remains.

The ready-to-paste package includes the user-provided bilingual instructions as
`README-LEEME.txt` in the ZIP root, alongside the x64 BepInEx bootstrap and mod.
The verified artifact is
`dist/Las-Pichi-Ruleta-0.5.127.zip` (10,604,725 bytes, SHA-256
`2C458AA88A4B6F1A1E2864B5AD18E5709BB5DB8DB102E540D84125BB63798B42`).
It contains 123 files (plus two ZIP directory entries): x64 Doorstop, 18 BepInEx
core files, the 0.5.127 DLL, 99 mod assets and the bilingual README. The packed
DLL matches the build at SHA-256
`1BBAA142A775B79E44422E73F2F048672ED4E50AFEC174402E4ADF2518AA2E11`.
The package contains no config, cache, logs, saves, patchers or unrelated
plugins. `README-LEEME.txt` is actual plain text: no Markdown headings, lists,
bold, italics, code spans or Markdown separators. Numbered instructions use
`1)` notation and lists use plain Unicode bullets. It was round-trip verified as
UTF-8, including Spanish accents.

Last updated: 2026-08-08
Current local version: 0.5.129

This file is the working context for the next agent. Read it before changing the
mod. The user has iterated on the layout by eye, so preserve all explicit
coordinates and avoid broad rewrites.

The accepted HUD architecture, layer matrix, layout invariants and extension
checklist now live in [HUD_INTEGRATION.md](HUD_INTEGRATION.md). Read that guide
before adding any new battle indicator.

## Current 0.5.122 state

Temporary local override: version 0.5.123 re-enables
`EnableLanguageTestShortcut` solely for GameBanana screenshots. The first
`Ctrl+F8` press selects English; cleanup still restores the original language.
Set the flag back to `false` before the next public ZIP. The existing 0.5.122
ready-to-paste package remains the release build with the shortcut disabled.
The temporary DLL built and installed with zero errors and zero warnings;
SHA-256 `CCF7A8040430C56E814F8E0F28158CA7CA8A0C163B5FCBA55EF8D7CB423B9CE6`.

Version 0.5.124 fixes the localized boss-name source while retaining the
temporary screenshot shortcut. `LocalizedBossName()` now mirrors Cuphead's
`MapDifficultySelectStartUI`: it requests `<level>WorldMap` first and only then
falls back to the former `<level>` key. This matters for `Levels.Graveyard`,
whose plain lookup produced no text and exposed the Spanish `BossEntry` fallback
under English. Do not replace this with a hardcoded English exception; the
native WorldMap key supports every game language.
The 0.5.124 build was installed after Cuphead closed; compiled and installed
DLLs match at SHA-256
`14C0A738CD36B2D77FC3450831691776BD3870529A8FAE81B3D9EA115BCBB6C0`.

Runtime testing showed that `GraveyardWorldMap` still has no usable English
text, consistent with a native artwork-only boss-name entry. Version 0.5.125
therefore returns the approved textual fallback `Angel and Demon` for
`Levels.Graveyard` while `Localization.language == English`, before either
native text lookup can fall through to the Spanish `BossEntry.Character`.
Keep this exception until a complete official textual catalog for the
artwork-only languages is available. Ctrl+F8 remains enabled for screenshots.
The 0.5.125 compiled and installed DLLs match at SHA-256
`9AD3B6A6FB41F4EF6C4B650D4546185A20AB5E244E2B5887C6238819B0652D13`.

- `assets/card/roulette-card.png` is the user-provided third background
  revision (595×668). No card coordinate or layout value changed.
- `EnableLanguageTestShortcut = false`; the temporary `Ctrl+F8` localization
  shortcut is dormant in normal builds.
- Empty weapon/super/charm catalog entries use
  `equip_icon_empty_0001`. `DrawEquipSlot()` therefore animates all three
  native empty frames exactly like the disabled challenge slot.
- Weapon A is always non-empty. Weapon B has an exact 20% chance of
  `Weapon.None`; otherwise it is non-empty and cannot duplicate Weapon A.
- Empty results are white only in the battle HUD.
  `ApplyWhiteBattleHudEmptyIcon()` extracts the native sprite alpha and writes
  a white silhouette; a procedural 72×72 segmented ring is the fallback. The
  cached texture is destroyed with the HUD. The Equip Card keeps the native
  dark animated art.
- `impact_01.wav` is processed with +20 dB into a fast −1 dB limiter. Measured
  integrated loudness changes from about −20.01 to −12.4 LUFS and mean level
  from −20.2 to −11.4 dB. `BattleHudImpactVolume = 1f`, avoiding a second
  boost in Unity. The clip remains routed through Cuphead's native SFX mixer,
  so Master or SFX at zero still mutes it completely.
- Saltbaker return now checks the dedicated `MapBakeryLoader` first. This is
  the actual DLC bakery interactive entity present when `Map.CreatePlayers()`
  runs; `MapLevelLoader`/`MapSceneLoader` remain fallbacks. It still calls the
  game-owned `SetPlayerReturnPos()` rather than storing coordinates.
- `ForceTestBoss = false`; the dormant Saltbaker/Devil sequence remains available
  for future diagnostics but no longer affects spins. Relic, plane-relic,
  challenge and five-card selectors are also disabled.
- Current verification: build completed with 0 errors and 0 warnings. The
  compiled and installed DLLs match at SHA-256 `689C1EF0FE1D528F19B5ACA0C94BD23B09B6C367BB05BB0B57DB397FEE82100C`.
  The processed and installed WAVs match at `F44C76F5A12C7356E608915BC48D010C9613B2FCE4FD0D658800DD3EC63BAB98`.
- After pulling 0.5.122 on this PC, a fresh rebuild was installed with DLL
  SHA-256 `1B4E2F5AE403F75166065D72751032A7A98071B4A7774C7132C693A4201AE4EE`;
  its card and WAV also match the repository. The ready-to-paste artifact is
  `dist/Gilomx-Boss-Roulette-0.5.122-BepInEx-x64.zip` (10,602,141 bytes,
  SHA-256 `8BB029AA69DD723E943C167AAB80A7862B01E8B82C1D8B673DF1B4B8D6ECF64E`).
  Its 122 entries were inspected: x64 Doorstop, 18 BepInEx core files, the
  current DLL and all 99 assets. It contains no config, log, cache, save or
  unrelated plugin files.
- Challenge GIF previews can be regenerated with
  `tools/build_challenge_gifs.py` using the bundled Codex Python/Pillow runtime.
  The script mirrors `EquipIconFramesPerSecond = 12.5f`: 80 ms per frame,
  three frames, 42 complete cycles, 126 frames and 10.08 seconds. It omits the
  GIF loop extension so playback stops on frame 3. The verified seven-file
  artifact plus README is
  `dist/Gilomx-Boss-Roulette-Challenge-GIFs-0.5.122.zip` (54,941 bytes,
  SHA-256 `EBB36B6FF7AE0EA8E2BA5AA3365ACA134FFEA84B79D02D832514796CBED3A71E`).

## Native boss-door return and covered map input (0.5.121)

- LoadResult() resolves the selected boss to its native map with Cuphead's
  public Level.world*BossLevels arrays, records that map as
  PlayerData.Data.CurrentMap, and keeps the target level until the map returns.
- A Harmony prefix on private Map.CreatePlayers() runs before Cuphead reads
  CurrentMapData. It finds the loaded entrance, marks the map session started,
  clears stale enteringFrom, then calls the game's own
  AbstractMapInteractiveEntity.SetPlayerReturnPos(). Native player creation
  therefore starts at the boss door and supports both players without a visible
  post-load teleport.
- Entrance lookup accepts an exact MapLevelLoader.level. Saltbaker first uses
  its dedicated MapBakeryLoader, then Levels.Kitchen or a MapSceneLoader for
  scene_level_kitchen as fallbacks; King Dice and Devil can use
  MapDicePalaceSceneLoader.
- The map association is World 1/2/3/4 from Cuphead's native arrays, DLC from
  worldDLCBossLevelsWithSaltbaker, plus explicit Levels.Graveyard.
- A Harmony prefix skips AbstractMapInteractiveEntity.Update() while the
  roulette is visible or its card is still exiting. This prevents the same
  Accept/Enter/Z edge from activating a boss entrance behind the card.
- Do not replace this with stored coordinates: entrance transforms and
  returnPositions are owned by the scene and SetPlayerReturnPos() already
  handles single-player and multiplayer offsets.
- User manual validation passed: returning from the selected fight places the
  player at that boss entrance, and Accept/Enter/Z no longer opens a native
  boss selector behind the roulette card.
- Release-candidate state: ForcedTestChallenge is None; relic, plane-relic,
  five-card HUD and boss selectors are false; EnableLanguageTestShortcut is
  also false.
- Build/install verification: 0 errors, 0 warnings; DLL SHA-256
  EB6E2FD62CCF76365E0D7C488CE644E16958D25C242919D549ACB11D179772F8;
  LogOutput.log confirms 0.5.121.

## Spanish-America regional override (0.5.120)

- `SpanishSpain` keeps `SIN DISPARO NORMAL` for
  `challenge.no_peashooter`.
- `SpanishAmerica` uses `SIN PEASHOOTER` for that one ID and inherits every
  other value from the Spanish base dictionary.
- The full active America table is in
  `translations/translation_spanish_america.md`; the base/Spain table remains
  in `translations/translation_spanish_shared.md`.
- All other approved wording remains unchanged.
- The card PNG was replaced at the user's request without changing any layout
  coordinate or drawing logic.
- Build verification: 0 errors, 0 warnings. Compiled and installed DLL SHA-256: `88DA9E3E6F61E0F9B0AF9DF0D71A3BECB5ECD06F88D96F30B07ED1DA0FE87067`; `LogOutput.log` confirms version 0.5.120.

### Checklist localization layout (0.5.117)

- Checklist labels use a fixed 360-unit rectangle beginning at `x = 72`.
- `wordWrap` is disabled for these labels so long approved translations remain
  on one line without changing their wording or font size.
- The right-side value keeps its original rectangle and right alignment; the
  longest current value, Italian `DISATTIVO`, remains clear of the label.

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

The user explicitly authorizes the agent to close Cuphead when a local test
build must replace the loaded DLL, then relaunch the game normally through
Steam. Prefer a graceful window close when available and use process
termination only if the game does not exit; never relaunch Cuphead directly
outside Steam because that bypasses Steam Input and can hide the controller.

## Current Git state

This handoff documents the roulette implementation through version 0.5.120.
Always inspect `git status` before editing, and do not reset, restore, or
overwrite unrelated user changes.

Localization activation is complete for Cuphead's 12 language enums. From 0.5.120, Spanish Spain uses the Spanish base dictionary while Spanish America clones that base and overrides only `ChallengeNoPeashooter` with `SIN PEASHOOTER`.

## Localization-safe internal model (0.5.109)

Version 0.5.109 removes visible Spanish copy from every gameplay decision.
`ModifierId` is the canonical identity for `NoDash`, `NoMiniPlane`,
`MiniPlaneOnly`, `NoBombs`, `NoPeashooter`, `NoEx`, `BlackAndWhite`, and
`None`. `ModifierEntry` keeps only that ID, compatibility kind and artwork.
Challenge restrictions, forced development selectors, lifecycle checks and
the black-and-white renderer compare IDs exclusively.

`RouletteStatus` replaces the mutable status sentence. In particular,
`DrawSpinBand()` now checks `RouletteStatus.SaveRequired` rather than searching
for the Spanish word `PARTIDA`. `ModLocalization` maps `ModText` and
`ModifierId` to visible copy, subscribes to
`Localization.OnLanguageChangedEvent`, and unsubscribes during plugin cleanup.
The current table intentionally contains only the accepted Spanish strings;
every language falls back to that table until translations are approved.

The Equip Card, native map prompts, persistent challenge label and battle HUD
resolve their copy every frame. The map prompt invalidates its cached layout on
a language event so its width is measured again. The battle snapshot stores a
`ModifierId`, allowing its visible label to change without altering the active
rule. Helpers for the unused legacy interface can still resolve equipment names
through Cuphead, but the active Equip Card displays only equipment icons.

When adjusting localization, use the exact 29-value active catalogs in `translations/` and the full reviewed deliveries in `translations/review_by_language/`. Preserve the policy that fight subtitles appear only in the two Spanish variants unless the user explicitly changes it. Validate native fonts and widths before changing approved wording.

## Temporary language review shortcut (0.5.110)

`Ctrl+F8` calls Cuphead's public `Localization.language` setter and cycles the
complete enum in official order, starting with `English` on the first press.
This deliberately changes the complete game language, not only the mod, so a
review also exercises native boss/equipment names, fonts and
`OnLanguageChangedEvent`. A small top-center notice remains for three seconds.

The first use snapshots the original language. `OnApplicationQuit()` and
`OnDestroy()` restore that value, and the selector never calls
`SettingsData.Save()`. Avoid opening and closing Cuphead's Options screen while
a test language is active because the game itself saves settings when that
menu closes. The release off-switch is the single constant
`EnableLanguageTestShortcut` near the other temporary selectors in `Plugin.cs`;
set it to `false` before packaging the public build.

`TRANSLATION_REVIEW_TEMPLATE.md` is the user-facing handoff format. Make one
copy per language, fill only `Traducción aprobada`, and preserve each ID. The
user may attach that Markdown file or paste short `id = value` lines in chat.
The template also lists the 12 languages in Spanish, their Cuphead enum names,
the exact Ctrl+F8 order, and a suggested filename for every language.

### Verified public translation scope

The 2026-08-06 render-path audit reduced that template to exactly **29 visible
strings**. The active paths are `EquipCardLayout.DrawRoulette()`, the two native
map prompts, and the battle challenge HUD. `Plugin.OnGUI()` never calls
`DrawRouletteLegacy()`, and `ModLocalization.StatusText()` is called only from
that legacy method. Therefore `status.scene_loading` and every other
`status.*` phrase are not player-visible and must not be sent for translation.

The same exclusion applies to the old brand/tagline/controls/close copy,
equipment names (`Nada` and both relic labels), `challenge.none`, BepInEx config
descriptions, logs and the temporary Ctrl+F8 notice. `ui.action.select_save` is
also unreachable on the active card because `OnGUI()` requires
`CanUseRouletteOnMap()`, which already requires initialized save data. Keep
`LOCALIZATION_TRANSLATIONS.md` only as a historical proposal pool; the review
template is the source of truth for what the user needs to translate.

## Approved English localization (0.5.111)

The user supplied a completed `translation_english.md` with all 29 public IDs.
The exact accepted copy is preserved in
`translations/translation_english.md` and loaded by `ModLocalization` only for
`Localization.Languages.English`. Notable deliberate wording includes
`SHOT-A`, `SHOT-B`, `AUTO-LOAD`, `REGULAR`, `NO MINI-BOMBS` and
`BLACK & WHITE`; do not normalize these without asking the user.

All ten non-Spanish languages now use an approved independent dictionary. Switching languages with the temporary `Ctrl+F8` tester should update the active Equip Card, map prompt and challenge HUD through Cuphead's normal language event.

## Reviewed French, Italian and German localizations (0.5.118)

The reviewed 29-value deliveries replace the earlier 0.5.112–0.5.114 tables.
French now deliberately uses `ARME A/B`, `FACILE`, `NORMAL` and
`SANS TIR PRINCIPAL`. Italian uses `SPARO A/B`, `FACILE`, `NORMALE`,
`APRI LA ROULETTE`, `SENZA DASH`, `SENZA MINI BOMBE`,
`SENZA MITRAGLIATRICE` and `MONOCROMO`. German uses `WAFFE A/B`,
`NOCHMAL DREHEN`, `OHNE MINIBOMBEN` and `OHNE MASCHINENGEWEHR`.
These reviewed values supersede all older wording in this handoff.

## Spanish localization with one regional override (0.5.120)

`SpanishSpain` uses the reviewed base table. `SpanishAmerica` copies all
base values during initialization and overrides only
`ChallengeNoPeashooter` as `SIN PEASHOOTER`; Spain retains
`SIN DISPARO NORMAL`. Keep this distinction when changing or validating
Spanish localization.

## Reviewed Korean and five newly active languages (0.5.118)

The reviewed Korean delivery replaces the 0.5.116 wording; notable changes
include `무기 A/B`, `필살기`, `능력`, `보통`, `어려움`,
`소형 비행기 총알만`, `소형 폭탄 금지`, `기본 공격 금지` and
`EX 공격 금지`. Russian, Polish, Portuguese Brazil, Japanese and Simplified
Chinese also receive independent 29-value tables. All exact deliveries are in
`translations/review_by_language/`; the active snapshots are in
`translations/`. Runtime font and fit verification is still required for
Hangul, Cyrillic and CJK.

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
  three-way fire.
- Both airplane weapon restrictions also patch
  `PlanePlayerWeaponManager.SwitchWeapon(Weapon)`. Cursed/Divine Relic chooses
  a new airplane shot in `CheckBasic()` and calls this lower-level method
  directly, bypassing `HandleWeaponSwitch()`. The prefix replaces the requested
  weapon with the challenge's required Peashooter/bomb variant before the
  manager ends or starts firing. This covers both relic outcomes and both
  players without disabling EX, super, shrinking, or other relic effects.
  Keep the `OnLevelStart` normalization as well: it handles the random initial
  weapon before the first `CheckBasic()` call.
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

## King Dice parry visibility gate correction (0.5.72)

Manual testing of 0.5.71 showed that the row could still blink during parries
in later King Dice scenes. Parenting was no longer the cause: the active row
was already under the persistent overlay. The remaining dependency was
`PlaceBattleHudOnGameplayLayer()`, which called
`TryGetNativeBattleHudCanvas()` before deciding where to render. Some
`DicePalace*` fights briefly disable their native `LevelHUD.Canvas` during a
parry. That made the method return false, and `UpdateBattleResultHud()` hid the
custom row for the affected frame.

Version 0.5.72 routes an active King Dice chain directly through the new shared
`PlaceBattleHudOnPersistentOverlay()` helper before consulting native HUD
availability. `PlaceBattleHudInsideMenuLayer()` uses the same helper, removing
the duplicated persistent-parent code. This exception applies only while
`battleHudFollowNativeVictoryLayer` is false. The real `DicePalaceMain` victory
still sets that flag and uses `TryGetNativeBattleHudCanvas()` plus
`TrySwapBattleHudToNativeVictoryLayer()`, preserving the accepted knockout fade
and removal behavior. This change was insufficient: the user then confirmed
that the visual blink remained and also occurred in every boss, not only King
Dice. Do not treat native-Canvas availability as the established root cause.

## Native battle HUD material parity (0.5.73)

Inspection of Cuphead's `Assembly-CSharp.dll` with Mono.Cecil confirmed that
`LevelHUDPlayerHealth` obtains its native `UnityEngine.UI.Image` in `Awake()`
and changes only animator state, transform scale, and `Graphic.color`; it does
not install or rewrite a custom material during updates. The roulette row did
the opposite: `CreateBattleHudIcon()` and `CreateBattleHudRoot()` assigned the
shared saturation material immediately, and
`UpdateBattleResultHudSaturation()` forced it back onto all five `RawImage`s and
the challenge `Text` every frame even when saturation was exactly 1.

Version 0.5.73 keeps the same normal material path as Cuphead. `RawImage` icons
use their default UI material, and `battleHudChallengeBaseMaterial` preserves
the material inherited from the native text template. The custom saturation
material is assigned only while `Blanco y negro` is active on the persistent
overlay. Moving to the native victory Canvas restores the base materials because
that Canvas already receives the game's visual transition. This removes the
always-active custom shared-material path from ordinary parry Canvas rebuilds
without sacrificing the black-and-white challenge or final knockout behavior.
Manual testing showed that the parry blink still remained, so material parity
alone was not sufficient and must not be recorded as the final root cause.

## Fully independent active-gameplay overlay (0.5.74)

Version 0.5.74 removes the two remaining ways a parry can visually affect the
active row. First, `PlaceBattleHudOnGameplayLayer()` records the current
`Level.GetInstanceID()`. For a new level instance it waits until
`TryGetNativeBattleHudCanvas()` succeeds once, matching the original HUD's entry
readiness. After that first success, native Canvas enabled/active changes no
longer toggle the persistent row for that attempt. Loads and retries create a
new `Level` instance and reset the gate; `SceneLoader.CurrentlyLoading` still
hides the row immediately.

Second, `CreatePersistentBattleHudCanvas()` assigns the highest available Unity
sorting layer and `short.MaxValue` sorting order. This ensures a screen overlay
used for the parry cannot be composited above the roulette icons and text.
`PlaceBattleHudInsideMenuLayer()` no longer has a King Dice exception: pause and
game-over always use their native menu hierarchy, so the topmost gameplay Canvas
cannot cover those cards. Final victory still sets
`battleHudFollowNativeVictoryLayer` and swaps to the native `LevelHUD.Canvas`;
that path intentionally bypasses the persistent sorting and follows the accepted
knockout fade/removal.

Manual 0.5.74 testing still reproduced the blink on every parry. Therefore the
row's native visibility dependency and overlay sorting are not sufficient to
explain the symptom. Do not continue changing these blindly.

## Frame-level parry trace (0.5.75 diagnostic)

Version 0.5.75 adds temporary Harmony prefixes to
`LevelPlayerMotor.OnParryHit()` and
`PlanePlayerParryController.OnParrySuccess()`. An active roulette HUD then logs
24 consecutive LateUpdate samples tagged `HUD_PARRY_BEGIN` and
`HUD_PARRY_FRAME`. Each frame records `ShouldShowBattleResultHud()`, loading,
root `activeSelf`/`activeInHierarchy`, full parent path, parent Canvas render
mode/sorting layer/order/enabled state, native LevelHUD enabled/active state,
first-icon enabled/alpha, CanvasRenderer culling/alpha, and shader name.

This diagnostic must be reproduced before another visual fix. If all recorded
values remain stable while the user sees the blink, the effect is downstream of
the UI object's state and likely belongs to final frame composition. If a value
changes, use the first differing trace frame to correct that exact lifecycle
path. Remove or disable the temporary per-frame trace after establishing the
cause so normal releases do not add log noise.

The first 0.5.75 reproduction produced no `HUD_PARRY` lines even though the
visual symptom occurred. Version 0.5.76 therefore expands the temporary Harmony
targets to `LevelPlayerParryController.StartParry()`,
`PlanePlayerParryController.StartParry()`, `LevelPlayerMotor.OnParryHit()`,
`PlanePlayerParryController.OnParrySuccess()`, and
`PlayerStatsManager.OnParry()`. Startup logs the installed target count; verify
that it is nonzero before asking for another reproduction.

The 0.5.76 startup confirmed five installed hooks, but the next reproduction
still produced no trace because `BeginBattleHudParryTrace()` silently returned
before logging when `battleHudPresentationActive` was false. Version 0.5.77
logs before any session filter and includes `session`/`root` in the begin line.
It also hooks `LevelPlayerMotor.ForceParry()` and `ChaliceDashParry()` to cover
Ms. Chalice explicitly. A missing trace after this version means none of the
seven patched methods ran; a trace with `session=false` instead proves a roulette
session-lifecycle problem rather than a rendering-state problem.

The 0.5.77 reproduction still produced no hook lines. Mono.Cecil call-site
inspection then located the actual success pause in
`AbstractParryEffect/<hit_cr>c__Iterator1.MoveNext()`, which invokes virtual
`OnPaused()` and `OnUnpaused()`. Version 0.5.78 hooks base/ground `OnPaused()`
and base/plane `OnSuccess()`. It also adds `HUD_STATE_CHANGE`, a hook-independent
LateUpdate watcher that logs only when the persistent row's activity, parent,
Canvas, native Canvas availability, first-icon visibility/alpha/culling, or
material instance changes. This guarantees evidence even if another parry
implementation bypasses all patched methods.

## Parry hit-stop versus real pause menu (0.5.79)

The 0.5.78 capture established the exact cause. Immediately after a successful
parry, the HUD remained on the same persistent canvas with the same enabled
canvas, icon alpha, culling state and UI material, but its root changed from
`activeSelf=true` to `activeSelf=false` at frame 3191. It stayed disabled
through frame 3201 and became active again at frame 3202. During those same 11
frames Cuphead temporarily changed `PauseManager.state` for the parry hit-stop.

`UpdateBattleResultHudLayer()` previously treated every nonzero
`PauseManager.state` as a real pause menu. Because the actual pause background
does not exist during parry hit-stop, that branch returned false and
`UpdateBattleResultHud()` explicitly disabled `battleHudRoot`. Version 0.5.79
only reparents the row when `pauseBackground` exists and its GameObject is
active in the hierarchy. A nonzero pause state without that visible menu falls
through to `PlaceBattleHudOnGameplayLayer()`, so parries no longer toggle the
row. Real pause, game over and final-victory behavior remain unchanged.

All temporary Harmony parry hooks, frame logs and state watchers from
0.5.75–0.5.78 were removed after establishing the cause. The speculative
highest sorting layer/order and per-Level readiness gate from 0.5.74 were also
reverted to reduce regression risk. The native material parity introduced in
0.5.73 remains because it matches Cuphead's original health HUD and preserves
the special saturation material only for the `Blanco y negro` challenge.

Version 0.5.88 adds a separate cooperative placement path. It reflects
`LevelHUD.cuphead`/`mugman`, then measures only the native health and super-card
components relative to `LevelHUD.Canvas`. Those inner edges are converted from
the native canvas through screen coordinates into the current roulette HUD
parent, so the calculation survives gameplay, pause/game-over reparenting and
resolution scaling. The roulette row is centered between those edges with an
18-unit safety gap per player. Its challenge text width is capped by the
remaining cooperative space. If P2 is absent, inactive, or its bounds are not
ready, the exact original single-player right anchor `(-26, 15)` is restored.
This still needs visual validation with a real two-player session, especially
after pause, retry and victory transitions.

Version 0.5.89 temporarily enables `ForceFiveSuperCardsForHudTest`. A Harmony
prefix changes only the `float` passed to `LevelHUDPlayerSuper.OnSuperChanged`
to that player's `SuperMeterMax`, so both native HUDs render five cards while
the underlying `PlayerStatsManager.SuperMeter` and combat remain untouched.
This is strictly a layout-test selector: set it back to `false` after the
cooperative spacing is approved. The BepInEx log prints a warning while it is
active.

Version 0.5.90 additionally sets `ForcedTestChallenge` to
`No disparo Peashooter`, the longest current challenge label. This forces a
compatible plane boss and exercises the narrow cooperative result row together
with both native five-card HUDs. Clear this selector after visual validation.

Version 0.5.91 changes only the real-pause route. While PauseGUI's Background
is active, the roulette row is reparented to `LevelHUD.Canvas`, the same source
as the native health and super HUD. It therefore enters Cuphead's blurred
gameplay image and stays below PauseGUI's Confirm/Back prompts. Its pause-only
bottom margin is 10 instead of 15 units, aligning the label more closely with
those prompts. Resuming reparents it to the independent overlay again, so the
existing parry-flash isolation remains unchanged.

Version 0.5.92 corrects the pause-layer detection after runtime validation.
`TryFindBattleHudNativeLayers()` used to return the first matching PauseGUI
kept in memory even when inactive. It now retains that first match only as a
template fallback and continues scanning for an active Background. A real pause
then uses `PlaceBattleHudInsideMenuLayer(activeBackground)`, exactly like the
working game-over path, while keeping the 10-unit pause-only bottom margin.

Version 0.5.93 corrects the remaining draw-order difference observed in a real
one-player pause. A Unity UI Graphic on a parent renders before its children,
so placing the roulette row inside PauseGUI/Background still left it above that
background. The row is now the first direct child of PauseGUI instead. The
Background, pause card and help prompts are later siblings and therefore render
over it, matching the dim/blur treatment and prompt priority seen in the native
HUD. The active-PauseGUI selection fix from 0.5.92 remains.

Version 0.5.94 combines the two facts learned separately. The 0.5.91 native
LevelHUD experiment never actually ran in the reported pause because the code
still selected an inactive PauseGUI; 0.5.92 fixed that selection but then used
a PauseGUI parent, which screenshots proved remains outside the processed
gameplay image. With the active pause reliably identified, the row now moves to
`LevelHUD.Canvas`, exactly where native health/super already receive pause
dimming/blur. PauseGUI stays on its own later Canvas, so Confirm/Back remain
above. A one-shot log line records native Canvas render mode and sorting order
whenever the parent actually changes.

Version 0.5.95 removes the final name-based dependency from runtime pause
detection. `Resources.FindObjectsOfTypeAll<LevelPauseGUI>()` is filtered to a
valid active scene object and its public `AbstractPauseGUI.state`; Paused and
Animating count as visible, while parry hit-stop leaves this UI-specific state
Unpaused. The existing native-Canvas move therefore runs from a direct game
signal instead of a cloned help-row hierarchy.

The map prompt regression was the template's `LocalizationHelper` restoring its
original `VOLVER` localization after `ABRIR RULETA` had been set. The helper is
disabled on the cloned action Text, and the label/width are also reasserted in
the unchanged-layout fast path as a defensive measure.

Version 0.5.96 keeps the validated pause-Canvas behavior and changes only
`BattleHudPauseBottomMargin` from 10 to 15, matching the normal
`BattleHudBottomMargin`. The row therefore does not shift vertically during
pause reparenting.

Version 0.5.97 responds to visual validation that the ScreenSpaceCamera pause
path was much blurrier than game over. The active `LevelPauseGUI` is still
identified from its native state, but the row is now its first child instead of
joining `LevelHUD.Canvas`. A root `CanvasGroup` uses alpha 0.48 during pause and
returns to 1.0 everywhere else. PauseGUI's later siblings stay above the row,
while the independent UI path avoids the camera blur and visually approaches
the dim game-over presentation.

Version 0.5.98 raises `BattleHudPauseAlphaMultiplier` from 0.48 to 0.55. Both
`BattleHudBottomMargin` and `BattleHudPauseBottomMargin` are reduced from 15 to
12, moving the row down by three units in every state. Because both
single-player and cooperative placement consume those constants, the move is
static and identical for 1P/2P with no pause animation or jump.

Version 0.5.99 raises `BattleHudPauseAlphaMultiplier` from 0.55 to 0.70. Both
bottom margins rise from 12 to 13, moving the row up by one static unit in all
states and for both player counts.

Version 0.5.100 keeps pause entry immediate at alpha 0.70, but replaces the
single-frame return to alpha 1.0 after unpausing with a 0.30-second
`Mathf.MoveTowards` transition driven by `Time.unscaledDeltaTime`. It does not
animate or change the HUD position.

## Separate cursed and divine relic outcomes (0.5.80)

Cuphead exposes the Broken, Cursed and Divine Relic as the same enum value,
`Charm.charm_curse`. Its effective strength is the integer returned by
`CharmCurse.CalculateLevel(PlayerId)`: `-1` before the graveyard, `0` through
`3` while cursed, and `4` at the divine maximum. The native Equip Card uses
icons `equip_icon_charm_curse_1_0001` through
`equip_icon_charm_curse_5_0001` for those five active grades.

Version 0.5.80 gives the roulette two independent entries. `Reliquia Maldita`
uses grade `0` and the first native icon; `Reliquia Divina` uses grade `4` and
the fifth native icon. `EquipmentEntry<T>.CurseLevelOverride` stores this
per-result distinction even though both entries equip the same enum.

The Harmony postfix on `CharmCurse.CalculateLevel()` is gated by a setup-depth
counter. Prefix/finalizer pairs open that gate only around
`PlayerStatsManager.LevelInit()`, `LevelPlayerAnimationController.Start()`, and
`PlanePlayerAnimationController.Start()`. Consequently health, curse abilities,
weapon randomization and the matching player animation initialize at the chosen
grade, while map UI, graveyard state and win/progression calls still receive the
real saved value. Do not broaden the postfix to the complete battle: King Dice
and normal win paths query relic progress and must remain untouched.

Version 0.5.81 temporarily sets `ForceRelicTestSequence = true`. The first
roulette spin after loading the plugin selects Reliquia Maldita, the second
selects Reliquia Divina, and later spins alternate. Only the charm is forced;
all other results remain random. Set the constant back to `false`, remove the
temporary counter/helper, and bump the version after user acceptance.

Version 0.5.83 also sets `ForcePlaneRelicChallengeTestSequence = true` and
forces a four-spin matrix: Maldita + No bombas, Divina + No bombas, Maldita +
No Peashooter, then Divina + No Peashooter. `RandomBossForModifier()` therefore
chooses only a compatible plane boss from the current base/DLC availability
pool. The selector forces `uglyMode` on so the challenge is actually applied
even if the saved RETO setting is off. Remove both temporary selectors and
their counters/helpers after acceptance.

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

## Shared Spanish fight-title catalog (0.5.101)

The 0.5.65 fallback still allowed `SpanishAmerica` to become empty whenever a
native `<level>Selection` entry had no usable text; Esther Espuelas exposed
that case. `LocalizedFightName()` no longer reads `Selection`. It returns the
existing `BossEntry.Fight` catalog for both `SpanishSpain` and
`SpanishAmerica`, and returns an empty subtitle for every other language. The
large boss name remains localized through Cuphead. This is the user's explicit
policy and guarantees complete, identical level titles in both Spanish modes.

Version 0.5.101 also disables `ForcedTestChallenge` and
`ForceFiveSuperCardsForHudTest`; normal random results and real super meters
are restored.

## Roulette Devil victory returns to the map (0.5.102)

Cuphead's `WinScreen/<main_cr>.MoveNext()` waits until grading is finished and
the player confirms. It then checks `Level.PreviousLevel`: `Levels.Devil`
loads `Cutscene.Load(scene_title, scene_cutscene_outro, ...)`, while an ordinary
boss calls `SceneLoader.LoadLastMap()`. By this point `_OnPreWin`, grade and
progress updates, achievements, loadout restoration and
`PlayerData.SaveCurrentFile()` have already run.

The plugin records `returnToMapAfterRouletteDevilWin` only when `_OnPreWin`
belongs to a roulette-loaned `Levels.Devil` fight. A Harmony prefix targets only
the `Cutscene.Load(Scenes, Scenes, Transition, Transition, Icon)` overload and
requires that one-shot flag, `Level.PreviousLevel == Levels.Devil`,
`scene_title`, and `scene_cutscene_outro`. It calls `SceneLoader.LoadLastMap()`
and skips the cutscene. That reuses the established map-return cleanup for the
loaned loadout and battle HUD. Normal Devil victories do not set the flag and
retain Cuphead's complete ending.

The flag resets at both `BeginBattleResultHudSession()` and
`EndBattleResultHudSession()` so an abandoned or unrelated session cannot
redirect a future cutscene.

## Saltbaker ending bypass and HUD hold (0.5.103)

The same `WinScreen` coroutine has a second final-boss branch. After grading a
`Levels.Saltbaker` victory it calls
`Cutscene.Load(scene_map_world_DLC, scene_cutscene_dlc_ending, ...)`. Version
0.5.103 generalizes the one-shot state to
`returnToMapAfterRouletteFinalBossWin`. The existing `Cutscene.Load(Scenes,
Scenes, ...)` Harmony prefix now recognizes either the exact Devil pair or the
exact Saltbaker pair, calls `SceneLoader.LoadLastMap()`, and skips only that
roulette ending. Normal campaign victories set no flag and retain both stories.
All grade, progression, achievement and save work still occurs before this
interception.

Saltbaker can disable `LevelHUD.Canvas` before the scene actually changes to
WinScreen. Moving the roulette row to that native Canvas at `_OnPreWin` made it
disappear early. `KeepBattleResultHudThroughVictory(true)` sets
`battleHudHoldOverlayThroughVictory` only for Saltbaker, and
`ShouldShowBattleResultHud()` tolerates scene loading until `Level.Current` is
no longer a battle.

Version 0.5.105 no longer leaves that held row above the screen fade.
`PlaceBattleHudOnSceneTransitionLayer()` reads the private `SceneLoader.canvas`
through `SceneLoaderCanvasField`, reparents the same HUD root there and makes it
the first sibling. Cuphead's native fader remains a later sibling on the same
canvas, so its three-second black transition covers the roulette HUD and the
game image together. This preserves the row after Saltbaker disables
`LevelHUD.Canvas`, but removes the bright-overlay pop before grading. If the
SceneLoader canvas is temporarily unavailable, the persistent overlay remains
a safe fallback and the method retries every frame. Other bosses retain the
accepted native-victory clone/fade path through `LevelHUD.Canvas`.

Version 0.5.106 generalizes the temporary boss selector to `ForceTestBoss` and
`ForcedTestBossLevel`. Both are currently configured to force
`Levels.DicePalaceMain` for the King Dice HUD-chain test. The existing
`BattleHudUsesDicePalaceChain()` must preserve the snapshot, reveal progress
and impact count across every internal board/miniboss scene. Disable
`ForceTestBoss` after manual acceptance.

Version 0.5.107 fixes the early disappearance observed after Dice Palace
minibosses 2, 4 and 7 (and therefore applies to every internal miniboss). Those
wins do not call the final `KeepBattleResultHudThroughVictory()` path, so
`battleHudFollowNativeVictoryLayer` remains false. Previously,
`ShouldShowBattleResultHud()` hid the row as soon as
`SceneLoader.CurrentlyLoading` became true, before the screen fade began. A
Dice Palace chain now counts as a valid reason to survive loading. While an
internal load is active and the final-victory flag is still false,
`PlaceBattleHudOnGameplayLayer()` moves the root to the same
`SceneLoader.canvas`/first-sibling layer used by Saltbaker. The native fader
therefore covers it at the correct time. The real `DicePalaceMain` victory has
`battleHudFollowNativeVictoryLayer == true` and retains the accepted
`LevelHUD.Canvas` route.

Version 0.5.108 disables `ForceTestBoss` after the complete King Dice test.
Normal random boss selection is restored. `ForcedTestBossLevel` remains set to
`Levels.DicePalaceMain` only as a dormant future-test target.

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
- Charms: Galletita Astral, Reliquia Maldita, Reliquia Divina, and Anillo de
  Corazón.
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

Versions 0.5.84-0.5.87 make the map/reroll prompt device-aware. The last active
`Rewired.Controller` decides the presentation:

- Keyboard: `ABRIR RULETA  F6` and `VOLVER A GIRAR  F7`.
- Xbox-style controller: `ABRIR RULETA  LT + Y`; reroll uses `RT`.
- PlayStation-style controller: `L2` plus the native Equip glyph; reroll uses
  `R2`.
- Nintendo-style controller: `ZL` plus the native Equip glyph; reroll uses
  `ZR`.

The rightmost Equip button is still a real `CupheadGlyph` configured for
`CupheadButton.EquipMenu`, so Cuphead chooses the correct face-button symbol.
The physical trigger is a compact text capsule because there is no trigger
entry in `CupheadButton`. Controller identity selects `LT`, `L2`, or `ZL`.

The cloned PauseGUI row originally contains `CONFIRMAR`, glyph, action, glyph.
The mod disables the localization behaviour on the first text and reorders the
children to action, trigger, `+`, Equip glyph. Do not remove that behaviour
guard: localization otherwise writes `CONFIRMAR` over the plus sign.

For keyboard, `CupheadGlyph` can still receive `OnControlsChanged` after being
disabled and overwrite F6/F7 with the Equip binding. The cached-layout branch
therefore reasserts the expected manual key. `ConfigureManualPromptGlyph()`
also restores scale 1, disables best-fit, updates the root `LayoutElement`, and
uses a 35-unit minimum width for the rightmost F6/F7 capsule. The trigger
capsule keeps its separate compact sizing. Preserve the right edge constant
1290 and the 4.5-unit action-to-key gap unless the user requests a visual move.

The roulette dim layer must be behind the card and the native F7 prompt must be
in front of it.

Physical-controller validation is complete. The user tested the full flow with
a real controller and accepted it: prompt order and glyph, LT/L2/ZL plus Equip
open/close combo, D-pad/stick navigation, Accept/Cancel, and RT/R2/ZR reroll
with automatic loading disabled all work correctly. Keyboard F6/F7 remains
validated as well; the earlier Steam Link limitation is no longer an
outstanding test item.

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

The temporary relic test switches introduced in 0.5.81 and 0.5.83 are both
`false` as of 0.5.84. Do not report the current build as forced-plane or
forced-relic mode. The helper code remains available only as a dormant testing
facility.

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

- Version 0.5.117 builds with zero errors and zero warnings on 2026-08-07.
  Checklist labels now use a 360-unit single-line area while the value retains
  its original right-aligned rectangle. Release DLL SHA-256:
  `015F5AF17164EB8A83A71ACA2C619C2B080D2F18E6D31D1E48727F25D805CBC9`.
  The installed DLL has the same hash and `LogOutput.log` confirms
  `Gilomx Boss Roulette 0.5.117` loaded. Visual verification remains for Italian
  `CARICAMENTO AUTOMATICO` beside `DISATTIVO`.

- Version 0.5.116 builds with zero errors and zero warnings on 2026-08-07.
  Direct ordered comparison reports 29 Korean delivery entries, 29 dictionary
  entries and zero differences. Release DLL SHA-256:
  `222244391CD5F4BC3EA087CF319564C55A0DFA1254A669654287398C6FCCCD45`.
  The installed BepInEx DLL has the same hash, and `LogOutput.log` confirms
  `Gilomx Boss Roulette 0.5.116` loaded. Runtime visual verification remains:
  press `Ctrl+F8` seven times and inspect Hangul fit in the Equip Card, both map
  prompts and the battle challenge HUD.

- Version 0.5.115 builds with zero errors and zero warnings on 2026-08-06.
  Ordered comparison confirms that the 29 shared Spanish public values exactly
  match the original dictionary. Release DLL SHA-256:
  `885D23F74F3C5BEDC8E90405ED1BD46842BFB74715877E3FCE710965E91648DA`.
  It was not installed because Cuphead process 35736 remained open. After
  closing the game, install this newer DLL instead of the superseded 0.5.114
  build and verify Ctrl+F8 positions five and six show identical mod copy.
- Version 0.5.114 builds with zero errors and zero warnings on 2026-08-06.
  Direct ordered comparison against the German delivery reports 29 entries and
  zero differences. Release DLL SHA-256:
  `D6323C2A6D1D59B718EF96F8FBCF4DDAC695A9A31776B7AC9DBA6CB10B9C1000`.
  It was not copied into BepInEx because Cuphead process 35736 was still open;
  close the game before installation. Runtime verification then needs four
  `Ctrl+F8` presses to reach German and a fit check of card, prompts and HUD.
- Version 0.5.113 builds with zero errors and zero warnings on 2026-08-06.
  Direct ordered comparison against the Italian delivery reports 29 entries
  and zero differences. The installed DLL matches the release build with
  SHA-256
  `8C763BAE455AF49895AC2BC2EAC5BA6312FBEF488D976A7C59C41A753A4DA415`.
  Runtime verification remains: press `Ctrl+F8` three times to reach Italian,
  then inspect text fit in the card, map prompts and challenge HUD.
- Version 0.5.112 builds with zero errors and zero warnings on 2026-08-06.
  Direct ordered comparison against the user's French delivery reports 29
  entries and zero differences. The BepInEx DLL matches the release build with
  SHA-256
  `60D4E406EEFFB99C273309599E4401654A4D236AE0D7474240DB457A02462974`.
  Runtime verification remains: press `Ctrl+F8` twice from the original
  language cycle to reach French, then inspect card, map prompts and challenge
  HUD for text fit and accents.
- Version 0.5.111 builds with zero errors and zero warnings on 2026-08-06.
  Static comparison confirms that the 29 English dictionary values exactly
  match `translations/translation_english.md`. The release DLL was installed
  in BepInEx and its hash matches the build. SHA-256:
  `BFF31BE9AF7BFAD0D3E00DB07968D0F768D4FD873D72E8C01D70094AB1F55428`.
  Runtime verification remains: use `Ctrl+F8` once, open the card, spin with
  manual and automatic loading, and confirm the card, map prompts and challenge
  HUD all use the approved English copy.
- Version 0.5.110 builds with zero errors and zero warnings on 2026-08-06. The
  installed DLL matches the release build with SHA-256
  `FC251CF0ABAAC8FBF0CB8F3A188FA6858F746630D81F6ED983674F363C7D7A3F`.
  Manual verification must launch through Steam, press `Ctrl+F8` repeatedly,
  confirm the first result is English and all 12 languages cycle, then exit and
  confirm the original language returns.
- Version 0.5.109 builds with zero errors and zero warnings on 2026-08-06.
  Static verification confirms Spanish coverage for all 47 `ModText` values
  and finds no remaining `activeChallenge` string comparisons,
  `status.IndexOf()` localization logic or `ModifierEntry.Name` gameplay use.
  Runtime language tables are intentionally not active yet, so the immediate
  manual regression check is that Spanish presentation and all eight challenge
  behaviors remain unchanged.
- Version 0.5.79 builds with zero errors and zero warnings on 2026-08-06. The
  release DLL was installed over the temporary diagnostic build and its SHA-256
  matches the build output:
  `13DD9EAF39649E85BE2E7564ADA63144AB6627E23EAD061710C8E678F2B77AB4`.
  Manual testing then confirmed that successful parries no longer make the HUD
  disappear and reappear. The pause-state false positive is therefore the
  accepted root cause and version 0.5.79 is the accepted fix.
- Version 0.5.74 builds with zero errors and zero warnings and was installed
  successfully on 2026-08-06. `BepInEx\LogOutput.log` confirms
  `Loading [Gilomx Boss Roulette 0.5.74]` and the normal ready message. Manual
  acceptance must parry repeatedly in ground, plane, and later King Dice fights;
  then recheck pause, defeat/retry, loading iris, `Blanco y negro`, and knockout.
- Version 0.5.73 builds with zero errors and zero warnings and was installed
  successfully on 2026-08-06. `BepInEx\LogOutput.log` confirms
  `Loading [Gilomx Boss Roulette 0.5.73]` and the normal ready message. Manual
  acceptance showed that matching the native materials alone did not stop the
  parry blink; this result led to the full visibility/sorting isolation in
  0.5.74.
- Version 0.5.72 builds with zero errors and zero warnings and was installed
  successfully on 2026-08-06. `BepInEx\LogOutput.log` confirms
  `Loading [Gilomx Boss Roulette 0.5.72]` and the normal ready message.
- Version 0.5.71 builds with zero errors and zero warnings and was installed
  and launched successfully on 2026-08-05. `BepInEx\LogOutput.log` confirms
  `Loading [Gilomx Boss Roulette 0.5.71]` and the normal ready message.
- The ready-to-paste artifact is
  `dist/Gilomx-Boss-Roulette-0.5.71-BepInEx-x64.zip` (10,575,732 bytes,
  SHA-256 `8DB50162FBDEF4E630C18543FEB3072471F4CCD26FE66207D27392BD6361F217`).
  Its 122 entries were inspected: it contains the x64 Doorstop bootstrap,
  18 BepInEx core files, the 0.5.71 DLL, the complete asset tree including
  `impact_01.wav`, and no config, log, cache, save, or unrelated plugin files.
- Manual 0.5.71 King Dice testing passed the one-time animation behavior but
  found that later minijefes could still blink the row on parry because their
  native HUD canvas briefly became unavailable. Version 0.5.72 removes that
  remaining gate. Recheck parries across several internal `DicePalace*` scenes;
  the final `DicePalaceMain` knockout must still darken and remove the row with
  Cuphead's native health HUD.
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
