/* scoped */
(() => {
const { Button, Icon } = window.TempestEngineeringDesignSystem_1d4355;

const LINKS = ['Platform', 'Tempest OS', 'Field services', 'About'];

function SiteHeader() {
  const [open, setOpen] = React.useState(null);
  return (
    <header style={{ position: 'sticky', top: 0, zIndex: 40, display: 'flex', alignItems: 'center', gap: 'var(--space-9)', height: 72, padding: '0 var(--space-8)', background: 'rgba(11,14,30,.86)', backdropFilter: 'blur(10px)', borderBottom: '1px solid var(--border-subtle)' }}>
      <img src="../../assets/logo/tempest-logo-horizontal-navy.png" alt="Tempest Engineering" style={{ height: 42 }} />
      <nav style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-7)' }}>
        {LINKS.map((l) => (
          <a key={l} href="#" onMouseEnter={() => setOpen(l)} onMouseLeave={() => setOpen(null)}
            style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: open === l ? 'var(--paper-050)' : 'var(--text-muted)', borderBottom: 0, transition: 'var(--transition-control)' }}>{l}</a>
        ))}
      </nav>
      <div style={{ flex: 1 }} />
      <a href="#" style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-muted)', borderBottom: 0 }}>Sign in</a>
      <Button size="md" iconRight={<Icon name="arrow-right" size={14} />}>Talk to engineering</Button>
    </header>
  );
}
Object.assign(window, { SiteHeader });

})();
