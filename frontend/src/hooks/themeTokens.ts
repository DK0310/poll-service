// Light "daytime broadcast" values for the Tailwind @theme tokens.
//
// Why JS instead of CSS: Tailwind v4 flattens @theme var() at build time and
// strips any CSS redeclaration of a --color-* theme key, so the light theme
// can't override these tokens from a stylesheet. Applying them as INLINE custom
// properties on <html> wins the cascade and bypasses the build entirely. Dark is
// the CSS default (tailwind.css @theme); light = these inline props; toggling to
// dark just removes them.
//
// NOTE: the pre-paint init script in index.html inlines the same values (it must,
// to avoid a flash) — keep the two in sync.
export const LIGHT_TOKENS: Record<string, string> = {
  '--color-bg': '#f4f2fb', // light plum-tinted studio (cool, brand hue — not cream)
  '--color-panel': '#ffffff',
  '--color-panel-2': '#ece8f6',
  '--color-line': 'rgba(24, 17, 43, 0.12)',
  '--color-line-strong': 'rgba(24, 17, 43, 0.22)',
  '--color-fg': '#17131f', // near-black plum — ~16:1 on bg
  '--color-fg-muted': '#574f68', // ~7:1 on white
  '--color-fg-faint': '#7c7490',
  '--color-tangerine': '#e4531c', // deepened to clear AA on light
  '--color-grape': '#6b4ce0',
  '--color-teal': '#0f9c8f',
  '--color-amber': '#b26a00',
  '--color-danger': '#d22d2d',
  '--shadow-board': '0 1px 2px rgba(24, 17, 43, 0.05), 0 18px 40px -24px rgba(24, 17, 43, 0.22)',
  '--shadow-glow-tangerine': '0 8px 22px -10px rgba(228, 83, 28, 0.45)',
  '--shadow-glow-grape': '0 8px 22px -10px rgba(107, 76, 224, 0.4)',
  '--shadow-glow-teal': '0 8px 22px -10px rgba(15, 156, 143, 0.4)',
};

// Apply the light token overrides (theme === 'light') or clear them so the CSS
// dark @theme defaults take over (theme === 'dark').
export function applyThemeTokens(theme: 'light' | 'dark'): void {
  const root = document.documentElement;
  if (theme === 'light') {
    for (const [k, v] of Object.entries(LIGHT_TOKENS)) root.style.setProperty(k, v);
  } else {
    for (const k of Object.keys(LIGHT_TOKENS)) root.style.removeProperty(k);
  }
}
