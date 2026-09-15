import React from 'react';

const PADS = { none: 0, sm: 'var(--space-5)', md: 'var(--space-7)', lg: 'var(--space-8)' };

export function Card({ children, title, eyebrow, actions, padding = 'md', accent, interactive = false, notch = false, style, ...rest }) {
  const [hover, setHover] = React.useState(false);
  return (
    <section
      onMouseEnter={() => setHover(true)} onMouseLeave={() => setHover(false)}
      style={{
        position: 'relative', background: 'var(--surface-card)',
        border: `1px solid ${interactive && hover ? 'var(--border-accent)' : 'var(--surface-card-border)'}`,
        borderRadius: notch ? 0 : 'var(--radius-md)', clipPath: notch ? 'var(--notch-clip-tr)' : undefined,
        borderTop: accent ? `2px solid ${accent === true ? 'var(--accent-primary)' : accent}` : undefined,
        transition: 'var(--transition-control)', cursor: interactive ? 'pointer' : undefined, ...style,
      }}
      {...rest}
    >
      {(title || eyebrow || actions) && (
        <header style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 'var(--space-5)', padding: `var(--space-5) ${PADS[padding] === 0 ? 'var(--space-5)' : PADS[padding]}`, borderBottom: '1px solid var(--border-subtle)' }}>
          <div>
            {eyebrow && <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xs)', fontWeight: 600, letterSpacing: 'var(--tracking-widest)', textTransform: 'uppercase', color: 'var(--text-accent)', marginBottom: 6 }}>{eyebrow}</div>}
            {title && <h3 style={{ margin: 0, fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 600, letterSpacing: 'var(--tracking-tight)', color: 'var(--text-heading)' }}>{title}</h3>}
          </div>
          {actions && <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)', flex: '0 0 auto' }}>{actions}</div>}
        </header>
      )}
      <div style={{ padding: PADS[padding] }}>{children}</div>
    </section>
  );
}
