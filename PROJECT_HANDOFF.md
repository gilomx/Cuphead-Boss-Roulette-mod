# La Pichi Ruleta - Project Handoff

Current release: **La Pichi Ruleta 0.6.0**.

The clean installer is `dist/La-Pichi-Ruleta-0.6.0.zip`: 457 files, 433 tracked
mod assets, one plugin DLL and the 18-file BepInEx core. Its SHA-256 is
`0086CBA10D97276AF793AC45AB93BD3A24FAFBFEB78D75C3F11D09B04E4FFB0B`.
Rejected generated audio, configs, logs, caches, PDBs and unrelated plugins are
not included.

## Creator Tools: phase-transition handoff (2026-08-24)

The current work on `codex/creator-tools-config-panel` deliberately avoids a
generic player-input guard. Phase protection is session-only, defaults to on,
and can be disabled from Modo Molestoso for A/B testing. It affects manual
interaction tests, random test mode, and Pesky Mode through the shared dispatch
guard.

- Devil phase 1→2: `DevilLevelSittingDevil.StartTransform` signals the
  transition. Dispatch remains active for 6 seconds of playable time, then
  blocks. `DevilLevel.ZoomOut` forces activation if necessary and clears active
  catalog actors. The postfix on
  `DevilLevel/<disable_input_cr>c__Iterator3.MoveNext` ends protection when the
  iterator returns false.
- Saltbaker phase 1→2 only: the prefix on
  `SaltbakerLevelSaltbaker.phase_one_to_two_cr` starts a 2.5-second playable
  delay. New dispatches are blocked after that delay. The postfix on
  `AniEvent_HandsClosed` clears active interaction actors in the same frame as
  Saltbaker clears native phase-one objects and fires, while the hands cover
  the camera. The postfix on `AniEvent_RestorePlayers` ends protection when
  weapon, super, and player control have been restored.
- Saltbaker phase 2→3: the prefix on `SaltbakerLevelSaltbaker.OnPhaseThree`
  runs immediately after the native `KillFires` call. It blocks dispatch with
  no extra delay and clears active interaction actors. Protection ends when
  `SaltbakerLevel/<phase_two_to_three_cr>c__Iterator0.MoveNext` returns false,
  after the white fader is hidden and the phase-three bouncer is active. The
  normal scheduler chooses the next automatic interval after resuming.
- Saltbaker phase 3→4 is intentionally untouched pending manual review.

The general battle-entry delay has been removed. `Iniciando batalla` is a
visual load-intent state driven by `SceneLoader.LoadLevel`/roulette play and
`_OnLevelStart`; it must not pause, rearm, or alter actor scheduling. A duplicate
level-start registration must not clear the first actor.

Latest verification: Release build completed with 0 errors and 0 warnings. The
compiled and installed DLLs match at SHA-256
`AC3835257391AD60D779DA4089B1A1F4ED180F12FEBF9F81959ACCCDB5BD5022`.

## Cierre publicado para el siguiente agente (2026-08-19)

- `main` y `origin/main` quedaron sincronizados en `be9f388` (`Release La
  Pichi Ruleta 0.6.0`). El commit inmediatamente anterior es `a762958` (`Fix
  Equip Card with Creator Tools enabled`).
- La compilación final de 0.6.0 terminó con 0 errores y 0 advertencias;
  `node --check assets/creator-tools/overlay.js` también pasó.
- La prueba manual aceptó Modo Tieso, la continuidad del overlay en Rey Dado,
  `Reaparecer`, el logo durante la calificación, la Equip Card nativa y el
  atajo de mando `LT + botón Equip mapeado`.
- El ZIP final es el indicado arriba. Su DLL, el DLL compilado y la entrada del
  ZIP coinciden por SHA-256; el paquete contiene un solo plugin y ningún config,
  log, caché, PDB ni mod ajeno.
- El árbol rastreado quedó limpio. Permanecen sin rastrear `dist/`, `tmp/`, dos
  WAV sintéticos rechazados (`upside_down_cartoon_whistle.wav` y
  `upside_down_whoosh.wav`) y sus dos generadores. No usar `git add .` ni
  incluirlos en otra release.
- No queda un bloqueo funcional conocido para 0.6.0. El siguiente agente debe
  comenzar con `git pull`, leer esta cabecera y preservar el GUID, nombre de la
  DLL, carpeta `GilomxBossRoulette` y ruta de configuración existentes.

## Equip Card blocker resuelto (2026-08-18)

El commit `a762958` retiró toda activación/desactivación y todo parche sobre
`MapEquipUI`. La Equip Card nativa vuelve a abrir, navegar y cerrar normalmente
en el mapa con Creator Tools activo. La prueba manual quedó aceptada en la
misma sesión que el Stream Overlay.

La causa diferencial de Creator Tools era construir demasiado pronto el
catálogo web de configuración forzada: esa ruta refrescaba el catálogo de DLC
durante `Plugin.Awake`, antes de que `MapUI` terminara de inicializar la entrada
de la Equip Card. `CreatorToolsForceConfig` conserva `/api/config` en
`ready:false` hasta que `CanUseRouletteOnMap()` confirma que el mapa está listo.

La build siguiente restaura `LT + Equip` sin volver a tocar `MapEquipUI` ni
`get_CanPause()`: un postfix estrecho sobre
`AbstractPauseGUI.GetButtonDown(CupheadButton)` actúa sólo cuando la instancia
es `MapEquipUI` y el botón consultado es `EquipMenu`. Cuphead ejecuta primero
sus guards nativos; después el mod exige el botón Equip mapeado y LT en el mismo
joystick, abre o cierra la ruleta y consume únicamente ese edge. `Shift + LT`,
Equip sin gatillo y combinaciones entre jugadores siguen siendo nativos.

Mientras la ruleta está visible o termina su salida, ese mismo postfix devuelve
false para Equip y evita que nazca una segunda tarjeta detrás; al llegar
`cardVisibility` a cero queda liberado. No reintroducir
`MapEquipUI.enabled = false`, parches a `get_CanPause()`,
`IsControllerToggleModifierHeld()` ni lectura del combo desde `Plugin.Update`.
La prueba manual con mando ya confirmó que `LT + Equip` abre/cierra la ruleta y
que Equip sin gatillo conserva la tarjeta nativa. El detector usa el botón que
Cuphead tenga mapeado a Equip en ese mismo joystick, no un botón físico
hardcodeado.

## Logo fuera de partidas activas (2026-08-18)

El JSON del Stream Overlay publica `battleActive` por separado de `visible`.
`overlay.js` sólo selecciona la vista Logo cuando `battleActive` es falso; por
eso `Reappear` puede retirar el HUD al perder y volver a reproducir su entrada
sin insertar el logo entre ambas animaciones. El cache-buster actual es
`hud-logo-gap-10`.

La salida rápida sólo se activa cuando la sesión es terrestre, el HUD queda
oculto y la opción es `Reappear`: iconos de 260 ms separados por 180 ms y texto
de 200 ms tras 130 ms. Con cinco iconos suma 1.05 s. Las sesiones aéreas
conservan el perfil normal de 770 ms.

Al ocultarse para `Reappear`, los contadores publicados se reinician antes de
la escena nueva. Así el navegador no reproduce una entrada inmediata con los
cinco elementos de la escena anterior y otra 1.1 s después cuando el HUD nativo
comienza su revelado real. Ese reset sólo acompaña las rutas donde el HUD nativo
también reinicia su reloj; los huecos breves de capa conservan el progreso. Las
entradas de combate obedecen exclusivamente a `revealed/textVisible`; sólo
Vista previa programa una secuencia completa del lado del navegador. Los
hand-offs internos de King Dice mantienen el overlay visible y preservan el
revelado, mientras los hooks nativos de `Retry` y `Restart` marcan un reinicio
explícito, ocultan el overlay directamente y no lo liberan hasta
`SceneLoader.OnFadeInEndEvent`; por eso sí reproducen su salida y entrada aunque
Dice Palace conserve una capa de batalla válida durante toda la recarga.
Aunque ese evento llegue antes de completar los 1.05 s, el navegador deja la
nueva vista en cola: una salida rápida de retry no se puede cancelar a mitad y
`finishExitAnimation()` aplica después el estado más reciente.
El JSON separa `completeExit` (salida iniciada por Retry/Restart y obligatoria)
de `fastRetryExit` (sólo el perfil terrestre de 1.05 s). Así los retries aéreos
también terminan su salida normal y los huecos incidentales siguen cancelables.

