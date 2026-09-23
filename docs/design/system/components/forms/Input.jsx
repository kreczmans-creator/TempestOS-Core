import React from 'react';
import { Icon } from '../core/Icon.jsx';

const H = { sm: 'var(--control-h-sm)', md: 'var(--control-h-md)', lg: 'var(--control-h-lg)' };

export function Input({ label, hint, error, icon, suffix, size = 'md', mono = false, id, style, containerStyle, ...rest }) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, ...containerStyle }}>
      {label && <label htmlFor={fid} style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-2xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-muted)' }}>{label}</label>}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, height: H[size] || H.md, padding: '0 10px',
        background: 'var(--bg-input)', border: `1px solid ${borderColor}`, borderRadius: 'var(--radius-sm)',
        boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none', transition: 'var(--transition-control)',
      }}>
        {icon && <Icon name={icon} size={14} color="var(--text-faint)" />}
        <input
          id={fid} onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
          style={{
            flex: 1, minWidth: 0, height: '100%', border: 0, background: 'transparent', outline: 'none',
            color: 'var(--text-heading)', fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)',
            fontSize: size === 'sm' ? 'var(--text-xs)' : 'var(--text-sm)', ...style,
          }}
          {...rest}
        />
        {suffix && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', color: 'var(--text-faint)' }}>{suffix}</span>}
      </div>
      {(hint || error) && <span style={{ fontSize: 'var(--text-2xs)', color: error ? 'var(--status-danger)' : 'var(--text-faint)' }}>{error || hint}</span>}
    </div>
  );
}
