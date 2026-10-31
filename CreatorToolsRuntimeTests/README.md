# Creator Tools runtime regression harness

This dependency-free console harness links the production stream worker,
dashboard, rules, backlog, event models and local server directly into its test
assembly. Only Unity's interaction controller/queue and the stream source are
replaced by narrow recording fakes.

The harness also links the production mini-boss admission policy directly.
Its shared limit defaults to one mini-boss total: every second actor waits,
whether its type matches or differs. An explicit zero disables that special
limit while leaving normal queue capacity in charge. Tests cover all 25 type
pairs in both states, legacy settings above one, slot release and preservation
of the active snapshot.
Unity scene discovery and the water-floor integration still require in-game
verification.

The production Pesky pacing/settings policies are linked too. Tests cover
post-disappearance recovery, companion capacity, gameplay-time intervals,
explicit opt-in for regular interactions, migration defaults, persistence and
atomic panel updates. Unity scene discovery and the physical dispatch queues
are not exercised by these policy tests.

Spawn-group tests cover the six new persisted fields, atomic validation,
legacy cooldown migration with backups, and independent settings. The production
automatic selector is exercised with a large common catalog, overdue mini-bosses,
incompatible arenas, opt-in cross-queue reservation, and separate clocks. Batch
policy tests retain object identity/donors/delays and recheck capacity/exclusivity.
They use recording delegates; physical queues and Unity controller wiring still
need in-game verification.

Body-scale tests link the production common-interaction policy and verify screen
proportions under ordinary, aircraft/Howling, and native-size ground cameras,
including intermediate zoom values and native-ground precedence. Unity level
detection, inherited transforms, animated colliders and labels still need in-game
verification.

Balance tests verify all five mini-bosses' 65% HP in normal and expert modes,
the default single-intense admission policy, same-frame batch rechecks, selector
eligibility, atomic validation, independent persistence and v8 migration backups.
The deprecated category toggle cannot exclude intense attacks anymore. Item IDs in the recording
fixture include the catalog plus the stream harness's synthetic test item.
The selector probe-budget test verifies that due minis, earned reservations,
full slots and waiting clocks avoid irrelevant scene queries, while incompatible
minis retain the common fallback. This measures delegate calls, not Unity frame times.
The visual-snapshot hierarchy test retains inactive sprite branches and their
complete animator/transform paths, while skipping collider/spawn metadata and
ancestors outside the actor. Actual Unity animation playback still needs in-game QA.
Loading-barrier tests preserve the original native iterator's yields and cleanup,
avoid extra retry frames, drain source-scene and native texture requests, and
cover timeout/cancellation/resumption. A ready prefab does not permit the native
loader to close bundles while their textures are still loading. These tests do
not run Unity or verify the rendered actors.
Platform-rule tests cover independent TikTok/Twitch follows, accumulated Bits,
separate new/gifted/shared repeat subscriptions, custom reward names, legacy
TikTok migration, full backup recovery and dashboard simulations. They use
normalized fixtures and recording queues; no Twitch account or payment is used.
Settings-transfer tests cover all persistent categories, default replacement,
rule round trips without connection identities, pending-import cancellation,
backups, rollback on locked files, malformed and nonportable input, and local
HTTP authorization. They use temporary files and simulated account markers;
real account credentials and game installations are never touched.
The current harness has 171 test groups.

`tools/verify_native_loading_contract.ps1` reads the installed game's IL and the
compiled mod. It checks the covered loading window and verifies that all ten
catalog source scenes follow Cuphead's own scene-plus-asset completion loop
before unloading, with native asset requests also included in the final barrier.

The UI mock's separate HTTP contract tests run with:

```powershell
cd creator-tools-ui
node scripts/test-spawn-settings.mjs
```

They use a temporary local port and stop their child server after testing. They
validate settings/persistence projections, not gameplay scheduling or physics.

Run it from the repository root:

```powershell
dotnet run --project .\CreatorToolsRuntimeTests\CreatorToolsRuntimeTests.csproj
```

The source files use a `.cs.txt` suffix intentionally. This prevents the main
SDK-style mod project from discovering test fakes through its recursive `*.cs`
compile glob.

`NuGet.Config` clears package sources because the harness has no package
dependencies; restore and execution therefore work without network access.
Generated `bin` and `obj` artifacts are redirected to the operating-system
temporary directory so the parent mod project's recursive source glob never
sees generated assembly-attribute files.

## Live interaction performance regressions

