# Product

> Strategic design context for the frontend (impeccable). For system architecture,
> schema, and APIs, [ARCHITECTURE.md](ARCHITECTURE.md) remains authoritative. Visual
> system lives in [DESIGN.md](DESIGN.md).

## Register

brand

The redesign centers on the **marketing landing page** (`/`), where design IS the
product. The in-app screens (create, vote, results, analytics, admin) are a **product**
surface that serves the workflow; they inherit the landing's visual system but optimize
for clarity over expression.

## Users

- **Hosts** — teachers, facilitators, team leads, event/meetup organizers. They land on
  the homepage to decide "can this run a live poll in front of my room right now?" Context:
  often minutes before a session, sometimes projected on a screen.
- **Voters** — the audience (students, attendees, teammates). No account; they arrive via a
  short link or QR on a phone and answer in seconds.

The job to be done: **turn a question into a shared live moment** — ask, everyone answers,
the room watches the result form in real time.

## Product Purpose

A real-time polling & survey tool (AMD201 microservices coursework). Create a multiple-choice,
yes/no, 1–5 rating, or open-text question, share a 5-character link, and collect votes that
stream onto a live bar chart over WebSockets — plus anonymous Q&A, creator analytics, and
auto-close on expiry. Success for the landing: a host immediately understands the live payoff
and clicks "Create a poll".

## Brand Personality

**Energetic, playful, and trustworthy.** Voice is warm and plain-spoken — short, confident
sentences, a little fun ("Ask the room. Watch it answer."), never jargon or hype. The tension to
hold: lively enough to feel like a live event, clean enough to be used in a classroom or at
work without feeling like a toy. Three words: *kinetic · dramatic · precise.*

**Committed visual identity: "Election Night"** — the app looks like a live broadcast results
board (dark studio, glowing multi-color bars, mono data). The energy comes from drama + motion,
the trust from precise data styling. See [DESIGN.md](DESIGN.md). (An earlier light/warm "Rally"
attempt was rejected for reading as a generic recolored SaaS template — see anti-references.)

## Anti-references

- **Mentimeter / Slido clone look** — navy + pink (or corporate blue), flat white cards,
  Inter everywhere. The previous design was an explicit Mentimeter copy; the whole point of
  the redesign is to stop looking like one.
- **Generic friendly-SaaS** — purple gradient hero, three identical icon cards, a tiny
  uppercase tracked eyebrow above every section, hero-metric template.
- **Editorial-serif "Stripe-adjacent" reflex** — display serif + italic + ruled columns +
  monochrome restraint. The second-order trap for "a polling tool that isn't Mentimeter."
- **Gradient text and side-stripe accent borders** (impeccable absolute bans).

## Design Principles

1. **The live moment is the hero.** Sell the thing that's actually special — answers landing
   in real time — with motion and the real results visual, not stock illustration.
2. **Color carries the energy; type and space carry the trust.** A committed full palette
   (tangerine + grape + teal) supplies the play; generous spacing and a clean grotesk keep it
   professional.
3. **Show the product, not a metaphor.** Imagery = the actual live-results UI / data viz.
4. **Earn every animation.** Motion reinforces "live" (bars grow, counts tick); it's never
   decoration, and it always has a reduced-motion fallback.
5. **No reflexes.** If the choice is what every polling tool / every AI landing would do,
   pick again.

## Accessibility & Inclusion

- Target **WCAG 2.1 AA**: body text ≥ 4.5:1, large text ≥ 3:1 (muted text token is ≥ 7:1 on
  white). Never color-only signaling — status uses shape + label.
- Visible focus states on all interactive elements (focus-visible outline in an accent that
  contrasts the control).
- `prefers-reduced-motion` honored for every animation (entrance, bars, pulse, scroll-reveal).
- Voters are on phones in varied lighting; the projected results view must stay legible at a
  distance (large figures, tabular numerals, high-contrast bars).
