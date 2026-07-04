# Design — "Election Night"

> Visual system for the frontend (impeccable). Strategy lives in [PRODUCT.md](PRODUCT.md);
> system architecture in [ARCHITECTURE.md](ARCHITECTURE.md). Tokens are defined in
> [frontend/src/tailwind.css](frontend/src/tailwind.css) as Tailwind v4 `@theme` variables.
>
> **Status:** the landing page (`/`) ships in Tailwind (Phase 18.1). The app pages were first brought
> onto the same identity in **Phase 18.2 via a token re-palette** (legacy `index.css`, dark-first via
> `<html data-theme="dark">`, `useTheme` toggle removed). In **Phase 18.3** the six core screens —
> Login, Register, Create (`PollForm`), Vote (`VoteForm`), Results (`LiveBarChart`), Admin, plus
> `ShareLink` + `QandAPanel` — were **rebuilt as native Tailwind utilities** on a shared app-screen
> component vocabulary (see "App-screen components" below). Expression is split by surface (product
> register): **restrained** forms (Login/Register/Create), **broadcast** data screens
> (Vote/Results/Admin). My Polls + Analytics remain on the re-paletted `index.css` until a later pass
> retires it. The whole app is visually unified and dark-only.

## App-screen components (Phase 18.3)

Reusable controls in `tailwind.css` (`@layer components`, above the `legacy` layer so they win over
`index.css`): `.board-panel` (studio card), `.board-label`, `.board-input` (placeholder ≥ 4.5:1 via
`fg-muted`, tangerine focus ring), `.board-btn` (tangerine pill, glow, hover → amber) + `--block`,
`.board-btn-outline` (ghost), `.board-bar-track` / `.board-bar-fill--lead|teal|grape` (the result-bar
motif with accent glows), `.board-spin`. New token: `--color-danger` for destructive actions/errors.

## Theme

The app looks and feels like a **live broadcast results board / control room** — dark, dramatic,
data-forward, real-time. Color strategy is **drenched dark**: a near-black plum-blue "studio" base
with three saturated accents (tangerine / grape / teal) that **glow**, plus amber for the live/winner
highlight. The glowing horizontal **result bar** is the structural motif everywhere; **mono**
percentages and a climbing vote count carry the "live" energy. Energetic + playful comes from the
drama and motion; professional comes from the precise data styling and restraint.

## Color

`--color-*` tokens (Tailwind generates `bg-*`, `text-*`, `border-*` utilities).

| Token | Value | Role |
|---|---|---|
| `bg` | `#0B0913` | near-black plum-blue base (the "studio") |
| `panel` | `#15121F` | boards / cards |
| `panel-2` | `#1E1A2B` | rows, elevated bits |
| `line` | `rgba(255,255,255,0.10)` | hairlines |
| `line-strong` | `rgba(255,255,255,0.18)` | emphasized rules |
| `fg` | `#F5F2FB` | primary text |
| `fg-muted` | `#A79FB8` | muted text (≥7:1 on bg) |
| `fg-faint` | `#6F6880` | faint labels |
| `tangerine` | `#FF6B3D` | primary / leading bar |
| `grape` | `#8B6BFF` | accent 2 |
| `teal` | `#2DD4C4` | accent 3 |
| `amber` | `#FFC44D` | live-now / winner highlight |

Accent **glows** via accent-colored `box-shadow` tokens (`shadow-glow-tangerine|grape|teal`, e.g.
`0 0 28px -4px <accent>`). Leading bars glow; trailing bars sit at lower opacity. No gradient text,
no side-stripe borders.

## Typography

