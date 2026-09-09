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
