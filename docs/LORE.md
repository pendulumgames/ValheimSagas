# OpenRouter storytelling

Sagas requires a host OpenRouter key and `EnableOpenRouter = true` before generating any player or server story. There is no local template fiction. Missing keys, exhausted budgets, invalid output or provider failures leave generation pending; recorded statistics remain available. Existing template chapters remain stored but are hidden from API responses, biographies and continuity. New generation uses a distinct ID namespace so a legacy template cannot block a real chapter.

## Host and personal presets

The host sets `[Lore] OpenRouterKey` or `OPENROUTER_API_KEY` on the hosting process. Never put this key in a distributed client configuration. New configurations enable the worker, but it does nothing without a key. The default route is `openrouter/free`; explicit `vendor/model:free` and `@preset/slug` routes are supported. Host requests always set both prompt and completion price ceilings to zero and never silently select paid service.

A personal character/world login can save up to eight named presets and an optional personal key. A preset contains a model or OpenRouter preset route, an explicit paid opt-in, a USD-per-million-token ceiling (applied independently to input and output), and 1?20 HTTP attempts per UTC day. Named settings are local Sagas configurations; `@preset/slug` refers to a preset created in the key owner's OpenRouter account. Sagas does not create remote OpenRouter presets. Choosing Host default or removing the personal key restores host routing for future work. Existing chapters are not rewritten by a settings change.

Personal keys use AES-256-CBC encryption with a random IV and HMAC-SHA256 integrity protection. The random master key is stored separately in `personal-lore.key` beside the database. Back up both together. Encryption protects a database-only copy; it does not hide a key from the host administrator. Keys never return in API responses, public pages or logs and are not stored in browser storage. Remote browser submission requires HTTPS; a loopback reverse proxy is supported. The admin must configure TLS correctly. The same host gate applies even when a player supplies a key.

## Evidence and continuity

Prompt version `sagas-4` separates new chapter events, retained career totals, supporting current context, a factual continuity capsule and previous fictional prose. Relevant facts include kills and stars, boss victories and contributing teammates, observed fight durations, earned collections versus generated drops, rarity discoveries, deaths, arrivals/departures, bounties, carried coins, active/equipped items and effects, effective resistances and permitted exploration summaries. Exploration cell footprint is labeled as an upper bound, not exact mapped area. Current gear and wealth snapshots are not asserted to describe an older event.

Prompts are bounded: up to 100 new events and 64 supporting context entries, with capped equipment, actors and text lengths. Omitted context is disclosed. It is not a raw database dump and cannot include events never tracked or already removed by configured retention. Coordinates, raw account/character/world IDs, event IDs, credentials and raw logs are excluded from outbound narrative data. User-controlled strings are untrusted JSON data, not instructions. Server facts retain individual attribution and do not imply all players were on the same expedition.

Only currently shared profiles contribute. Exploration additionally requires map sharing. Chapters record privacy dependencies, including previous narrative participants. Revoking sharing hides dependent chapters and biographies; re-enabling sharing can restore preserved history. Consent is rechecked after generation. Fiction stays visually distinct from the immutable supporting facts. Model prose can still make mistakes: the ledger is authoritative.

## Queue, costs and failures

Generation occurs in a single persistent background queue, never on a profile page request. Startup considers retained history; milestones and cooldowns control new chapters. Completed chapters are cached durably. Every attempt, including retries, reserves a daily allowance. Host requests share one daily budget; personal requests have their own per-character/world counter. Price ceilings constrain provider token rates, not a guaranteed total dollar spend; use OpenRouter key credit limits for a hard account-side budget. Presets do not bypass Sagas' explicit routing and price constraints.

Requests use `https://openrouter.ai/api/v1/chat/completions`, a 25-second deadline, 64 KiB response cap, 1,000 output-token limit and at most three attempts. Transient failures retry with bounded backoff; long rate limits and invalid responses leave work pending. A persisted five-minute delay prevents immediate failed-job loops. Production redirects are disabled. Tool choice is disabled and no Sagas browsing/tools are supplied. Logs contain diagnostic categories, never credentials or provider response bodies.

Only validated JSON titles/prose are stored; HTML, links and control characters are rejected. An omitted/invalid biography stays absent while a valid chapter can still be saved. Provider model and prompt version are persisted. This validation cannot prove fictional prose factually accurate.

## Verification and references

All automated provider checks use synthetic HTTP responses without real credentials or paid calls. Tests cover free/paid ceilings, preset requests, key isolation/encryption/restart, request budgets, retries, response limits, consent, persistence and continuity. Real provider availability, preset execution and paid billing still need an operator-controlled test using their own key.

Official documentation checked September 19, 2026: [free router](https://openrouter.ai/docs/guides/routing/routers/free-router), [presets](https://openrouter.ai/docs/guides/features/presets), [provider routing](https://openrouter.ai/docs/guides/routing/provider-selection). Free routing requires an account/API key and availability varies; it is not a storyteller quality ranking.
