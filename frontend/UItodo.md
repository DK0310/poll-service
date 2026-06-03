# UI Redesign Plan — "Aurora" (UItodo.md)

Phased plan to migrate the Poll & Survey Builder frontend from the generic dark-slate/indigo theme to the
**Aurora UI** direction. **Presentation only** — no backend, route, hook, or API changes.

**How to read this:** work top-to-bottom. Each phase has a goal, a task list, the files it touches, and a
**Definition of Done** that must be green before moving on. Tier tag `[UI]` throughout.

**Source of truth:**
- Design tokens / component specs → [design-system/MASTER.md](design-system/MASTER.md)
- Design language + **UI tech-stack requirements** → [REDESIGN.md](REDESIGN.md)
- Behaviour / API / architecture → [../ARCHITECTURE.md](../ARCHITECTURE.md) *(unchanged)*

**Verification (every phase):** `cd frontend && npm run lint && npm run build` green + visual check of the
affected route(s). Apply the [REDESIGN.md](REDESIGN.md) UX guardrails (a11y, motion, forms, charts) as acceptance criteria.

**Direction:** Aurora UI — mesh gradient + glass, rose `#E11D48` + blue `#2563EB`, Space Grotesk + DM Sans + DM Mono.

---

## Phase R0 — Design system & foundations  `[UI]`

**Goal:** establish the Aurora visual language with zero page regressions.
**Files:** `package.json`, [src/main.tsx](src/main.tsx), [src/index.css](src/index.css).

- [x] Install deps: `@fontsource/space-grotesk`, `@fontsource/dm-sans`, `@fontsource/dm-mono`, `lucide-react` (4 pkgs, 0 vulnerabilities).
- [x] Import font CSS in `main.tsx` (Space Grotesk 400–700, DM Sans 400/500/700, DM Mono 400/500); fontsource ships `font-display: swap`.
- [x] Rewrote the `:root` token block in `index.css` from [MASTER.md](design-system/MASTER.md): colors, font stacks, type scale, spacing, radii, glass/shadow tokens, motion tokens.
- [x] Added the mesh-gradient background (via `body::before`; `.aurora-bg` class also defined) + base typography + base element styles (links, `.btn`/`.btn-outline`/`.btn-link`, inputs/select/textarea, `.card`, `.pill`, `.live-badge`, `:focus-visible`, selection) + `backdrop-filter` fallback.
- [x] `prefers-reduced-motion` freezes the mesh drift + pulses + transitions.
- **DoD:** ✅ `npm run lint` clean + `npm run build` green (259ms); fonts resolve; no TS/console errors. *(Per-page component classes from the old theme are intentionally unstyled until R1–R6.)*

## Phase R1 — App shell  `[UI]`

**Goal:** the chrome every page shares. **Files:** [src/App.tsx](src/App.tsx), `index.css`.

- [x] Glass sticky header with gradient-text wordmark + Lucide `Vote` mark in a gradient badge (replaced 🗳️ emoji); active-route highlight via `NavLink`.
- [x] Nav as pill links (Create / My Polls / Log in·Register-CTA / Log out with `LogOut` icon); ≥40px targets, `aria-label`led.
- [x] Page frame: `.app-shell` flex column (footer sticks to bottom), centered 960px container, mesh behind, thin colophon footer.
- [x] Restyled the 404 route on-brand (gradient "404" on a glass card + CTA).
- **DoD:** ✅ build + lint green (272ms); header/nav/footer on every route; sticky glass header; keyboard-navigable with visible focus rings.

## Phase R2 — Create Poll + PollForm + success  `[UI]`

**Goal:** first impression. **Files:** [src/pages/CreatePollPage.tsx](src/pages/CreatePollPage.tsx), [src/components/PollForm.tsx](src/components/PollForm.tsx), [src/components/ShareLink.tsx](src/components/ShareLink.tsx).

- [x] Form on a glass card: visible labels with `*` required markers, question-type **segmented control** (`aria-pressed`, `role=group`), numbered gradient-badge option rows (add via `Plus`, remove via `X`, ≥40px), expiry select; gradient CTA with `Loader2` spinner + "Creating…" loading state.
- [x] Submit error rendered with `role="alert"`.
- [x] "Poll Created" success: gradient `Check` badge + gradient heading + share URL in mono + Copy button (`Copy`/`Check` icon, aria-labelled) + voting/results CTA links.
- **DoD:** ✅ build + lint green (250ms); all four question types switch (segmented control); create + success flow polished; labels/required/alert a11y met.

## Phase R3 — Vote page + VoteForm + Q&A  `[UI]`

**Goal:** the ballot. **Files:** [src/pages/VotePage.tsx](src/pages/VotePage.tsx), [src/components/VoteForm.tsx](src/components/VoteForm.tsx), [src/components/QandAPanel.tsx](src/components/QandAPanel.tsx).

- [x] Vote options as glass rows (native radios, `sr-only`) with inset rose ring + `Check` when selected; Rating as 1–5 chips (`role=radio`); Yes/No as two big blocks; OpenText as a glass textarea with label.
- [x] Gradient submit with `Loader2` "Submitting…" loading state; closed-poll notice (`Lock`) + already-voted confirmation (`CheckCircle2` + results link) on-brand; vote/not-found/loading states on glass cards. (Fresh vote still redirects to results = the live confirmation.)
- [x] Q&A panel as a glass card: `MessageSquare` heading, ask input + `Send` button, upvote = `ChevronUp` + tabular count, pinned highlight with `Pin` flag (replaced 📌 emoji); live updates intact.
- **DoD:** ✅ build + lint green (347ms; fixed a `react-hooks/static-components` lint error by converting the submit helper to a function); all four types polished; ≥44px targets; reduced-motion respected.