```powershell
dotnet run --project ./CreatorToolsRuntimeTests/Performance/Performance.csproj
dotnet run --project ./CreatorToolsRuntimeTests/VisualPerformance/VisualPerformance.csproj
dotnet run --project ./CreatorToolsRuntimeTests/PeskyBattle/PeskyBattle.csproj
dotnet run --project ./CreatorToolsRuntimeTests/DashboardPerformance/DashboardPerformance.csproj
dotnet run --project ./CreatorToolsRuntimeTests/ScreenCoverProbe/ScreenCoverProbe.csproj
```

`DashboardPerformance` compares the real 30-record dashboard serializer with
its previous implementation, preserving every counter, payload field, escape,
history order, connection/profile change and immutable prior reader snapshot.
In the production on-demand mode, background event evaluation and due
simulations continue without building dashboard JSON; a synchronized HTTP read
builds it only when dirty and otherwise reuses the previous string. Readers are
pure managed code and never execute gameplay. The server releases its lock
before acquiring the model lock. Runtime tests verify immediate fresh HTTP reads
after 150 unobserved events, pure simulation reads, cached strings, lock order
and bounded HTTP errors. Legacy push-mode hosts retain their publication path
with reusable buffers. Idle or future-only simulation ticks allocate no empty
work list. Allocation measurements are offline .NET observations, not Unity FPS.

`ScreenCoverProbe` retains fresh discovery per frame, including scene changes,
pirate ink, new children, destroyed objects and the previous renderer filters.
It removes Unity's extra generic typed-array copy and uses one child-renderer
list, cleared even after early returns/errors. The remaining untyped discovery
array still comes from Unity. Native/compiled wiring is checked during Dev
publication by `tools/verify_native_screen_cover_contract.ps1`.

`Performance` serializes the production queue with 200 earned interactions over
five simulated seconds at 60 and 144 frames per second. It compares per-frame
JSON allocation with the former 100 ms panel cadence, verifies every donor remains in
the projection, and covers immediate control acknowledgements, live countdowns,
repeated publication passes and clock restarts. This measures managed snapshot
work under .NET 10, not Cuphead FPS or Unity's garbage collector.

The current runtime separately leases heavy interaction/pesky JSON publication
to actual readers of those two state endpoints. It publishes once at bootstrap,
then at most every 500 ms while GETs renew a two-second audience lease. Once
that lease expires, ordinary gameplay produces no full panel snapshots. Control
acknowledgements bypass pacing; leaving Cuphead's focus captures both snapshots
once before Unity can stop background updates. A returning GET requests a fresh
snapshot on the next Unity update, without blocking HTTP or accessing Unity from
the server thread. An old background request whose lease expired cannot trigger
an unnecessary refresh when gameplay resumes. Backlog/deferred counts and
master/clear projections remain current in HTTP responses independently.

Audience tests cover bootstrap/idle suspension, foreground cadence, concurrent
requests, expired background reads, urgent controls and zero-allocation checks.
Runtime HTTP tests verify independent endpoint audiences, cached nonblocking
responses, rejected requests and unaffected OBS endpoints. Existing tests retain
control sequence acknowledgements, clear generations, limits and headless event
processing. The foreground panel also stops its automatic polling while hidden
or unfocused and refreshes on return; explicit control requests remain live.
Run its browser-state regressions from `creator-tools-ui` with
`npm run test:foreground-polling`.

The production serializers now reuse immutable fragments for each queue entry;
all fragment inputs are checked, while readiness, plane restrictions and active
timers remain live. A reference serializer checks byte-for-byte compatibility,
including escaping, supplemental help entries, mutated fields and float edge
representations. Repeated polls of 200 unchanged entries allocate no new
serializer objects after warmup. Panel snapshots also reuse their builder and
character buffer, retaining the previous immutable output when content is
unchanged. `CopyTo` avoids Cuphead Mono's `StringBuilder.ToString()` backing-store
sharing and the next write's copy. Tests cover growing/shrinking projections,
unchanged 32K-character snapshots, old reader references, clear commands and
cached settings invalidation. These zero-allocation measurements describe the
isolated .NET 10 queue/projection workload, not all Unity or panel-controller
work; changed snapshots still need a new immutable output string.

`VisualPerformance` uses narrow Unity recording stubs to check projectile reuse,
echo cleanup, firing-time damage snapshots and shared camera discovery. Spawn
reservation tests retain active actors and players, observe movement and
reactivation immediately, preserve transition fallbacks, and verify 10,000
queries perform no scene scans or managed allocations after warmup. It
checks that ordinary bullets retain their damage marker without frame callbacks,
and that camera zoom and scene changes remain live. Real rendering, Unity's
deferred destruction and actual frame times require in-game verification.

