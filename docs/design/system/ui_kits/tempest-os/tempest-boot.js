/* TempestBoot — real startup lifecycle for the Tempest OS console. Presentation-free:
   the splash subscribes to this state; it never invents progress.
   Defines window.TempestBoot unconditionally: this file is the source of truth even
   if a stale copy was evaluated earlier (e.g. baked into a cached bundle). */
(() => {
const NS = 'TempestEngineeringDesignSystem_1d4355';
const asset = (p) => new Promise((res, rej) => { let tries = 0; (function go() { const i = new Image(); i.onload = res; i.onerror = () => (++tries < 3 ? setTimeout(go, 300) : rej(new Error('asset ' + p))); i.src = p + (tries ? '?r=' + tries : ''); })(); });
const poll = (fn, timeout, what) => new Promise((res, rej) => {
  const t0 = performance.now();
  (function tick() { if (fn()) return res(); if (performance.now() - t0 > timeout) return rej(new Error(what)); setTimeout(tick, 40); })();
});
const PHASES = [
  { key: 'runtime', label: 'RUNTIME HOST', run: () => poll(() => window.React && window.ReactDOM, 8000, 'runtime host') },
  { key: 'ds', label: 'DESIGN SYSTEM', run: () => poll(() => window[NS], 8000, 'design system bundle') },
  { key: 'type', label: 'TYPE SYSTEM', run: () => Promise.race([
      Promise.all([document.fonts.load('600 16px "Chakra Petch"'), document.fonts.load('16px Inter'), document.fonts.load('16px "Space Mono"')]).then(() => document.fonts.ready),
      new Promise((r) => setTimeout(r, 3000)), // fonts are non-fatal: cap the wait
    ]) },
  { key: 'assets', label: 'BRAND ASSETS', run: () => Promise.all([
      '../../assets/logo/derived/mark-hub.svg',
      '../../assets/logo/derived/mark-indigo.svg',
      '../../assets/logo/derived/mark-cyan.svg',
      '../../assets/logo/derived/mark-violet.svg',
      '../../assets/logo/derived/tempest-os-wordmark-dark.png',
      '../../assets/imagery/tempest-desktop-wallpaper-3840x2160.png',
    ].map(asset)) },
  { key: 'modules', label: 'MODULE SYSTEM', run: () => poll(() => window.AppShell && window.LoginScreen && window.OverviewScreen && window.TelemetryScreen && window.AlertsScreen, 8000, 'module system') },
  { key: 'shell', label: 'UI SHELL', run: () => new Promise((r) => { let done = false; const fin = () => { if (!done) { done = true; r(); } }; requestAnimationFrame(() => requestAnimationFrame(fin)); setTimeout(fin, 300); }) },
];
let state = { status: 'initialising', index: 0, label: PHASES[0].label, error: null };
const subs = new Set();
const set = (p) => { state = { ...state, ...p }; subs.forEach((f) => f(state)); };
async function sequence() {
  set({ status: 'initialising', error: null });
  for (let i = 0; i < PHASES.length; i++) {
    set({ index: i, label: PHASES[i].label });
    try { await PHASES[i].run(); } catch (e) { set({ status: 'failed', error: PHASES[i].label }); return; }
  }
  set({ status: 'ready', label: 'READY' });
}
let started = false;
window.TempestBoot = {
  start() { if (!started) { started = true; sequence(); } return this; },
  getState: () => state,
  subscribe(f) { subs.add(f); f(state); return () => subs.delete(f); },
  retry() { sequence(); },
};
})();
