# Known Issues

Tracked defects in Poll & Survey Builder. Each issue has a stable ID, a severity, and a status.
**Do not fix an issue until its status is moved to `Ready to fix`** — entries here are for triage and
record-keeping first.

**Severity:** `Critical` (data loss / security / app unusable) · `High` (core feature broken) ·
`Medium` (feature degraded, workaround exists) · `Low` (cosmetic / minor).
**Status:** `Open` (confirmed, not started) · `Investigating` · `Ready to fix` · `Fixed`.

| ID | Title | Area | Severity | Status |
|---|---|---|:--:|:--:|
| [ISSUE-001](#issue-001--expired-polls-do-not-auto-close) | Expired polls do not auto-close | Poll API · expiry | High | **Fixed** |
| [ISSUE-002](#issue-002--my-polls-navigation-back-from-analytics-lands-on-results) | "Back" from Analytics lands on Results, not My Polls | Frontend · navigation | Low | Open |
| [ISSUE-003](#issue-003--vote-page-has-no-in-page-back-return-control) | Vote page has no in-page back / return control | Frontend · navigation | Low | Open |
| [ISSUE-004](#issue-004--landing-page-renders-dark-in-dark-mode-low-contrast) | Landing page renders dark in dark mode (low contrast) | Frontend · theming | Low | Open |

---

## ISSUE-001 — Expired polls do not auto-close

| Field | Value |
|---|---|
| **Area** | Poll API — poll expiry / auto-close |
| **Severity** | High (a Merit feature — expiry auto-close — does not work) |
| **Status** | **Fixed** (2026-06-10) |
| **Reported** | 2026-06-07 |
| **Environment** | Production (Render) — *needs confirming against local docker-compose* |

### Summary
A poll created with an expiry time does not close once that time passes. After the expiry has
elapsed the poll still behaves as open.

### Steps to reproduce
1. Log in and create a poll, setting the expiry to ~1 hour.
2. Wait for more than 1 hour to pass.
3. Open the poll's vote/results page.

### Expected
- After `ExpiresAt`, the poll is treated as **closed**: voting is rejected and the results page shows
  the "closed — final results" state.

### Actual
- After 1 hour the poll is **not closed** — it still presents as open.

### Notes / areas to investigate (when fixing — not yet confirmed)
- The background `PollCleanupService` is what flips an expired poll's persisted `Status` to `Closed`.
  It only runs **while the Poll API process is alive** — on a free-tier host the instance sleeps when
  idle, so the periodic sweep may never fire for a poll that expires during an idle window.
- Check whether "closed" is driven by the **persisted `Status`** or by the **computed `IsActive`/`IsExpired`**
  (which derive from `ExpiresAt` and should reflect expiry immediately, regardless of the cleanup service).
- Verify **UTC vs local time** handling of `ExpiresAt` end-to-end (frontend expiry selection → stored value → comparison),
  in case the stored instant is off by the local-time offset.
- Confirm the `PollCleanup:IntervalSeconds` configured value in the affected environment.

### Root cause (confirmed by code review)
The user-facing surfaces were **already correct** — `Poll.IsActive` is computed (`!IsExpired && !IsClosed`)
and `PollResponse.IsActive` returns it, so vote-rejection (`VoteService` checks `!poll.IsActive`), the
results "closed" banner, the vote page, and the My Polls pill all reflect expiry **the instant
`ExpiresAt` passes**, independent of the sweep. The genuine gap was the **persisted `Status` column**:
flipping it to `Closed` relied solely on `PollCleanupService`, which only runs while the Poll API
process is alive — on a free-tier host that sleeps when idle, the sweep may never fire, so `Status`
stayed `Open` (and any future code/report reading `Status` would be wrong). *(No timezone bug: expiry
is set as `DateTime.UtcNow.AddHours` and compared against `DateTime.UtcNow`.)*

### Fix (2026-06-10)
**Lazy close-on-read** added to `PollService.GetByCodeAsync`: when a fetched poll is past `ExpiresAt`
but still `Open`, it is closed and persisted *then* — so auto-close no longer depends on the background
sweep being awake (the first view self-heals `Status`). `PollCleanupService` stays as a proactive
optimization. Files: [`PollService.cs`](services/poll-api/PollApi/Services/PollService.cs);
tests `GetByCode_LazilyClosesExpiredOpenPoll` + `GetByCode_DoesNotPersist_WhenNotExpired` in
[`PollServiceTests.cs`](services/poll-api/PollApi.Tests/Services/PollServiceTests.cs).
**Verified:** `dotnet test` poll-api green — **35 passed** (33 → 35). Ships on the next deploy to `main`.

---

## ISSUE-002 — My Polls navigation: "Back" from Analytics lands on Results

| Field | Value |
|---|---|
| **Area** | Frontend — My Polls / PollCard / page navigation |
| **Severity** | Low (navigation annoyance; workaround: press Back twice) |
| **Status** | Open |
| **Reported** | 2026-06-07 |
| **Environment** | Production (Render) / frontend |

### Summary
Two related problems on the **My Polls** dashboard:
1. The action buttons on each poll card aren't clear enough about where they lead.
2. After opening **Analytics** for a poll, pressing **Back** returns to the **Live Results** page
   instead of **My Polls** — so the user has to press Back **twice** to get back to the My Polls list.

### Steps to reproduce
1. Log in → go to **My Polls**.
2. On a poll, click the button that opens **Analytics**.
3. Press the browser/app **Back** control once.

### Expected
- Back from Analytics returns directly to **My Polls** (one step).
- The card buttons clearly say where each one goes (e.g. View / Results / Analytics).

### Actual
- Back from Analytics lands on **Live Results**; a second Back is needed to reach My Polls.
- The buttons aren't distinct enough, so it's unclear which one was taken.

### Notes / areas to investigate (when fixing — not yet confirmed)
- Likely the My Polls card routes through **Results** to reach **Analytics** (history becomes
  `My Polls → Results → Analytics`), so one Back pops to Results. Check `PollCard` links and the
  Analytics page's back/return target.
- Fix directions to weigh: link **Analytics directly** from the My Polls card, and/or make the
  Analytics "back" return to its origin (My Polls), and **clarify/relabel** the card's buttons.

### Fix
Pending — **do not change code yet** (awaiting go-ahead).

---

## ISSUE-003 — Vote page has no in-page back / return control

| Field | Value |
|---|---|
| **Area** | Frontend — VotePage (`/poll/:code`) navigation |
| **Severity** | Low (navigation convenience; workaround: browser/nav controls) |
| **Status** | Open |
| **Reported** | 2026-06-08 |
| **Environment** | Production (Render) / frontend |

### Summary
The vote page has no on-page "back" / "return" affordance. To leave it the user must click the
browser's back arrow (or the brand/nav links in the header) — there is no in-content button to go
home / back to the previous screen.

### Steps to reproduce
1. Open a poll's vote page (`/poll/{code}`).
2. Look for a way to return without using the browser/nav-bar controls.

### Expected
- A clear in-page control to go back (e.g. a "← Back" / "Home" link near the top of the card),
  consistent with how the Analytics page has its `analytics-back` link.

### Actual
- No in-page back/return button; leaving the page depends on the browser arrow or the header brand link.

### Notes / areas to investigate (when fixing — not yet confirmed)
- `VotePage.tsx` renders the poll card with no back link; consider a small `← Back` / `Home` link
  (reuse the existing `analytics-back`-style affordance for consistency).
- Decide the target: browser back (`navigate(-1)`) vs. a fixed destination (`/` home). A fixed Home
  link is more predictable than history-based back.
- Consider the same affordance on the Results page for consistency (not requested — confirm scope).

### Fix
Pending — **do not change code yet** (awaiting go-ahead).

---

## ISSUE-004 — Landing page renders dark in dark mode (low contrast)

| Field | Value |
|---|---|
| **Area** | Frontend — theming (dark mode) / landing page (`/`) |
| **Severity** | Low (cosmetic; dark mode only; affects the marketing landing) |
| **Status** | Open |
| **Reported** | 2026-06-11 |
| **Environment** | Local dev (frontend) — Phase 17 dark mode |

### Summary
With **dark mode** on (Phase 17), the **landing page** (`/`) is meant to stay light by design, but it
comes out **dark and low-contrast** — the section areas (`.lp-section`, hero) look covered in dark and
content is **barely readable**. The intent was: app pages dark, the marketing landing always light.

### Steps to reproduce
1. Open the app and toggle **dark mode** (sun/moon in the nav).
2. Navigate to the landing page `/`.

### Expected
- The landing renders in its **light** marketing design regardless of theme (white/`#f4f4f7`
  background, navy text, white feature/preview cards), consistent and readable.

### Actual
- The landing background is **dark** while text/cards intended for a light background show through —
  e.g. navy hero copy on a dark backdrop (very low contrast); white cards float on a dark page.

### Notes / areas to investigate (when fixing — not yet confirmed)
- Phase 17 keeps the landing light by **re-asserting light tokens on `main.lp`**
  (`:root[data-theme='dark'] main.lp { --bg/--surface/--ink/... }` in [index.css](frontend/src/index.css)).
  But `main.lp` itself has **no `background` paint** — it's transparent, so the **dark `body`**
  (`background: var(--bg)`, dark in dark mode) **shows through** behind the landing. The re-asserted
  vars fix text/token colors *inside* containers but not the page backdrop.
- Likely fix: give the landing surface an explicit light paint, e.g.
  `:root[data-theme='dark'] main.lp { background: var(--bg); }` (with the already-reasserted light `--bg`),
  and/or verify the shared **nav/header** contrast over the light landing. Hero uses a
  `linear-gradient(..., var(--bg))` — confirm it reads light once `--bg` is light *and* painted.
- Scope question to confirm when fixing: should the **nav/header** also switch to a light treatment on
  the landing, or is a dark nav above the light landing acceptable?

### Fix
Pending — **do not change code yet** (awaiting go-ahead).
