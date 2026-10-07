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
- 🔄 **Customer profiles — two *made-up* ones exist (2026-10-06), real ones still don't.** `samples/customers.sample.json`, loaded with `dotnet run --project src/BcReleasePlanPortal.Worker -- --seed-customers samples/customers.sample.json` (matches by name, so re-running updates in place). Both are fictional, named "(sample)" and flagged `Flags.IsSample`; contacts are role-only placeholders. They're designed to exercise the match engine against the real data:
  - **Brightline Fabrication B.V.** — production/manufacturing: Manufacturing + subcontracting extensions, SOAP endpoints for a shop-floor MES, **AMC Banking 365 Fundamentals** (hits the AMC Fundamentals removal in 30.0).
  - **Northgate Distribution B.V.** — wholesale/distribution: multi-warehouse, Shopify B2B, EDI, multi-company, APIs and webhooks, **Peppol BIS 2.1** invoicing and **Finance reports API (beta)** dashboards (hit the Peppol BIS 2.x and Finance reports API removals in 30.0).
  - Schema addition (inside existing JSON columns, no migration): `Integrations.Other` — named APIs/standards/formats a customer depends on, written as Microsoft names them so matching can find them in titles; `Flags.IsSample`.

**Next steps:**
1. ~~Re-check the MCP roadmap dataset for a BC product tag~~ — done, see above. Consider dropping the `power_automate_demo` filter now that BC has live data.
2. ~~Capture real Learn HTML~~ — done; fixtures in `tests/.../Fixtures/learn_*`.
3. Replace the samples with 2 real `Customer` profiles once real customer data — and an owner for it (open item 4) — exists. Phase 4's publish step must refuse a customer with `Flags.IsSample`.

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
- ✅ 130 unit tests, several built on real MCP responses captured live rather than fabricated fixtures.
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

✅ **Built (2026-10-06), tuned against the two sample profiles — not yet against real ones.**

- `Domain/Matching/MatchScorer.cs` — pure function, a reason string for every point. Candidate at **30**.

  | Rule | Points | Source |
  |---|---|---|
  | Item modules ∩ modules in use — item triaged | +30 | doc |
  | … — item not yet triaged (classifier guess) | +15 | tuned |
  | Objects touched ∩ extended objects | +40 | doc (never fires yet: no `ObjectsTouched` source) |
  | Customer publishes SOAP and item mentions SOAP | +40 | provisional |
  | AppSource app named in the item title | +40 | provisional |
  | `Integrations.Other` dependency named in the title | +40 | provisional |
  | Customer uses Copilot and item is about Copilot/agents | +20 | provisional |
  | Enabled automatically for users (and already relevant) | +10 | provisional (`EnabledBy` has no source) |
  | Deprecation/retirement/breaking (and already relevant) | +20 | provisional |
  | New capability/enhancement only in modules they don't use | −20 | provisional |

  The design doc itself isn't in either repo; only the two "doc" weights were recorded here. The rest are provisional — set so one named dependency alone makes a candidate and urgency amplifies relevance but never creates it.
- **Tuning on real data, kept for the record.** First run: 47 and 61 candidates, mostly "+30 uses Reporting" from unconfirmed classifier tags → unconfirmed module overlap now counts half, so triage is what turns module-only matches on (the triage screen re-scores immediately). Names now need *all* their distinctive words (two-of-three matched "Finance reports API" to "Trace G/L account usage in finance reports"); app and dependency names are matched on titles only. Only `bc` items are scored — profiles describe BC tenants; SharePoint/M365 items had matched on modules.
- Result today: **Brightline** 3 candidates — AMC Fundamentals removal (60) on top; Finance reports API warning/removal (35, via Reporting). **Northgate** 19 — Shopify B2B (95), Finance reports API (75), Peppol BIS 2.x (60), EDI and the Shopify items (55), Copilot/agent items (35).
- `Data/MatchRunner.cs` syncs `CustomerItem` rows: writes only score and reasons; never deletes a row someone has worked on (relevance, decision, note, owner); drops untouched rows that stop matching. Runs after every ingest (daily job and `--run-once`), after `--seed-customers`, and after every triage action.
- Customers page lists each customer's candidates with score and reasons. Verified in Edge: confirming a Reporting item in triage adds it to Brightline's list immediately; reopening removes it.

**Next steps:**
1. Tune against real profiles (Phase 0) — every provisional weight above is a guess until then.
2. A source for `ObjectsTouched` (Phase 1) to switch on the +40 object rule.
3. Customers page is read-only; screening (`Relevance`) and decisions belong to the Phase 3 customer board.

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

