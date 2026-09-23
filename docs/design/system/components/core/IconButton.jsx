import React from 'react';
import { Icon } from './Icon.jsx';

const BOX = { sm: 28, md: 36, lg: 44 };
const GLYPH = { sm: 14, md: 16, lg: 20 };

export function IconButton({ icon, label, size = 'md', variant = 'ghost', active = false, disabled = false, onClick, style, ...rest }) {
  const [hover, setHover] = React.useState(false);
  const box = BOX[size] || BOX.md;
  const outline = variant === 'outline';
  return (
    <button
      type="button" aria-label={label} title={label} disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        width: box, height: box, flex: '0 0 auto', padding: 0,
        borderRadius: 'var(--radius-sm)',
        border: outline ? '1px solid var(--border-default)' : '1px solid transparent',
        background: active ? 'var(--bg-selected)' : hover && !disabled ? 'var(--bg-hover)' : 'transparent',
        color: active ? 'var(--text-accent)' : hover && !disabled ? 'var(--text-heading)' : 'var(--text-muted)',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.4 : 1,
        transition: 'var(--transition-control)', ...style,
      }}
      {...rest}
    >
      {typeof icon === 'string' ? <Icon name={icon} size={GLYPH[size] || 16} /> : icon}
    </button>
  );
}