La transición de victoria añade un hand-off estricto HUD → Logo: después de
`finishExitAnimation()` mantiene ambos nodos ocultos durante 80 ms antes de
`enterLogo()`. `hudToLogoTimer` bloquea estados Logo repetidos durante ese gap y
se cancela si el objetivo cambia a HUD o Hidden.

`creatorToolsBattleCompleted` se activa en el postfix de `WinScreen.Awake`,
después de validar los flags de victoria terminal preparados por
`KeepBattleResultHudThroughVictory`, sin cerrar aún
`creatorToolsBattleSessionActive`. Fuerza `battleVisible=false` incluso bajo
`Keep`, bloquea reaperturas/reveals tardíos y publica `battleActive` como
`sessionActive && !completed`. Así el logo aparece durante la calificación. Los
minijefes internos de King Dice no preparan esos flags terminales.

## Modo Tieso aceptado (2026-08-18)

`ModifierId.StiffMode` permanece correctamente clasificado como
`ModifierKind.Ground`: los combates aéreos normales no deben ofrecer un reto de
fijado que allí no tiene efecto. En tierra, el postfix de
`LevelPlayerMotor.HandleLocked` replica mantener pulsado el botón Lock sólo
mientras el jugador toca el suelo, bloquea el dash y permite dirigir el salto.

Rey Dado es la excepción porque su cadena terrestre contiene dos salas con
controles de avión. Cuando el resultado activo es Rey Dado + Modo Tieso,
`ShouldBlockMiniPlane()` reutiliza el guard de `NoMiniPlane` únicamente dentro
de `DicePalaceFlyingHorse` y `DicePalaceFlyingMemory`. El HUD y el resultado
siguen mostrando Modo Tieso; no cambian de reto al entrar en esas salas. La
prueba manual quedó aceptada el 2026-08-18.

El arte definitivo ya está integrado: `assets/modifiers/locked_01..03.png`
anima la ruleta a través de la convención normal de tres frames, el HUD usa
`locked_01.png` y Creator Tools resuelve su copia estática de 82 x 82 desde
`assets/creator-tools/modifiers/locked_01.png`. `challenge.stiff_mode` ya
está implementado en los doce idiomas mediante
`ModLocalization.LabelReview.cs`: inglés usa `LOCKED MODE`, ambos españoles
usan `MODO TIESO` y los otros nueve idiomas usan las entregas finales
registradas.

## Estado verificado para el siguiente agente (2026-08-17)

La ronda de localización descrita anteriormente como pendiente está cerrada:
las cinco etiquetas de retos nuevos y las 20 de Creator Tools se implementaron
para los doce idiomas en `ModLocalization.LabelReview.cs`. La etiqueta nueva
`creator.menu.logo` se mantiene aparte en
`ModLocalization.CreatorToolsBrand.cs`. Creator Tools convierte las cadenas
visibles a mayúsculas en runtime para reproducir el estilo de Cuphead.

El menú nativo ya no mezcla los objetos de texto independientes de etiqueta y
valor. `CreatorToolsMenu.LocalizedRows.cs` genera una sola fila centrada con el
formato `ETIQUETA: VALOR`, conserva en rojo únicamente el valor seleccionado,
ajusta el tamaño al ancho seguro y calcula el centro óptico usando los glifos
visibles. Esta solución fue comprobada al cambiar de idioma desde el menú y al
iniciar Cuphead directamente en otro idioma; también elimina el destello del
valor nativo anterior durante los cambios de opción.

La página de Stream Overlay contiene diez filas: Status, Preview, Retry, Size,
Order, Alignment, Opacity, Logo, Copy URL y Back. `LOGO` persiste mediante la
clave histórica `Creator Tools/MostrarNombre`. Cuando el HUD del resultado no
está visible y el overlay sigue activo, `overlay.js` muestra el nombre del mod
y la etiqueta `MOD`. HUD y logo comparten la misma celda, pero una máquina de
estados termina la animación de salida antes de iniciar la entrada del otro,
por lo que nunca se superponen. El nombre y la etiqueta tienen flotaciones
leves con duraciones/fases distintas; la etiqueta mide 1.4 veces el recurso
original. El cache-buster actual es `logo-sequence-4`.

### Estado de los interruptores de prueba tras `5ffa895`

- `EnableLanguageTestShortcut = false`: `Ctrl+F8` volvió a quedar dormido.
- `ForceQueenBeeInkRainLoadoutForTesting = false`: el forzado de Reina Abeja y
  Lluvia de tinta quedó desactivado.
- `ForceLongestOverlayChallengeForTesting = false`: el antiguo forzado de
  `MiniPlaneOnly` está apagado.

No queda ningún selector de prueba activo. La compilación de cierre terminó con
cero errores y cero advertencias; el DLL y
los assets de overlay instalados se verificaron por SHA-256. Las pruebas
manuales confirmaron el menú multilingüe, los retos y el comportamiento del
overlay. La ronda de `challenge.stiff_mode` quedó cerrada con 12/12 idiomas;
crear un inventario nuevo cuando se acuerden más IDs, sin reabrir las 25
etiquetas ya cerradas.

## Creator Tools scope after 0.5.131 (2026-08-16)

### Interaction catalog (updated 2026-08-22)

The reserved web configuration hierarchy is now `/config/roulette` for the
existing force panel and `/config/interactions` for provider-agnostic audience
effects. All three routes load the same React SPA; `/config` opens Roulette by
default while client-side navigation keeps the shared shell mounted.

`CreatorToolsInteractionController.cs` owns the main-thread interaction queue
consumer and the executor registry. The local server may accept and queue
commands on its background thread, but it must never instantiate or inspect
Unity objects there. Keep future catalog items out of `Plugin.cs` and add
isolated `ICreatorToolsInteractionExecutor` implementations behind the
controller instead. Public IDs live in `CreatorToolsInteractionIds.All`; that
same registry feeds the random test. The React `interactionItems` collection
feeds both catalog cards and manual-test rows. Every future item must be added to
both paths, with no one-off random-test list.

The first five test items use Cuphead's original runtime mapping:
`hilda_purple_zeppelin` uses `enemyPrefabA` and its native single shot, while
`hilda_green_zeppelin` uses `enemyPrefabB` and its native spread attack. In
Hilda both go through `SummonEnemy()`. On the map, `NativeZeppelinCache`
additively preloads the Hilda scene with guarded `Level` lifecycle methods,
captures inactive clones of both prefabs, deactivates the temporary scene roots
and unloads that scene. Each retained graph contains the original sprite
sequences, animator controller, clips, projectile prefab, effects and death
pieces. Other battles and platforming levels instantiate the requested graph
with fresh `LevelProperties.FlyingBlimp.GetMode(CurrentMode)` values and a
camera-relative native entry position. The mod adds only the shared catalog
presentation: front sorting, camera-size normalization and the donor label. The
gameplay actors are never rebuilt from extracted art; the two web previews are
the native first idle frames generated by
`tools/extract_native_zeppelin_previews.py`.
`NativeZeppelinSpawnPattern` chooses a random safe lane from 120–610 and tries
to keep at least 165 units from every active donated zeppelin. It samples the
native `stopDistance`, shifts it right by a second random 55–105 units and
clamps the attack point to 390–535 so donated enemies do not travel as far left.
`SummonEnemy()` replaces its argument with another native sample, so the
executor writes the selected value to the spawned enemy immediately afterward.
The donor name is an independent world-space `TextMeshPro` using Cuphead's
Memphis font. `CreatorToolsInteractionPresentation.PrepareActor` is mandatory
for every future visual catalog item: it brings the actor forward, creates the
label and keeps presentation failures nonfatal. The follower captures one
offset above the first valid root sprite, follows the actor without recomputing
animated bounds, then holds its final position and fades text plus outline for
0.6 seconds after the actor is destroyed. The fade uses
`CupheadTime.GlobalSpeed`, so it freezes with gameplay during pause and defeat.
Do not parent the label to the actor, destroy it from the actor's teardown, or
replace it with `OnGUI`; all three break this lifecycle. The canonical contract
and test checklist are in [INTERACTION_CATALOG.md](INTERACTION_CATALOG.md).
The type-A pink-projectile cadence counter remains native.

