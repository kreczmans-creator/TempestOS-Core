import React from 'react';

export function Radio({ label, description, name, value, checked, disabled = false, onChange, style, ...rest }) {
  return (
    <label style={{ display: 'inline-flex', alignItems: 'flex-start', gap: 10, cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.45 : 1, ...style }}>
      <input type="radio" name={name} value={value} checked={!!checked} disabled={disabled} onChange={onChange} style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }} {...rest} />
      <span aria-hidden="true" style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 16, height: 16, marginTop: 2, flex: '0 0 auto',
        borderRadius: '50%', border: `1px solid ${checked ? 'var(--accent-primary)' : 'var(--border-default)'}`,
        background: 'var(--bg-input)', transition: 'var(--transition-control)',
      }}>
        {checked && <span style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--accent-primary)' }} />}
      </span>
      <span>
        {label && <span style={{ display: 'block', fontSize: 'var(--text-sm)', color: 'var(--text-heading)' }}>{label}</span>}
        {description && <span style={{ display: 'block', fontSize: 'var(--text-2xs)', color: 'var(--text-faint)', marginTop: 2 }}>{description}</span>}
      </span>
    </label>
  );
}
