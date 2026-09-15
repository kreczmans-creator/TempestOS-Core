/* scoped */
(() => {
const { Card, Icon, Button, Badge, Tag } = window.TempestEngineeringDesignSystem_1d4355;

const ITEMS = [
  { icon: 'cpu', title: 'Dispatch control', body: 'Closed-loop scheduling against grid signals, curtailment instructions and forecast, recalculated every 30 seconds.' },
  { icon: 'activity', title: 'Telemetry', body: 'Every channel from every unit, retained at full resolution for 13 months and queryable from the console.' },
  { icon: 'shield-check', title: 'Safety & compliance', body: 'Protection logic and audit trails built to IEC 61400-25, validated on hardware before it reaches a site.' },
];

function Capabilities() {
  return (
    <section style={{ padding: 'var(--section-y) var(--space-8)', background: 'var(--navy-800)' }}>
      <div style={{ maxWidth: 1240, margin: '0 auto' }}>
        <div className="t-eyebrow">What we build</div>
        <h2 style={{ margin: '12px 0 var(--space-9)', fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xl)', fontWeight: 600, letterSpacing: 'var(--tracking-tightest)', color: 'var(--text-heading)', maxWidth: '24ch' }}>
          Three systems, one control loop
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 'var(--space-7)' }}>
          {ITEMS.map((it) => (
            <Card key={it.title} interactive padding="lg">
              <Icon name={it.icon} size={22} color="var(--cyan-500)" />
              <h3 style={{ margin: '18px 0 10px', fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 600, color: 'var(--text-heading)' }}>{it.title}</h3>
              <p style={{ margin: 0, fontSize: 'var(--text-sm)', lineHeight: 'var(--leading-relaxed)', color: 'var(--text-body)' }}>{it.body}</p>
            </Card>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-11)', alignItems: 'center', marginTop: 'var(--space-13)', padding: 'var(--space-10)', background: 'var(--navy-700)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)' }}>
          <div>
            <img src="../../assets/logo/tempest-os-logo-dark.png" alt="Tempest OS" style={{ height: 30, marginBottom: 'var(--space-6)' }} />
            <p style={{ margin: 0, fontSize: 'var(--text-base)', lineHeight: 'var(--leading-relaxed)', color: 'var(--text-body)', maxWidth: '46ch' }}>
              The console operators actually use. One number that matters, and the path to change it — on the wall in the control room or on a laptop in a field van.
            </p>
            <div style={{ display: 'flex', gap: 8, marginTop: 'var(--space-7)', flexWrap: 'wrap' }}>
              <Tag>on-prem</Tag><Tag>hosted</Tag><Tag>offline-capable</Tag>
            </div>
            <div style={{ marginTop: 'var(--space-8)' }}><Button variant="secondary" iconRight={<Icon name="arrow-right" size={14} />}>Read the docs</Button></div>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-5)' }}>
            <Readout label="Sites live" value="9" />
            <Readout label="Units managed" value="144" />
            <Readout label="Uptime, 12mo" value="99.98" unit="%" />
            <Readout label="Replan cycle" value="30" unit="s" />
          </div>
        </div>
      </div>
    </section>
  );
}
Object.assign(window, { Capabilities });

})();
