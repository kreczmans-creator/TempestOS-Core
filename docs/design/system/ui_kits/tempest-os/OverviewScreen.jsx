/* scoped */
(() => {
const { Card, Badge, Button, IconButton, Tag, Icon, Tabs } = window.TempestEngineeringDesignSystem_1d4355;

const SITES = [
  ['north-ridge', 'Array 01–04', 48, '12.4', 'Nominal', 'success'],
  ['calder-flats', 'Array 05–07', 36, '9.1', 'Nominal', 'success'],
  ['brae-point', 'Array 08–09', 24, '4.8', 'Degraded', 'warning'],
  ['selkie-bank', 'Array 10–12', 36, '0.0', 'Offline', 'danger'],
];

function OverviewScreen({ onOpenAlerts }) {
  const [tab, setTab] = React.useState('sites');
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-7)', maxWidth: 1240 }}>
      <div style={{ display: 'flex', gap: 'var(--space-5)' }}>
        <Readout label="Fleet output" value="26.3" unit="MW" delta="+4.2% vs 1h" />
        <Readout label="Availability" value="98.6" unit="%" delta="-0.4pt" tone="down" />
        <Readout label="Units reporting" value="142" unit="/ 144" />
        <Readout label="Open alerts" value="3" delta="1 new" tone="down" />
      </div>

      <Card eyebrow="Last 6 hours" title="Fleet output" accent
        actions={<><Tag>1h</Tag><Tag selected onClick={() => {}}>6h</Tag><Tag>24h</Tag><IconButton icon="download" label="Export" size="sm" /></>}>
        <Sparkband seed={2.1} height={150} />
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-4)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>
          <span>08:00Z</span><span>10:00Z</span><span>12:00Z</span><span>14:00Z</span>
        </div>
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: '1.6fr 1fr', gap: 'var(--space-7)', alignItems: 'start' }}>
        <Card padding="none" title="Sites" actions={<Button variant="secondary" size="sm" iconLeft={<Icon name="plus" size={12} />}>Add site</Button>}>
          <Tabs value={tab} onChange={setTab} size="sm" style={{ padding: '0 var(--space-5)' }} items={[{ value: 'sites', label: 'Sites', count: 4 }, { value: 'arrays', label: 'Arrays', count: 12 }]} />
          <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 'var(--space-5)' }}>
            <thead><tr><Th>Site</Th><Th>Arrays</Th><Th align="right">Units</Th><Th align="right">Output</Th><Th>State</Th><Th /></tr></thead>
            <tbody>
              {SITES.map(([id, arrays, units, out, state, tone]) => (
                <tr key={id}>
                  <Td mono>{id}</Td><Td dim>{arrays}</Td><Td align="right" mono>{units}</Td>
                  <Td align="right" mono>{out} MW</Td>
                  <Td><Badge tone={tone} dot>{state}</Badge></Td>
                  <Td align="right"><IconButton icon="chevron-right" label={`Open ${id}`} size="sm" /></Td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-7)' }}>
          <Card eyebrow="Live" title="Event log" padding="sm" actions={<IconButton icon="pause" label="Pause stream" size="sm" />}>
            <div style={{ display: 'flex', flexDirection: 'column' }}>
              <LogLine t="14:02:11Z" level="WARN" msg="array-04 wind_shear=18.4m/s → feather" />
              <LogLine t="14:02:12Z" level="INFO" msg="dispatch replan ok (142/144)" />
              <LogLine t="13:58:04Z" level="OK" msg="deploy tempest-os 4.2.1 complete" />
              <LogLine t="13:51:47Z" level="ERR" msg="selkie-bank link lost (breaker B2)" />
              <LogLine t="13:44:02Z" level="INFO" msg="curtailment window closed" />
              <LogLine t="13:40:19Z" level="INFO" msg="grid signal 50.02Hz nominal" />
            </div>
          </Card>
          <Card eyebrow="Requires action" title="Alerts" padding="sm" accent="var(--amber-500)"
            actions={<Button variant="ghost" size="sm" onClick={onOpenAlerts}>Open queue</Button>}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
              {[['warning', 'Wind shear — array 04', '14:02Z'], ['danger', 'Link lost — selkie-bank', '13:51Z'], ['warning', 'Gearbox temp — unit 09-14', '12:20Z']].map(([tone, label, t]) => (
                <div key={label} style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)' }}>
                  <Badge tone={tone} dot />
                  <span style={{ flex: 1, fontSize: 'var(--text-sm)', color: 'var(--text-heading)' }}>{label}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>{t}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}
Object.assign(window, { OverviewScreen });

})();
