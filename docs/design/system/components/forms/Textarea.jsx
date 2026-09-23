import React from 'react';

export function Textarea({ label, hint, error, rows = 4, mono = false, id, style, containerStyle, ...rest }) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, ...containerStyle }}>
      {label && <label htmlFor={fid} style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-2xs)', fontWeight: 600, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', color: 'var(--text-muted)' }}>{label}</label>}
      <textarea
        id={fid} rows={rows} onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
        style={{
          padding: '10px 12px', background: 'var(--bg-input)', border: `1px solid ${borderColor}`,
          borderRadius: 'var(--radius-sm)', boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none',
          color: 'var(--text-heading)', fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)',
          fontSize: 'var(--text-sm)', lineHeight: 'var(--leading-normal)', outline: 'none', resize: 'vertical',
          transition: 'var(--transition-control)', ...style,
        }}
        {...rest}
      />
      {(hint || error) && <span style={{ fontSize: 'var(--text-2xs)', color: error ? 'var(--status-danger)' : 'var(--text-faint)' }}>{error || hint}</span>}
    </div>
  );
}
