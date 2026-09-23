/* scoped */
(() => {
const { Card, Badge, Button, IconButton, Tabs, Switch, Select, Tooltip, Icon } = window.TempestEngineeringDesignSystem_1d4355;

const CHANNELS = [
  ['rotor_speed', 'rpm', '14.2', 'success'],
  ['wind_speed', 'm/s', '11.8', 'success'],
  ['wind_shear', 'm/s', '18.4', 'warning'],
  ['nacelle_temp', '°C', '41.2', 'success'],
  ['gearbox_temp', '°C', '78.9', 'warning'],
  ['pitch_angle', 'deg', '6.4', 'success'],
  ['grid_freq', 'Hz', '50.02', 'success'],
  ['power_out', 'kW', '3140', 'success'],
];

function TelemetryScreen() {
  const [tab, setTab] = React.useState('signals');
  const [live, setLive] = React.useState(true);
  const [sel, setSel] = React.useState('wind_shear');
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '260px 1fr', gap: 'var(--space-7)', maxWidth: 1240, alignItems: 'start' }}>
      <Card padding="none" title="Channels" actions={<IconButton icon="search" label="Search channels" size="sm" />}>
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          {CHANNELS.map(([name, unit, val, tone]) => {
            const active = name === sel;
            return (
              <button key={name} type="button" onClick={() => setSel(name)}
                style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px var(--space-5)', border: 0, borderLeft: `2px solid ${active ? 'var(--cyan-500)' : 'transparent'}`, background: active ? 'var(--bg-selected)' : 'transparent', cursor: 'pointer', textAlign: 'left' }}>
                <span style={{ width: 5, height: 5, borderRadius: '50%', background: tone === 'warning' ? 'var(--amber-500)' : 'var(--green-500)' }} />
                <span style={{ flex: 1, fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', color: active ? 'var(--text-heading)' : 'var(--text-muted)' }}>{name}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', color: 'var(--text-faint)' }}>{val} {unit}</span>
              </button>
            );
          })}
        </div>
      </Card>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-7)' }}>
        <Card padding="sm" eyebrow="north-ridge / array-04" title={sel} accent
          actions={<><Switch size="sm" checked={live} onChange={() => setLive(!live)} label="Live" /><Select size="sm" options={['6h', '24h', '7d']} containerStyle={{ width: 96 }} /><IconButton icon="maximize-2" label="Expand" size="sm" /></>}>
          <Tabs value={tab} onChange={setTab} size="sm" items={[{ value: 'signals', label: 'Signal' }, { value: 'residual', label: 'Residual' }, { value: 'spectrum', label: 'Spectrum' }]} style={{ marginBottom: 'var(--space-6)' }} />
          <Sparkband seed={sel.length + 3} height={180} color={sel === 'wind_shear' ? 'var(--amber-500)' : 'var(--cyan-500)'} bars={80} />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-4)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>
            <span>08:00Z</span><span>10:00Z</span><span>12:00Z</span><span>14:00Z</span>
          </div>
        </Card>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 'var(--space-5)' }}>
          <Readout label="Current" value="18.4" unit="m/s" delta="+6.1 in 10m" tone="down" />
          <Readout label="Threshold" value="16.0" unit="m/s" />
          <Readout label="Time over" value="4m 12s" />
        </div>

        <Card padding="sm" title="Unit breakdown" actions={<Badge tone="warning" dot>2 feathered</Badge>}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead><tr><Th>Unit</Th><Th align="right">Output</Th><Th align="right">Rotor</Th><Th align="right">Pitch</Th><Th>State</Th></tr></thead>
            <tbody>
              {[['04-01', '3140', '14.2', '6.4', 'Running', 'success'], ['04-02', '3090', '14.0', '6.8', 'Running', 'success'], ['04-03', '0', '0.4', '88.0', 'Feathered', 'warning'], ['04-04', '0', '0.2', '88.0', 'Feathered', 'warning']].map((r) => (
                <tr key={r[0]}><Td mono>{r[0]}</Td><Td align="right" mono>{r[1]} kW</Td><Td align="right" mono>{r[2]}</Td><Td align="right" mono>{r[3]}°</Td><Td><Badge tone={r[5]} dot>{r[4]}</Badge></Td></tr>
              ))}
            </tbody>
          </table>
        </Card>
      </div>
    </div>
  );
}
Object.assign(window, { TelemetryScreen });

})();
