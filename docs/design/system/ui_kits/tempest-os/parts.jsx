/* scoped */
(() => {
const { Icon, Badge } = window.TempestEngineeringDesignSystem_1d4355;

/* Shared readouts and table primitives used across Tempest OS screens. */
function Readout({ label, value, unit, delta, tone }) {
  return (
    <div style={{ flex: 1, minWidth: 0, padding: 'var(--space-5)', background: 'var(--surface-card)', border: '1px solid var(--surface-card-border)', borderRadius: 'var(--radius-md)' }}>
      <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xs)', fontWeight: 600, letterSpacing: 'var(--tracking-widest)', textTransform: 'uppercase', color: 'var(--text-faint)' }}>{label}</div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 5, marginTop: 8 }}>
        <span style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xl)', fontWeight: 600, letterSpacing: 'var(--tracking-tight)', lineHeight: 1, color: 'var(--text-heading)' }}>{value}</span>
        {unit && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-xs)', color: 'var(--text-faint)' }}>{unit}</span>}
      </div>
      {delta && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 5, marginTop: 8, fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: tone === 'down' ? 'var(--red-500)' : 'var(--green-500)' }}>
          <Icon name={tone === 'down' ? 'trending-down' : 'trending-up'} size={12} />{delta}
        </div>
      )}
    </div>
  );
}

/* Deterministic pseudo-random series so the chart band is stable across renders. */
function series(seed, n) {
  const out = [];
  for (let i = 0; i < n; i++) out.push(0.45 + 0.4 * Math.abs(Math.sin(seed + i * 0.37)) + 0.08 * Math.abs(Math.cos(seed * 2 + i)));
  return out;
}

function Sparkband({ seed = 1, height = 132, color = 'var(--cyan-500)', bars = 64 }) {
  const data = series(seed, bars);
  return (
    <div style={{ position: 'relative', height, display: 'flex', alignItems: 'flex-end', gap: 2 }}>
      <div style={{ position: 'absolute', inset: 0, backgroundImage: 'linear-gradient(var(--paper-a08) 1px, transparent 1px)', backgroundSize: '100% 33%' }} />
      {data.map((v, i) => (
        <div key={i} style={{ flex: 1, height: `${Math.round(v * 100)}%`, background: i === bars - 1 ? 'var(--paper-050)' : color, opacity: i === bars - 1 ? 1 : 0.55 + 0.45 * v, borderRadius: 1 }} />
      ))}
    </div>
  );
}

function Th({ children, align }) {
  return <th style={{ textAlign: align || 'left', padding: '0 var(--space-5) var(--space-3)', fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-faint)', borderBottom: '1px solid var(--border-subtle)', whiteSpace: 'nowrap' }}>{children}</th>;
}

function Td({ children, align, mono = false, dim = false }) {
  return <td style={{ textAlign: align || 'left', padding: 'var(--space-4) var(--space-5)', fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)', fontSize: mono ? 'var(--text-xs)' : 'var(--text-sm)', color: dim ? 'var(--text-muted)' : 'var(--text-heading)', borderBottom: '1px solid var(--border-subtle)', whiteSpace: 'nowrap' }}>{children}</td>;
}

function LogLine({ t, level, msg }) {
  const c = { WARN: 'var(--amber-500)', INFO: 'var(--cyan-500)', ERR: 'var(--red-500)', OK: 'var(--green-500)' }[level];
  return (
    <div style={{ display: 'flex', gap: 10, fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', lineHeight: 1.9, whiteSpace: 'nowrap' }}>
      <span style={{ color: 'var(--text-faint)' }}>{t}</span>
      <span style={{ color: c, width: 34, flex: '0 0 auto' }}>{level}</span>
      <span style={{ color: 'var(--text-body)', overflow: 'hidden', textOverflow: 'ellipsis' }}>{msg}</span>
    </div>
  );
}

Object.assign(window, { Readout, Sparkband, Th, Td, LogLine, series });

})();
