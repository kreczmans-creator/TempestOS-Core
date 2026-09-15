/* scoped */
(() => {
const { Button, Input, Checkbox, Icon } = window.TempestEngineeringDesignSystem_1d4355;

function LoginScreen({ onSignIn }) {
  return (
    <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: "url('../../assets/imagery/tempest-desktop-wallpaper-3840x2160.png') center/cover" }}>
      <form onSubmit={(e) => { e.preventDefault(); onSignIn(); }}
        style={{ width: 360, padding: 'var(--space-8)', background: 'rgba(11,14,30,.82)', backdropFilter: 'blur(8px)', border: '1px solid var(--border-default)', borderTop: '2px solid var(--cyan-500)', borderRadius: 'var(--radius-md)', boxShadow: 'var(--shadow-panel)' }}>
        <img src="../../assets/logo/tempest-os-logo-dark.png" alt="Tempest OS" style={{ height: 34, marginBottom: 'var(--space-8)' }} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-5)' }}>
          <Input label="Operator ID" icon="user" defaultValue="m.okafor" mono />
          <Input label="Passphrase" type="password" icon="lock" defaultValue="••••••••••" />
          <Checkbox label="Trust this workstation" description="Skips MFA for 12 hours" checked onChange={() => {}} />
          <Button type="submit" full size="lg" iconRight={<Icon name="arrow-right" size={15} />}>Sign in</Button>
        </div>
        <div style={{ marginTop: 'var(--space-7)', paddingTop: 'var(--space-5)', borderTop: '1px solid var(--border-subtle)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)', display: 'flex', justifyContent: 'space-between' }}>
          <span>site: north-ridge</span><span>4.2.1</span>
        </div>
      </form>
    </div>
  );
}
Object.assign(window, { LoginScreen });

})();
