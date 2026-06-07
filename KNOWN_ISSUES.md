# Known Issues

Tracked defects in Poll & Survey Builder. Each issue has a stable ID, a severity, and a status.
**Do not fix an issue until its status is moved to `Ready to fix`** — entries here are for triage and
record-keeping first.

**Severity:** `Critical` (data loss / security / app unusable) · `High` (core feature broken) ·
`Medium` (feature degraded, workaround exists) · `Low` (cosmetic / minor).
**Status:** `Open` (confirmed, not started) · `Investigating` · `Ready to fix` · `Fixed`.

| ID | Title | Area | Severity | Status |
|---|---|---|:--:|:--:|
| [ISSUE-001](#issue-001--expired-polls-do-not-auto-close) | Expired polls do not auto-close | Poll API · expiry | High | Open |
| [ISSUE-002](#issue-002--my-polls-navigation-back-from-analytics-lands-on-results) | "Back" from Analytics lands on Results, not My Polls | Frontend · navigation | Low | Open |

---

## ISSUE-001 — Expired polls do not auto-close

| Field | Value |
|---|---|
| **Area** | Poll API — poll expiry / auto-close |
| **Severity** | High (a Merit feature — expiry auto-close — does not work) |
| **Status** | Open |
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

### Fix
Pending — **do not change code yet** (awaiting go-ahead).

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