`rootpack_homing_carrot` is the small native
`VeggiesLevelCarrotHomingProjectile` created during Psycarrot's phase. The
executor targets an active player and preserves Cuphead's difficulty-specific
speed, rotation, health, collision, damage and death behavior. Its required
`VeggiesLevelCarrot` parent is a disabled persistent instance. The projectile
has no mod TTL: it retains native death from shots, player/ground collision and
its 1000-second native fallback, occupying one active slot until then. Scene end
and disposal still destroy it. It chooses any X along the upper viewport edge;
after creation its active sprite bounds move until their lowest pixel sits 16
base units above that edge, so body and label enter from fully offscreen. The
main visual renderer is passed explicitly to the shared donor-label presentation
because the projectile can render below its root. Its preview is extracted by
`tools/extract_native_homing_carrot_preview.py`.

`cagney_homing_plant` uses native blue seed variant `A` from
`FlowerLevelEnemySeed`; its `OnSpawnPlant` animation event creates the homing
`FlowerLevelVenusSpawn`. The seed starts fully above a random viewport X and
uses current-difficulty `LevelProperties.Flower`. Ground and platforming levels
retain native ground collision. Plane levels suppress that callback, and every
seed has a common fallback: once its complete sprite passes 16 base units below
the lower viewport edge, the runtime freezes its fall and invokes native
`OnSeedLand` there. This lets the plant grow offscreen and fly back naturally
when a level has no floor.

`CagneyHomingPlantInteractionState` owns the complete seed-to-plant lifetime, so
one queue slot remains active until the resulting plant dies. A scoped Harmony
postfix identifies the Venus instance created by this catalog seed. The same
donor label is created hidden while the seed falls, rebinds to the plant and
starts a 0.45-second fade only after the plant renderer intersects the viewport.
It tracks changing bounds for only 0.55 seconds of growth before locking its
offset again; never create a second label for this transition. Donor-label world
scale always uses absolute components so a native negative X orientation cannot
mirror the text. The plant inherits camera-size normalization
through a scaled wrapper, while its own local X scale returns to native `±1`.
This is required because native `move_cr` multiplies movement by local scale;
scaling the plant root directly changes speed. The wrapper keeps sprite and
collider proportional without changing HP, damage, rotation or movement. Its
preview comes from `tools/extract_native_cagney_homing_plant_preview.py`.

`frogs_firefly` uses the native `FrogsLevelTallFirefly` prefab and the current
difficulty's `LevelProperties.Frogs.CurrentState.tallFireflies` values. It
starts at a random safe viewport Y with both its sprite and donor label fully
beyond the right edge, then moves toward an initial viewport X chosen uniformly
between 0.78 and 0.84. This shortens the former 0.72 entrance, varies where each
firefly settles and keeps the complete donor label inside the safe right edge.
After that entrance, its untouched native coroutine repeats the original
follow-delay and eased approach toward the selected player. Native HP, damage,
initial invincibility, collision death and death animation remain intact; there
is no mod TTL, so the queue slot remains active until it dies or the level ends.

`initialMove_cr` forces the firefly's local X scale to one. Camera-size
normalization therefore lives on a wrapper while the native actor retains its
own local scale. The handle owns that wrapper and cleanup removes it after the
actor dies. The preview comes from
`tools/extract_native_frogs_firefly_preview.py`.

The persistent firefly template stays inactive between spawns. It must be
temporarily activated immediately around native `Create`, with deactivation in
a `finally`: `AbstractProjectile.Create` copies the template's active state and
`Init` starts `initialMove_cr` synchronously. Creating the clone inactive makes
Unity discard that coroutine and leaves an offscreen actor occupying its queue
slot forever. Creator Tools also refuses to dispatch while
`CupheadTime.GlobalSpeed <= 0`, including the focus-loss pause used when moving
between the browser panel and the game.

`CreatorToolsInteractionRenderPriority` reasserts actor and donor-label sorting
on every `LateUpdate`. Normal gameplay uses `ForegroundEffects`, ahead of boss
foreground layers but below UI and global overlays. While a visible
`PlayerScreenEffectController` cover exists, both temporarily use `Enemies` so
bomb-transformation darkness and similar filters render above them. Captain
Brineybeard's ink is a separate `PirateLevelSquidInkOverlay`, so the same check
also treats its root `SpriteRenderer.enabled` lifetime as a cover. That native
renderer lives on `Foreground`; moving catalog content to `Enemies` therefore
keeps the ink above actor, donor label and marked zeppelin bullets from the
first splat through the complete fade. Never use the globally highest sorting
layer for catalog actors.

Zeppelin bullets are unparented roots, so they cannot inherit their shooter's
render-priority component. Harmony snapshots active
`FlyingBlimpLevelEnemyProjectile` instances immediately around `FireSingle` and
`FireSpreadshot`; only new projectiles from an enemy already marked with
`CreatorToolsInteractionRenderPriority` receive `BringActorToFront`. Native
Hilda enemies and every unrelated projectile remain untouched. The projectile
then follows the same normal `ForegroundEffects` and temporary covered
`Enemies` behavior as its catalog shooter.

`CreatorToolsInteractionPresentation.MatchGameplayCameraScale` preserves visual
size between bosses by multiplying native root scale by
`(camera.orthographicSize * 2) / 720`. Cuphead's base camera height is 720, while
zoomed-out fights such as Chef Saltbaker expose more world units and otherwise
make imported actors look tiny. Scale the complete root so renderers and
colliders stay aligned and the label preserves the same apparent spacing.
The renderer gap multiplies explicitly; the local fallback passes through
`TransformPoint`, which already applies actor scale and must not be multiplied
again.

Per-item donor-label adjustments use reference pixels and are scaled by the
same camera factor as the shared 14-pixel gap. Cagney's homing plant adds `+10`
for a final vertical gap of 24 pixels. The Frogs firefly adds `-70` for a final
vertical gap of -56 pixels. Zeppelin variants and the homing carrot remain at
zero. `SetVerticalOffsetPixels` applies the adjustment after `PrepareActor` or
`RebindTo`, keeping the seed-to-plant transfer and zoom correction shared.

Native scene preloads must suppress global scene lifecycle components in
addition to each boss-specific `Level` implementation. The four source scenes
contain temporary `AudioManagerComponent`, pause GUI, HUD, player manager,
player input and gameplay camera instances. Letting their `Awake`, `Start`,
enable/disable or destroy callbacks run can replace live singletons and corrupt
audio, controls, pause and camera state. `InstallGlobalLifecycleGuards` patches
those callbacks with each cache's scene-scoped preload prefix. Keep this guard
when adding a new native source scene.

Known limitation: the normalization currently covers the catalog actor root,
not independent objects spawned later. `FlyingBlimpLevelEnemy.FireSingle` and
`FireSpreadshot` call `BasicProjectile.Create`, whose `AbstractProjectile.Create`
path instantiates each bullet as an unparented root. Consequently, zeppelin
bullets remain at native world scale and look about 18.9% too small in Chef
Saltbaker (`camera.zoom = 0.811`) even though the zeppelin itself is corrected.
A future fix should scope a temporary spawn context to catalog zeppelins and
apply their camera factor to the returned projectile root exactly once. Scale
the complete projectile root so its sprite and collider stay aligned; do not
mutate shared native prefabs or change damage and speed.

`NativeInteractionPreloadCoordinator` serializes the Hilda, Root Pack, Cagney
and Frogs additive scene captures. Never start two native catalog scene loads concurrently
while either operation is waiting below scene activation; Unity's async scene
queue can stall. Each cache retains its own narrow Harmony lifecycle guard,
scoped by `__instance` to only its temporary scene, and releases the shared
preload slot after unload or disposal. Never replace that prefix with a global
boolean-only suppression: a real fight may begin while a prefab is loading.

