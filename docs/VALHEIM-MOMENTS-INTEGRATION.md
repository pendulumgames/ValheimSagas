# Valheim Moments integration assessment

Status: investigated, not enabled. Sagas does not import or publish Moments media in this release. Moments remains entirely optional.

Read-only source inspection on September 23, 2026 covered the local Valheim Moments 0.23.2 repository. No Moments source, configuration, gallery files, game saves, or live server data were changed.

## What exists today

- `ValheimMoments/Plugin.cs` creates local gallery entries during `StartEncoding` and raid composition. Recording and encoding are already handled by Moments; Sagas would not need to take another screenshot.
- Internal `MomentGallery` stores animated `.webp` clips under `Gallery/Recovery` or `Gallery/Saved`, and a 256 by 144 raw RGBA preview beside a private `index.bin`.
- The version-1 gallery index records a caption, recorder display name, timestamps, status, and a Discord message link. It does **not** persist the world ID or stable Viking ID. A display name cannot safely identify a Viking across worlds or duplicate names.
- In-memory gallery entries have an `Origin` session object, but it does not survive restart. Completion may follow asynchronous delivery or a later retry. A completion-time lookup of the currently selected Viking is therefore insufficient evidence of ownership.
- There is no public versioned media-export event or gallery export contract in this version. `HostConfiguration.Export` exports configuration, not media.
- Sagas currently validates PNG portraits and item/map icons. Its portrait chunk assembler deliberately reconstructs a portrait upload. Animated WebP clips must not be sent through that channel disguised as portraits.

Private reflection hooks are technically possible. The blocker is a reliable identity and publication contract, rather than an inability to locate files. Importing the existing gallery wholesale could expose a different world's adventures or attribute a clip to the wrong Viking. The low-resolution raw preview also does not meet the quality expected of a profile gallery.

## Minimum useful implementation

The next step requires a corresponding Moments change, separately authorized and tested in that repository:

1. Add a versioned export event carrying an immutable capture-time world ID, Viking ID, UTC timestamp, local media ID, caption, and an owned static preview. Keep recorder display names for presentation only. Include an optional event identity only when Moments genuinely knows it; never invent a boss-event association from timing alone.
2. Make website publication an explicit player choice, separate from local saving and Discord delivery. Prefer a per-memory **Share to Sagas** action; general profile sharing alone should not publish every captured screen. Players need a removal action and a clear statement that the host stores a copy.
3. Hand off a bounded encoded still image after existing background encoding. Do not start capture, call Unity APIs from workers, or synchronously read/encode imagery during gameplay. Transfer byte ownership safely so Moments cleanup cannot invalidate an export.
4. Add a distinct, authenticated Sagas adventure-media endpoint and storage collection. Validate dimensions, format, byte length, owner, world, and consent. Bound pending work, transfer size, request rates, per-player gallery count, and host disk use. Use gameplay-priority backpressure or an authenticated website upload rather than bypassing existing transport limits.
5. Expose only shared media through the profile/saga API, with private-profile revocation and deletion enforced on the server. Show a capture date and player-supplied caption; the image is not proof of a kill or item acquisition.
6. Lazy-load static thumbnails and make any future animated playback opt-in. Do not load clips into atlas polling or preload entire galleries.

Historical import would additionally need the player to explicitly choose the target world and Viking and select each clip, because the current saved index cannot reconstruct those identities. It should not run automatically.

## Required verification before enabling

Test a world/character switch while an export is pending, duplicate player names, a delayed completion, a delivery retry, gallery cleanup during transfer, consent revocation, malformed media, server quotas, absent/incompatible Moments versions, and a headless dedicated host. Measure actual frame timing and game transport queues. Source inspection alone is not an in-game integration test.

The authorized companion plan is `SAGAS-INTEGRATION-PLAN.md` in the ValheimMoments repository. It is documentation only; implementation there is explicitly deferred by the owner.
