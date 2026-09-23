import React from 'react';
import { Icon } from './Icon.jsx';

const TONES = {
  neutral: { color: 'var(--text-muted)', border: 'var(--border-default)', bg: 'transparent', dot: 'var(--slate-500)' },
  info:    { color: 'var(--cyan-400)', border: 'var(--cyan-a40)', bg: 'var(--cyan-a12)', dot: 'var(--cyan-500)' },
  success: { color: 'var(--green-500)', border: 'rgba(18,185,129,.4)', bg: 'rgba(18,185,129,.12)', dot: 'var(--green-500)' },
  warning: { color: 'var(--amber-500)', border: 'rgba(245,165,36,.4)', bg: 'rgba(245,165,36,.12)', dot: 'var(--amber-500)' },
  danger:  { color: 'var(--red-500)', border: 'rgba(229,72,77,.42)', bg: 'rgba(229,72,77,.12)', dot: 'var(--red-500)' },
  brand:   { color: 'var(--violet-400)', border: 'rgba(108,41,217,.45)', bg: 'var(--violet-a16)', dot: 'var(--violet-500)' },
};

export function Badge({ children, tone = 'neutral', dot = false, icon, style, ...rest }) {
  const t = TONES[tone] || TONES.neutral;
  return (
    <span
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6, height: 20, padding: '0 8px',
        borderRadius: 'var(--radius-xs)', border: `1px solid ${t.border}`, background: t.bg, color: t.color,
        fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xs)', fontWeight: 'var(--weight-semibold)',
        letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase', whiteSpace: 'nowrap', ...style,
      }}
      {...rest}
    >
      {dot && <span style={{ width: 5, height: 5, borderRadius: '50%', background: t.dot, flex: '0 0 auto' }} />}
      {icon && <Icon name={icon} size={11} />}
      {children}
    </span>
  );
}