Normal door entry and roulette entry now share the same interaction lifecycle.
`_OnLevelStart` remains the primary registration point, with an instance-ID
poll of the stable `Level.Current` as a fallback for native paths whose singleton
settles after the hook. Both paths retain the 2.5-second start guard. The map
is still the preferred preload window; after that guard, a stable unpaused
Battle/Platforming level may finish serialized caches that did not start before
the player entered. Every cache first captures its source from the real loaded
fight and refuses to additively load a second copy when that source scene is the
current gameplay scene. Loading, pause, defeat, results and maps never dispatch.
If a retry reuses the same `Level` instance, the next `_OnLevelStart` clears the
previous attempt's active actors before rearming; pending queue entries survive.

`CreatorToolsInteractionQueue` is the authoritative runtime queue. Test calls
accept `item`, `donor`, `quantity`, and `delay`; mixed batches append in arrival
order. Maximum-on-screen is persisted and configurable from 1–20, batches are
capped at 50, delays at 3600 seconds, and active plus pending entries at 200.
Dispatches remain at least 0.35 seconds apart. An active row remains in the API
until its generic interaction handle completes, then the next pending row may
dispatch. The random test chooses any registered and currently available ID at
varying 1.25–3.25 second intervals without building a backlog. Spawning is
blocked during loading, real pause, defeat/end-of-level and the first three
seconds after `_OnLevelStart`; actors already visible remain frozen on defeat
and are cleaned when the `Level` is destroyed.

The React provider also owns a transient optimistic projection. A test batch is
appended immediately with negative local IDs and `waiting_game` status because
Unity may stop calling `Update` while Cuphead is unfocused. Once the interaction
revision changes, those rows are removed and replaced by the authoritative API
queue in the same render. This projection is display-only: it must never spawn,
reorder, or confirm gameplay effects.

Creator Tools intentionally stops here for the current release: the native map
menu, WebSocket overlay, overlay presentation settings, retry behavior, preview,
and clipboard URL are the supported public scope.

Future work may add live-stream interaction providers to the same Creator Tools
area so audience events can influence roulette configuration or results. Keep
that work provider-agnostic at the configuration boundary; do not couple the
existing local overlay server to one streaming platform. Any provider
integration must remain optional and must not change normal roulette behavior
when disabled or unavailable.

## Creator Tools localization audit (2026-08-16, closed 2026-08-17)

The 25 audited in-game IDs now have approved runtime values in all 12 Cuphead
languages: five challenge names and 20 Creator Tools strings. Normal boss,
weapon, super and charm names still come from Cuphead.

The runtime call audit removed eight stale IDs from the public translation
scope: `creator.action.copy_overlay_url`, `creator.feedback.copied`,
`creator.controls.change_back`, `creator.status.server_disabled`,
`creator.status.no_port`, `creator.status.client`, `creator.status.clients` and
`creator.status.port_updated`. They are reachable only from the abandoned
IMGUI menu helpers (`DrawCreatorToolsMenu`, `CreatorToolsMenuValue` and
`CreatorToolsServerStatus`), which have no callers. The native Cuphead menu uses
`creator.action.copy_url` and `creator.feedback.url_copied` instead.

`translations/PENDING_LABEL_LOCALIZATION_REVIEW.md` preserves the literal
deliveries and `translations/LOCALIZATION_STATUS.md` records the completed
stage. The native menu now uses the 12-language catalog. The compiled `/config`
prototype remains deliberately excluded and unannounced, so do not add its
labels or special equipment values retroactively to this completed round.
Preserve
`LA PICHI RULETA`, `CREATOR TOOLS`, `Cuphead`, numeric scales, percentages and
the local URL without translation.

## Roulette winner on the grade screen (2026-08-16)

Immediately before normal victory restoration,
`CaptureRouletteWinCharacters()` records the live
`PlayerStatsManager.isChalice` state for both players. This is deliberately the
actual battle character rather than an inference from the selected charm.
Non-Chalice victories still restore both temporary-loadout snapshots from the
`Level._OnPreWin` prefix, before Cuphead saves the victory.

`WinScreen.Awake` has a prefix/postfix guard. The prefix reapplies the captured
character flags to `Level.ScoringData`, the same source vanilla uses to choose
its artwork. The postfix reinforces the chosen one-player root if necessary and
then clears the pending snapshot. Loadout restoration remains in `_OnPreWin`,
so loaned equipment is never intentionally left for Cuphead's native victory
save.

Runtime evidence showed the Chalice GameObject already active but invisible on
the grade screen, followed by a missing Chalice return animation and a delayed
base character on the map. Deferring Astral Cookie restoration through
`WinScreen.Awake` did not change either symptom. The actual cause was the same
transient false `DLCManager.DLCEnabled()` result that had already removed DLC
charms from Creator Tools. `PreserveConfirmedDlcPostfix` now returns true to
native Cuphead callers after this process has positively confirmed ownership,
so `AssetLoader` can load Chalice result/map visuals. Base-game-only sessions
remain false because their ownership cache is never set.

## Sticky DLC ownership for Creator Tools charms (2026-08-16)

`RefreshAvailableContent()` now treats a successful `DLCManager.DLCEnabled()`
result as valid for the lifetime of the Cuphead process. Runtime evidence showed
the game reporting DLC available at startup and then transiently returning false
after focus/scene changes. The next roulette spin rebuilt every content pool as
base-game-only; `SanitizeCreatorToolsForceSelection()` consequently replaced a
selected DLC charm with Empty even though the browser had sent the correct
`charm` index.

The cache remains conservative for base-game-only installations: they stay
base-only unless a later refresh positively confirms DLC ownership. Once true,
ownership cannot fall back to false until Cuphead is restarted. This rule must
remain shared by normal random selection and `/config` forcing so their visible
catalogs cannot disagree.

The same sticky value now guards Cuphead's own public
`DLCManager.DLCEnabled()` through a Harmony postfix. This is required because
native `AssetLoader<T>` consults that method before loading DLC assets. Do not
limit the cache back to `RefreshAvailableContent()`: Creator Tools would keep
showing DLC entries while the game silently refused to load their visuals.

## Creator Tools internal force-panel prototype (2026-08-16)

This prototype may remain compiled, but it is not part of the next public
update: do not link it from the game, README, release notes or translation
templates. Preserve the implementation notes below only for future development.

Creator Tools now serves a recording-oriented configuration page at
`http://127.0.0.1:18081/config` from the same local listener as the browser
overlay. `CreatorToolsServer.cs` owns `/config`, `/config.css`, `/config.js`,
`/api/config` and `/api/config/set`; `CreatorToolsForceConfig.cs` publishes the
localized runtime catalog, drains queued browser commands on Unity's main
thread and creates an exact `RouletteResult` before normal random selection.

The page exposes Boss, Shot 1, Shot 2, Super, Charm and Challenge plus the
session-only `FORZADO ACTIVO` checkbox. Its catalog honors the installed DLC
and experimental challenge switches. Shot 1 rejects Empty, Shot 2 rejects a
duplicate of Shot 1 but may be Empty, and Challenge only shows `both` plus the
selected boss's `ground` or `plane` entries. Enabling the checkbox makes every
new spin use the selected values; disabling it immediately returns spins to
the normal random path. The state is deliberately not persisted and starts
disabled on every Cuphead process.

Cuphead suspends its normal Unity `Update` while another window has focus, but
the HTTP listener continues running on its background thread. Browser changes
therefore remain queued and the page reports `PENDIENTE: VUELVE A CUPHEAD`
until focus returns and `UpdateCreatorToolsForceConfig()` applies them. Future
work must preserve that main-thread boundary; do not mutate roulette or Unity
state directly from the server thread.

