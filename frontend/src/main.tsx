import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Legacy app pages — self-hosted Inter (no CDN; Docker/offline-safe)
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import '@fontsource/inter/800.css'
// "Rally" identity (Phase 18) — Bricolage Grotesque (display) + Hanken Grotesk (body)
import '@fontsource/bricolage-grotesque/600.css'
import '@fontsource/bricolage-grotesque/700.css'
import '@fontsource/bricolage-grotesque/800.css'
import '@fontsource-variable/hanken-grotesk/index.css'
// Election Night — Geist Mono for all data readouts (counts, %, codes)
import '@fontsource/geist-mono/400.css'
import '@fontsource/geist-mono/500.css'
import '@fontsource/geist-mono/600.css'
// index.css is imported INTO a lower cascade layer from tailwind.css (legacy layer)
import './tailwind.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
