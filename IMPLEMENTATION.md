# BC Release Plan Portal — Implementation Plan

Tracks progress against the phases in the design doc (§11 Build plan). Updated as work lands.

Legend: ✅ Done · 🔄 In progress / partial · ⬜ Not started

---

## Phase 0 — Spike

**Goal (doc):** confirm roadmap RSS/CSV shape for BC items, confirm MCP responses, hand-build 2 customer profiles.

🔄 **Partial.**

- ✅ Confirmed live, against the real server (2026-08-30): `https://www.microsoft.com/releasecommunications/mcp` works exactly as documented — JSON-RPC over a single-POST "streamable HTTP" call, 4 read-only tools (`get_recent_m365_roadmaps`, `get_m365_roadmap_by_id`, `get_recent_azure_updates`, `get_azure_update_by_id`), no auth.
- ⚠️ **Finding that changes scope:** the dataset behind that MCP server is Microsoft 365 + Azure only (1775 items, 36 products) — **Business Central is not present**, today. The doc's premise ("BC roadmap content moves onto this surface from September 2026") doesn't hold yet, or BC lands somewhere else entirely. See `RoadmapIngestOptions` doc comments for the workaround (config-driven product filters, seeded with a product that has live data so the pipeline is provably working).
- ⬜ Learn "what's new"/deprecated-features page shape — not confirmed. `learn.microsoft.com` is blocked by this environment's network policy; never reachable to inspect.
- ⬜ Hand-built customer profiles — not done. `Customer`/`CustomerProfile` schema exists (Phase 1) but no real profile data.

**Next steps:**
1. Re-check the MCP roadmap dataset periodically (or after September 2026) for a Dynamics 365 / Business Central product tag appearing.
2. From a machine that can reach `learn.microsoft.com`, capture real HTML for one "What's new and changed in update N" page and one deprecated-features page, then implement `ILearnPageSource` for real (see Phase 1 notes).
3. Hand-build 2 real `Customer` profiles once real customer data is available (not fixture data).

---

## Phase 1 — Ingest

**Goal (doc):** schema, daily job, hash/diff, ChangeEvent, Learn scrapers.

✅ **Done**, except the Learn scrapers.

Built in `src/BcReleasePlanPortal.Domain`, `src/BcReleasePlanPortal.Ingest`, `src/BcReleasePlanPortal.Data`, `src/BcReleasePlanPortal.Worker`:

- ✅ Full schema (EF Core + SQLite): `RoadmapItem`, `ChangeEvent`, `ImpactNote`, `Customer` (+ owned value objects), `CustomerItem`, `ReleasePlan`/`ReleasePlanLine`.
- ✅ MCP client (`Mcp/McpJsonRpcClient.cs`, `Mcp/RoadmapMcpClient.cs`) — real, tested against the live server.
- ✅ Rule-based `change_type` and module classifiers (`Normalization/`), each flagging low-confidence output `NeedsConfirmation` rather than asserting.
- ✅ Payload hashing + field-level diffing → `ChangeEvent` rows (`Diffing/`). Verified: catches a GA date move, stays silent when nothing changed, idempotent on re-run.
- ✅ Daily background job (`Worker/DailyIngestBackgroundService.cs`, 06:00 Europe/Amsterdam, configurable) + `dotnet run --run-once` for manual runs.
- ✅ Teams webhook alerting for urgent changes (`Alerts/`), no-op/logged when no webhook URL is configured.
- ✅ Config-driven product filters (`RoadmapIngest:ProductFilters` in `appsettings.json`) — not hardcoded to BC, ready for other Microsoft platforms per the "we sell them all" direction.
- ✅ 24 unit tests, several built on real MCP responses captured live rather than fabricated fixtures.
- ⬜ **Learn scrapers** (`Learn/UnavailableLearnPageSource.cs`) — deliberate stub. `learn.microsoft.com` unreachable from this environment, so no CSS selectors were guessed at. This is also why `RoadmapItem.TargetVersion`, `ObjectsTouched`, and `EnabledBy` stay empty/Unknown for every item ingested so far — those fields only exist on Learn pages, not the MCP/roadmap API.