The native navigation shipped in the same batch renames the map pause entry
and compact hub to `LA PICHI RULETA`. The hub borrows Cuphead's main Options
card and contains the normal `STREAM OVERLAY` row plus Back. The overlay page
continues borrowing the larger Visual card and now orders Status, Preview, On
Retry, Size, Order, Alignment and Opacity before the centered Copy URL action
and Back. Preview turns off when leaving the overlay page. Copy URL uses a
dedicated clone of Cuphead's centered bottom action text so the two-column
Visual layout cannot displace it. Long challenge fallback text in `overlay.js`
shrinks down to 60 percent and then wraps inside the safe width instead of
leaving the browser canvas.

Validation completed on 2026-08-16: `/config` and `/api/config` returned HTTP
200, the rendered page was visually checked at 1440x1000, JavaScript passed
`node --check`, and a temporary plane-boss selection (Hilda Berg, fixed
equipment and `NO DISPARO BOMBAS`) was accepted by the running game. The final
server state was confirmed with `enabled:false`.

## Creator Tools naming and retry behavior (2026-08-15)

The map pause entry and main native card are now named `CREATOR TOOLS`; the
roulette broadcast feature is identified separately as `OVERLAY DE RULETA`
(`ROULETTE OVERLAY` outside Spanish). The main card acts as a small tool hub:
it opens the overlay-specific native settings page, keeps temporary Preview
immediately below it, and retains the centered Copy URL action. This leaves a
clear container for future creator features without crowding Cuphead's fixed
six-setting Visual card.

The overlay settings page contains Status, On Retry, Size, Order, Alignment and
Opacity, with the native bottom action returning to the Creator Tools hub.
Cancel also returns to the hub first; cancelling again restores Cuphead's
original Visual menu objects and closes back to the pause menu. Preview still
turns itself off only when the complete Creator Tools menu is closed.

`AlReintentar` is persisted under the existing `Creator Tools` BepInEx config
section. Its default is `REAPARECER`, preserving the accepted recording
behavior: defeat plays the overlay exit and retry replays its entrance in sync
with the battle HUD. `MANTENER` is the streaming behavior: temporary HUD/battle
visibility gaps cannot hide the overlay or reduce its reveal state, so it stays
stable across any number of retries. The definitive
`EndCreatorToolsBattleSession()` still performs the normal exit on victory,
abandonment/return to map or replacement by a new roulette session.

Six new pending localization IDs describe this hierarchy and behavior:
`creator.menu.roulette_overlay`, `creator.menu.status`, `creator.menu.retry`,
`creator.retry.keep`, `creator.retry.reappear` and `creator.action.back`.
Creator Tools therefore has 27 pending strings; with the five new challenge
names, the current pending catalog contains 32 IDs. Runtime still uses the
established Spanish/English fallback until all 12 language reviews are approved.

## Localization documentation split (2026-08-15)

The 12 active files under `translations/` now separate the established
29-ID approved catalog from 32 new IDs awaiting review: five challenge names
and 27 visible Creator Tools strings. `translations/LOCALIZATION_STATUS.md`
is the source of truth for this temporary stage. Historical deliveries under
`translations/review_by_language/` remain unchanged.

After all 12 new deliveries are approved, move the 32 rows into each approved
table, archive the reviewed deliveries, update runtime dictionaries, perform
font/width regression, and remove the temporary approved/pending split.


## Forced five-challenge acceptance sequence (2026-08-15)

All five completed new challenges are enabled: RGB Shift, Upside Down, Ink
Rain, Half Damage and HP.1. The temporary selector
`ForceNewChallengeSequenceForTesting` is now false after acceptance. When
enabled, it cycles through those five challenges while the boss remains random
within the compatible pool.

The older per-challenge `Force...ForTesting` switches remain false; enabling
all of them would not form a sequence because `ForcedTestChallenge` resolves
them by priority. Normal builds keep the sequence off and select randomly from
all compatible enabled challenges. Keep the five `Enable...Challenge` switches
true while the project intends to publish all five.

## Complete Creator Tools icon set (2026-08-15)

Creator Tools now owns a complete static overlay-only set for all 34 unique
result images: one native white Empty icon, nine weapons, three supers, nine
charms and twelve challenges. The five final missing challenge deliveries were
normalized from `trippy.png`, `flip.png`, `hp1.png`, `ink.png` and
`halfdamage.png` to `assets/creator-tools/modifiers/rgb_01.png`,
`upside_down_01.png`, `hp1_01.png`, `inkrain_01.png` and
`halfdamage_01.png`.

`overlay.js` redirects every non-empty `weapons/`, `supers/`, `charms/` and
`modifiers/` runtime path to the matching file below `assets/creator-tools`.
Empty continues using `assets/creator-tools/empty.png`. This routing is confined
to the browser overlay: the equip-card roulette and in-game battle HUD continue
using their normal animated/original assets. Keep the overlay filenames aligned
with `RouletteData` first-frame paths when future equipment or challenges are
added.

## Final animated art for the new challenges (2026-08-14)

The five implemented new challenges now have their final three-frame 80 x 80
RGBA icon sequences under `assets/modifiers`: `rgb_01..03`,
`upside_down_01..03`, `hp1_01..03`, `inkrain_01..03` and
`halfdamage_01..03`. The source deliveries named Trippy, Flip, HP1, Ink and
Half Damage were normalized to the runtime identifiers; the corrected Trippy
files are the RGB sequence and contain no stray edge pixel.

`RouletteData.Modifiers` points each of these challenges at frame 01 and uses
the normal three-frame convention. The equip-card roulette therefore animates
them at 12.5 fps (80 ms per frame), while the battle HUD and Creator Tools keep
using frame 01 as their static representation, matching established challenge
behavior.

No feature switches changed with the art integration. Ink Rain remains enabled
for ordinary selection. RGB Shift, Upside Down, HP.1 and Half Damage remain
dormant (`Enable...Challenge = false`) until their deliberate release and any
remaining short regression passes; every `Force...ForTesting` switch remains
false.

## Creator Tools first development build (2026-08-13)

The disabled isolation build confirmed that Creator Tools exposed the native
`ELIMINAR JUGADOR 2` row. Root cause is Cuphead's hard-coded
`LevelPauseGUI.OnPause()` access to `menuItems[4]`: inserting the mod row at 4
shifted Player 2 Leave to 5, so Cuphead toggled the mod row and left its original
Player 2 row active. The compatibility prefix now gives `OnPause()` the original
eight-item array; its postfix restores the extended array and refreshes colors.
This preserves Cuphead's multiplayer visibility logic instead of duplicating it.
Creator Tools is enabled again and the corrected DLL is installed for manual
single-player plus local-co-op verification.

The menu layout was also revised. Six adjustable settings keep native Visual row
positions, while URL copy uses Cuphead's separate centered bottom action row
(the slot normally used by Back) and displays only `COPIAR URL`/`URL COPIADA`.
This removes the overflowing `COPIAR URL DEL OVERLAY` label/value pair and uses
the common large options card shared by Visual and Audio more faithfully. Build
verification remains 0 errors and 0 warnings.

That original single-page order was superseded by the Creator Tools hub and
Overlay de ruleta page documented at the top of this handoff. Opacity retains
the safe 25-100% range but advances in 5-point increments. Preview is
deliberately session-only: enabling it starts Creator Tools automatically if
necessary, then the browser reveals its five simulated icons and challenge
label in the normal entry order. Closing the complete Creator Tools screen
publishes the hidden state and reproduces the complete exit before resetting
Preview, so it cannot remain visible in OBS.

`dist/Las-Pichi-Ruleta-Creator-Tools-Iconos-1X-2X.zip` contains the exact 34
unique PNG paths currently sent by the overlay: 10 weapon/empty, 3 super, 9
charm and 12 challenge images. Current equipment art is 72x72 and challenge art
is 80x80, while CSS displays icons at 92x92 in 1X and 184x184 in 2X and pulses
them to 107.5% (about 198 px at 2X). Replacement artwork should therefore use a
single consistent 256x256 transparent RGBA canvas; that downscales cleanly at
both modes and retains headroom during the reveal pulse. Keep artwork centered
with consistent internal margins across every category.

