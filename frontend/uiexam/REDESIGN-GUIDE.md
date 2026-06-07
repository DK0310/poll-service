# PollBuilder UI Redesign Guide — Mentimeter Look

A handoff spec for an AI coding agent. It explains how to restyle the **poll-service**
React frontend to match the attached wireframe HTML files (Mentimeter-style: navy + pink,
light, flat white cards) **without changing any features, routes, or backend code.**

---

## 0. TL;DR (read this first)

- **Goal:** make the live app look like the attached wireframes. **Visual only.**
- **How:** rewrite the design tokens + a handful of component rules in
  **`frontend/src/index.css`**. Do **not** edit `.tsx` files for styling.
- **Why it's safe:** the wireframes use the **exact same CSS class names** as the real
  app, so re-theming the shared stylesheet re-skins every page automatically.
- **Canonical target stylesheet:** the attached **`css/wireframe.css`** is already the
  finished Mentimeter theme using the real class names. Treat it as the source of truth —
  port its values into `frontend/src/index.css`.
- **Work on a branch.** Never push to `main` directly.

---

## 1. What you're given vs. what you change

| | Path | Role |
|---|---|---|
| **Wireframes (input)** | `*.html` + `css/wireframe.css` | The target look. Static mockups of every screen. Do not ship these. |
| **Real app (output)** | `poll-service/frontend/src/` | What you actually edit. A React 19 + Vite + TS SPA. |
| **The one file to edit** | `poll-service/frontend/src/index.css` | The whole "Aurora" design system lives here. Re-theme it. |

> The wireframe `css/wireframe.css` and the app's `index.css` share **identical class
> names and CSS variable names**. The only differences are the *values* (palette, fonts,
> card treatment) and a few component rules. Your job is to make `index.css` produce the
> wireframe values.

---

## 2. Golden rules (do NOT break these)

1. **No feature, logic, routing, or backend changes.** This is a reskin.
2. **Do not rename CSS classes or CSS variables.** The `.tsx` files reference them by
   name; renaming breaks the app.
3. **Do not edit files under `poll-service/services/`** (the .NET microservices).
4. **Avoid `.tsx` edits.** The only allowed exception is the optional landing-page route
   in §7, and only if explicitly approved.
5. **Keep accessibility intact** — focus rings, `aria-*`, `.sr-only`, reduced-motion
   block. Restyle them, don't delete them.
6. **Verify before finishing** (see §8): `npm run lint && npm run build` must stay green.

---

## 3. The design tokens (port these into `:root` in `index.css`)

Replace the existing token **values** (keep the names). These are the Mentimeter values:

```css
:root {
  /* Color — names kept, values remapped to Mentimeter */
  --rose:      #fd5a8b;  /* primary pink            */
  --rose-2:    #ff7da3;  /* lighter pink            */
  --rose-deep: #e23f72;  /* pink hover / strong     */
  --blue:      #196cff;
  --violet:    #7b61ff;

  --bg:           #f4f4f7;  /* light app background  */
  --surface:      #ffffff;  /* flat white cards      */
  --surface-solid:#ffffff;
  --surface-bd:   #e3e3ea;  /* card border           */

  --ink:      #14152d;  /* navy ink / headings   */
  --ink-soft: #3a3a4d;
  --muted:    #6b6b80;
  --border:   #e3e3ea;

  --success: #047857;
  --danger:  #dc2626;

  /* Type — Inter everywhere (was Space Grotesk / DM Sans / DM Mono) */
  --font-display: 'Inter', system-ui, sans-serif;
  --font-body:    'Inter', system-ui, sans-serif;
  --font-mono:    ui-monospace, 'SFMono-Regular', 'Roboto Mono', monospace;

  /* Radii — tighter, flatter */
  --radius-sm: 8px;  --radius: 12px;  --radius-lg: 16px;

  /* Elevation — soft neutral shadow (no glass) */
  --shadow:    0 1px 2px rgba(20,21,45,.05), 0 10px 28px -14px rgba(20,21,45,.18);
  --shadow-sm: 0 1px 2px rgba(20,21,45,.06);
  --glow:      0 8px 20px -8px rgba(253,90,139,.5);

  /* Spacing / motion tokens: keep as-is */
}
```