✅ **Customer decision board built (2026-10-06)** — `/customers/{id}`, linked from each customer card. Five columns (Undecided, Adopt, Test first, Ignore, Blocked) over that customer's candidates; each card shows score, change type, version/GA, "why it matched", and takes a customer-specific note (`CustomerItem.OverrideNote`). Deciding stamps `DecidedAt`. A decided item stays on the board with a "no longer matches" badge if its score drops — the match engine never deletes worked-on rows. Writes via `Data.CustomerBoardService`. Verified in Edge (move, note, reload persists); test decisions rolled back afterwards. Not yet: `DecidedBy`/`Owner`/`TargetWindow` (no auth), `Relevance` screening (decision covers it for now), drag-and-drop.

✅ **Impact notes built (2026-10-07)** — one explanation per roadmap item (`ImpactNote`, schema unchanged), reused for every customer it affects.

- **Impact notes** in the top bar lists every item on at least one customer's board (a candidate or already decided), urgent first, then by number of customers affected, with its note status: *No note*, *Draft*, *Reviewed*, or *Changed since review*.
- The editor (`/impact-notes/{itemId}`, also linked from each triage row and board card) shows Microsoft's facts and description beside four fields — Summary, Why it matters, Action required, Effort (None/S/M/L) and Risk (Low/Medium/High) — and a side panel of affected customers with their score, decision and customer note.
- **Save draft** vs **Mark reviewed** (`ReviewedAt`): a reviewed note needs a summary; editing a reviewed note makes it a draft again. Phase 4 should publish reviewed notes only.
- **Changed since review**: any `ChangeEvent` on the item after `ReviewedAt` flags the note and lists the changes in the editor, until someone reviews it again.
- Writes via `Data.ImpactNoteService`. Verified in Edge: refused review without summary, draft, review, reload keeps every field, list shows Reviewed. The test note was rolled back.
- Not yet: `Author` (no auth); AI-drafted copy (doc principle #3 — the editor is where it would plug in, never the ingest path).

**Next steps:**
1. Regenerate the design mockup with English labels and sample copy (layouts unchanged).
2. ~~Triage screen~~ — done (above). Reopen semantics to revisit once real triage happens: reopening keeps the human values in place (the classifiers only take over again when Microsoft next modifies the item).
3. ~~Impact note editor~~ — done (above). Next: optionally an AI-drafted first version of Summary/Why/Action from Microsoft's description, always landing as a draft for a person to review.
4. ~~Per-customer decision board~~ — done (above).
5. Needs Phase 2 (match engine) and real `Customer` data (Phase 0) before this is meaningfully usable end to end.

---

## Phase 4 — Publish

**Goal (doc §9):** freeze snapshot → Word (template) / Markdown / CSV export.

🔄 **Built (2026-10-07), awaiting the user's check of the document. Word output uses a calm blue placeholder design — the company letterhead template wasn't available.**

- **Release plan** page per customer (`/customers/{id}/release-plan`, linked from the customer card and board): period, what's in the plan with each item's note status, **Download preview (.docx)**, **Publish version N**, and published versions with downloads.
- **What goes in** (`Publishing/PlanModel.cs`, `PlanRules`): deprecations/retirements/breaking changes are *Mandatory* even if undecided (not deciding doesn't make a removal go away); Adopt and Test first are *Recommended*; Blocked is *For information*; Ignored and undiscussed (Undecided, non-urgent) items are left out.
- **Publishing gates**: refused for sample customers (preview only, with a "SAMPLE CUSTOMER — NOT FOR DISTRIBUTION" banner); refused while any included item lacks a reviewed impact note, or one changed since review (the page lists them with links); refused when the plan is empty.
- **Freeze**: publishing copies every line's text into `ReleasePlanLine` (migration `AddReleasePlanLineDetail` adds why-it-matters, customer note, target version, risk), numbers it 1.0, 1.1 …, marks the previous version Superseded, and keeps the .docx under `Publishing:OutputFolder` (`release-plans/`, git-ignored). Downloads serve the kept file.
- **Document** (`Publishing/WordPlanWriter.cs`, Open XML SDK): A4, Calibri, navy/blue headings; title block; Summary with an overview table; Mandatory changes / Recommended new functionality / For information, each item with its facts, summary, why it matters, what to do and the customer note; Next steps table; Changes since previous version. Footer with page X of Y.
- Tests run the Open XML validator on every generated document — it caught two element-order errors that could have made Word report the file as damaged.

**Explicit design goal for this phase (added 2026-08-30):** the Word document is not a nice-to-have export alongside the portal — it's the fallback deliverable for any customer who never touches the portal at all. On company letterhead, built entirely from the same curated data (`ImpactNote` + `CustomerItem` decisions filtered to that customer), it has to stand alone as a complete, credible release plan with zero portal access assumed. This was always implicit in the doc's §1 framing ("a deliverable customers relied on you for") — worth stating outright now so the Word template work in this phase doesn't get treated as secondary to the portal.

**Next steps:**
1. ~~Freeze logic~~ and ~~Word export~~ — built (above); swap the placeholder design for the letterhead template once it is available.
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
