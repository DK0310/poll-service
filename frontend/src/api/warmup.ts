import api from './api';

// Free-tier cold start: Render spins services down after ~15 min idle, and a cold
// .NET service takes ~30–60s to boot. Fire-and-forget "wake" pings the moment the app
// loads, so the user's first real action (login / create / vote) doesn't pay the full
// boot time — the services wake while they read the page and type.
//
// The pings hit non-existent codes and 404 — that's intentional and harmless; we only
// need to wake each process (which also runs its startup DB connect/migrate).
let warmed = false;

export function warmBackend(): void {
  if (warmed) return;
  warmed = true;
  const wake = (path: string) => api.get(path).catch(() => {});
  wake('/auth/warmup'); // identity-api (login / register)
  wake('/polls/warmup'); // gateway + poll-api
  wake('/polls/warmup/results'); // vote-api
}