### Fonts
The app currently loads fonts via `@fontsource/*` (DM Sans, Space Grotesk, DM Mono),
imported in `src/main.tsx`. Switch to **Inter**:
- Add the package: `npm i @fontsource/inter`
- In `src/main.tsx`, replace the three `@fontsource/...` imports with
  `import '@fontsource/inter/400.css'` … `/500.css` `/600.css` `/700.css` `/800.css`.
- (Or, simplest: add `@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');` at the very top of `index.css` and skip the package.)

---

## 4. Component rule changes (beyond tokens)

Tokens get you ~80% there. Also apply these specific rule edits in `index.css`:

1. **Kill the aurora background.** Remove (or empty) the `body::before` / `.aurora-bg`
   mesh-gradient block and its `aurora-drift` keyframes. Background is now flat `--bg`.
2. **Flatten cards.** In `.card`, remove `backdrop-filter`/`-webkit-backdrop-filter` and
   the `@supports not (...)` glass fallback. Keep `background: var(--surface)` (solid white),
   the border, and `box-shadow: var(--shadow)`.
3. **Header.** `.app-header` → `background: rgba(255,255,255,.9)`, soften blur to `8px`.
4. **Buttons solid.** `.btn` `background: var(--rose)` (was a gradient); hover
   `background: var(--rose-deep)`. Keep `box-shadow: var(--glow)`.
5. **Brand mark navy.** `.brand__mark { background: var(--ink); color:#fff; }` (was pink
   gradient). `.brand__text { color: var(--ink); font-weight:800; }`.
6. **Gradient text.** `.h-gradient` → `linear-gradient(120deg, var(--ink) 20%, var(--rose))`
   (navy→pink, was rose→blue).
7. **Nav active pill.** `.nav-link--active { background:#ffe9f0; border-color:#ffd0e0;
   color:var(--rose-deep); }`. `.nav-link--cta { background:var(--rose); }`.
8. **Selection states use a pink tint, not glass:** `.vopt--on`, `.yesno-block--on`,
   `.qanda-item--pinned` → `background:#fff0f5` + `box-shadow: 0 0 0 2px var(--rose) inset`.
9. **Pills:** `.pill--open { background: var(--rose); }`,
   `.pill--closed { background:#b9b9c6; }`.
10. **Bar chart:** `.bar-fill--lead { background: var(--rose); }`,
    `--blue { background: var(--blue); }`, `--violet { background: var(--violet); }`.

> Shortcut: every one of these already exists, finished, in the attached
> **`css/wireframe.css`**. Diff `wireframe.css` against `index.css` and copy the resolved
> rules across. Class names match 1:1, so it ports cleanly.

---

## 5. Wireframe file → real route / component map

Restyle is global, but use this to **visually verify each screen** after theming:

| Wireframe file | Route | React page / component |
|---|---|---|
| `home.html` | *(landing — see §7)* | new, optional |
| `create.html` | `/` | `pages/CreatePollPage.tsx` + `components/PollForm.tsx`, `ShareLink.tsx` |
| `vote.html` | `/poll/:code` | `pages/VotePage.tsx` + `components/VoteForm.tsx`, `QandAPanel.tsx` |
| `results.html` | `/poll/:code/results` | `pages/ResultsPage.tsx` + `components/LiveBarChart.tsx`, `QandAPanel.tsx` |
| `analytics.html` | `/poll/:code/analytics` | `pages/AnalyticsPage.tsx` + `components/LineChart.tsx` |
| `my-polls.html` | `/my-polls` | `pages/MyPollsPage.tsx` + `components/PollCard.tsx` |
| `admin.html` | `/admin` | `pages/AdminDashboardPage.tsx` |
| `login.html` | `/login` | `pages/LoginPage.tsx` |
| `register.html` | `/register` | `pages/RegisterPage.tsx` |

> Each wireframe also shows **alternate states** (closed poll, already-voted, empty list,
> success receipt, all four question types). Make sure the themed CSS covers them — they
> use classes like `.notice--closed`, `.notice--voted`, `.empty-state`,
> `.create-success`, `.vote-yesno`, `.rating-row`, `.text-answer`.

---

## 6. Class contract (the shared vocabulary)

