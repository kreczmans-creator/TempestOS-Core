/* scoped — Tempest OS launch splash, per approved motion reference
   (uploads/TempestOS_launch_8_second_ROYAL_BLUE_OSCILLATING_RIPPLE.gif):
   black → royal-blue oscillating ripple exits frame → hub fades in → indigo,
   cyan, violet stroke groups fade in (opacity only — no growth, no rotation,
   no lock pulse) → the one icon lifts → TEMPEST OS wordmark below.
   Presentation only: startup state comes from window.TempestBoot (boot.js).
   The mark is never redrawn — layers are exact color-separated crops of the
   canonical appicon (assets/logo/derived/tempest-mark-layer-*.png). */
(() => {
const { Button } = window.TempestEngineeringDesignSystem_1d4355;
const RM = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
const L = '../../assets/logo/derived/';
const LAYERS = [['hub', 'hub'], ['indigo', 'g1'], ['cyan', 'g2'], ['violet', 'g3']];
const WORD = L + 'tempest-os-wordmark-dark.png';
const EASE = 'cubic-bezier(.2,0,.2,1)';
const ORD = ['start', 'ripple', 'hub', 'g1', 'g2', 'g3', 'lift', 'word', 'done'];

function SplashScreen({ boot, onDone, demo }) {
  const [t, setT] = React.useState('start');
  const [bs, setBs] = React.useState(boot.getState());
  const [exiting, setExiting] = React.useState(false);
  const at = (s) => ORD.indexOf(t) >= ORD.indexOf(s);
  React.useEffect(() => boot.subscribe(setBs), [boot]);
  React.useEffect(() => {
    const steps = RM
      ? [['hub', 60], ['g1', 60], ['g2', 60], ['g3', 60], ['lift', 60], ['word', 120], ['done', 700]]
      : [['ripple', 80], ['hub', 2200], ['g1', 3000], ['g2', 3750], ['g3', 4500], ['lift', 5600], ['word', 5950], ['done', 6800]];
    const ids = steps.map(([s, ms]) => setTimeout(() => setT(s), ms));
    return () => ids.forEach(clearTimeout);
  }, []);
  React.useEffect(() => {
    if (t === 'done' && bs.status === 'ready' && !demo) {
      setExiting(true);
      const id = setTimeout(onDone, RM ? 100 : 380);
      return () => clearTimeout(id);
    }
  }, [t, bs.status, demo]);

  const ICON = 'clamp(180px, 26vmin, 280px)';
  const failed = bs.status === 'failed';
  const lifted = RM || at('lift');
  return (
    <div aria-label="Tempest OS starting" style={{ position: 'relative', height: '100%', overflow: 'hidden', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--navy-800)', opacity: exiting ? 0 : 1, transition: `opacity 360ms ${EASE}` }}>
      <style>{`@keyframes tmpst-gather{0%{r:4.5vmax;opacity:0}30%{opacity:.75}100%{r:0.45vmax;opacity:0}}@keyframes tmpst-core{0%{r:0.05vmax;opacity:0}45%{r:0.62vmax;opacity:1}80%{r:0.5vmax;opacity:.9}100%{r:0.12vmax;opacity:0}}@keyframes tmpst-glow{0%{r:0.6vmax;opacity:.55}100%{r:62vmax;opacity:0}}@keyframes tmpst-ripple{0%{r:0.4vmax;opacity:0}5%{opacity:1}45%{opacity:.7}100%{r:78vmax;opacity:0}}@keyframes tmpst-status-in{from{opacity:0}to{opacity:1}}@keyframes tmpst-tick{0%,100%{opacity:.25}50%{opacity:1}}.tmpst-rp{fill:none;opacity:0}`}</style>
      {!RM && at('ripple') && !at('g1') && (
        <svg aria-hidden="true" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }}>
          <defs>
            <radialGradient id="tmpst-g">
              <stop offset="0%" stopColor="var(--indigo-600)" stopOpacity=".8" />
              <stop offset="65%" stopColor="var(--indigo-600)" stopOpacity=".22" />
              <stop offset="100%" stopColor="var(--indigo-600)" stopOpacity="0" />
            </radialGradient>
          </defs>
          {/* charge: energy gathers inward to the future hub point */}
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="1.25" style={{ animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="2" style={{ animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 140ms 1 forwards' }} />
          <circle cx="50%" cy="50%" fill="#f5f6fa" style={{ opacity: 0, animation: 'tmpst-core 700ms cubic-bezier(.3,0,.4,1) 1 forwards' }} />
          {/* release: shockwave + pressure glow travel out and exit the frame */}
          <circle cx="50%" cy="50%" fill="url(#tmpst-g)" style={{ opacity: 0, animation: 'tmpst-glow 1500ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="3" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="2.25" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 800ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="1.25" style={{ opacity: 0, animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 980ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="1" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 1160ms 1 forwards' }} />
        </svg>
      )}
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', transform: lifted ? 'translateY(0)' : 'translateY(calc(clamp(180px, 26vmin, 280px) * 0.135))', transition: RM ? 'none' : `transform 640ms ${EASE}` }}>
        <div style={{ position: 'relative', width: ICON, aspectRatio: '1', flex: '0 0 auto' }}>
          {LAYERS.map(([name, stage]) => (
            <img key={name} src={`${L}mark-${name}.svg`} alt="" draggable="false" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: RM || at(stage) ? 1 : 0, transition: RM ? 'none' : `opacity ${name === 'hub' ? 680 : 750}ms ${EASE}` }} />
          ))}
        </div>
        <img src={WORD} alt="Tempest OS" draggable="false" style={{ width: `calc(${ICON} * 1.02)`, marginTop: `calc(${ICON} * 0.13)`, opacity: at('word') ? 1 : 0, transition: RM ? 'none' : `opacity 520ms ${EASE}` }} />
      </div>
      <div role="status" aria-live="polite" style={{ position: 'absolute', bottom: 44, left: 0, right: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10, fontFamily: 'var(--font-mono)', fontSize: 10, letterSpacing: '.28em', textTransform: 'uppercase', color: 'var(--text-faint)', opacity: at('word') || failed ? 1 : 0, transition: `opacity 260ms ${EASE}` }}>
        {failed ? (
          <>
            <span style={{ color: 'var(--red-500)' }}>●</span>
            <span>Startup halted — {bs.error}</span>
            <Button size="sm" variant="ghost" onClick={() => boot.retry()}>Retry</Button>
          </>
        ) : (
          <>
            <span aria-hidden="true" style={{ width: 4, height: 4, borderRadius: '50%', background: bs.status === 'ready' ? 'var(--green-500)' : 'var(--cyan-500)', animation: bs.status === 'ready' || RM ? 'none' : 'tmpst-tick 1200ms ease-in-out infinite' }} />
            <span key={bs.label} style={{ animation: RM ? 'none' : 'tmpst-status-in 140ms ease-out' }}>{bs.label}</span>
          </>
        )}
      </div>
    </div>
  );
}

Object.assign(window, { SplashScreen });
})();
