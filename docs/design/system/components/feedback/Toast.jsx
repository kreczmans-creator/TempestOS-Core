import React from 'react';
import { Icon } from '../core/Icon.jsx';
import { IconButton } from '../core/IconButton.jsx';

const TONES = {
  info:    { accent: 'var(--cyan-500)', icon: 'info' },
  success: { accent: 'var(--green-500)', icon: 'check-circle' },
  warning: { accent: 'var(--amber-500)', icon: 'alert-triangle' },
  danger:  { accent: 'var(--red-500)', icon: 'alert-octagon' },
};

export function Toast({ title, children, tone = 'info', action, onClose, timestamp, style, ...rest }) {
  const t = TONES[tone] || TONES.info;
  return (
    <div
      role="status"
      style={{
        display: 'flex', alignItems: 'flex-start', gap: 'var(--space-4)', width: 380, padding: 'var(--space-5)',
        background: 'var(--bg-surface-raised)', border: '1px solid var(--border-default)', borderLeft: `2px solid ${t.accent}`,
        borderRadius: 'var(--radius-sm)', boxShadow: 'var(--shadow-lg)', ...style,
      }}
      {...rest}
    >
      <Icon name={t.icon} size={16} color={t.accent} style={{ marginTop: 1 }} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
          <span style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-sm)', fontWeight: 600, color: 'var(--text-heading)' }}>{title}</span>
          {timestamp && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', color: 'var(--text-faint)' }}>{timestamp}</span>}
        </div>
        {children && <div style={{ marginTop: 4, fontSize: 'var(--text-xs)', color: 'var(--text-muted)', lineHeight: 'var(--leading-normal)' }}>{children}</div>}
        {action && <div style={{ marginTop: 'var(--space-4)' }}>{action}</div>}
      </div>
      {onClose && <IconButton icon="x" label="Dismiss" size="sm" onClick={onClose} />}
    </div>
  );
}
