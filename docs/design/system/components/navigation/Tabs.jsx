import React from 'react';
import { Icon } from '../core/Icon.jsx';

export function Tabs({ items = [], value, onChange, size = 'md', style, ...rest }) {
  const [hover, setHover] = React.useState(null);
  const pad = size === 'sm' ? '0 10px' : '0 14px';
  const h = size === 'sm' ? 30 : 38;
  return (
    <div role="tablist" style={{ display: 'flex', alignItems: 'stretch', gap: 2, borderBottom: '1px solid var(--border-subtle)', ...style }} {...rest}>
      {items.map((it) => {
        const active = it.value === value;
        return (
          <button
            key={it.value} role="tab" aria-selected={active} type="button"
            onClick={() => onChange && onChange(it.value)}
            onMouseEnter={() => setHover(it.value)} onMouseLeave={() => setHover(null)}
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 7, height: h, padding: pad,
              border: 0, borderBottom: `2px solid ${active ? 'var(--accent-primary)' : 'transparent'}`,
              background: !active && hover === it.value ? 'var(--bg-hover)' : 'transparent',
              color: active ? 'var(--text-heading)' : 'var(--text-muted)',
              fontFamily: 'var(--font-display)', fontSize: size === 'sm' ? 'var(--text-2xs)' : 'var(--text-xs)',
              fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase',
              cursor: 'pointer', marginBottom: -1, transition: 'var(--transition-control)',
            }}
          >
            {it.icon && <Icon name={it.icon} size={14} />}
            {it.label}
            {it.count != null && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)', letterSpacing: 0 }}>{it.count}</span>}
          </button>
        );
      })}
    </div>
  );
}
