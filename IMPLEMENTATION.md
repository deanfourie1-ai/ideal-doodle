# BC Release Plan Portal — Implementation Plan

Tracks progress against the phases in the design doc (§11 Build plan). Updated as work lands.

Legend: ✅ Done · 🔄 In progress / partial · ⬜ Not started

---

## Conventions

**Language: English throughout.** Schema, curation UI, and the published customer document are all
English. This resolves the design doc's §13 open decision 2, which left the working language open
and leaned toward "English internally, Dutch only at the publish boundary" — the publish-boundary
half is dropped; there is no Dutch layer.

Consequences, so this doesn't get re-litigated field by field:

- No language suffixes on schema fields. `ImpactNote.Summary` / `.WhyItMatters` / `.ActionRequired`,
  `CustomerItem.OverrideNote`, `ReleasePlanLine.Title` / `.Summary` / `.Action` — all previously
  carried an `Nl` suffix, renamed 2026-08-30.
- Decision states (`onbeslist` / `overnemen` / …) render in English: Undecided, Adopt, Test first,
  Ignore, Blocked.
- Document section headings are English: Summary, Mandatory changes, Recommended new functionality,
  For information, Next steps, Changes since previous version.
- If a translated document is ever wanted, it becomes a layer over these fields (a translation step
  at publish time), never a second set of columns.

Two things that look like exceptions but aren't: **`Localisation-NL`** is one of Microsoft's own BC
module names (the Dutch localisation of the product), and **`Werkinstructie_Template.docx`** is the
real filename of the company's existing Word template. Both are proper nouns, not language choices —
the template's *content* is English.

Unaffected by this: `CustomerContact.Language` still records a real person's own preference, and
`RoadmapIngest.TimeZoneId` (`Europe/Amsterdam`) is geography, not language.

---

## Phase 0 — Spike

**Goal (doc):** confirm roadmap RSS/CSV shape for BC items, confirm MCP responses, hand-build 2 customer profiles.

🔄 **Partial.**