Contrast-axis trio (none on impeccable's overused list):

- **Display — Bricolage Grotesque** (600/700/800) — headings, wordmark.
- **Body — Hanken Grotesk Variable**.
- **Mono — Geist Mono** (400/500/600) — every number, percentage, vote count, poll code, ticker, and
  `LIVE` readout. Mono is justified here (real data on a results board), not costume.

Headings: `clamp()` fluid scale, hero max ≤ 5rem, `tracking-[-0.03em]` (≥ −0.04em floor), `text-balance`.
Numbers use `tabular-nums`.

## Components / motifs (landing)

- **Result bar** — the core unit: a track (`bg-panel-2`) + an accent fill that grows from `scaleX(0)`
  / width, leading fill glows. Reused in the hero board, the mini boards, and as stat displays.
- **Hero board** — the hero *is* an interactive, votable poll (`HeroBoard`): tap an option → bars
  react, big mono %, climbing `votes` count, `LIVE ●` pulse, crowned leader. Shows the product by
  being one.
- **Mini boards** — each of the 4 question types rendered as a small live results board
  (multiple-choice bars, 1–5 rating histogram, yes/no split, open-text comment feed).
- **Ticker** — a thin mono marquee of facts (reduced-motion → static row).
- **Buttons** — pill; primary = tangerine fill with `text-bg` + `shadow-glow-tangerine`, hover →
  amber + lift. Links underline in tangerine (never the legacy blue).
- **Surfaces** — `rounded-board` (1.25rem) panels, hairline `line` borders, `shadow-board`. Faint
  broadcast **grid texture** (`.board-grid`) + radial glow wash on hero/CTA.

## Layout

`max-w-6xl`, `px-6`, `py-20`/`py-24` rhythm. Hero is asymmetric (`1fr / minmax(380px,460px)`),
stacking on mobile. Sections separated by hairlines + a `bg-panel` band, not nested cards.

## Motion

Scoped under `.board` / `.board-*`; eased with `--ease-out-quint`, no bounce.
- Hero entrance (`board-rise` / `board-rise-2`), bars grow (`board-bar`), live pulse (`board-pulse`),
  ticker marquee (`board-ticker`).
- **Entrances run on mount, never gate visibility on scroll** (`.board-reveal` = an on-mount
  animation), so sections can't ship blank in headless renders or if JS/IO fails — per impeccable.
- Every animation has a `prefers-reduced-motion: reduce` no-op fallback.

## App pages (Phase 18.2 — token re-palette)

App pages render through the legacy `index.css`, which is **fully token-driven** and already had a
complete dark-mode variant (Phase 17). The re-theme:

- Base `:root` accents remapped to Election Night: `--rose` → tangerine `#FF6B3D`, `--blue` → teal
  `#2DD4C4`, `--violet` → grape `#8B6BFF`, `--success`/`--danger` brightened for dark; `--glow` →
  tangerine. Fonts → Bricolage / Hanken / Geist Mono.
- The `:root[data-theme="dark"]` neutrals remapped to the studio palette (`--bg #0B0913`,
  `--surface #15121F`, `--ink #F5F2FB`, `--muted #A79FB8`, hairline borders).
- Forced dark-first via `<html data-theme="dark">`; the `useTheme` toggle was deleted.
- Result bars (`LiveBarChart`) gained accent **glows**; leftover hardcoded light chips (active nav
  pill, pin-flag, lead-tag, closed-notice) remapped to tangerine tints / surface.
- impeccable bans cleared in `index.css`: gradient text (`.h-gradient` → solid tangerine) and the
  toast side-stripe (`border-left` → full border + colored icon).

## Tooling notes

- **Tailwind v4** via `@tailwindcss/vite`, CSS-first (`@theme`); no `tailwind.config.js`.
- **Cascade layers for coexistence:** Preflight is not imported; the legacy `index.css` is imported
  into the **lowest** layer (`@layer legacy, theme, base, components, utilities;` +
  `@import './index.css' layer(legacy)` in `tailwind.css`). Unlayered CSS would otherwise beat all
  Tailwind utilities regardless of specificity — which made the first attempt render legacy element
  styles (h1 color, link blue, fonts) under new backgrounds. App pages still get the legacy styling
  (they use legacy classes, not utilities). Once every page is migrated and `index.css` is retired,
  the legacy layer + Preflight decision can be revisited.
