# Creator Tools runtime regression harness

This dependency-free console harness links the production stream worker,
dashboard, rules, backlog, event models and local server directly into its test
assembly. Only Unity's interaction controller/queue and the stream source are
replaced by narrow recording fakes.

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
