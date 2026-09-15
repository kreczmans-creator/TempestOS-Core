import React from 'react';

export function Switch({ checked = false, onChange, label, disabled = false, size = 'md', style, ...rest }) {
  const w = size === 'sm' ? 30 : 38, h = size === 'sm' ? 16 : 20, k = h - 6;
  return (
    <label style={{ display: 'inline-flex', alignItems: 'center', gap: 10, cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.45 : 1, ...style }}>
      <input type="checkbox" role="switch" checked={checked} disabled={disabled} onChange={onChange} style={{ position: 'absolute', opacity: 0, width: 0, height: 0 }} {...rest} />
      <span aria-hidden="true" style={{
        position: 'relative', width: w, height: h, flex: '0 0 auto', borderRadius: 'var(--radius-pill)',
        background: checked ? 'var(--accent-primary)' : 'var(--bg-input)',
        border: `1px solid ${checked ? 'var(--accent-primary)' : 'var(--border-default)'}`,
        transition: 'var(--transition-control)',
      }}>
        <span style={{
          position: 'absolute', top: 2, left: checked ? w - k - 4 : 2, width: k, height: k, borderRadius: '50%',
          background: checked ? 'var(--navy-900)' : 'var(--slate-400)',
          transition: `left var(--dur-fast) var(--ease-standard), background-color var(--dur-fast) var(--ease-standard)`,
        }} />
      </span>
      {label && <span style={{ fontSize: 'var(--text-sm)', color: 'var(--text-heading)' }}>{label}</span>}
    </label>
  );
}