- ✅ Confirmed live, against the real server (2026-08-30): `https://www.microsoft.com/releasecommunications/mcp` works exactly as documented — JSON-RPC over a single-POST "streamable HTTP" call, 4 read-only tools (`get_recent_m365_roadmaps`, `get_m365_roadmap_by_id`, `get_recent_azure_updates`, `get_azure_update_by_id`), no auth.
- ✅ **Business Central is now on the MCP server** (confirmed 2026-10-06). On 2026-08-30 the dataset was Microsoft 365 + Azure only (1775 items, 36 products) and BC was absent. On 2026-09-30 Microsoft published 80 items tagged `Dynamics 365 Business Central` (69 Launched with GA 2026-10; 11 In development with GA 2026-10 → 2027-04), so the doc's "September 2026" premise held. The tag matches the existing `bc` entry in `RoadmapIngest:ProductFilters` exactly, so no code change was needed. First real ingest run (2026-10-06): 82 seen / 82 new (80 BC + 2 Power Automate demo), paging via `skip` past the 50-item limit worked.
- ✅ Learn page shapes confirmed live (2026-10-06, from a dev machine — the earlier block was the build environment's network policy, not Learn). Major-update "What's new" pages (x.0) have a feature table with a **Roadmap ID** column — an exact join to roadmap items. Minor updates (x.1–x.5) have a feature table without IDs. `toc.json` lists every update page, so versions are discovered, not configured. The deprecated-features page is one long page of per-wave sections (h2 per wave, h3 per feature, a "Moved/Removed/Replaced? | Why?" table each) with no roadmap IDs. Release plans stopped in September 2026 (Microsoft moved BC onto the AI at Work roadmap), which removes the only source `EnabledBy` ever had.
- ⬜ Hand-built customer profiles — not done. The `Customer` schema exists (Phase 1; the doc's separate `CustomerProfile` is folded into it as owned value objects) but there is no real profile data.

**Next steps:**
1. ~~Re-check the MCP roadmap dataset for a BC product tag~~ — done, see above. Consider dropping the `power_automate_demo` filter now that BC has live data.
2. ~~Capture real Learn HTML~~ — done; fixtures in `tests/.../Fixtures/learn_*`.
3. Hand-build 2 real `Customer` profiles once real customer data is available (not fixture data).

---

## Phase 1 — Ingest

**Goal (doc):** schema, daily job, hash/diff, ChangeEvent, Learn scrapers.

✅ **Done.** Remaining gaps are data Microsoft doesn't publish (see `EnabledBy`/`ObjectsTouched` below).

Built in `src/BcReleasePlanPortal.Domain`, `src/BcReleasePlanPortal.Ingest`, `src/BcReleasePlanPortal.Data`, `src/BcReleasePlanPortal.Worker`:

- ✅ Full schema (EF Core + SQLite): `RoadmapItem`, `ChangeEvent`, `ImpactNote`, `Customer` (+ owned value objects), `CustomerItem`, `ReleasePlan`/`ReleasePlanLine`.
- ✅ MCP client (`Mcp/McpJsonRpcClient.cs`, `Mcp/RoadmapMcpClient.cs`) — real, tested against the live server.
- ✅ Rule-based `change_type` and module classifiers (`Normalization/`), each flagging low-confidence output `NeedsConfirmation` rather than asserting.
- ✅ Payload hashing + field-level diffing → `ChangeEvent` rows (`Diffing/`). Verified: catches a GA date move, stays silent when nothing changed, idempotent on re-run.
- ✅ Daily background job (`Worker/DailyIngestBackgroundService.cs`, 06:00 Europe/Amsterdam, configurable) + `dotnet run --run-once` for manual runs.
- ✅ Teams webhook alerting for urgent changes (`Alerts/`), no-op/logged when no webhook URL is configured.
- ✅ Config-driven product filters (`RoadmapIngest:ProductFilters` in `appsettings.json`) — not hardcoded to BC, ready for other Microsoft platforms per the "we sell them all" direction.
- ✅ 84 unit tests, several built on real MCP responses captured live rather than fabricated fixtures.
- ✅ **Module classifier hardened against real BC data** (2026-10-06). The first BC ingest exposed plain substring matching: `sepa` hit "separate", `vat` hit "elevated"/"avatars", `bin` hit "combines", `al language` hit "natural language" — 17 wrong tags across 80 items, 11 of them `Localisation-NL`. Now whole-word matching (with plural/verb endings; a trailing `*` marks a stem), and bare `dutch` became `dutch locali*` because Microsoft lists Dutch among supported UI languages. Re-ingested from a fresh DB: all 17 wrong tags gone, no correct tags lost, `Localisation-NL` count 11 → 0, untagged items 11 → 17 (correct: no tag beats a wrong one). Regression tests use two of the real items as fixtures. Follow-up the same day: bare `customer` dropped from Sales (it alone tagged 11 of 13 Sales items — "customers can…" is in most descriptions); replaced by `sales document`, `sales return`, `customer card`, `customer ledger`. 9 wrong Sales tags removed; "Manage Shopify B2B companies, catalogs, and pricing" lost Sales too and now goes to triage untagged.
- ✅ **Learn "What's new" source** (`Learn/HttpLearnPageSource.cs`, AngleSharp). Each run reads the two most recent major-update pages and sets `TargetVersion` by Roadmap ID — meaning the update a feature becomes **generally available** in; rows marked Public preview are skipped (29.0 lists Expense Agent features as preview whose GA is April 2027). Learn only ever sets or moves a version, never clears one, so a Learn outage can't look like Microsoft un-scheduling features; a first version is enrichment and raises no ChangeEvent, a version that moves does. Verified on the live data: 68 of 80 BC items get 29.0, the 11 in-development items and one preview-only item stay empty, a second run changes nothing. Viewer shows a Version column.
- ✅ **Learn deprecated-features source** (2026-10-06). Each feature section of the deprecated-features page becomes its own item (`Source = LearnDeprecation`), from the current BC major version onward — earlier removals have already happened to every online tenant. Change type comes from the page's own words, so it's confident: "(removal)"/Removed → Retirement, "(warning)" → Deprecation, Moved → BehaviourChange, Replaced → Deprecation. `TargetVersion` from the wave heading (or derived: 2026 wave 2 = 29.0 — the formula reproduces every version the page prints), `GaDate` = wave start (April/October). External ID is version + title slug, not Learn's anchor, because Learn numbers duplicate anchors by position. A feature's warning and its later removal are separate items — two distinct events for a customer. Verified live: 4 items (Finance reports API warning in 29.0; AMC Fundamentals, Peppol BIS 2.x and Finance reports API removals in 30.0); second run is a no-op. Both sources share one persist/diff/alert path.
- ⚠️ **`EnabledBy` has no source any more** — it came from release plans, which Microsoft stopped publishing in September 2026. Stays `Unknown` for new items. `ObjectsTouched` isn't on the "What's new" pages either; deprecation prose names objects only in free text.

**Next steps:**
1. New items never alert today (only changes to known items do) — for the MCP source that was fine, but a *new* deprecation appearing is exactly the urgent case. Decide whether a new `Retirement`/`BreakingChange` item should alert on arrival.
2. Find a source for `ObjectsTouched` — the match engine's highest-value signal (§7: `objects_touched ∩ extends_objects` → +40). Learn has none in structured form; candidates are the deprecation prose (fuzzy) or AL symbol/obsolete-tag data from Microsoft's base app (structured, needs investigation).
3. Note for future DB upgrades: re-ingest only re-normalizes items Microsoft has modified, so a classifier change does not reach existing rows. Fine while no curation exists (the DB was simply rebuilt); once ImpactNotes/CustomerItems hold real work, a classifier change needs an explicit re-classify step instead. Learn deprecation items are the opposite: they are re-normalized every run, so a classifier change *does* reach them, and logs a `Modules` ChangeEvent (seen 2026-10-06 when "customer" was dropped from Sales). Both behaviours should converge on the same explicit re-classify step.
4. Expect every BC item to come back `Enhancement` + `NeedsConfirmation` from the change classifier (by design — no deprecation/retirement/breaking keywords in this wave); triage will be fully manual until the rules or an LLM pass improve.
5. Decide the .NET 9 question: this runs on .NET 8 because 9's SDK wasn't available via this environment's package sources. Revisit on a machine/environment where it is, or explicitly commit to 8 LTS.

---

## Phase 1.5 — Basic viewer *(not in the doc's original plan; built as a checkpoint)*

✅ **Done.** `src/BcReleasePlanPortal.Web` — a minimal read-only Blazor page at `/` showing every ingested `RoadmapItem` (title, product, modules, change type, status, GA date, needs-confirmation flag), with product tabs. No auth, no editing. Exists purely so the ingest pipeline's output is visible without querying SQLite by hand; this page grows into the real Triage screen in Phase 3 rather than being thrown away. *(Superseded 2026-10-06: the page became the Triage screen — see Phase 3.)*

---

## Phase 2 — Match engine

**Goal (doc §7):** score `RoadmapItem × CustomerProfile`, explainable match reasons, tuned against real profiles.

⬜ **Not started.**

**Next steps:**
1. Implement the scoring rules from §7 as a small, pure function (`modules ∩ modules_in_use` → +30, `objects_touched ∩ extends_objects` → +40, SOAP/ISV/version/enabled-by rules, new-capability-outside-scope penalty) producing a score + `match_reasons` list.
2. Persist results into `CustomerItem.MatchScore`/`MatchReasons` — schema already exists (Phase 1).
3. Needs real `Customer` profiles (Phase 0) and, ideally, `ObjectsTouched` data (Phase 1's Learn gap) to exercise the highest-value rule.
4. Unit tests per rule, plus a golden-file test against a hand-built profile.

---

## Phase 3 — Curation UI

**Goal (doc §8):** triage queue, impact editor, customer board.

🔄 **Partial.** A visual design mockup of all 5 screens exists — a Claude Design canvas at
https://claude.ai/code/artifact/f5f8ec88-0391-4c5c-a27d-73a4d73039a5 — the Triage screen is now real UI (built without refreshing the mockup; the layout follows the existing viewer); the other four screens aren't built yet.

✅ **Triage screen built (2026-10-06)** — `/` in `BcReleasePlanPortal.Web`, now interactive (Blazor Server):

- Default view is the queue (items with `NeedsConfirmation`), urgent types first, then by GA date; "All items" tab and product filter are links (`?view=all`, `?product=bc`). Description expandable per row; Learn deprecations badged.
- **Confirm** accepts the classifiers' reading; **Edit** sets change type + modules (checkboxes from `Domain.BcModules.All`, the taxonomy the classifier keywords are now tested against); **Reopen** undoes a decision; every action offers **Undo**.
- New column `RoadmapItem.TriagedAt` (migration `AddRoadmapItemTriagedAt`) — non-null means change type and modules are human-owned. Writes go through `Data.RoadmapTriageService`.
- **Ingest respects triage** (`Ingest/Diffing/TriageCarryOver.cs`): re-ingest keeps the human values and raises no ChangeEvents for them — *except* when the fresh classification is urgent (deprecation/retirement/breaking) and differs from the human one: then the classifier wins and the item goes back to the queue, so a human "Enhancement" can never hide a removal announced later.
- Verified in the real app by driving Edge with Playwright: confirm, undo, edit-and-save, reopen all round-trip to the DB; a decision made in the UI on a Learn deprecation (re-normalized every run, so the strictest case) survived two ingest runs unchanged with zero ChangeEvents. Test decisions were rolled back afterwards — the local DB starts with 84 items in the queue, none triaged.
- Not yet: who triaged (no auth — `TriagedAt` only); bulk confirm; a favicon (the only console 404).

⚠️ **The mockup is out of date as of 2026-08-30:** it was drawn with Dutch labels throughout
(`onbeslist` / `overnemen` / `eerst testen` / `negeren` / `geblokkeerd`, `Verplichte wijzigingen`,
Dutch impact-note fields and sample document copy). Under the English-throughout convention above,
it needs regenerating before it's used as a build reference — the layouts and information hierarchy
still hold, only the labels and sample copy are wrong.

⬜ Not built: the impact note editor (effort/risk selectors,
matched-customers panel), the per-customer Kanban decision board.

**Next steps:**
1. Regenerate the design mockup with English labels and sample copy (layouts unchanged).
2. ~~Triage screen~~ — done (above). Reopen semantics to revisit once real triage happens: reopening keeps the human values in place (the classifiers only take over again when Microsoft next modifies the item).
3. Build the Impact Editor screen against `ImpactNote` (schema exists) — this is where AI-drafted copy would plug in per the doc's core principle #3 (AI enrichment, never in the ingest path).
4. Build the per-customer Kanban board against `CustomerItem.Decision` (schema exists) — needs Phase 2's match engine to have real candidates to show.
5. Needs Phase 2 (match engine) and real `Customer` data (Phase 0) before this is meaningfully usable end to end.

---

## Phase 4 — Publish

**Goal (doc §9):** freeze snapshot → Word (template) / Markdown / CSV export.

⬜ **Not started.** `ReleasePlan`/`ReleasePlanLine` schema exists (Phase 1) but nothing populates or exports it.

**Explicit design goal for this phase (added 2026-08-30):** the Word document is not a nice-to-have export alongside the portal — it's the fallback deliverable for any customer who never touches the portal at all. On company letterhead, built entirely from the same curated data (`ImpactNote` + `CustomerItem` decisions filtered to that customer), it has to stand alone as a complete, credible release plan with zero portal access assumed. This was always implicit in the doc's §1 framing ("a deliverable customers relied on you for") — worth stating outright now so the Word template work in this phase doesn't get treated as secondary to the portal.

**Next steps:**
1. "Freeze" logic: copy curated `CustomerItem` + `ImpactNote` state into `ReleasePlanLine` rows, versioned.
2. Word export via Open XML SDK against the existing `Werkinstructie_Template.docx` conventions (doc §9.1, §10), on company letterhead. Section headings in English per the conventions above — the doc's §9.1 names them in Dutch (`Samenvatting`, `Verplichte wijzigingen`, …); use the English equivalents.
3. Markdown and CSV export.
4. Needs Phase 3 (curation UI) to produce anything worth publishing.

---

## Phase 5 — Pilot

**Goal (doc):** run one real customer plan end to end, fix what breaks.

⬜ **Not started.** Blocked on Phases 2–4.

---

## Phase 6 — Customer view *(phase 2+ in the doc, scope expanded 2026-08-30)*

⬜ **Not started.** Originally scoped (doc §9.2) as a tokenised, read-only, no-login link rendering a frozen snapshot. Expanded scope, agreed 2026-08-30:

- **Live "what's new" view, filtered by the customer's own modules.** Not just the frozen document — a page the customer can return to that shows items relevant to them (via `CustomerItem`, filtered by `Customer.ModulesInUse`), with a "changes since publication" banner as originally scoped.
- **"Ask for help" action per feature → creates a ticket in TopDesk.** Decision made 2026-08-30: TopDesk specifically, via its REST API, not a mailto: or generic webform. Needs a TopDesk API credential/config, a payload mapping (subject/description built from the `RoadmapItem` + `ImpactNote`, caller reference tied to the `Customer` record so it lands against the right account), and error handling for a failed ticket creation (never lose the customer's request — fail with a visible retry, don't silently drop it).
- **Consequence for "no login":** a bare tokenized link with no server-side identity has nothing to attach a TopDesk caller reference to. This still doesn't need full customer login (the doc's original instinct not to build customer auth for an internal-consulting-firm tool is sound), but the link now needs to resolve to a specific `Customer` record server-side rather than just gating access to a static document. A per-customer tokenized link that maps to a `Customer.Id` (revocable, expiring, same as originally scoped) still satisfies this without building real auth.

**Next steps:**
1. Design the tokenized-link-to-`Customer` resolution (revocable, expiring — doc's original requirement, still holds).
2. Build the filtered "what's new" view against `CustomerItem`/`Customer.ModulesInUse` (needs Phase 2's match engine and real profile data).
3. TopDesk integration: confirm API auth method (API key vs. OAuth), build the ticket-creation call, decide what "success" looks like to the customer (confirmation + ticket number shown inline).
4. Needs Phases 2–4 first — there's nothing to show a customer until items are matched, curated, and at least one plan is published.

---

## Phase 7 — Profile automation *(phase 2+ in the doc)*

⬜ **Not started.** BC admin centre API for versions/update dates; AL repo parser for `extends_objects`.

---

## Phase 8 — ISV layer *(phase 2+ in the doc)*

⬜ **Not started.** Manual entry + ISV release-note ingestion (Continia/idyn/Anvaigo).

---

## Open items carried over from the design doc (§13)

Two of these have been resolved since; the rest still stand.

1. Billable service line vs. included-in-support vs. sales differentiator?
2. ~~English or Dutch as the internal working language?~~ **Resolved 2026-08-30: English throughout** — see Conventions at the top. Goes further than the doc's own recommendation, which kept Dutch at the publish boundary; there is no Dutch layer at all now.
3. F&SCM later? `RoadmapItem.Product` is already a free-text string, not a fixed enum, specifically to keep this open (and to support "all Microsoft platforms we sell," per direction given during Phase 1).
4. Who owns profile data — still unresolved, and now blocking Phase 0/2/3.
5. ISV roadmaps: same document or separate annex — still open, relevant once Phase 8 starts.
6. *(New, resolved 2026-08-30)* Support-ticket target for the Phase 6 customer portal: **TopDesk**, via API. Still open within that: API auth method, and exact caller/customer mapping for tickets created this way.
