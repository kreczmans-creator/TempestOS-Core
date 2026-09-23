import React from 'react';
import { Icon } from './Icon.jsx';

export function Tag({ children, onRemove, onClick, selected = false, style, ...rest }) {
  const [hover, setHover] = React.useState(false);
  const interactive = !!onClick;
  return (
    <span
      onClick={onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6, height: 24, padding: onRemove ? '0 4px 0 10px' : '0 10px',
        borderRadius: 'var(--radius-sm)',
        border: `1px solid ${selected ? 'var(--border-accent)' : 'var(--border-subtle)'}`,
        background: selected ? 'var(--bg-selected)' : hover && interactive ? 'var(--bg-hover)' : 'var(--bg-surface-raised)',
        color: selected ? 'var(--text-accent)' : 'var(--text-body)',
        fontFamily: 'var(--font-mono)', fontSize: 'var(--text-2xs)', letterSpacing: 'var(--tracking-normal)',
        cursor: interactive ? 'pointer' : 'default', transition: 'var(--transition-control)', ...style,
      }}
      {...rest}
    >
      {children}
      {onRemove && (
        <button type="button" aria-label="Remove" onClick={(e) => { e.stopPropagation(); onRemove(e); }}
          style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 16, height: 16, padding: 0, border: 0, background: 'transparent', color: 'var(--text-faint)', cursor: 'pointer', borderRadius: 'var(--radius-xs)' }}>
          <Icon name="x" size={11} />
        </button>
      )}
    </span>
  );
}