Direct inspection of Cuphead's `atlas_equip_icons` and
`atlas_equip_icons_dlc` bundles confirmed that the original weapon, super and
charm sprite rectangles are 80x80; the native empty sprite is 73x73. There is no
higher-resolution official equipment source in the shipped game. The mod's
72x72 PNG copies are slightly smaller exports, but recovering the native 80x80
only adds eight pixels and still requires upscale at the overlay's 184px 2X
size. The 256x256 replacement recommendation therefore remains correct.

The browser overlay now exits instead of hiding in one frame. It follows the
same sequence and timing as entry: visible icons leave from first to last with
the same 280 ms stagger and the challenge label scales/fades last. Each icon
uses the same 380 ms pulse shape as entry. A renewed visible state cancels the
exit cleanly for transient scene/HUD changes, while socket errors still hide
immediately. Overlay icon spacing is now 8 px at 1X and 16 px at 2X instead of
using a negative gap.

`assets/creator-tools/empty.png` is a static 73x73 extraction of Cuphead's
native `equip_icon_empty_0001` sprite. Its original alpha silhouette is kept
and its visible pixels are white, exactly matching the transformation used by
the in-game battle HUD. Every overlay slot whose data path is
`weapons/vacio.png` is redirected to this PNG; no runtime-generated HTTP image
is required. `tools/extract_native_empty_icon.py` documents and reproduces the
extraction from `atlas_equip_icons` when UnityPy and Pillow are available.

The nine non-empty ground weapon icons now have overlay-only 82x82 test assets
under `assets/creator-tools/weapons`. The browser redirects `weapons/*.png` to
that directory while `weapons/vacio.png` keeps using the native white empty
sprite. Roulette cards and the in-game result HUD continue reading the original
`assets/weapons` files. CSS display dimensions remain 92x92 at 1X and 184x184
at 2X, so the larger artwork is tested without changing overlay layout.

Creator Tools size now offers three native menu values: 1X, 1.5X and 2X. The
saved setting migrated from `ConfigEntry<int>` to `ConfigEntry<float>`; existing
`1` and `2` config values remain valid and normalization snaps arbitrary values
to the nearest supported size. JSON writes the float with invariant culture so
Spanish Windows locales cannot produce invalid `1,5` JSON. The 1.5X CSS metrics
are proportional: 138 px icons, 12 px icon gap, 21 px section gap, 51 px text
fallback and 114 px native-label height limit.

Horizontal alignment now applies to the overlay content's children as well as
the outer stage. Left, center and right set `#content` to `flex-start`, `center`
or `flex-end`, so the icon row and both native-image/fallback challenge labels
share the same corresponding edge instead of leaving the text centered.

### Creator Tools pending work

The Dice Palace is the known special case still pending. The in-game result HUD
already treats its board, miniboss scenes and King Dice as one roulette level,
but Creator Tools currently observes each miniboss scene as a new overlay
session. Consequently the browser clears and replays its entrance whenever the
player changes miniboss. The fix must preserve one Creator Tools session ID and
one completed reveal state across every `DicePalace*` scene belonging to the
same `Levels.DicePalaceMain` run. It must animate only at the real beginning,
remain stable through board/miniboss transitions and exit only after final
victory or abandonment. Defeat/retry inside the chain must retain the same
result without replaying the entrance unless the whole roulette attempt truly
starts over.

Manual verification still pending for the next session:

- Complete a full Dice Palace route and confirm no overlay entrance repeats.
- Recheck 1X, 1.5X and 2X with left/center/right alignment and both vertical
  orders, including a challenge label wider and narrower than the icon row.
- Spin a ground result containing `Nada` and confirm the native white PNG is
  visible; also inspect the nine overlay-only 82x82 weapon replacements.
- Verify Preview entry/exit, real battle exit timing, one airplane battle and a
  local cooperative session in OBS/browser-source conditions.

Overlay HTML/CSS/JS are now served with `Cache-Control: no-store` so OBS reloads
presentation updates; static PNG icon assets retain their one-hour cache.

The first Creator Tools implementation is installed but is not yet a public
release. `CreatorToolsServer.cs` owns a loopback-only `TcpListener` that serves
the browser source and upgrades `/ws` with a small in-process RFC 6455 server.
It supports text frames, ping/pong, close, multiple clients and clean shutdown;
the network threads never inspect Unity objects. The Unity thread replaces one
immutable JSON snapshot and the broadcaster pushes only revisions. An isolated
test passed the HTML request, `101 Switching Protocols`, a state frame, and the
automatic port fallback from occupied `18081` to `18082`.

`CreatorToolsOverlay.cs` publishes the existing `battleHudResultSnapshot` rather
than creating a second gameplay state. Ground sessions expose five icon paths;
airplane sessions expose charm and challenge. Reveal messages are emitted from
`UpdateBattleResultHudReveal()` at the same icon/text boundaries as the in-game
HUD. New sessions, temporary battle-layer hiding, retry continuity and final
cleanup all follow `Begin/EndBattleResultHudSession()`. Preview is overridden
and disabled by a real battle. The localized challenge label is rendered on the
Unity thread from `battleHudChallengeText` into a cached transparent PNG, so the
browser uses the same native font and material instead of bundling a web font.

The browser files live in `assets/creator-tools`. They reconnect with backoff,
hide stale state immediately, cache static icon files and accept live scale,
icons-above/text-above order, alignment and opacity changes over WebSocket.

`CreatorToolsMenu.cs` inserts `CREATOR TOOLS` at index 4 of `MapPauseUI`,
directly after native `OPCIONES`. Later native selections are temporarily mapped
back by one only while `LevelPauseGUI.Select()` executes, preserving every
original action. Selecting it now enters Cuphead's real `OptionsGUI` Visual
screen and temporarily loans its rows to the Creator Tools hub and its
`OVERLAY DE RULETA` settings page. The mod therefore uses
the exact native card/noise background, fonts, selected colors, arrows, repeat
timing, motion, sounds, keyboard/controller navigation and pause transition
instead of drawing a lookalike IMGUI card. The hierarchy and current row sets
are documented in the newest section at the top of this file. On final Cancel every
original Visual button, localization helper, value, active state, title and
`currentItems` entry is restored before the native options screen closes, so
resolution, fullscreen, V-Sync and the other game settings remain untouched.
Combat pause menus are not modified. Creator Tools defaults to disabled.

The development DLL and browser assets are installed. Cuphead loaded plugin
version `0.5.130` without exceptions and the project builds with 0 errors and 0
warnings. Manual acceptance still needs: open the new native Visual-based screen
and inspect its seven labels/rows; close and reopen ordinary `OPCIONES -> VISUAL`
to confirm its original values are restored; test keyboard/controller changes
and URL copy; add the URL to OBS or a browser; then spin one ground and one
airplane battle to inspect timing, native challenge text and cleanup.

## Half Damage challenge validated and parked (2026-08-13)

`ModifierId.HalfDamage` is implemented for ground and airplane battles. While the
challenge is active, player-owned `DamageDealer` hits temporarily multiply the
native damage multiplier by `0.5`, then restore it immediately after the hit. The
guard excludes incoming player damage, so enemy attacks and player HP are not
modified. This single damage path covers regular shots, airplane weapons, EX
moves and supers without permanently changing weapon data.

The challenge was manually validated through the complete King Dice chain,
including board transitions, internal `DicePalace*` minibosses and the final
fight. `ActiveChallengeMatches()` intentionally treats every `DicePalace*` scene
as part of the `Levels.DicePalaceMain` roulette session, so the modifier and HUD
persist until the chain is completed or abandoned.

The user-provided static `assets/modifiers/halfdamage.png` is retained only as
the earlier reference; the final runtime art is the three-frame
`halfdamage_01..03.png` sequence. `EnableHalfDamageChallenge` and
`ForceHalfDamageChallengeForTesting` are both `false`; therefore Half Damage
cannot appear or execute in normal roulette play. Its art is no longer a release
blocker; reactivate it only after the planned short regression and deliberate
public-release decision.

## Native Ink Rain splats and final acceptance (2026-08-13)

Ink Rain is enabled for normal roulette selection. All temporary selectors are
now disabled: `ForceInkRainChallengeForTesting` and `ForceTestBoss` are `false`.
The dormant target remains `Levels.DicePalaceMain`, but it has no effect unless
the boss-test switch is explicitly enabled.

