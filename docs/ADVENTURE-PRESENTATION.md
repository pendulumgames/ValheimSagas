# Adventure presentation follow-up

Design review requested September 23, 2026. Illustration direction approved; a browser-based Eikthyr study now exists in TestingOutputs/trophy-hall-study/index.html. Trophy hall and comparison production integration are implemented for 0.3.34, with verification in progress. Animated saga journeys remain the next planned feature. No profile or live-server changes yet.

## Trophy hall
- Replace numbered-card emphasis with oversized transparent boss trophy/head artwork extending above the card, a restrained glow, and a carved-plaque base.
- First recorded defeat reveals the trophy. Keep lifetime unlock identity separate from the selected-period records so filtering does not visually revoke a victory.
- Display first recorded victory, highest-star victory (team, date and duration if known), and fastest measured fight as distinct records. Current 0.3.33 only selects first and fastest; it shows their stars but does not select a highest-star record.
- For highest-star ties, choose shortest valid measured duration at that star tier; if none have timing, choose the earliest recorded victory. Never substitute a lower-star duration. Explain tie rule in details.
- User selected original boss illustrations over runtime game trophies. Original transparent Eikthyr art created using the built-in image tool, without paid API calls or game asset extraction. User specifically wants the complex ornamental border: layered rails, stepped/interwoven corners and central ornaments, not a plain gold outline. Implement this frame in crisp SVG separately from illustration and live text.
- Missing/modded art needs an intentional fallback, with no false claim that a head render exists.

## Viking comparison
- Two distinct, consistently colored Viking columns with larger portrait/name headers, labels in a central column, aligned numeric values and clear gutters.
- Preserve accessible table semantics and responsive readability. Portrait fallbacks remain intentional. Color is supplementary, never the only identifier.
- Avoid winner judgments for deaths, drops or play time. Preserve period filters, integration-aware rows and sharing permissions.

## Saga atlas layer
- Independent Saga toggle and separate lazy-loaded, bounded feed. Preserve terrain rendering performance.
- Larger chapter-standard markers, distinct from ordinary pins; a banner-shaped story panel uses the chapter title, short prose excerpt, participants, date and a Read chapter link.
- Selecting a chapter highlights its significant recorded moments. Do not present event fact strings as the story title.
- User proposes animated journeys with moving arrow/trail, a play button, and a full chapter page reachable from map or Viking saga. Recommended controls: play/pause/replay/scrub, optional camera follow, reduced motion, separate participant tracks. Connections represent recorded moments, not tracked footsteps. Avoid implying teleports/ocean crossings are walking paths; do not connect different players as one route or expose unshared/unexplored points.
- Honor map/profile consent, world/scope changes, fog, overlapping markers, mobile input and reduced motion. Invalidate late replies after context changes.
- No additional OpenRouter calls are required to display existing chapters.

## Preview artifact
- TestingOutputs/trophy-hall-study/index.html: standalone responsive interactive study, real transparent generated artwork and SVG frame, synthetic records explicitly labeled.
- Adjacent desktop.png and mobile.png: screenshots; eikthyr.png: source artwork.
- Shows earned and unclaimed states and a replayable first-victory transition. These previews do not claim backend integration or actual gameplay records.

## Verification needed for implementation
- Highest-star selection/timing ties and lifetime unlock versus period filters.
- Artwork capture bounds, consent, persistence and missing optional boss art.
- Comparison accessibility and desktop/mobile screenshots.
- Saga layer privacy, stale-reply races, clusters and full-world stress performance.
- Capture previews for user review before profile installation.

Preview refinement: unrecorded bosses now use a blurred black silhouette behind layered procedural mist rather than dimmed detailed artwork. The first-forsaken label has a solid dark inset and higher-contrast gold text below the illustration detail. Mist is static (no continuous animation work). Desktop/mobile screenshots regenerated; reveal action, reduced motion, image decoding and no-overflow/browser-error checks pass.

