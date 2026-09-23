# Map performance: 0.3.29 investigation and 0.3.30 fix

The published 0.3.29 renderer did not scale to a fully explored world. Version 0.3.30 implements chunked rendering and incremental delivery. The measurements below are synthetic browser checks, not dedicated-server performance acceptance.

## Reproduce without a game or server

Install the existing browser-test dependencies, then run from the repository root:

```powershell
node tests/web/map-stress.cjs .dev/map-stress0330.json
```

The harness serves the actual local website into headless Edge, blocks external requests, and supplies clearly synthetic terrain and pins. It disables automatic refresh/animation scheduling so explicit calls can be measured independently. No profiles, databases, credentials or saves are accessed.

The fixture covers the same 76,648 terrain cells across a 10 km-radius disk, a 2,000-by-1,000 CSS-pixel canvas on a 2,560-by-1,440 viewport, and 1,000 deterministic distributed pins. Half are personal pins and half are game pins, exercising exploration checks. Twenty synthetic owners are represented. Optional SLS stress data contains 40,000 squares. The final scenario adds 5,000 events across 1,000 positions with both heat layers enabled.

The historical baseline used lazily generated 64-by-64 pixel strings to avoid allocating a roughly 1.67 GB synthetic JSON payload. The new run uses the actual new 4-by-4 overview format plus exact 64-by-64 exploration masks, composed into 332 overview chunks. Initial overview preparation took 1,838 ms total in work slices targeting four milliseconds; it is excluded from warm pan/zoom redraw times. Twelve redraws per scenario are spaced 17 ms apart to let the compositor progress. HTTP transfer and detail loading are tested separately. Icon imagery uses normal fallback symbols rather than downloaded assets. Results are diagnostic timings, not hardware-independent FPS predictions.

## 0.3.30 measured results

| Full-world scenario | Median redraw | Maximum redraw |
|---|---:|---:|
| No markers | 1.0 ms | 1.3 ms |
| 400 markers | 3.1 ms | 3.6 ms |
| 1,000 markers | 5.5 ms | 21.7 ms |
| 1,000 markers + 40,000 SLS squares | 6.5 ms | 14.8 ms |
| Same + 5,000 heat-map events | 9.1 ms | 24.8 ms |

Every measured warm redraw performed zero terrain-tile decodes. Raw synthetic results are in `docs/benchmarks/`. Detail requests are bounded to 64 coordinates per request and a desired working set of 2,048 tiles; tests verify that a wide view settles instead of repeatedly downloading evicted tiles. Dense expanded-pin behavior also remains covered by the existing browser suite.

## Historical 0.3.29 findings

- Marker-only production rendering: 400 markers takes roughly 4–6 ms after the first sample; 1,000 takes roughly 11–15 ms. At this zoom the distributed fixtures form five clusters and one cluster, respectively. Other distributions and expanded fans can have different costs.
- Full-world redraws with SLS disabled: **43,057 ms with 1,000 markers**, **42,758 ms with 400**, and **43,346 ms with zero**. Every pass decoded all 76,648 tiles. Reducing pin count does not address the dominant terrain cost.
- Separate production `terrainTile` microbenchmark: 3,000 cached tiles took 29 ms on the second pass with zero misses; 5,000 took 1,125 ms with all 5,000 misses. These earlier timings exclude map compositing and markers.
- `terrainTile` retains only 4,096 individual canvases. When more unique visible tiles are traversed in the same order, insertion evicts tiles needed later and the next redraw repeats decoding and canvas creation. Increasing this cap alone does not solve whole-world memory or draw-call costs.
- `drawMap` resets canvas dimensions and visits/draws every visible terrain tile for each drag event. Browser pointer events are not coalesced into one render per animation frame.
- SLS's below-fog path additionally allocates a viewport mask and traverses terrain again on each redraw. The 40,000-zone / 1,000-pin SLS-on run exceeded the 45-second guard and was stopped; this is not a completed timing.
- `/api/map` returns the complete selected exploration; the browser requests it in the five-second refresh cycle. Gzip is supported when available, but the server still builds the full JSON and the browser must decode it. The 1.67 GB figure is uncompressed pixel text at the stress fixture's scale, not a measured live-server download.

## Implemented in 0.3.30

1. Cached overview chunks and bounded 16/64-pixel near-view detail. Exact exploration bit masks govern icon/event visibility; coarse partially explored blocks remain hidden until detailed pixels load.
2. Reused SLS paths, layer canvases and fog masks; cached heat sprites and event grouping.
3. Drag/wheel work coalesced into animation frames. Existing pin clustering and fan interaction are retained.
4. Bounded incremental overview pages, separately requested near-view detail and SLS metadata. Each request enforces current sharing; scope changes reset the browser cache. Existing database rows migrate in batches of at most 128.
5. Backend tests cover paging, migration/restart, updates, exact fog, sharing revocation and HTTP authorization. Browser checks cover terrain orientation, detail invalidation, bounded downloads, races, layers and existing map interactions.

Remaining acceptance: open the new package on a real dedicated host with substantial exploration, pan/zoom in a normal browser and Steam overlay, and check initial indexing/download behavior. The initial below-fog SLS metadata request still prepares a compact mask cache; later requests reuse it. First-time overview loading is gradual, and close-up detail can briefly look coarse while it downloads. No live server, game profile or save was modified by these tests.