Outside Captain Brineybeard, challenge hits now reproduce Cuphead's native ink
presentation from the original five `Pirate_Ink_Large/Small` animation families.
The mod loads 71 tightly cropped original frames from
`assets/inkrain/screen-native`, reads each frame's original normalized pivot from
`pivots.tsv`, preserves the native 12 fps timing and animation-event lifetimes,
randomizes the same large/small family and horizontal mirror, and renders each
short-lived hit through a real `SpriteRenderer` on Cuphead's `Effects` layer.
This replaces the former manually stretched GUI/render-texture copy. Because the
splats are camera-relative sprites, they follow rotating arenas and receive the
same camera/film treatment as the game. Actors are destroyed as soon as their
native clip finishes or the challenge state is cleared.

Captain Brineybeard continues to call his live
`PirateLevelSquidInkOverlay.Current.Hit()` so that encounter uses the game's
actual native overlay directly. The challenge's introductory squid position,
scale and occlusion behind the dock/foreground sea were manually accepted. The
rain, darkness progression, hold/restoration timing and all accepted difficulty
values were intentionally left unchanged by the splat replacement.

Manual regression passed in these paths:

- Goopy: single and repeated hits, darkness/restoration, pause/resume, retry,
  abandon to map, victory, results cleanup and entering a later non-roulette fight.
- Captain Brineybeard: direct comparison and coexistence with the native squid.
- Dogfight: normal, 90, 180 and 270-degree camera orientations.
- Hilda Berg local co-op: both players, shared darkness, retry/exit and heavy
  projectile load.
- King Dice: board/miniboss transitions, native-size splats, cleanup, retry and
  complete victory chain.

No further full boss regression is required for this isolated presentation
change. The final animated Ink Rain icon was subsequently delivered and wired
as `inkrain_01..03.png`.

## Captain Brineybeard native Ink Rain integration (2026-08-13)

The selected `Levels.Pirate` policy deliberately keeps the challenge's added rain
and its introductory squid while sharing Captain Brineybeard's native ink
presentation. `StartAttempt()` detects Pirate early from either `Level.Current`
or the roulette's active boss because the native level singleton may not exist yet.
For this encounter, every challenge-drop hit calls
`PirateLevelSquidInkOverlay.Current.Hit()` directly. This means the screen splat,
darkness increments, hold/fade timing, scale and randomized native splat family are
Cuphead's own implementation rather than a resized mod copy. If the native overlay
is unavailable, the general Ink Rain overlay remains the safe fallback.

Challenge drops and ground impacts are tinted from the current native overlay
alpha so they remain behind the darkness instead of glowing over it. The ordinary
non-Pirate compositor and all accepted difficulty/density values are unchanged.