**Next steps:**
1. Implement `ILearnPageSource` for real once the pages are reachable and inspected (see Phase 0).
2. Wire `TargetVersion`/`ObjectsTouched`/`EnabledBy` into `RoadmapItemNormalizer` once a Learn source exists — this is what makes the match engine's highest-value signal (§7: `objects_touched ∩ extends_objects` → +40) possible.
3. Decide the .NET 9 question: this runs on .NET 8 because 9's SDK wasn't available via this environment's package sources. Revisit on a machine/environment where it is, or explicitly commit to 8 LTS.

---

## Phase 1.5 — Basic viewer *(not in the doc's original plan; built as a checkpoint)*

✅ **Done.** `src/BcReleasePlanPortal.Web` — a minimal read-only Blazor page at `/` showing every ingested `RoadmapItem` (title, product, modules, change type, status, GA date, needs-confirmation flag), with product tabs. No auth, no editing. Exists purely so the ingest pipeline's output is visible without querying SQLite by hand; superseded by the real Triage screen in Phase 3.

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

🔄 **Partial.** A visual design mockup of all 5 screens exists (published as a Claude Design canvas artifact earlier in this project — not yet built as real UI). The read-only viewer (Phase 1.5) covers a sliver of "triage queue" but has no actions.

⬜ Not built: confirm/reject actions on triage rows, the impact note editor (Dutch fields, effort/risk selectors, matched-customers panel), the per-customer Kanban decision board.

**Next steps:**
1. Turn `BcReleasePlanPortal.Web`'s Home page into the real Triage screen: confirm/reject buttons that write `RoadmapItem.NeedsConfirmation = false` (and let a human override `ChangeType`/`Modules`).
2. Build the Impact Editor screen against `ImpactNote` (schema exists) — this is where AI-drafted Dutch copy would plug in per the doc's core principle #3 (AI enrichment, never in the ingest path).
3. Build the per-customer Kanban board against `CustomerItem.Decision` (schema exists) — needs Phase 2's match engine to have real candidates to show.
4. Needs Phase 2 (match engine) and real `Customer` data (Phase 0) before this is meaningfully usable end to end.

---

## Phase 4 — Publish

**Goal (doc §9):** freeze snapshot → Word (template) / Markdown / CSV export.

⬜ **Not started.** `ReleasePlan`/`ReleasePlanLine` schema exists (Phase 1) but nothing populates or exports it.

**Explicit design goal for this phase (added 2026-08-30):** the Word document is not a nice-to-have export alongside the portal — it's the fallback deliverable for any customer who never touches the portal at all. On company letterhead, built entirely from the same curated data (`ImpactNote` + `CustomerItem` decisions filtered to that customer), it has to stand alone as a complete, credible release plan with zero portal access assumed. This was always implicit in the doc's §1 framing ("a deliverable customers relied on you for") — worth stating outright now so the Word template work in this phase doesn't get treated as secondary to the portal.

**Next steps:**
1. "Freeze" logic: copy curated `CustomerItem` + `ImpactNote` state into `ReleasePlanLine` rows, versioned.
2. Word export via Open XML SDK against the existing `Werkinstructie_Template.docx` conventions (doc §9.1, §10), on company letterhead.
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

## Phase 7 — Profile automation *(phase 2+ in the doc)*

⬜ **Not started.** BC admin centre API for versions/update dates; AL repo parser for `extends_objects`.

## Phase 8 — ISV layer *(phase 2+ in the doc)*

⬜ **Not started.** Manual entry + ISV release-note ingestion (Continia/idyn/Anvaigo).

---

## Open items carried over from the design doc (§13)

Still open, unchanged by anything built so far:

1. Billable service line vs. included-in-support vs. sales differentiator?
2. English internal / Dutch at publish boundary — confirmed as the working convention in code so far (e.g. `ImpactNote.SummaryNl` etc.), except the Customer Board's decision-state labels, which the user explicitly asked to be Dutch in the UI.
3. F&SCM later? `RoadmapItem.Product` is already a free-text string, not a fixed enum, specifically to keep this open (and to support "all Microsoft platforms we sell," per direction given during Phase 1).
4. Who owns profile data — still unresolved, and now blocking Phase 0/2/3.
5. ISV roadmaps: same document or separate annex — still open, relevant once Phase 8 starts.
6. *(New, resolved 2026-08-30)* Support-ticket target for the Phase 6 customer portal: **TopDesk**, via API. Still open within that: API auth method, and exact caller/customer mapping for tickets created this way.
