# Gameplay architecture and verification

## Shared source

The client owns `Assets/Scripts/SharedRules`. Unity compiles these files normally;
the server links exactly the same source through `KarnivalRulesRoot` in its project.
The default resolves the existing sibling Unity/VS project layout. For other
checkouts pass `-p:KarnivalRulesRoot=<absolute client Assets/Scripts/SharedRules>`
to MSBuild. Check out both repositories for builds and regression tests.

- `PlayableTimeline`: playable interval, remaining time, completion reasons, multiplier.
- `MazeMotion`: collision/sweep rules, with allocation-free prediction and optional
  traveled segments for authoritative goal detection. Adapters supply Unity/server coordinates.
- `PlateStackerRules`: logical spawn, catch, stack-spacing and exit geometry.
- `DirectionalInputHistory`: deterministic snapshot replay for client prediction.
- `Protocol`: shared round-start and PlateStacker payload readers/writers.
  Other game payloads remain in their game-specific definitions; migrate them to
  shared codecs when changing their formats, without introducing a second serializer.

Protocol 45 adds authoritative playable start/end to round-start messages.
Client and server must be deployed together. Geometry changes must also update
the presentation standard/validator when the visual contract changes.

## Responsibilities

`MiniGameRoundManager` owns session phases; `SessionBettingLedger` owns wager
validation, lock/debit, settlement and refunds. Transport callbacks and session
mutations remain on the server tick thread.

`MiniGameCoordinator` owns client round flow. `DeferredPresentation` owns delayed
summary cancellation, and `MiniGameResultsReader` owns result decoding.
PlateStacker's view retains visual reconciliation and animation; contact timing
and movement replay no longer define separate arithmetic implementations.

`IGameplayClock` is injectable on rounds/manager. Production uses one monotonic,
UTC-shaped clock. Inputs and messages use it; known outcomes pass their actual
event timestamps. Database audit timestamps still use wall-clock UTC.

## Tests

Server cases are grouped under `KarnivalServer.Tests/Cases`; the lightweight
console runner remains in Program.cs. Run:

```powershell
dotnet build KarnivalServer.Tests/KarnivalServer.Tests.csproj -c Release -p:UseSharedCompilation=false -m:1
dotnet KarnivalServer.Tests/bin/Release/net10.0/KarnivalServer.Tests.dll "<server-root>" "<client-root>"
```

Unity batch methods (or their matching Tools/Karnival menu entries):

- `MiniGamePresentationStandardizer.ValidateAllFromCommandLine`: scene/layout contract.
- `MiniGameBehaviorRegression.RunAll`: countdown, crowd determinism and actual scene fuse behavior.

Do not run an additional Unity instance against a project already open in the editor.

## Performance

The per-tick human-presence check no longer creates a list. Maze input batches
write directly from their existing buffer using a cached delegate. Shared Maze
prediction does not allocate movement-segment collections.

`ServerPerformance` reports ticks exceeding their configured budget (throttled)
and database operations taking at least 20 ms. Persistence intentionally remains
synchronous until representative load measurements justify a worker queue.
If measurements show stalls, queue database work and apply results on the tick
thread; never mutate sessions from a database worker. No production-load speedup
is claimed from synthetic or static analysis.
