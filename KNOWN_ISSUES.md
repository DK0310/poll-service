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
| [ISSUE-002](#issue-002--my-polls-navigation-back-from-analytics-lands-on-results) | "Back" from Analytics lands on Results, not My Polls | Frontend · navigation | Low | **Fixed** |
| [ISSUE-003](#issue-003--vote-page-has-no-in-page-back-return-control) | Vote page has no in-page back / return control | Frontend · navigation | Low | **Fixed** |
| [ISSUE-004](#issue-004--landing-page-renders-dark-in-dark-mode-low-contrast) | Landing page renders dark in dark mode (low contrast) | Frontend · theming | Low | **Fixed** |
| [ISSUE-005](#issue-005--dark-mode-form-fields-show-invisible-text-light-field--white-text) | Dark mode: form fields show invisible text (light field + white text) | Frontend · theming | Medium | **Fixed** |
| [ISSUE-006](#issue-006--full-stack-cold-start-breaks-the-warm-up-ping-register--login--my-polls-fail) | Full-stack cold start breaks the warm-up ping (register / login / My Polls fail) | Frontend + Gateway · Render free tier | Medium | Open |

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
| **Status** | **Fixed** (2026-06-11) |
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

### Root cause (confirmed by code review)
Two things, only one of which was still a real defect:
1. **Card buttons** — already resolved by the Mentimeter restyle: `PollCard` now shows distinct,
   labelled actions (**Results / Analytics / Vote**, each with an icon), so "which one did I click"
   is no longer ambiguous.
2. **The "Back" defect** — the Analytics page's in-page **"Back to live results"** link was
   **hardcoded** to `/poll/{code}/results` regardless of origin. Reaching Analytics from **My Polls**
   (a direct `PollCard` link, so history was `My Polls → Analytics`) and then clicking that in-page
   "back" pushed you to **Results** — a page you'd never visited — and a second Back was needed to
   return to My Polls.

### Fix (2026-06-11)
The Analytics "back" link now returns to **where you came from**. `PollCard`'s Analytics link tags its
origin (`state={{ from: 'my-polls' }}`), and `AnalyticsPage` reads `location.state` to pick the target:
from My Polls → **"Back to my polls"** → `/my-polls`; otherwise (from the Results page, or a direct
URL) it keeps the default **"Back to live results"** → `/poll/{code}/results`. Files:
[`PollCard.tsx`](frontend/src/components/PollCard.tsx), [`AnalyticsPage.tsx`](frontend/src/pages/AnalyticsPage.tsx).
**Verified:** `npm run lint` clean + `vite build` green. Frontend-only; no backend/route/ARCHITECTURE change.

---

## ISSUE-003 — Vote page has no in-page back / return control

| Field | Value |
|---|---|
| **Area** | Frontend — VotePage (`/poll/:code`) navigation |
| **Severity** | Low (navigation convenience; workaround: browser/nav controls) |
| **Status** | **Fixed** (2026-06-11) |
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

### Fix (2026-06-11)
Added an in-page **"← Back to home"** link at the top of the vote page, with a **fixed `/` target**
(more predictable than history-based `navigate(-1)` for a voter who arrived via a share link/QR and has
no in-app history). Reuses the `btn-outline` look via a small new `.page-back` style (mirrors the
existing `analytics-back` affordance, top-positioned). Files:
[`VotePage.tsx`](frontend/src/pages/VotePage.tsx), [`index.css`](frontend/src/index.css) (`.page-back`).
Results-page affordance left out of scope (not requested). **Verified:** `npm run lint` clean +
`vite build` green. Frontend-only; no ARCHITECTURE change.

---

## ISSUE-004 — Landing page renders dark in dark mode (low contrast)

| Field | Value |
|---|---|
| **Area** | Frontend — theming (dark mode) / landing page (`/`) |
| **Severity** | Low (cosmetic; dark mode only; affects the marketing landing) |
| **Status** | **Fixed** (2026-06-11) |
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

### Root cause (confirmed by code review)
`main.lp` (`.lp main, main.lp`) is `display:block; padding:0` with **no `background`** — it's
transparent. The `:root[data-theme='dark'] main.lp` block re-asserted the light **tokens** (`--bg`,
`--ink`, …) so content *colors* were right, but with nothing painting the surface, the dark `body`
(`background: var(--bg)`, dark) showed through behind it. The hero paints its own light gradient and
`.lp-section--alt` is `#fff`, but plain `.lp-section`s are transparent → dark backdrop, navy-on-dark.

### Fix (2026-06-11)
Added `background: var(--bg);` to `:root[data-theme='dark'] main.lp` — since `--bg` is re-asserted to
`#f4f4f7` in that same block, the whole landing surface now paints light and the transparent sections
read against it. File: [`index.css`](frontend/src/index.css). **Scope decision:** the shared dark
nav/header sitting above the light landing is left as-is (a dark nav over light content is a normal,
intentional-looking pattern — not the reported readability defect). **Verified:** `npm run lint` clean
+ `vite build` green. Matches the documented "landing stays light by design" decision in
[ARCHITECTURE.md](ARCHITECTURE.md) (no ARCHITECTURE change needed).

---

## ISSUE-005 — Dark mode: form fields show invisible text (light field + white text)

| Field | Value |
|---|---|
| **Area** | Frontend — theming (dark mode) / form inputs |
| **Severity** | Medium (input is unreadable in dark mode; workaround: use light mode) |
| **Status** | **Fixed** (2026-06-11) |
| **Reported** | 2026-06-11 |
| **Environment** | Local dev (frontend) — Phase 17 dark mode |

### Summary
With **dark mode** on, typing into some form fields shows **nothing** — the text you type is white
**and** the field background stays light, so it's white-on-white (invisible). The placeholder also
reads as near-white/very faint on the same light field.

### Steps to reproduce
1. Toggle **dark mode** (sun/moon in the nav).
2. Go to **Login** or **Register** (or an OpenText poll's answer box) and type into a field.

### Expected
- In dark mode the field has a **dark background** and **light text** (or any combination with real
  contrast), and the placeholder is clearly dimmer than typed text but still legible.

### Actual
- Typed text is white on a **light/default field background** → invisible. Placeholder is a faint
  dusty-rose that looks near-white on the light field.

### Notes / areas to investigate (when fixing — not yet confirmed)
- The Phase 17 dark block only remaps fields that carry the **`.input` class**
  (`:root[data-theme='dark'] .input { background: var(--surface); }` in [index.css](frontend/src/index.css)),
  and `.input` text is `color: var(--ink)` (white in dark). That pairing is fine.
- **The broken fields don't use `.input`:**
  - [LoginPage.tsx](frontend/src/pages/LoginPage.tsx) / [RegisterPage.tsx](frontend/src/pages/RegisterPage.tsx)
    render **bare `<input>`** (no class) — so they get **no dark background override**; the browser's
    default light field background stays, while text inherits the dark-mode white `--ink` → white-on-white.
  - [VoteForm.tsx](frontend/src/components/VoteForm.tsx) OpenText box uses **`className="text-answer"`**,
    also outside the `.input` dark override.
- `::placeholder { color: #c89aa6; }` (line ~307) is a **fixed** dusty-rose, never themed — on a light
  field it reads near-white.
- Likely fix: theme the **element selectors** (`input, textarea, select`) under
  `:root[data-theme='dark']` (dark `background` + `var(--ink)` text), not just `.input`; and add a
  dark `::placeholder` color (e.g. `var(--muted)`). Scope to **app pages** — keep landing fields light
  (consistent with ISSUE-004's landing-stays-light intent).

### Fix (2026-06-11)
Added dark-mode rules for the **element selectors** (`input`, `textarea`, `select`) — not just
`.input` — setting `background: var(--surface); color: var(--ink);`, plus a themed
`::placeholder { color: var(--muted); }`. Using **tokens** means no extra scoping is needed: inside the
light landing (`main.lp` re-asserts light tokens) any field resolves to a light surface + navy text
automatically, so the landing-stays-light intent holds for free. File:
[`index.css`](frontend/src/index.css). **Verified:** `npm run lint` clean + `vite build` green —
Login/Register inputs and the OpenText `.text-answer` box are now legible in dark mode. Frontend-only;
no ARCHITECTURE change.

---

## ISSUE-006 — Full-stack cold start breaks the warm-up ping (register / login / My Polls fail)

| Field | Value |
|---|---|
| **Area** | Frontend (`warmup.ts`) + Gateway — Render free tier, multi-service cold start |
| **Severity** | Medium (blocks register/login/My Polls until manually worked around; only hits after a long full-stack idle period) |
| **Status** | Open |
| **Reported** | 2026-07-13 |
| **Environment** | Production (Render, 5 free-tier services: frontend, gateway, identity-api, poll-api, vote-api) |

### Summary
After the whole stack (all 5 Render services **and** the Azure SQL databases) sat idle long enough for
everything to go cold simultaneously, visiting the frontend and waiting was **not** enough to recover —
unlike the normal case where a single cold service wakes fine within the page-load wait. Register calls
returned `429`, and after registration/login succeeded, **My Polls** showed "Failed to load your polls".

### Steps to reproduce
1. Let the entire stack go idle long enough that the Azure SQL databases auto-pause (a much longer
   window than the ~15 min Render free-tier service sleep) — so frontend, gateway, identity-api,
   poll-api, and vote-api are *all* asleep at once, not just one.
2. Visit the frontend and immediately try to **Register** or **Log in**.
3. Observe `429 Too Many Requests` on `/api/auth/register` (and on the `warmup` pings).
4. After eventually registering/logging in, open **My Polls**.

### Expected
- `warmBackend()` in [`warmup.ts`](frontend/src/api/warmup.ts) fires on page load and the user's first
  real action should "just work" once the page has been open a little while, per the original warm-up
  design (commit `d50290b`).

### Actual
- `POST /api/auth/register` returned `429` even after waiting past the normal ~50s cold-boot window.
- Hitting each backend directly confirmed the real problem: **identity-api**, **gateway**, and
  **poll-api** were each independently cold and had to be woken one at a time (direct requests to
  `pollbuilder-identity-api-latest`, `pollbuilder-gateway-latest`, and
  `pollbuilder-poll-api-latest-1` — note the `-1` suffix on that last one — each showed Render's own
  "Application loading" cold-start page).
- Once identity-api and gateway were warm, register/login worked. Once poll-api and vote-api were also
  warm, **My Polls** loaded correctly.

### Notes / areas to investigate (when fixing — not yet confirmed)
- Root cause: `warmBackend()` fires 3 fire-and-forget pings (`/auth/warmup`, `/polls/warmup`,
  `/polls/warmup/results`) straight through the frontend's nginx proxy to the **gateway**
  ([`nginx.conf`](frontend/nginx.conf)). If the **gateway itself** is also cold, those pings can't reach
  identity-api/poll-api/vote-api until the gateway finishes its own ~50s boot first — so instead of every
  service waking in parallel (~50s total), the gateway's boot is serialized in front of each downstream
  service's boot (100s+), while Render's edge returns `429`s on the retries piling up in between.
- This only surfaces when **every** layer is cold at once, which requires an idle window long enough for
  the Azure SQL DB to auto-pause too — much rarer than a single service napping after 15 minutes, hence
  not caught earlier.
- Possible fix direction: have `warmBackend()` wake the gateway first and confirm it's live (poll until a
  non-429/503 response) before firing the downstream service pings, or expose one combined
  gateway-side health-check that transitively warms its clusters. Alternatively, moving off the free tier
  removes the sleep behavior entirely.

---

## Design notes — multi-question survey refactor (2026-07-13)

Not defects — assumptions worth recording after the Poll → Question → Option refactor (the survey
model where a poll has many questions, each with its own type and options):

- **No survey-edit endpoint → `QuestionId` is immutable.** A poll's questions are set at creation and
  never edited or re-ordered (there is no update endpoint). The Vote API stores `Vote.QuestionId` and
  trusts that the id it learned from the Poll API stays valid for the poll's lifetime. If a future
  edit/replace-questions feature is added, it must not recycle or renumber question ids under existing
  votes (delete-and-recreate the poll instead, or migrate votes).
- **Batch voting requires an answer to every question.** A submission must answer **all** of the poll's
  questions or it is rejected (`"Please answer every question"`); there is no partial/save-progress
  submission. This keeps `TotalVoters` equal to each question's vote count and matches the
  one-submission-per-voter dedup. Relaxing to partial answers would require rethinking the
  `TotalVoters` metric and the `(PollCode, QuestionId, VoterToken)` uniqueness.
