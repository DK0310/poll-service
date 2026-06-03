# Frontend Redesign — "Aurora"

A full UI overhaul of the Poll & Survey Builder, replacing the generic dark-slate / indigo theme with a
distinctive **Aurora UI** aesthetic (flowing mesh gradients, glassy surfaces, rose + engagement-blue).
Logic, routes, hooks, and the API contract do **not** change — this is a design-system + markup/CSS overhaul.

> **This file is the redesign *reference*** — the design language and the UI tech-stack requirements.
> - Full design tokens / component specs → [design-system/MASTER.md](design-system/MASTER.md) (source of truth).
> - The phased task list → **[UItodo.md](UItodo.md)**.
> - Behaviour / API / architecture remain governed by [../ARCHITECTURE.md](../ARCHITECTURE.md).

---

## Design Language — Aurora (chosen)

**Concept:** a luminous, premium polling app. A slow-drifting **mesh gradient** (rose → blue → violet) lives
behind everything; content sits on **frosted-glass cards**; headlines and CTAs use rose↔blue gradients. Energetic
but clean. *(Direction selected from [`design-preview10.html`](design-preview10.html); validated by the
`ui-ux-pro-max` skill, style "Aurora UI".)*

| Token | Choice | Notes |
|---|---|---|
| **Display font** | **Space Grotesk** | wordmark, headlines, questions, big numerals (skill's pick) |
| **Body font** | **DM Sans** | paragraphs, options, labels |
| **Mono font** | **DM Mono** | poll codes, tallies, %, dates, LIVE badge (tabular) |
| **Background** | `#FFF1F2` + animated **mesh gradient** | rose-tinted; never a flat fill |
| **Surface** | `rgba(255,255,255,.72)` glass (backdrop-blur 16px) | all cards |
| **Primary (rose)** | `#E11D48` | CTAs, selected, leading bar, live dot |
| **Accent (blue)** | `#2563EB` | links, focus ring, analytics line |
| **Ink / muted** | `#2e1620` / `#7c5260` | body text (on glass, AA) / captions |

**Signature devices (the memorable bits):**
- Slow **mesh-gradient drift** behind glass cards (freezes under reduced-motion).
- **Gradient-text** headlines + **gradient CTAs** (rose→pink) with a soft colored glow.
- Selected vote option → inset rose ring + soft gradient tint; results bars are gradient fills (leader in rose).
- Blinking **`● Live`** dot; glassy KPI stat cards; subtle card hover-lift.

See **[design-system/MASTER.md](design-system/MASTER.md)** for the complete token set, contrast notes,
component CSS, motion tokens, and chart spec.

---

## UI Tech Stack & Requirements

What the redesign needs on top of the existing **React 19 + TypeScript + Vite 8** SPA. Pure presentation —
no backend, routing, or API changes.

### New dependencies (frontend only)
| Package | Purpose | Why |
|---|---|---|
| `@fontsource/space-grotesk` | Display font (self-hosted) | No CDN call → works in the nginx/Docker container & offline |
| `@fontsource/dm-sans` | Body font (self-hosted) | same |
| `@fontsource/dm-mono` | Mono font for codes/tallies | same |
| `lucide-react` | SVG icon set | Replaces emoji used as icons (skill rule `no-emoji-icons`); one consistent stroke |
| `framer-motion` *(optional)* | Orchestrated/staggered reveals | Only if CSS transitions prove insufficient; otherwise omit to keep the bundle lean |

Install (example): `npm i @fontsource/space-grotesk @fontsource/dm-sans @fontsource/dm-mono lucide-react`

### Styling approach
- **Vanilla CSS + CSS custom properties** (design tokens) in `src/index.css` — **no Tailwind / CSS-in-JS** (keep the diff and bundle small; matches the current setup).
- Tokens defined once in `:root` from [MASTER.md](design-system/MASTER.md); components reference `var(--token)` only — no raw hex.
- One fixed `.aurora-bg` mesh layer; glass surfaces via `backdrop-filter` (with `-webkit-` prefix).
- Mobile-first; breakpoints **375 / 768 / 1024 / 1440**; container max-width; `min-height: 100dvh`.

### Build / verification requirements (unchanged tooling)
- `npm run lint` (eslint) and `npm run build` (tsc + vite) must stay green every phase.
- Fonts use `font-display: swap`; reserve space to avoid layout shift (CLS).
- `prefers-reduced-motion` honored (mesh drift + pulses freeze).
- Docker: fonts self-hosted so the `node:22→nginx` image build needs no network for assets.

### Browser support
- Evergreen Chromium/Firefox/Safari. `backdrop-filter` has a graceful fallback (solid `--surface` at higher opacity) for older engines.

---

## UX guardrails (from the `ui-ux-pro-max` skill — apply in every phase)

Gradeable acceptance criteria, cross-checked against the skill's design-intelligence DB:

- **Accessibility (CRITICAL):** text contrast ≥ 4.5:1 (body text on **glass**, never on the gradient); visible
  `:focus-visible` rings (blue, 2–4px); `aria-label` on icon-only buttons; sequential headings; never color-only meaning.
- **Interaction (CRITICAL):** touch targets ≥ 44×44px, ≥ 8px gaps; `cursor:pointer`; buttons show loading/disabled
  during async (vote/create); feedback within ~100ms.
- **Motion (MEDIUM):** 150–300ms, `transform`/`opacity` only, ease-out enter / ease-in exit, stagger 30–50ms;
  honor `prefers-reduced-motion`; **subtle, not excessive** (skill caution for Aurora); ≤ 1–2 animated elements/view.
- **Typography:** base 16px, line-height 1.55, tabular figures for tallies/%/counts; Space Grotesk for **display only**, DM Sans for body.
- **Icons:** **Lucide SVG**, not emoji; one consistent stroke width.
- **Charts (R4/R5):** line chart, ~20% fill, **line-style not color-only**, hairline grid, mono axis labels; toggleable
  **data-table fallback**; large **total-votes KPI**; meaningful empty/loading states; freeze animation under reduced-motion.
- **Forms (R2/R6):** visible labels (not placeholder-only), error **below field** + `role="alert"`, required markers, submit success feedback.
- **Aurora-specific:** light theme (not dark-by-default); keep text off the raw gradient; verify contrast over the lightest mesh region.

> Pre-delivery (R7): run the skill as a validation pass —
> `python <skill>/scripts/search.py "animation accessibility loading" --domain ux` — and walk Quick-Reference §1–§3.

---

## 🔍 Live visual previews (reference)

Ten self-contained mocks were built to choose the direction. **Chosen: Aurora (preview 10).** The rest are kept for reference.

**Batch 1 — hand-authored directions**

| File | Direction | Status |
|---|---|---|
| [`design-preview.html`](design-preview.html) | Editorial Ballot | reference |
| [`design-preview1.html`](design-preview1.html) | Neo-Brutalist | reference |
| [`design-preview2.html`](design-preview2.html) | Retro Terminal | reference |
| [`design-preview3.html`](design-preview3.html) | Playful / Soft | reference |
| [`design-preview4.html`](design-preview4.html) | Swiss Typographic | reference |
| [`design-preview5.html`](design-preview5.html) | Art-Deco Luxury | reference |

**Batch 2 — generated with the `ui-ux-pro-max` skill**

| File | Direction | Status |
|---|---|---|
| [`design-preview6.html`](design-preview6.html) | Glassmorphism | reference |
| [`design-preview7.html`](design-preview7.html) | Bento / Data-Dense | reference |
| [`design-preview8.html`](design-preview8.html) | Claymorphism | reference |
| [`design-preview9.html`](design-preview9.html) | Neumorphism | reference |
| [`design-preview10.html`](design-preview10.html) | **Aurora UI ✅ CHOSEN** | **building** |

---

## Out of scope
- No backend/API/route/hook/logic changes (pure presentation).
- No new pages or features (the four Merit/Distinction features stay as-is, just restyled).
- No Tailwind / CSS framework migration; no state-management changes.

## Verification (every phase)
`cd frontend && npm run lint && npm run build` must be green before a phase is marked done in
[UItodo.md](UItodo.md); do a visual check of the affected route(s).
