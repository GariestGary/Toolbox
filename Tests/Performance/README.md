# Toolbox performance benchmarks

## Updater benchmarks

Run the `UpdaterPerformance` category from Unity Test Runner in Play Mode. Results are available in
**Window > Analysis > Performance Test Report**. For quick iteration, filter individual parameterized cases such as
`RegularUpdateTiny(1000)` or `MonoCachedTickTiny(1000)`.

The primary suite compares one minimal component per GameObject:

- regular Unity `Update` callbacks;
- Toolbox `Updater.Update` dispatching `MonoCached.Tick` callbacks.

Each steady-state case creates and initializes 100, 1,000, 5,000, or 10,000 objects before timing, warms up for 10
frames, resets its callback counter, and records 30 frames. VSync is disabled and `Application.targetFrameRate` is uncapped for each case, then
both settings are restored. Fixed steps are suppressed during the Update/Tick cases so `FixedProcessControl` does not
contaminate the render-update comparison. `Empty` performs only the correctness counter increment; `Tiny` also
accumulates delta time. Frames are recorded through the Performance Testing package's scoped frame measurement API;
the correctness check asserts exactly `object count × 30` measured callbacks. The scoped form avoids the package's
trailing `WaitForEndOfFrame`, which can remain suspended in the Editor when the Game view is not being repainted.

During a full Toolbox Play Mode run, Unity Test Runner uses its generated test scene rather than a project scene.
Benchmark objects are hidden under a temporary root and destroyed after every case; they do not accumulate in the
Hierarchy between tests.

Initialization samples exclude GameObject creation. `RegularInitialization` measures `AddComponent`, while
`MonoCachedInitialization` measures `AddComponent` plus `Updater.InitializeMonos`. Membership samples use pre-created
components. `UpdaterRemoveMonosEndToEnd` includes both queuing every logical removal and the eventual stable in-place
compaction at an initialization boundary, rather than timing only the deferred queue operation. Method benchmarks also
record `GC.Alloc` call counts through the Performance Testing package.

Editor results are useful for relative development comparisons, but Player performance tests should be used for
serious conclusions. The benchmark code contains no Editor-only API. FixedUpdate comparisons are intentionally omitted:
fixed-step frequency is not equivalent to one callback per rendered frame. LateUpdate is also left for a separate suite
so the mandatory Update/Tick matrix remains practical to run.

## Messenger benchmarks

Run the `MessengerPerformance` category. All cases use synchronous `Measure.Method` samples with one warmup and five
measurements. Cheap dispatch operations are batched inside each sample:

- unbound subscribers: 0, 1, 10, 100, 1,000, and 5,000;
- live GameObject bindings: 1, 100, and 1,000;
- one target subscriber plus 0, 100, 1,000, 5,000, or 10,000 subscribers of unrelated message types;
- direct delegate context baseline: 1, 10, 100, and 1,000 listeners;
- parameterless `Send<T>()` with cache on/off, and cached parameterless versus an existing message instance;
- subscribe, repeated single unsubscribe, and bulk unsubscribe batches of 100, 1,000, and 10,000;
- deferred self-removal plus final stable compaction for 100, 1,000, 5,000, and 10,000 subscribers.

Dispatch batches use 10,000 sends for up to 10 subscribers, 1,000 sends for 100 subscribers, 100 sends for 1,000
subscribers, and 20 sends for 5,000 subscribers. Sample names include the operation count. Steady-state dispatch cases
record `GC.Alloc`; callback correctness is validated outside the timed sample.

## Pooler benchmarks

Run the `PoolerPerformance` category. Automatic Pooler GC is disabled. Pool definitions, prefabs, hierarchy creation,
and initial pooled-object instantiation happen before hot-path measurements.

Each pool tracks unused objects in a stable-order min-heap. Public Spawn therefore preserves the former "earliest
`pool.objects` entry wins" reuse order while acquire and despawn-return both cost `O(log n)` without per-operation heap
node allocations. An object is removed from availability before spawn callbacks, so reentrant Spawn cannot acquire it.

- prewarmed Spawn+Despawn, Spawn-only, and Despawn-only: 100, 1,000, 5,000, and 10,000 objects;
- public `IsObjectPooledAndUsed` lookup against a fixed pooled object: 10,000 queries with 100, 1,000, 5,000,
  and 10,000 total pooled objects;
- no lifecycle handlers versus one and three cached handlers: 100 and 1,000 objects;
- hierarchy traversal off/on: 1,000 prewarmed Spawn/Despawn cycles per sample;
- non-generic `IPooled`, `IPooled<int>`, and `IPooled<reference>`: 1,000 cycles per sample;
- target tag with 1, 10, 100, or 1,000 unrelated pools: 10,000 prewarmed spawns per sample;
- 1, 10, or 100 pools under the same tag: 10,000 prewarmed spawns per sample;
- cold runtime expansion by 1 or 100 instances.

The 5,000/10,000 throughput cases use three measurements; other hot cases use five. Expansion is deliberately reported
separately because it includes `Instantiate`, Updater initialization, and lifecycle metadata discovery. The tag lookup
diagnostics return the object through public `TryDespawn` after each spawn so Pool availability bookkeeping remains
synchronized. The pooled-object lookup benchmark deliberately targets the last prewarmed object, which exposes
the old linear scan while keeping every timed query on the public API. Pooler fixture teardown destroys every object still
referenced by every pool immediately, including
spawned objects detached from the pool root; this cleanup runs after sampling and cannot affect reported timings.
