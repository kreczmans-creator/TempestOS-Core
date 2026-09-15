Underline tabs — in-page view switching; the active tab carries a 2px cyan rule.

```jsx
<Tabs value={tab} onChange={setTab} items={[
  { value: 'overview', label: 'Overview', icon: 'gauge' },
  { value: 'alerts', label: 'Alerts', count: 3 },
]} />
```

Not for primary app navigation (that's the sidebar rail in the Tempest OS kit).