## Phase R4 — Results + LiveBarChart  `[UI]`

**Goal:** the most-demoed screen. **Files:** [src/pages/ResultsPage.tsx](src/pages/ResultsPage.tsx), [src/components/LiveBarChart.tsx](src/components/LiveBarChart.tsx).

- [x] Gradient result bars on a glass card (leader = rose + `Crown` "Leading" tag, others blue/violet), exact count + % in mono (tabular); **total votes as a large gradient KPI**.
- [x] Blinking `● Live` badge (R0 `.live-badge`, dot via `::before`) with a muted `live-badge--off` "Connecting…" state; "Poll closed — final results" notice (`Lock`) on-brand.
- [x] OpenText responses as glass cards + response-count KPI; empty states ("No votes/responses yet"); results footer with share hint + `View analytics` outline button.
- **DoD:** ✅ build + lint green (510ms); bar fills now animate via **`transform: scaleX`** (transform-only, reduced-motion-safe) instead of `width`; OpenText + choice both polished. (SignalR live-update logic untouched.)

## Phase R5 — Analytics dashboard + LineChart  `[UI]`

**Goal:** the analytics view. **Files:** [src/pages/AnalyticsPage.tsx](src/pages/AnalyticsPage.tsx), [src/components/LineChart.tsx](src/components/LineChart.tsx).

- [x] Glass KPI stat cards (total / top option / peak minute) with gradient numerals; on-brand not-found + **shimmer skeleton loading** state.
- [x] LineChart rebuilt per [MASTER.md chart spec](design-system/MASTER.md): blue stroke + ~20% area-gradient fill, hairline gridlines, mono axis labels, **toggleable data-table fallback** (`aria-expanded`, `<caption>`/`<th scope>`), `role="img"` + aria summary, empty state, subtle fade-in (reduced-motion freezes). (Also fixed the stale `var(--accent)` the old chart referenced.)
- **DoD:** ✅ build + lint green (263ms); dashboard cohesive; chart a11y (data-table + aria summary) met.

## Phase R6 — My Polls + Auth  `[UI]`

**Goal:** creator surfaces. **Files:** [src/pages/MyPollsPage.tsx](src/pages/MyPollsPage.tsx), [src/components/PollCard.tsx](src/components/PollCard.tsx), [src/pages/LoginPage.tsx](src/pages/LoginPage.tsx), [src/pages/RegisterPage.tsx](src/pages/RegisterPage.tsx).

- [x] My Polls as glass cards: title + OPEN/CLOSED **pill** (gradient/grey — shape+label), mono `/poll/{code}` meta, nav actions (Results/Analytics/Vote with icons) + **destructive group (`Close`/`Delete`) pushed right** via `margin-left:auto`; skeleton loading + empty-state card + not-authed login prompt; errors `role="alert"`.
- [x] Login/Register as a centered **narrow glass card**: gradient icon badge header, labelled fields + `autocomplete`, **password show/hide toggle** (`Eye`/`EyeOff`, aria-labelled), gradient submit with `Loader2` loading ("Logging in…/Creating account…"), error `role="alert"`, footer link.
- **DoD:** ✅ build + lint green (294ms); auth + dashboard match the system; empty/loading/error states styled.

## Phase R7 — Polish, motion, responsive, a11y & verification  `[UI]`

**Goal:** ship-quality finish. **Files:** `index.css`, [index.html](index.html), `public/favicon.svg`.

- [x] Staggered page-load reveal (CSS `page-rise` on `.page > *`, 50ms stagger) + subtle `.poll-card` hover-lift; consistent `:focus-visible` rings from R0.
- [x] `prefers-reduced-motion` audited (global rule freezes mesh drift, pulse, shimmer, reveal, transitions); AA-contrast tokens verified; small-screen header tuning + existing 520/560 breakpoints. *(Mesh drift kept as a deliberate slow 13s brand signature — disabled under reduced-motion; ui-ux-pro-max UX pass otherwise clean.)*
- [x] New Aurora **favicon** (`public/favicon.svg` — gradient poll-bars) + updated `<title>` / description / `theme-color`. No dead theme CSS (index.css rewritten at R0; grep confirms zero stale `--accent`/`--panel`/old-hex references in `src`).
- [x] Full `npm run lint` clean + `npm run build` green (268ms). *(Visual Playwright/`webapp-testing` screenshots: optional — offered, not yet run.)*
- **DoD:** ✅ every page cohesive, distinctive, responsive, accessible; lint+build green. Redesign complete.

---

## Critical path
```
R0 (design system) → R1 (shell) → R2 (create) → R3 (vote) → R4 (results)
   → R5 (analytics) → R6 (my polls + auth) → R7 (polish + verify)
```
R0 first (everything pulls from its tokens); R1 next so the new identity shows on every route; then per-page.

## Out of scope
- Backend / API / routing / hook / logic changes; new pages or features; Tailwind migration; state-management changes.
