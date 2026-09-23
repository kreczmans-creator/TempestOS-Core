/* scoped */
(() => {
const { Icon, IconButton, Badge, Tooltip } = window.TempestEngineeringDesignSystem_1d4355;

const NAV = [
  { key: 'overview', icon: 'gauge', label: 'Overview' },
  { key: 'telemetry', icon: 'activity', label: 'Telemetry' },
  { key: 'alerts', icon: 'bell', label: 'Alerts' },
  { key: 'dispatch', icon: 'calendar-clock', label: 'Dispatch' },
  { key: 'assets', icon: 'wind', label: 'Assets' },
  { key: 'settings', icon: 'settings', label: 'Settings' },
];

function Rail({ screen, onNavigate }) {
  return (
    <nav style={{ width: 60, flex: '0 0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, padding: '14px 0', background: 'var(--navy-900)', borderRight: '1px solid var(--border-subtle)' }}>
      <img src="../../assets/logo/tempest-appicon-navy.png" alt="Tempest OS" style={{ width: 30, height: 30, marginBottom: 14 }} />
      {NAV.map((n) => {
        const active = n.key === screen;
        return (
          <Tooltip key={n.key} content={n.label} placement="right">
            <button type="button" onClick={() => onNavigate(n.key)} aria-label={n.label}
              style={{ position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center', width: 40, height: 40, border: 0, borderRadius: 'var(--radius-sm)', background: active ? 'var(--bg-selected)' : 'transparent', color: active ? 'var(--cyan-500)' : 'var(--text-faint)', cursor: 'pointer', transition: 'var(--transition-control)' }}>
              <Icon name={n.icon} size={18} />
              {active && <span style={{ position: 'absolute', left: -10, top: 10, bottom: 10, width: 2, background: 'var(--cyan-500)' }} />}
            </button>
          </Tooltip>
        );
      })}
      <div style={{ flex: 1 }} />
      <img src="../../assets/logo/tempest-social-avatar.png" alt="M. Okafor" style={{ width: 28, height: 28, borderRadius: '50%' }} />
    </nav>
  );
}

function TopBar({ title, breadcrumb, right }) {
  return (
    <header style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-5)', height: 56, flex: '0 0 auto', padding: '0 var(--space-7)', background: 'var(--navy-800)', borderBottom: '1px solid var(--border-subtle)' }}>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>{breadcrumb}</div>
        <h1 style={{ margin: 0, fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 600, letterSpacing: 'var(--tracking-tight)', color: 'var(--text-heading)' }}>{title}</h1>
      </div>
      <div style={{ flex: 1 }} />
      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)' }}>{right}</div>
    </header>
  );
}

function StatusStrip() {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-6)', height: 28, flex: '0 0 auto', padding: '0 var(--space-7)', background: 'var(--navy-900)', borderTop: '1px solid var(--border-subtle)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>
      <span style={{ color: 'var(--green-500)' }}>● link ok</span>
      <span>latency 41ms</span>
      <span>tempest-os 4.2.1</span>
      <div style={{ flex: 1 }} />
      <span>14:02:12Z</span>
    </div>
  );
}

function AppShell({ screen, onNavigate, title, breadcrumb, headerRight, children, toasts }) {
  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0, background: 'var(--navy-800)' }}>
      <Rail screen={screen} onNavigate={onNavigate} />
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <TopBar title={title} breadcrumb={breadcrumb} right={headerRight} />
        <main style={{ flex: 1, overflow: 'auto', padding: 'var(--space-7)', backgroundColor: 'var(--navy-800)', backgroundImage: 'var(--bg-grid)' }}>{children}</main>
        <StatusStrip />
      </div>
      <div style={{ position: 'fixed', right: 20, bottom: 44, display: 'flex', flexDirection: 'column', gap: 10, zIndex: 200 }}>{toasts}</div>
    </div>
  );
}

Object.assign(window, { AppShell, Rail, TopBar, StatusStrip, OS_NAV: NAV });

})();
