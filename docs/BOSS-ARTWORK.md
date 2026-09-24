# Boss hall artwork

The eight files in `src/Sagas.Web/bosses` are original generated, interpretive illustrations created for Valheim Sagas. They are not game screenshots, extracted models/textures, or official boss portraits. Kall's frozen monument is intentionally symbolic and does not depict his appearance, as requested by the project owner.

## Production recipe

- Built-in image generation, September 23, 2026; no external paid API calls or account credentials used.
- Eikthyr established the approved style: detailed dark fantasy painting, isolated head/upper body, transparent margins, atmospheric biome vignette fading out at the base. The original concept sheet is retained locally in TestingOutputs.
- Seven subsequent images used Eikthyr only as a style/composition reference. Subjects: ancient living tree (Elder), swamp ooze/bones (Bonemass), frost dragon (Moder), crowned skeletal king (Yagluth), chitinous insect queen (Queen), charred dragon with green fire (Fader), and a faceless frozen monument (Kall).
- Requested true transparency, no text or frame baked into the art. The delivered source PNGs and encoded WebPs are 1024 x 1536. All source corner alpha values were 0 or 1 out of 255.
- WebP encoding preserved dimensions and alpha using browser canvas at quality 0.86. Source PNGs remain in the isolated development folder; only the final WebPs ship. Builds consume the committed WebPs, so regeneration is not a build dependency.

The ornament is responsive SVG, not a generated bitmap. Names, records and accessibility descriptions are normal website markup. Layered static SVG mist and a blurred silhouette hide detailed artwork before a shared recorded victory. Only a newly observed unlock animates briefly; reduced-motion preference disables the transition. No continuous fog animation runs.

The package allowlist, static HTTP allowlist and artwork validator enumerate all eight files. Package checks verify their dimensions and hashes. Unknown modded bosses use a neutral rune rather than another boss's illustration.

Kall's monument may eventually be replaced with an approved reference-based illustration; his fight data is independent of that artistic choice.
