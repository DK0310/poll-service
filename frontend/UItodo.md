# UI Redesign Plan — "Aurora" (UItodo.md)

> **⚠️ Superseded look:** the **Aurora** plan below (R0–R7) is **complete** and kept as history. The
> **current initiative** is the **Mentimeter restyle + landing page (M0–M3)** — see
> [§ Redesign 2 — Mentimeter](#redesign-2--mentimeter-restyle--landing-page-m0m3) at the bottom of this file.

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

---
---

# Redesign 2 — Mentimeter restyle + landing page  (M0–M3)

Re-skin the (Aurora) frontend to the **Mentimeter look** — navy `#14152d` + pink `#fd5a8b`, flat white
cards, **Inter** — and add a marketing **landing page**. The reskin is **visual-only**; the landing page is
the **one exception** that touches `.tsx` + routing (per the handoff guide §7).

**Source of truth:**
- Target theme + component rules → [uiexam/REDESIGN-GUIDE.md](uiexam/REDESIGN-GUIDE.md)
- Finished theme values (canonical) → [uiexam/css/wireframe.css](uiexam/css/wireframe.css) — same CSS
  variable + class names as the app; **only values differ** (verified against `src/index.css`).
- Per-screen wireframes → `uiexam/pages/*.html`

**Branch:** `feat/mentimeter-restyle` (never restyle on `main`). Base commit: `d50290b`.
**Verification (every phase):** `cd frontend && npm run lint && npm run build` green + visual check of the
affected route(s); keep a11y (focus rings, `aria-*`, `.sr-only`, reduced-motion) intact; **do not rename any
CSS class or variable**; **do not touch `services/`**.

> **Note:** this redesign is orthogonal to KNOWN_ISSUES ISSUE-001/002 (functional bugs) — it does not fix them.

---

## Phase M0 — Theme tokens + fonts (foundations)  `[UI]`  ✅ DONE

**Goal:** flip the design tokens from Aurora → Mentimeter with no structural/markup change.
**Files:** `package.json`, [src/main.tsx](src/main.tsx), [src/index.css](src/index.css).

- [x] Fonts → **Inter**: installed `@fontsource/inter` (0 vulns); `main.tsx` now imports Inter 400/500/600/700/800 (dropped Space Grotesk / DM Sans / DM Mono); still self-hosted.
- [x] Remapped `:root` **values** to Mentimeter (names unchanged): pink `--rose #fd5a8b`, navy `--ink #14152d`, light `--bg #f4f4f7`, flat white `--surface #fff`, `--border #e3e3ea`, `+--teal`; Inter font stacks; radii **8/12/16**; soft neutral `--shadow`/`--shadow-sm`/`--glow`. (Spacing + motion tokens kept.)
- [x] **Killed the aurora background** — removed the `body::before`/`.aurora-bg` mesh + `aurora-drift` keyframes (flat `--bg`); also dropped the now-dead `body::before`/`.aurora-bg` line from the reduced-motion block.
- [x] **Flattened `.card`** — dropped `backdrop-filter` + the `@supports` glass fallback; solid white + border + soft shadow.
- [x] `.app-header` → `rgba(255,255,255,.9)`, blur `8px` (removed its `@supports` fallback). Updated the stale file-header comment.
- **DoD:** ✅ `npm run lint` clean + `npm run build` green (701ms); flat light theme (no mesh, no glass); Inter resolves; no TS/console errors. *(Component-level rule reskin — buttons/nav/selection/pills/bars — is M1.)*

## Phase M1 — Component re-skin (the §4 rules)  `[UI]`  ✅ DONE

**Goal:** port the component-level rule changes so every existing screen matches its wireframe.
**Files:** [src/index.css](src/index.css) only.

- [x] `.btn` solid `--rose` (hover `--rose-deep`, kept `--glow`); `.btn-outline` solid white (hover border `--ink`); `.brand__mark` navy bg/white; `.brand__text` navy 800; `.h-gradient` → `linear-gradient(120deg, var(--ink) 20%, var(--rose))`.
- [x] Nav: `:hover #ededf2`; `--active` pink tint (`#ffe9f0`/`#ffd0e0`/`--rose-deep`); `--cta` solid pink (hover `--rose-deep`). `.live-badge` → `--rose-deep`.
- [x] Selection states → `#fff0f5` + inset rose ring: `.vopt--on`, `.yesno-block--on`, `.rating-chip--on` (solid rose), `.qanda-item--pinned`; input `:focus-visible` → pink ring `rgba(253,90,139,.18)`; `pin-flag`/`lead-tag` → `#ffe9f0`; `notice--closed` → `#efeff3`.
- [x] `.pill--open` pink / `.pill--closed` `#b9b9c6`; bar fills `--lead`/`--blue`/`--violet` solid; `.seg-btn--on`, `.opt-num`, `.success-badge`/`.auth-badge`, `.vopt--on .check` solid pink.
- [x] **Flattened all glassy surfaces** (`rgba(255,255,255,.5–.7)` → solid `#fff`; share-link URL + bar-track → `var(--bg)`); recolored hover tints + `::selection` to the new palette; swept out every old-Aurora hex/rgba literal (0 remain). Preserved app-only rules (skeleton shimmer, page-rise reveal, `:has(:focus-visible)` a11y outlines, responsive).
- **DoD:** ✅ `npm run lint` clean + `npm run build` green (374ms); no class/variable renamed; gradients/glass gone; flat Menti look across create/vote/results/analytics/my-polls/admin/login/register + alt states. *(Landing page is M2.)*

## Phase M2 — Landing page + routing  `[UI]` *(the only `.tsx`/route change)*  ✅ DONE

**Goal:** add the marketing home page and move Create off `/`.
**Files:** new `src/pages/HomePage.tsx`, [src/index.css](src/index.css), [src/App.tsx](src/App.tsx); link repoints across pages.

- [x] Ported the **landing CSS block** into `index.css` (`lp-wrap`, `main.lp` full-bleed, `chip(--pink)`, `hero`/`hero-grid`/`lead`/`hero-cta`/`hero-note`, `preview-card`/`preview-top`, `logos`, `lp-section(--alt)`/`sub`, `feat-grid`/`feat`/`feat__ic`, `steps`/`step`/`step__n`, `cta-band`, `lp-foot`/`foot-grid`/`lp-foot__legal` + the 860px responsive rules).
- [x] Created **`HomePage.tsx`**: hero (chip, navy→pink headline, faux **live-results preview** using `scaleX` bars + `live-badge` + `Crown` lead-tag), 6-card **feature grid**, 3-step **how it works** (`id="how"`), dark **CTA band**. **lucide-react** icons (LayoutGrid/Zap/Link2/MessageSquare/BarChart3/Timer/Sparkles/Crown — no emoji); feature-accurate copy (4 types, SignalR live, 5-char link + browser-token voting, 1×/person Q&A upvote, analytics, expiry auto-close). CTAs → `/create`; "see how it works" → `#how`.
- [x] **`App.tsx`**: `/` → `HomePage`, `/create` → `CreatePollPage`; nav **Create** → `/create`; new `Layout` uses `useLocation` → `<main className="lp">` full-bleed + **`LandingFooter`** (rich footer with real `/create`·`/login`·`/register`·`/my-polls` links + GitHub) on `/`, colophon `Footer` elsewhere. Auth-aware `Nav` kept; brand → `/` (home).
- [x] Repointed every "create" CTA `Link to="/"` → `/create`: `NotFound`, MyPolls empty-state, Vote/Results/Analytics "poll not found" cards. (Brand + `RequireAdmin` redirect stay `/` = home.)
- **DoD:** ✅ `npm run lint` clean + `npm run build` green (377ms); `/` = full-bleed landing, `/create` = the form; guest CTA + all create links land on `/create`; no dead `/`-means-create links.

## Phase M3 — Verify, responsive, a11y & merge  `[UI]`

**Goal:** ship-quality finish + ready to merge.
**Files:** verification only (small fixes if found).

- [ ] Full `npm run lint` + `npm run build` green.
- [ ] Visual pass on **every** route + alt states; landing responsive (hero-grid / feat-grid / steps / foot-grid stack ≤860px; cards ≤520px).
- [ ] a11y: focus rings visible, `prefers-reduced-motion` still disables animation, `live-badge` pulse + KPI render in the new palette.
- [ ] Confirm **no class/variable renamed**, **no `.tsx` changed beyond HomePage + App + link repoints**, **no `services/` touched**; diff ≈ `index.css` + `main.tsx` + `HomePage.tsx` + `App.tsx` (+ minor link edits).
- [ ] Commit on the branch (`style(frontend): restyle UI to Mentimeter look + landing page`); open for review / merge to `main` (→ static site auto-deploys).
- **DoD:** every screen cohesive in the Mentimeter look; landing live; lint+build green; ready to merge.

---

## Critical path (Mentimeter)
```
M0 (tokens + fonts) → M1 (component reskin) → M2 (landing + routing) → M3 (verify + merge)
```
M0 first (everything pulls from tokens); M1 finishes the per-screen look; M2 adds the only structural change
(landing + `/create` move); M3 verifies and merges.

## Out of scope (Mentimeter)
- Any backend / API / hook / data change; renaming CSS classes or variables; editing `services/`;
  fixing KNOWN_ISSUES ISSUE-001/002 (tracked separately); the register "8 characters" copy (backend min is 6).