Final study refinement: fog mask reaches full transparency before its rectangular bounds, including the exposed upper edge. First-forsaken label restored to compact unboxed treatment and original vertical position, with a feathered dark backing/text shadow for contrast; unrevealed label muted further. Desktop/mobile screenshots refreshed; existing browser checks pass.

Fog density follow-up: restored heavier cloud coverage with a wider opaque center and increased mist alpha, preserving the fully feathered perimeter and compact text layout. Refreshed desktop/mobile screenshots; browser checks passed.

Fog outline refinement: replaced the elliptical mask with an asymmetrical, noise-displaced wispy mask and detached trailing wisps. Dense coverage preserved, soft perimeter remains inside the drawing bounds. Static effect; desktop/mobile screenshots refreshed and browser checks pass.

Fog direction correction: user wants weighted, lingering mist with thick and thin areas rather than a cloud-shaped cutout. Replaced shaped mask with overlapping horizontal density layers, a heavier lower bank and softer upper veils. Rectangular bounds feather away independently on both axes. Static preview, layout unchanged; screenshots and browser checks refreshed.

## 0.3.34 implementation
- Approved trophy art/frame/fog integrated into the actual website. Seven original boss illustrations plus the user-selected symbolic frozen monument for Kall. Unknown modded bosses use an intentional rune fallback.
- Highest-star evidence added independently of first/fastest records. Unlocks use shared lifetime receipts/evidence, independent of the selected window.
- Two-column Viking comparison with centered stat labels implemented.
- Eight alpha WebP files total 5,419,620 bytes (about 5.2 MiB), locally hosted and lazy loaded. No in-game capture, proprietary assets, paid API requests or new runtime dependency.
- Saga layer/replay/dedicated chapter page is still planned; no claim it ships in this package.

## 0.3.35 approved journey concept implemented

User approved TestingOutputs/saga-journey-concept-v1.png in full: ornate corners, gold trail, distinct icons, thematic scene and readable story panel. Production uses a separate transparent replay canvas above the existing terrain renderer. The same atlas DOM moves into the dedicated #journey/<chapter-id> page, retaining its map controls, fog, clock, zoom and terrain cache. The full chapter is below the map.

The Sagas toggle opens an independent chapter feed (24 chapters/page, up to 96 browser entries). Selected journeys contain up to 100 chronologically ordered locations; chapter text/evidence are loaded on demand. Permission/scope changes invalidate late replies and clear cached presentation. Replay supports scrub, pause/restart, speed, participant perspective and optional camera following; direct map interaction disables following. Offscreen/hidden-tab playback stops; reduced motion omits the moving arrow and still permits explicit step/play controls.

Scene artwork is a local composition of existing original biome and boss illustrations, not a newly generated image for each chapter. The panel labels prose as a chapter excerpt and event evidence separately. It never invents prose-to-event correspondence or a date for imported exploration. Trail breaks cover skipped evidence, different owners, >30-minute gaps and >1,200-metre displacements; that heuristic does not claim to identify actual portals. Visible trail samples follow explored ground.

The screenshot review uses a fresh development database populated with terrain exported read-only from an old isolated playtest copy, plus explicitly synthetic events and narrative. The original old database's culture-dependent text index produced looping keyset results under the development runtime; rebuilding that disposable copy failed, so records were exported into a clean fixture. No production database migration behavior was changed, and this observation is not evidence of a live-host failure.


## 0.3.36 story scenes
New chapters now supply up to six validated story scenes in the original generation request. Each scene has a title, passage and one or more request-local evidence aliases resolved privately by the server. Full text is assembled from the passages. Actual records determine coordinates and boss status; withdrawn/missing scene evidence hides the entire scene. Existing chapters are not converted and use six curated recorded highlights. The browser changes passage/artwork on selection and playback, names each chapter's Viking/server, uses rounded marker frames and larger double-bordered boss markers. The README screenshot is a working UI capture with explicitly illustrative events/story over copied test terrain, not a mock product screenshot or live provider verification.