The additional squid intro uses native sorting layer/order zero in Pirate. The
first manual pass placed it behind the front sea layer but still in front of the
wooden dock because its camera-relative fallback was too close to the camera. The
latest build now preserves the approved screen-space X/Y and moves only its world
Z to the native `PirateLevelSquid` gameplay plane (`z = 0`, or the live native
squid's exact Z when available). That DLL is installed and awaits one visual check:
confirm that the intro is behind both the front sea and wooden dock. Do not change
its coordinates, scale, rain tuning or difficulty values while checking this.

Temporary diagnostics used to inspect nearby renderers and native splat metrics
were removed before this handoff. They are unnecessary because Pirate hit splats
now execute the native `Hit()` method directly. Ink Rain and Pirate remain forced
in `ExperimentalFeatures.cs` and `Plugin.cs` intentionally for the next visual
test. Set `EnableInkRainChallenge`, `ForceInkRainChallengeForTesting` and
`ForceTestBoss` back to `false` before any public build.

## King Dice Ink Rain acceptance passed (2026-08-13)

The complete King Dice chain passed manual Ink Rain acceptance. The squid intro
plays once when entering the chain, regular rain resumes through internal
`DicePalace*` minions without duplicating the runtime/compositor, the challenge
and HUD persist into `DicePalaceMain`, and final victory performs the expected
cleanup. Pause, transitions, darkness and the complete run behaved correctly.

A retry-specific defect was found and fixed: both defeat-menu `Retry` and
pause-menu `Restart` reset the current sublevel but retained
`inkRainDicePalaceIntroShown`, so the new attempt skipped the squid intro.
`ResetInkRainChallengeForRetry()` now clears that flag. This method is called
only for an actual retry/restart; normal progress to another King Dice minion
does not call it and therefore still suppresses repeated squid intros. Manual
testing passed both retry paths and another complete King Dice run.

All temporary test selectors were disabled again after acceptance:
`ForceTestBoss`, `EnableInkRainChallenge`, and
`ForceInkRainChallengeForTesting` are false. The dormant boss target remains
`Levels.Bee`; the functional Dice Palace guards remain
`Levels.DicePalaceMain` and must not be changed when switching test bosses.

Remaining Ink Rain acceptance work is local co-op, the Captain Brineybeard
native-squid coexistence policy, a short DLC/exit regression after those special
cases, and the final animated challenge icon.
## Ink Rain acceptance checkpoint after Queen Bee (2026-08-13)

Cagney Carnation passed the requested Ink Rain compatibility check. The user
specifically confirmed that Cagney's native visual effect and the challenge
coexisted and behaved as expected; no special suppression or composition rule
is needed for this pairing.

Hilda Berg passed the ordinary-plane acceptance test with Ink Rain and the
Cursed Relic at curse grade `0`. The user confirmed the result, so ordinary
screen-top spawning, density and plane behavior are accepted, and the temporary
`ForceCursedRelicTest` switch is false again.

Queen Bee (`Levels.Bee`) subsequently passed the complex moving-platform test.
The user confirmed that Ink Rain behaved correctly in the complete fight, so
ground detection and drop behavior are now accepted in both a simple arena
(Goopy) and a moving-platform arena (Bee).

All temporary selectors are false at this handoff: `ForceTestBoss`,
`ForceCursedRelicTest`, `EnableInkRainChallenge`, and
`ForceInkRainChallengeForTesting`. `ForcedTestBossSequence` keeps `Levels.Bee`
only as a dormant target. The other new challenges also remain disabled. The
next agent must explicitly re-enable only Ink Rain and its force switch for a
new acceptance session.

Remaining Ink Rain work, in recommended order:

1. King Dice full chain: several minions and `DicePalaceMain`, checking that the
   squid intro plays once, rain resumes across internal scenes, no compositor or
   grace period duplicates, and cleanup waits for the real final victory.
2. Local co-op: independent collision, splats/darkness for both players,
   revival, pause, retry and cleanup.
3. Captain Brineybeard policy: choose whether to exclude Ink Rain or, preferably,
   keep the added rain while suppressing only the mod's duplicate squid intro.
   Never disable the boss's native squid attack silently.
4. Short regression over a DLC ground boss plus abandon-to-map/results after the
   special-case fixes. Goopy, Cagney, Hilda, Queen Bee and Dogfight already
   passed their intended geometry/lifecycle matrices.
5. Replace the provisional challenge presentation with the user's final animated
   icon; the runtime currently references only `inkrain_01.png`.

## Goopy Ink Rain ground/lifecycle acceptance passed (2026-08-12)

The complete Goopy Le Grande Ink Rain test passed manual acceptance. On a simple
ground arena the user approved the single squid intro per attempt, unchanged
Ready/Wallop timing, floor impacts and visual layering, pause behavior, player
splats/darkness, defeat and retry, knockout flow, and cleanup after leaving the
battle.

At that checkpoint `Plugin.ForceTestBoss` was cleared before the next target.
The current dormant target and all final switch values are recorded in the
newest Queen Bee checkpoint above.

## Dogfight Ink Rain acceptance passed (2026-08-12)

The complete `Los Perritos Pilotos` Ink Rain run passed manual acceptance. The
user confirmed that the challenge looked and behaved perfectly throughout the
rotating encounter. This validates sky-side spawn direction, lateral coverage,
density, sprite size through 90/270-degree orientations, survival of active
drops during rotation, collision alignment and final cleanup as one combined
gameplay test.

At that checkpoint `Plugin.ForceTestBoss` was cleared while Ink Rain continued
to the next acceptance target. The current dormant target and final switch
values are recorded in the newest Queen Bee checkpoint above.

## Controller map-return prompt fix and Ink Rain pause (0.5.130)

The ready-to-paste public installer is
`dist/Las-Pichi-Ruleta-0.5.130.zip` (10,628,818 bytes, SHA-256
`1B8F451DF845F09DA8E65B34A24F418C77ACF14D1239A99A88BBB4FC2BABEDBB`).
It contains 123 files plus two ZIP directory entries: x64 BepInEx/Doorstop, the
0.5.130 DLL, the 99 established public assets and `README-LEEME.txt`. The packed
DLL matches the release build at SHA-256
`AF9C928620EFC10A2962475B536E1C4A08FBF7A956031A96B5B0F3BBF97BB970`.
The archive was deliberately based on the verified 0.5.127 public asset set, so
it contains no Ink Rain, RGB, Upside Down or HP.1 presentation assets. It also
contains no BepInEx config, cache, logs, saves, patchers, temporary files or
unrelated plugins.

The controller shortcut remains physical left trigger plus Cuphead's native
`EquipMenu` action: Switch `ZL + X`, Xbox `LT + Y`, and PlayStation
`L2 + Triangle`. A regression appeared after returning from any fight, whether
roulette-launched or entered normally: the bottom-right row displayed only the
native `B` glyph and pressing it could not open the roulette.

The root cause was scene-owned UI plus a plugin-owned layout cache.
`nativeRoulettePrompt` is parented to Cuphead's map canvas and is destroyed when
the fight scene loads, while `nativeRoulettePromptLayoutToken` survives on the
persistent plugin. On the next map, `TryCreateNativeRoulettePrompt()` cloned a
fresh native Help row, but `ApplyNativeRoulettePrompt()` saw the old token and
took its unchanged-layout fast path. The new row therefore never had its
modifier, separator, Equip glyph or positions configured and exposed the
template's default `B` glyph.

`UpdateNativeRoulettePrompt()` now calls `DestroyNativeRoulettePrompt()` when it
detects that the scene-owned root is gone. This clears all stale component
references, the dim overlay and the layout token before cloning from the new
map. `TryCreateNativeRoulettePrompt()` also invalidates the token defensively
because every call creates a new object graph. Do not remove either invalidation
when optimizing prompt updates. Required manual matrix: initial map, normal
fight/abandon, normal fight/victory, roulette fight/abandon and roulette
fight/victory, verifying both the displayed trigger-plus-Equip combination and
actual open/close input after every return.

Ink Rain development is paused at this checkpoint. Both
`EnableInkRainChallenge` and `ForceInkRainChallengeForTesting` are false; its
implementation and assets remain intact for later arena testing. While the
master switch is false, the plugin also skips Ink Rain's Harmony patch install,
runtime component initialization and update heartbeat entirely.

## Ink Rain tuning, lifecycle, and rotating Dogfight support (2026-08-12)

At this checkpoint the experimental `InkRain` challenge remained enabled and
forced from `ExperimentalFeatures.cs` while arena-wide acceptance continued.
It is currently paused with both switches false as documented in the newer
0.5.130 section above. The temporary
boss selector used for Dogfight validation has been disabled again, so spins
choose bosses normally. The roulette now keeps a session-only history of the
last three boss results and excludes them when the compatible pool permits it;
small pools progressively relax the oldest exclusions rather than failing.

Accepted tuning at this checkpoint:

- Easy: 8 visible drops, waves every 0.80-1.00 seconds, 55% single and 45%
  double waves.
- Normal: 20 visible drops, waves every 0.40-0.65 seconds, 50% single, 35%
  double and 15% triple waves.
- Expert: 30 visible drops, waves every 0.25-0.60 seconds, 40% single, 35%
  double, 15% triple and 10% four-drop waves.
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

1. Complete the King Dice, co-op and Brineybeard work listed in the latest
   acceptance checkpoint above. Ordinary plane behavior passed in Hilda,
   moving-platform behavior passed in Queen Bee, Goopy passed the basic ground
   lifecycle, and Perritos Pilotos passed the rotating-camera case.
2. Enable and force Ink Rain only during an explicit acceptance session. Both
   switches are currently false and must remain false in public builds.
## Experimental Ink Rain challenge (2026-08-11 handoff)

`ModifierId.InkRain` is a first playable ground-and-plane prototype named
`LLUVIA DE TINTA` in Spanish and `INK RAIN` in the other localization tables.
`RouletteData` currently points to `modifiers/inkrain_01.png`. The three prepared
icon frames now show three native ink blobs in separate, deliberately
non-collinear lanes. Each travels down-left with its own trail leaning up-right,
so the group reads as simultaneous diagonal rain rather than three poses of one
projectile; only frame 01 is
currently referenced by the roulette/HUD. The feature
and its forced test selector were deliberately enabled during this prototype
checkpoint. Both switches are currently false for the 0.5.130 public-fix work.
Re-enable the force only when the remaining acceptance work resumes, and keep
the challenge experimental until every arena has been checked.

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
the active ceiling immediately returns to the difficulty's `8/20/30`; no regular
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

1. King Dice is a known pending compatibility case, not a normal single-scene
   boss. Reproduce and document the current Ink Rain errors across several
   `DicePalace*` minions and `DicePalaceMain`, then verify that every internal
   scene keeps the same challenge session without replaying the squid intro,
   duplicating rain/compositors, losing the damage grace, or clearing the
   challenge before the final boss. Camera and HUD replacement during those
   transitions also needs explicit coverage. Do not treat an internal minion
   victory as the end of Ink Rain.
2. Decide the intended behavior when the roulette selects Ink Rain for Captain
   Brineybeard (`Pirate`). His native fight already owns the same squid, ink
   projectiles and full-screen overlay. The current experimental implementation
   can coexist with those systems, but that may duplicate the introductory
   squid, stack two rain sources and make the overlay/difficulty confusing.
   Before public activation, choose and test one policy: exclude Ink Rain from
   this boss, keep the extra rain but suppress only the mod's squid intro, or
   deliberately allow both complete systems. This is a design decision, not a
   resolved bug; do not silently disable the native boss attack.
3. Validate local two-player behavior and run a short DLC/regression pass after
   the special-case fixes. The general single-player lifecycle and representative
   ground, platform, plane and rotating-camera arenas are already accepted.
4. Replace the provisional challenge icon with the user's finished animation.
   Keep all feature and test selectors disabled until the next explicit session.

## Completed dormant HP.1 challenge (0.5.129)

`ModifierId.HpOne` implements the ground-and-plane `HP.1` challenge. Its final
80 x 80 art is the three-frame `assets/modifiers/hp1_01..03.png` sequence. All
localization dictionaries currently use `HP.1` as the challenge name.

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
- Final animated art is integrated. The remaining release gate is a short
  roulette/HUD/retry/co-op regression and an explicit public activation
  decision; never enable a force selector for a public build.

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

The temporary display name is `180°` in all 12 language dictionaries. The final
three-frame `assets/modifiers/upside_down_01..03.png` sequence uses 80 × 80
transparent text-free art: a cream arrow with black vintage ink, tilted as an
elliptical ring in perspective so its wide front arc and narrow rear arc imply
a flat 180-degree turn. The merely vertically reversed second draft was
rejected. The accepted design narrows into the upper/rear arc, then grows into
a large foreshortened arrowhead that emerges from the back toward the viewer.
Runtime
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

Last updated: 2026-08-18
Current local version: 0.6.0

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
  same Rewired player. The current checkpoint experiments with a postfix on
  `MapEquipUI.get_CanPause()`, but manual validation found that it blocks the
  native card entirely. Treat this path as unresolved.
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
- The current `MapEquipUI.get_CanPause()` gate is an unsuccessful experiment:
  the native card no longer opens in manual testing. Replace or narrow it, but
  never disable the `MapEquipUI` component itself because that corrupts its
  native initialization and navigation state.

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
`BepInEx\plugins\CupheadModdingTemplate`.

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