`PeskyBattle` also links Tap Farming and checks unchanged publications allocate
no snapshot JSON, commands and counters publish immediately, active battle time
keeps its heartbeat, catalog changes refresh, and coordinator changes invalidate
the projection even while Tap Farming is inactive. Queue selection checks both
source lanes, rejected predecessors and FIFO order against a reference selector.
Once a lane has its first eligible entry, later entries in that lane must not
repeat expensive arena checks (200 real entries and 1,000 synthetic entries).

For gameplay QA, use the launcher Dev package on expert bosses with interactions
enabled, first with ordinary fire and then donation bursts and ghost help.
Compare the same boss and settings over a sustained session, including retries.
Check frame-time spikes as well as average FPS, queue counts, donor names and
effect expiry. Publishing a package never changes an already running session;
the launcher applies it at the next start.

## Tap Farming native phase progression

```powershell
dotnet run --project ./CreatorToolsRuntimeTests/TapFarmingDamage/TapFarmingDamage.csproj
pwsh -NoProfile -File ./tools/test-tap-farming-native-audit.ps1
```

This harness links the production damage bridge and Tap Farming controller.
It reproduces the Robot's bounded `DealDamageToNextNamedState()` loop while
inside player damage: synthetic phase-advance damage must reach native health
without spending the tap reserve, even when the reserve exceeds the boss's total
health. Subsequent ordinary hits must still spend reserve and pass through only
their unabsorbed remainder. It also covers nested scopes, exceptions and patch
installation failures. The native IL contract in
`tools/verify_native_tap_farming_contract.ps1` checks the actual game's Robot
call sites and helper loop, separately from the simulated phase mechanics.
An in-game Robot fight is still needed to verify animations and hitboxes.

The native contract also scans all 26 bosses in the roulette catalog (including
DLC), all 64 native property families, and nested coroutine methods. The audited
version has 22 named-state advance calls and 94 other generic damage calls:
76 read `DamageInfo.damage` directly, two forward player damage unchanged, and
16 implement parries, deferred component credits or scripted transitions.
`tools/tap-farming-native-audit.json` records every named-state caller;
`tools/tap-farming-native-damage-audit.json` records the 18 nonstandard damage
sites and their reviewed context. Unknown scripted routes, changed call counts,
unguarded property families or health-method overrides fail the contract.
The negative audit test mutates temporary copies of the native reference DLL:
an unknown damage route and a duplicate advance site must both be rejected.
It removes the copies afterward and checks the original reference hash is
unchanged; it never executes the copied assembly or starts Cuphead.

| Native route | Audit result |
| --- | --- |
| Robot heart | Synchronous death callback can exhaust the old advancement loop; covered by the generic guard. |
| King Dice's Tipsy Troop | Last local component death synchronously calls `AllDead` and the same bounded helper; the same risk is covered by the guard. |
| Djimmi, Sally, Howling Aces | The helper runs from coroutines, outside the original player hit; covered by the guard without asserting an equivalent observed freeze. |
| Goopy | Conditional `earlyKnockout` path uses the helper; explicit native callers pass false. Protected, with no equivalent freeze demonstrated. |
| Mausoleum and internal RetroArcade routes | Included in the full native scan, though absent from the roulette boss catalog. |

This is an IL and policy audit, not an in-game pass for every difficulty or a
test of Harmony rewriting all 64 generic types inside Unity's Mono. No second
unprotected softlock was found in the reviewed damage routes. A separate
limitation remains: Train, Root Pack and some minibosses/components use local HP
rather than the aggregate boss pool, so the current tap reserve does not extend
that local health. `Level.Timeline.DealDamage` records damage statistics and does
not drive native health or phase progression.

## Event stress testing

Run an isolated burst without a game, account or payment:

```powershell
pwsh -NoProfile -File ./tools/test-stream-stress.ps1 -Events 100000 -Viewers 1000
```

To spread the input over time, add `-Rate 1000`. Zero (the default) sends an
unrestricted burst. This rate counts normalized messages, including the
intentional duplicate after every seventh unique event. The mix is 60% likes,
30% gifts and 10% follows, using temporary fixture rules. The source generates
messages lazily so a large preallocated test queue cannot hide production
backlog growth. The production worker, dashboard, deduplicator, rules and backlog
run unchanged; gameplay admission is a recording fake.

