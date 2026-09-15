/* scoped */
(() => {
const { Button, Input, Icon } = window.TempestEngineeringDesignSystem_1d4355;

const COLS = [
  ['Platform', ['Dispatch control', 'Telemetry', 'Safety & compliance', 'Integrations']],
  ['Tempest OS', ['Overview', 'Release notes', 'Documentation', 'Status']],
  ['Company', ['About', 'Field services', 'Careers', 'Contact']],
];

function SiteFooter() {
  return (
    <footer className="t-light" style={{ background: 'var(--bg-page)', color: 'var(--text-body)', padding: 'var(--space-12) var(--space-8) var(--space-8)' }}>
      <div style={{ maxWidth: 1240, margin: '0 auto' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1.2fr repeat(3,1fr)', gap: 'var(--space-9)' }}>
          <div>
            <img src="../../assets/logo/tempest-logo-vertical-paper.png" alt="Tempest Engineering" style={{ height: 110, marginLeft: -8 }} />
            <div style={{ marginTop: 'var(--space-6)', display: 'flex', gap: 8, maxWidth: 300 }}>
              <Input placeholder="work email" containerStyle={{ flex: 1 }} />
              <Button size="md">Subscribe</Button>
            </div>
          </div>
          {COLS.map(([head, links]) => (
            <div key={head}>
              <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-2xs)', fontWeight: 600, letterSpacing: 'var(--tracking-widest)', textTransform: 'uppercase', color: 'var(--text-heading)' }}>{head}</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 'var(--space-5)' }}>
                {links.map((l) => <a key={l} href="#" style={{ fontSize: 'var(--text-sm)', color: 'var(--text-body)', borderBottom: 0 }}>{l}</a>)}
              </div>
            </div>
          ))}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-7)', marginTop: 'var(--space-10)', paddingTop: 'var(--space-6)', borderTop: '1px solid var(--border-subtle)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>
          <span>© 2026 Tempest Engineering</span><span>Privacy</span><span>Terms</span>
          <div style={{ flex: 1 }} />
          <span style={{ display: 'flex', gap: 12, color: 'var(--text-muted)' }}>
            <Icon name="github" size={15} /><Icon name="linkedin" size={15} /><Icon name="rss" size={15} />
          </span>
        </div>
      </div>
    </footer>
  );
}
Object.assign(window, { SiteFooter });

})();
