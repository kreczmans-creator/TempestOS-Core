import React from 'react';
import { Icon } from '../core/Icon.jsx';

const H = { sm: 'var(--control-h-sm)', md: 'var(--control-h-md)', lg: 'var(--control-h-lg)' };

export function Select({ label, hint, error, options = [], size = 'md', id, style, containerStyle, ...rest }) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, ...containerStyle }}>
      {label && <label htmlFor={fid} style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-2xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-muted)' }}>{label}</label>}
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
        <select
          id={fid} onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
          style={{
            appearance: 'none', width: '100%', height: H[size] || H.md, padding: '0 32px 0 10px',
            background: 'var(--bg-input)', border: `1px solid ${borderColor}`, borderRadius: 'var(--radius-sm)',
            boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none',
            color: 'var(--text-heading)', fontFamily: 'var(--font-body)', fontSize: size === 'sm' ? 'var(--text-xs)' : 'var(--text-sm)',
            outline: 'none', cursor: 'pointer', transition: 'var(--transition-control)', ...style,
          }}
          {...rest}
        >
          {options.map((o) => {
            const opt = typeof o === 'string' ? { value: o, label: o } : o;
            return <option key={opt.value} value={opt.value}>{opt.label}</option>;
          })}
        </select>
        <Icon name="chevron-down" size={14} color="var(--text-faint)" style={{ position: 'absolute', right: 10, pointerEvents: 'none' }} />
      </div>
      {(hint || error) && <span style={{ fontSize: 'var(--text-2xs)', color: error ? 'var(--status-danger)' : 'var(--text-faint)' }}>{error || hint}</span>}
    </div>
  );
}