The command prints the report path under `.deployment-cache/`. It verifies
exact event/unit totals, deduplication, earned interactions, complete backlog
drain, the 30-record history, 4096 remembered event keys and bounded donor
segments. Memory and timing fields are .NET 10 observations, not Unity frame
times or guaranteed live throughput. The test also bypasses the companion's
transport and normalization; the existing transport-pressure regressions cover
that boundary separately.

For a test game already running through the launcher, the separate sender uses
the existing local simulator:

```powershell
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 300 -Rate 10 -Type Like

# Choose an actual catalog gift ID with an enabled rule in the panel.
$stressGiftId = 'ID_FROM_AN_ENABLED_RULE'
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 1000 -Rate 20 -Type Mixed -GiftId $stressGiftId

# Random gifts from currently enabled TikTok gift rules and the local catalog.
pwsh -NoProfile -File ./tools/send-stream-stress.ps1 -Events 150 -Rate 5 -Type Gift -RandomGifts -RandomSeed 20261030
```

`Like`, `Gift`, `Follow` and `Mixed` are supported. Mixed sends 80% likes,
10% gifts and 10% follows. Every request creates one event with `count=1` and
`delaySeconds=0`; one event with count 1000 is not a 1000-event load test.
Likes/gifts share a configurable viewer pool (`-Viewers`); follows receive
unique identities. All test identities start with `stress-`. The destination
is always IPv4 loopback, port 18081 by default; `-Port` supports an isolated
fixture or a different local setup.

`-RandomGifts` uses a snapshot of unique catalog gift IDs with enabled TikTok
gift rules. Each gift request draws uniformly from that sorted pool; duplicate
rules for one gift do not increase its selection weight. `-RandomSeed` defaults
to 42 and makes the attempted sequence reproducible for the same pool and event
mix. It cannot be combined with `-GiftId`. The report saves the pool, seed and
accepted counts per gift. Existing quantities, thresholds and cooldowns still
apply, so gift-event totals need not equal earned interaction totals. Rules for
airplane-only effects may retain earned work until a compatible fight starts.

The sender preserves settings and uses existing rules. Disabled interactions,
unmatched gifts or thresholds can accept events without spawning attacks.
HTTP 202 acknowledges scheduling, not actor creation. The report records
accepted requests, queue-full rejections, uncertain outcomes, actual rate,
HTTP latency and before/after queue snapshots. Dashboard deltas include real
traffic too and cannot prove per-run losslessness; a restarted server session
invalidates their comparison. HTTP latency does not measure FPS. Observe game
frame times during the loaded fight separately.

Ctrl+C stops new sends; already accepted events may remain in the queues.
There are no automatic retries. Simulation requests use one TCP connection
and one write attempt, avoiding HttpClient's transparent retries of this
side-effecting GET. Read-only state snapshots may use HttpClient normally.
No script starts, stops or reloads the game or changes rules automatically.

The normal runtime harness tests the sender against a standalone production
server on a random port. Its additional response/error checks run with:

```powershell
pwsh -NoProfile -File ./tools/test-stream-stress-tool.ps1
```

This recording server never uses the live game's port. It checks rate, unique
event identities, shared counter reporting, 429 responses, non-JSON errors,
malformed confirmations and connection loss without duplicate wire requests.

## Startup frame-time investigation

```powershell
dotnet run --project ./CreatorToolsRuntimeTests/StartupPerformance/StartupPerformance.csproj
dotnet run --project ./CreatorToolsRuntimeTests/StartupProperties/StartupProperties.csproj
dotnet run --project ./CreatorToolsRuntimeTests/ZeppelinPerformance/ZeppelinPerformance.csproj
dotnet run --project ./CreatorToolsRuntimeTests/GameTheme/GameTheme.csproj
dotnet run --project ./CreatorToolsRuntimeTests/PauseMenuDiscovery/PauseMenuDiscovery.csproj
dotnet run --project ./CreatorToolsRuntimeTests/ControllerInput/ControllerInput.csproj
dotnet run --project ./CreatorToolsRuntimeTests/NativePromptLayout/NativePromptLayout.csproj
dotnet run --project ./CreatorToolsRuntimeTests/InkGroundProbe/InkGroundProbe.csproj
```

The ink ground-probe harness links the production reusable physics query. It
checks collider filtering, stale buffer entries, full-buffer fallback and
repeated buffer reuse. Actual Unity physics and the rendered ink still need
in-game QA.

The manual challenge prompt reuses its layout while its text, font metrics,
canvas, display size and scale are unchanged. `NativePromptLayout` checks
invalidation, repaired native overrides, reference cleanup and the map/active
manual challenge discovery gate. `tools/verify_native_prompt_layout_contract.ps1`
also checks the compiled Unity wiring and runs during Dev publication. It does
not measure actual canvas rebuild time or rendered appearance.

