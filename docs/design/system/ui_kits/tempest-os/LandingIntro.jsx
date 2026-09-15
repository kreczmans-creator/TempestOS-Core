/* scoped — Tempest Engineering website landing intro.
   Same charge/ripple and blade-group fades as the OS splash, but the mark
   slides HORIZONTALLY left as the TEMPEST ENGINEERING wordmark reveals to its
   right, then the whole lockup travels to the top-left corner as the site
   fades in beneath it. Geometry is computed in px from the viewport so the
   docked lockup lands exactly in the site header at every breakpoint.
   Pure presentation; onDone fires after dock. */
(() => {
const RM = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
const L = '../../assets/logo/derived/';
const LAYERS = [['hub', 'hub'], ['indigo', 'g1'], ['cyan', 'g2'], ['violet', 'g3']];
const EASE = 'cubic-bezier(.2,0,.2,1)';
const ORD = ['start', 'ripple', 'hub', 'g1', 'g2', 'g3', 'slide', 'word', 'dock', 'site'];

function LandingIntro({ onDone, demo }) {
  const [t, setT] = React.useState('start');
  const rootRef = React.useRef(null);
  const [vp, setVp] = React.useState({ w: innerWidth, h: innerHeight });
  const at = (s) => ORD.indexOf(t) >= ORD.indexOf(s);
  React.useEffect(() => {
    const m = () => { const el = rootRef.current; if (el) setVp({ w: el.clientWidth, h: el.clientHeight }); };
    m(); addEventListener('resize', m); return () => removeEventListener('resize', m);
  }, []);
  React.useEffect(() => {
    const steps = RM
      ? [['hub', 60], ['g1', 60], ['g2', 60], ['g3', 60], ['slide', 60], ['word', 60], ['dock', 120], ['site', 400]]
      : [['ripple', 80], ['hub', 2200], ['g1', 3000], ['g2', 3750], ['g3', 4500], ['slide', 5600], ['word', 5950], ['dock', 7100], ['site', 7500]];
    const ids = steps.map(([s, ms]) => setTimeout(() => setT(s), ms));
    return () => ids.forEach(clearTimeout);
  }, []);
  React.useEffect(() => {
    if (t === 'site' && !demo && onDone) { const id = setTimeout(onDone, 700); return () => clearTimeout(id); }
  }, [t, demo]);

  /* ---- shared geometry (px) — the header and the docked lockup both derive from this ---- */
  const mobile = vp.w < 640, tablet = vp.w >= 640 && vp.w < 1024;
  const PAD_X = mobile ? 20 : tablet ? 26 : 32;
  const PAD_Y = mobile ? 16 : tablet ? 22 : 28;
  const vmin = Math.min(vp.w, vp.h);
  const icon = Math.round(Math.max(mobile ? 96 : 130, Math.min(0.21 * vmin, 240))); // centred build size
  const GAP_R = 0.17, WORD_R = 1.5;                    // gap and wordmark width as ratios of icon
  const lockW = icon * (1 + GAP_R + WORD_R);           // full lockup width at scale 1
  const dockIcon = mobile ? 30 : tablet ? 36 : 40;     // docked icon height = header lockup height
  const scale = dockIcon / icon;
  const headerH = Math.round(dockIcon * 1.15);         // header row sized off the docked lockup
  const docked = RM || at('dock');
  const slid = RM || at('slide');

  return (
    <div ref={rootRef} aria-label="Tempest Engineering" style={{ position: 'relative', height: '100%', overflow: 'hidden', background: 'var(--navy-800)' }}>
      <style>{`@keyframes tmpst-gather{0%{r:4.5vmax;opacity:0}30%{opacity:.75}100%{r:0.45vmax;opacity:0}}@keyframes tmpst-core{0%{r:0.05vmax;opacity:0}45%{r:0.62vmax;opacity:1}80%{r:0.5vmax;opacity:.9}100%{r:0.12vmax;opacity:0}}@keyframes tmpst-glow{0%{r:0.6vmax;opacity:.55}100%{r:62vmax;opacity:0}}@keyframes tmpst-ripple{0%{r:0.4vmax;opacity:0}5%{opacity:1}45%{opacity:.7}100%{r:78vmax;opacity:0}}.tmpst-rp{fill:none;opacity:0}`}</style>
      {!RM && at('ripple') && !at('g1') && (
        <svg aria-hidden="true" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }}>
          <defs>
            <radialGradient id="tmpst-lg">
              <stop offset="0%" stopColor="var(--indigo-600)" stopOpacity=".8" />
              <stop offset="65%" stopColor="var(--indigo-600)" stopOpacity=".22" />
              <stop offset="100%" stopColor="var(--indigo-600)" stopOpacity="0" />
            </radialGradient>
          </defs>
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="1.25" style={{ animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="2" style={{ animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 140ms 1 forwards' }} />
          <circle cx="50%" cy="50%" fill="#f5f6fa" style={{ opacity: 0, animation: 'tmpst-core 700ms cubic-bezier(.3,0,.4,1) 1 forwards' }} />
          <circle cx="50%" cy="50%" fill="url(#tmpst-lg)" style={{ opacity: 0, animation: 'tmpst-glow 1500ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="3" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="2.25" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 800ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--cyan-500)" strokeWidth="1.25" style={{ opacity: 0, animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 980ms 1 forwards' }} />
          <circle className="tmpst-rp" cx="50%" cy="50%" stroke="var(--indigo-600)" strokeWidth="1" style={{ animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 1160ms 1 forwards' }} />
        </svg>
      )}
      {/* lockup: centred while building, docks into the header slot. Position and scale are px-exact from shared geometry */}
      <div style={{ position: 'absolute', top: docked ? PAD_Y : (vp.h - icon) / 2, left: docked ? PAD_X : (vp.w - lockW) / 2, transform: `scale(${docked ? scale : 1})`, transformOrigin: 'top left', transition: RM ? 'none' : `top 900ms ${EASE}, left 900ms ${EASE}, transform 900ms ${EASE}` }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: icon * GAP_R }}>
          <div style={{ position: 'relative', width: icon, aspectRatio: '1', flex: '0 0 auto', transform: slid ? 'translateX(0)' : `translateX(${(icon * (GAP_R + WORD_R)) / 2}px)`, transition: RM ? 'none' : `transform 640ms ${EASE}` }}>
            {LAYERS.map(([name, stage]) => (
              <img key={name} src={`${L}mark-${name}.svg`} alt="" draggable="false" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', opacity: RM || at(stage) ? 1 : 0, transition: RM ? 'none' : `opacity ${name === 'hub' ? 680 : 750}ms ${EASE}` }} />
            ))}
          </div>
          <div style={{ flex: '0 0 auto', width: icon * WORD_R, opacity: at('word') ? 1 : 0, transform: at('word') ? 'translateX(0)' : `translateX(${icon * -0.12}px)`, transition: RM ? 'none' : `opacity 620ms ${EASE}, transform 620ms ${EASE}` }}>
            <div style={{ fontFamily: 'var(--font-display)', fontWeight: 700, fontSize: icon * 0.339, letterSpacing: '-0.01em', lineHeight: 1, color: 'var(--paper-050)', whiteSpace: 'nowrap' }}>TEMPEST</div>
            <div style={{ fontFamily: 'var(--font-display)', fontWeight: 600, fontSize: icon * 0.089, color: 'var(--slate-400)', marginTop: icon * 0.045, display: 'flex', justifyContent: 'space-between', whiteSpace: 'nowrap' }}>{'ENGINEERING'.split('').map((c, i) => <span key={i}>{c}</span>)}</div>
          </div>
        </div>
      </div>
      {/* the site loading in beneath the docked lockup — header reserves the exact lockup slot */}
      <div aria-hidden={!at('site')} style={{ position: 'absolute', inset: 0, padding: `${PAD_Y}px ${PAD_X}px`, display: 'flex', flexDirection: 'column', opacity: at('site') ? 1 : 0, transition: RM ? 'none' : `opacity 700ms ${EASE} 150ms`, pointerEvents: at('site') ? 'auto' : 'none' }}>
        <div style={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center', gap: mobile ? 20 : 36, height: headerH, paddingLeft: lockW * scale + 24, fontFamily: 'var(--font-mono)', fontSize: 11, letterSpacing: '.22em', color: 'var(--slate-400)' }}>
          {mobile ? (
            <span aria-label="Menu" style={{ display: 'flex', flexDirection: 'column', gap: 5, padding: 6 }}>
              <span style={{ width: 22, height: 2, background: 'var(--slate-400)' }}></span>
              <span style={{ width: 22, height: 2, background: 'var(--slate-400)' }}></span>
              <span style={{ width: 14, height: 2, background: 'var(--cyan-500)' }}></span>
            </span>
          ) : (
            <>
              <span>SERVICES</span><span>PROJECTS</span><span>ABOUT</span><span style={{ color: 'var(--cyan-500)' }}>CONTACT</span>
            </>
          )}
        </div>
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center', maxWidth: 880 }}>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: mobile ? 10 : 12, letterSpacing: '.28em', color: 'var(--cyan-500)', marginBottom: mobile ? 16 : 22 }}>DESIGN ENGINEERING CONSULTANCY</div>
          <div style={{ fontFamily: 'var(--font-display)', fontWeight: 700, fontSize: 'clamp(34px, 6.4vw, 64px)', letterSpacing: '-0.02em', lineHeight: 1.08, color: 'var(--paper-050)', textWrap: 'pretty' }}>Engineering that holds up</div>
          <div style={{ fontFamily: 'var(--font-body)', fontSize: 'clamp(15px, 2.2vw, 19px)', lineHeight: 1.6, color: 'var(--slate-400)', marginTop: mobile ? 18 : 24, maxWidth: 560 }}>[One-paragraph positioning — sectors served, the kind of problems solved, and the standard the work is held to.]</div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 16, fontFamily: 'var(--font-mono)', fontSize: mobile ? 8.5 : 10, letterSpacing: '.2em', color: 'var(--text-faint)', paddingBottom: 4, whiteSpace: 'nowrap' }}>
          <span>TEMPEST DESIGN ENGINEERING LTD</span><span>WWW.TEMPEST-ENGINEERING.CO.UK</span>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { LandingIntro });
})();