Both the wireframes and the app rely on these. Treat the list as a checklist — if a class
looks right in every wireframe, it must look right in the app.

- **Shell:** `app-shell`, `app-header`, `app-header__inner`, `brand`, `brand__mark`,
  `brand__text`, `app-nav`, `nav-link`, `nav-link--active`, `nav-link--cta`, `app-footer`
- **Primitives:** `card`, `btn`, `btn--block`, `btn-outline`, `btn-link`, `btn-link.danger`,
  `pill`, `pill--open`, `pill--closed`, `live-badge`, `live-badge--off`, `h-gradient`,
  `muted`, `mono`, `tnum`
- **Create:** `poll-form`, `seg`, `seg-btn`, `seg-btn--on`, `opt-row`, `opt-num`,
  `opt-remove`, `opt-add`, `create-success`, `success-badge`, `share-link`,
  `share-link__url`, `created-links`
- **Vote:** `vote-form`, `vote-options`, `vopt`, `vopt--on`, `vopt__text`, `check`,
  `vote-yesno`, `yesno-block(--on)`, `rating-row`, `rating-chip(--on)`, `text-answer`,
  `notice`, `notice--closed`, `notice--voted`, `vote-share`
- **Q&A:** `qanda`, `qanda__head`, `qanda-form`, `qanda-list`, `qanda-item(--pinned)`,
  `qanda__upvote`, `qanda-text`, `pin-flag`, `qanda__pin`
- **Results:** `results-head`, `kpi`, `kpi__num`, `bar-list`, `bar-row`, `bar-track`,
  `bar-fill`, `bar-fill--lead/--blue/--violet`, `lead-tag`, `answer-list`, `answer-item`,
  `results-foot`, `results-share`
- **Analytics:** `analytics-head`, `stat-grid`, `stat-card`, `stat-card__num`,
  `chart-card__title`, `line-chart`, `chart-axis`, `analytics-back`, `skeleton*`
- **My polls / Admin / Auth:** `poll-list`, `poll-card`, `poll-card__head/meta/actions/nav/manage`,
  `poll-action`, `empty-state`, `admin-head`, `admin-list`, `admin-row*`, `auth-head`,
  `auth-badge`, `auth-form`, `pw-wrap`, `pw-toggle`, `auth-foot`

---

## 7. (Optional) Add the landing/main page

`home.html` is a **new** page the app doesn't have yet (today `/` is the Create form).
Only do this if explicitly requested. If so:

1. Create `pages/HomePage.tsx` translating `home.html`'s markup to JSX (it uses landing
   classes `hero`, `feat-grid`, `feat`, `steps`, `cta-band`, `lp-foot`, etc. — port those
   landing rules from `wireframe.css` into `index.css`).
2. In `App.tsx`: route `/` → `HomePage`, and move Create to `/create`. Update the nav
   `Create` link's `to` and any `Link to="/"` that meant "create" (e.g. in `PollForm`
   success, `NotFound`, empty states) to `/create`.
3. Keep all hero copy feature-accurate (question types, live SignalR results, short-link
   voting, Q&A, analytics, expiry) — it must describe what the app actually does.

---

## 8. Verification checklist (must pass before done)

- [ ] `cd frontend && npm install && npm run lint && npm run build` — all green.
- [ ] `npm run dev` and visually compare **every** route in §5 against its wireframe.
- [ ] Check alternate states: closed poll banner, already-voted notice, empty My Polls,
      create success receipt, and all 4 vote types (SingleChoice / YesNo / Rating / OpenText).
- [ ] Live bar chart, KPI number, and `live-badge` pulse render in the new palette.
- [ ] Focus rings visible; `prefers-reduced-motion` still disables animations.
- [ ] No class/variable was renamed; no `.tsx` changed (except the approved §7 work).
- [ ] No file under `services/` touched.
- [ ] Diff is essentially limited to `frontend/src/index.css` (+ `main.tsx` font imports).

---

## 9. Suggested commit / PR

- Branch: `feat/mentimeter-restyle`
- Commit message: `style(frontend): restyle UI to Mentimeter look (navy + pink, flat cards)`
- PR body: link the wireframes, note "visual-only reskin via index.css tokens; no feature
  or backend changes; lint + build green."
