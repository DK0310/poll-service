import { Moon, Sun } from 'lucide-react';
import { useTheme } from '../hooks/useTheme';

// Sun/Moon theme switch. Token-styled, so it reads correctly on both the
// dark landing header and the legacy app header. Shows the theme you'd switch
// TO (moon while light, sun while dark).
export function ThemeToggle() {
  const { theme, toggle } = useTheme();
  const toDark = theme === 'light';

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={toDark ? 'Switch to dark theme' : 'Switch to light theme'}
      title={toDark ? 'Switch to dark theme' : 'Switch to light theme'}
      className="grid h-9 w-9 place-items-center rounded-full border border-line text-fg-muted transition-colors duration-150 hover:bg-panel-2 hover:text-fg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-tangerine"
    >
      {toDark ? (
        <Moon size={17} strokeWidth={2.25} aria-hidden="true" />
      ) : (
        <Sun size={17} strokeWidth={2.25} aria-hidden="true" />
      )}
    </button>
  );
}
