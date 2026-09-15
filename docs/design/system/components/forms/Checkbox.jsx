import React from 'react';
import { Icon } from '../core/Icon.jsx';

export function Checkbox({ label, description, checked, indeterminate = false, disabled = false, onChange, style, ...rest }) {
  const on = checked || indeterminate;
  return (
    <label style={{ display: 'inline-flex', alignItems: 'flex-start', gap: 10, cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.45 : 1, ...style }}>
      <input type="checkbox" checked={!!checked} disabled={disabled} onChange={onChange} style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }} {...rest} />
      <span aria-hidden="true" style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 16, height: 16, marginTop: 2, flex: '0 0 auto',
        borderRadius: 'var(--radius-xs)', border: `1px solid ${on ? 'var(--accent-primary)' : 'var(--border-default)'}`,
        background: on ? 'var(--accent-primary)' : 'var(--bg-input)', color: 'var(--on-accent)', transition: 'var(--transition-control)',
      }}>
        {indeterminate ? <span style={{ width: 8, height: 2, background: 'var(--on-accent)' }} /> : checked ? <Icon name="check" size={11} /> : null}
      </span>
      <span>
        {label && <span style={{ display: 'block', fontSize: 'var(--text-sm)', color: 'var(--text-heading)' }}>{label}</span>}
        {description && <span style={{ display: 'block', fontSize: 'var(--text-2xs)', color: 'var(--text-faint)', marginTop: 2 }}>{description}</span>}
      </span>
    </label>
  );
}
