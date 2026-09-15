import React from 'react';
import { IconButton } from '../core/IconButton.jsx';

export function Dialog({ open = false, title, eyebrow, children, footer, onClose, width = 480, style, ...rest }) {
  if (!open) return null;
  return (
    <div
      role="presentation" onClick={onClose}
      style={{ position: 'fixed', inset: 0, zIndex: 100, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 'var(--space-8)', background: 'var(--surface-overlay)', backdropFilter: 'blur(3px)' }}
    >
      <div
        role="dialog" aria-modal="true" aria-label={typeof title === 'string' ? title : undefined}
        onClick={(e) => e.stopPropagation()}
        style={{
          width: '100%', maxWidth: width, background: 'var(--bg-surface-raised)',
          border: '1px solid var(--border-default)', borderTop: '2px solid var(--accent-primary)',
          borderRadius: 'var(--radius-md)', boxShadow: 'var(--shadow-panel)', ...style,
        }}
        {...rest}
      >
        <header style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 'var(--space-5)', padding: 'var(--space-6) var(--space-7)', borderBottom: '1px solid var(--border-subtle)' }}>
          <div>
            {eyebrow && <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xs)', fontWeight: 600, letterSpacing: 'var(--tracking-widest)', textTransform: 'uppercase', color: 'var(--text-accent)', marginBottom: 6 }}>{eyebrow}</div>}
            <h2 style={{ margin: 0, fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 600, letterSpacing: 'var(--tracking-tight)', color: 'var(--text-heading)' }}>{title}</h2>
          </div>
          {onClose && <IconButton icon="x" label="Close" size="sm" onClick={onClose} />}
        </header>
        <div style={{ padding: 'var(--space-7)', fontSize: 'var(--text-sm)', color: 'var(--text-body)' }}>{children}</div>
        {footer && <footer style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-3)', padding: 'var(--space-5) var(--space-7)', borderTop: '1px solid var(--border-subtle)', background: 'var(--bg-surface)' }}>{footer}</footer>}
      </div>
    </div>
  );
}
