/* scoped */
(() => {
const { Icon } = window.TempestEngineeringDesignSystem_1d4355;

function Numbers() {
  const rows = [['26.3 MW', 'managed at peak'], ['13 months', 'full-resolution telemetry'], ['30 s', 'dispatch replan cycle'], ['2011', 'engineering since']];
  return (
    <section style={{ padding: 'var(--space-11) var(--space-8)', backgroundColor: 'var(--navy-900)', backgroundImage: 'var(--bg-grid)', borderTop: '1px solid var(--border-subtle)', borderBottom: '1px solid var(--border-subtle)' }}>
      <div style={{ maxWidth: 1240, margin: '0 auto', display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 'var(--space-8)' }}>
        {rows.map(([n, l]) => (
          <div key={l}>
            <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xl)', fontWeight: 600, letterSpacing: 'var(--tracking-tight)', color: 'var(--cyan-500)', lineHeight: 1 }}>{n}</div>
            <div style={{ marginTop: 10, fontFamily: 'var(--font-display)', fontSize: 'var(--text-2xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-muted)' }}>{l}</div>
          </div>
        ))}
      </div>
    </section>
  );
}
Object.assign(window, { Numbers });

})();