The startup monitor writes one `[StartupPerformance]` summary per attempt after
15 seconds of eligible gameplay, or a partial summary if the attempt ends
earlier. It runs even with no queued interactions. Intro frames, pause/focus
gaps and skipped frame IDs are excluded from frame attribution. Unity exposes
frame N-1's interval through `Time.unscaledDeltaTime` at Update N, so the monitor
retains Update, LateUpdate, GUI, spawn, stage and GC data until the following
eligible frame supplies that interval. The summary therefore closes after the
final measured frame's visual callbacks have completed.

`pairedFrames` counts these complete frame records. `framesOver25ms`,
`framesOver30ms` and `spikesOver40ms` use their observed frame intervals.
`p50FrameApproxMs`, `p95FrameApproxMs` and `p99FrameApproxMs` use one-ms upper
buckets, capped at 1000+ ms. At most five `[StartupPerformanceFrame]` lines
follow the summary, ordered by frame duration. Each line identifies the native
frame ID and the Update, GUI, LateUpdate, spawn and GC values from that same
frame. The stage columns separate executors, the timed executor, control
commands, availability/pacing, dispatch, live-event state, publication, stream
draining, battle HUD, timed HUD, native prompts and late visual effects.
`timedExecutorMs` is included in `executorsMs`; `spawnTotalMs` is included in
`dispatchMs`. These overlapping columns must not be added together.

The existing `maxFrameMs`, `maxPluginUpdateMs`, `maxInteractionUpdateMs`,
`maxGuiMs`, `maxLateUpdateMs` and `maxSpawnMs` remain independent maxima and
must not be treated as one correlated frame. A partial attempt's last callback
sample can lack a following frame interval: its callback maxima and GC total
remain available, but it is absent from percentiles and the top-frame list.
Callback measurements are elapsed wall time, including lock waits, scheduling
and collections. `gc0Delta` is a process collection count; even a correlated
frame containing GC does not identify which system allocated the memory.
Frame intervals also include native game/render work and pacing. These are not
GPU timings or an exhaustive Unity profiler. The monitor creates no per-frame
strings, logs or sampling allocations; inactive passes perform no clock/GC
reads. The harness checks previous-frame attribution, post-Update visual/GC
work, skipped frames, top-five bounds, percentile counts and 1000 active
correlated frames without allocated bytes.

The properties test runs 1200 production spawn plans across three difficulties
and four attempts. It checks that the complete Hilda configuration is built
once per difficulty and reused, while positions, native values and the normal
fallback remain intact. The native contract verifies that zeppelins only read
this shared configuration; loading prepares it before actor creation.

The projectile harness covers exact returned native projectiles, ordinary and
parryable fire, spread shots, nested fire, exceptions and thread-local context.
One thousand catalog fires and warm availability checks require no global
object search. Its IL contract verifies all eight native creation sites and
the compiled plugin. The theme harness covers 1000 cached sprite lookups
without global font/sprite searches, destroyed Unity assets, retry replacement
and bounded missing-asset retries. The GUI does not refresh the theme while no
roulette card or language-test notice is visible.

Pause-menu queries use the game's registered `LevelGUI.Current` owner. Its
private serialized `pause` reference is resolved once per owner/menu lifetime
through cached field metadata; warm queries perform no scene discovery or
reflection. Every query still reads current active/lifetime/state values, and
native pause/unpause events invalidate the reference immediately. The harness
checks same-frame animation transitions, native owner/menu replacement, missing
or destroyed objects, recovery and 10000 warm queries with one field read and
no sampling allocations. The native/compiled contract verifies owner lifetime,
serialized-field wiring and both event invalidations. The controller
label test preserves existing left/right trigger matching while checking that
400000 warm lookups allocate no strings and device churn remains bounded.
Trigger-held/pressed logic and input polling order are unchanged.

Zeppelin spawn tests also cover the known non-Hilda path using its retained
prefab without looking for Hilda, live donor-label registry filtering instead
of global discovery, and shared camera lookup. The native Hilda path and the
unknown-level fallback remain covered. Availability checks the gameplay gate
once before testing prepared prefabs.

For in-game comparison, use the same boss and settings for attempts with no
pending attacks and attempts with the Rose test queue. Read the startup summary
for each attempt before changing another setting. A hitch with an empty queue
cannot be attributed solely to spawning donated actors. The publisher does
not reload the active session; this instrumentation starts at the next launch
of the updated Dev package.
