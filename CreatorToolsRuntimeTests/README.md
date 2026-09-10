# Creator Tools runtime regression harness

This dependency-free console harness links the production stream worker,
dashboard, rules, backlog, event models and local server directly into its test
assembly. Only Unity's interaction controller/queue and the stream source are
replaced by narrow recording fakes.

The harness also links the production mini-boss admission policy directly.
Its shared limit is fixed at one mini-boss total: every second actor waits,
whether its type matches or differs. Tests cover all 25 type pairs, legacy
settings above one, slot release and preservation of the active snapshot.
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
The current harness has 84 test groups.

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
