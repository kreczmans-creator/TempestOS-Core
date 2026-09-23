/* scoped */
(() => {
const { Card, Badge, Button, IconButton, Tag, Input, Textarea, Icon, Tooltip } = window.TempestEngineeringDesignSystem_1d4355;

const ALERTS = [
  { id: 'ALR-4192', tone: 'warning', sev: 'Warning', title: 'Wind shear above threshold', site: 'north-ridge', asset: 'array-04', t: '14:02:11Z', owner: 'auto', detail: 'Shear reached 18.4 m/s against a 16.0 m/s limit. Units 04-03 and 04-04 feathered automatically; dispatch re-planned to hold 26.3 MW fleet output.' },
  { id: 'ALR-4191', tone: 'danger', sev: 'Critical', title: 'Site link lost', site: 'selkie-bank', asset: 'breaker B2', t: '13:51:47Z', owner: 'm.okafor', detail: 'No telemetry for 11 minutes. Last known state: 36 units running, 8.9 MW. Local controller is assumed to be in island mode.' },
  { id: 'ALR-4188', tone: 'warning', sev: 'Warning', title: 'Gearbox temperature drift', site: 'brae-point', asset: 'unit 09-14', t: '12:20:03Z', owner: 'j.reyes', detail: 'Gearbox temperature 4.1°C above the 30-day band for this unit at comparable load. Maintenance window suggested within 14 days.' },
  { id: 'ALR-4180', tone: 'neutral', sev: 'Info', title: 'Curtailment window closed', site: 'calder-flats', asset: 'site', t: '11:04:55Z', owner: 'auto', detail: 'Grid operator lifted the curtailment instruction. Output ramped back to schedule over 6 minutes.' },
];

function AlertsScreen({ onConfirmOffline }) {
  const [sel, setSel] = React.useState(ALERTS[0].id);
  const [filter, setFilter] = React.useState('all');
  const list = filter === 'all' ? ALERTS : ALERTS.filter((a) => a.tone === filter);
  const active = ALERTS.find((a) => a.id === sel);
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 400px', gap: 'var(--space-7)', maxWidth: 1240, alignItems: 'start' }}>
      <Card padding="none" title="Alert queue"
        actions={<><Input size="sm" icon="search" placeholder="Filter" containerStyle={{ width: 180 }} /><IconButton icon="refresh-cw" label="Refresh" size="sm" /></>}>
        <div style={{ display: 'flex', gap: 8, padding: 'var(--space-5)', borderBottom: '1px solid var(--border-subtle)' }}>
          {[['all', 'all'], ['danger', 'critical'], ['warning', 'warning'], ['neutral', 'info']].map(([k, label]) => (
            <Tag key={k} selected={filter === k} onClick={() => setFilter(k)}>{label}</Tag>
          ))}
        </div>
        <div>
          {list.map((a) => {
            const on = a.id === sel;
            return (
              <button key={a.id} type="button" onClick={() => setSel(a.id)}
                style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-5)', width: '100%', padding: 'var(--space-5)', border: 0, borderBottom: '1px solid var(--border-subtle)', borderLeft: `2px solid ${on ? 'var(--cyan-500)' : 'transparent'}`, background: on ? 'var(--bg-selected)' : 'transparent', cursor: 'pointer', textAlign: 'left' }}>
                <Badge tone={a.tone} dot>{a.sev}</Badge>
                <span style={{ flex: 1, minWidth: 0 }}>
                  <span style={{ display: 'block', fontSize: 'var(--text-sm)', color: 'var(--text-heading)' }}>{a.title}</span>
                  <span style={{ display: 'block', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)', marginTop: 3 }}>{a.id} · {a.site} · {a.asset}</span>
                </span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>{a.t}</span>
              </button>
            );
          })}
        </div>
      </Card>

      <Card eyebrow={active.id} title={active.title} accent={active.tone === 'danger' ? 'var(--red-500)' : 'var(--amber-500)'}
        actions={<Tooltip content="Acknowledge"><IconButton icon="check" label="Acknowledge" size="sm" variant="outline" /></Tooltip>}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 'var(--space-5)' }}>
          <Tag>{active.site}</Tag><Tag>{active.asset}</Tag><Tag>owner:{active.owner}</Tag>
        </div>
        <p style={{ margin: 0, fontSize: 'var(--text-sm)', lineHeight: 'var(--leading-relaxed)', color: 'var(--text-body)' }}>{active.detail}</p>
        <div style={{ marginTop: 'var(--space-6)', paddingTop: 'var(--space-5)', borderTop: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column', gap: 'var(--space-5)' }}>
          <Textarea label="Operator note" rows={3} placeholder="What did you do?" />
          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            <Button variant="secondary" size="sm" full>Assign to me</Button>
            <Button variant="danger" size="sm" full onClick={onConfirmOffline}>Take offline</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
Object.assign(window, { AlertsScreen });

})();
