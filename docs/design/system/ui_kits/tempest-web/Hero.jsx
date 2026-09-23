/* scoped */
(() => {
const { Button, Badge, Icon } = window.TempestEngineeringDesignSystem_1d4355;

function Hero() {
  return (
    <section style={{ position: 'relative', padding: '120px var(--space-8) 96px', backgroundColor: 'var(--navy-800)', backgroundImage: 'var(--bg-grid)', borderBottom: '1px solid var(--border-subtle)' }}>
      <div style={{ maxWidth: 1240, margin: '0 auto', display: 'grid', gridTemplateColumns: '1.1fr .9fr', gap: 'var(--space-11)', alignItems: 'center' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 'var(--space-7)' }}>
            <Badge tone="info" dot>Tempest OS 4.2 is live</Badge>
          </div>
          <h1 style={{ margin: 0, fontFamily: 'var(--font-display)', fontSize: 'var(--text-6xl)', fontWeight: 600, lineHeight: 'var(--leading-tight)', letterSpacing: 'var(--tracking-tightest)', color: 'var(--text-heading)' }}>
            Control systems for grid-scale wind
          </h1>
          <p style={{ marginTop: 'var(--space-7)', maxWidth: '52ch', fontSize: 'var(--text-lg)', lineHeight: 'var(--leading-relaxed)', color: 'var(--text-body)' }}>
            We build the dispatch, telemetry and safety software that keeps turbine fleets producing. Nine sites, 144 units, one control loop.
          </p>
          <div style={{ display: 'flex', gap: 'var(--space-4)', marginTop: 'var(--space-9)' }}>
            <Button size="lg" notch iconRight={<Icon name="arrow-right" size={15} />}>Talk to engineering</Button>
            <Button size="lg" variant="secondary" iconLeft={<Icon name="play" size={15} />}>See Tempest OS</Button>
          </div>
          <div style={{ display: 'flex', gap: 'var(--space-8)', marginTop: 'var(--space-11)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', color: 'var(--text-faint)' }}>
            <span>IEC 61400-25 compliant</span><span>SOC 2 Type II</span><span>On-prem or hosted</span>
          </div>
        </div>
        <div style={{ position: 'relative', aspectRatio: '4 / 3', border: '1px solid var(--border-default)', borderTop: '2px solid var(--cyan-500)', borderRadius: 'var(--radius-md)', background: 'var(--navy-700)', overflow: 'hidden', boxShadow: 'var(--shadow-panel)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, height: 34, padding: '0 12px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--navy-900)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>
            <span style={{ width: 6, height: 6, borderRadius: '50%', background: 'var(--green-500)' }} />tempest-os / north-ridge
          </div>
          <div style={{ padding: 20 }}>
            <div style={{ fontFamily: 'var(--font-display)', fontSize: 10, fontWeight: 600, letterSpacing: '.28em', textTransform: 'uppercase', color: 'var(--text-faint)' }}>Fleet output</div>
            <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-4xl)', fontWeight: 600, color: 'var(--text-heading)', letterSpacing: 'var(--tracking-tight)', lineHeight: 1.1 }}>26.3 <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, color: 'var(--text-faint)' }}>MW</span></div>
            <div style={{ marginTop: 18 }}><Sparkband seed={2.1} height={120} /></div>
          </div>
        </div>
      </div>
    </section>
  );
}
Object.assign(window, { Hero });

})();
