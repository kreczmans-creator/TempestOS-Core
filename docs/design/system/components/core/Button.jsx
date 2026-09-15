import React from 'react';

const SIZES = {
  sm: { height: 'var(--control-h-sm)', padding: '0 12px', fontSize: 'var(--text-2xs)', gap: 6 },
  md: { height: 'var(--control-h-md)', padding: '0 16px', fontSize: 'var(--text-xs)', gap: 8 },
  lg: { height: 'var(--control-h-lg)', padding: '0 24px', fontSize: 'var(--text-sm)', gap: 10 },
};

const VARIANTS = {
  primary: {
    base: { background: 'var(--accent-primary)', color: 'var(--on-accent)', border: '1px solid transparent' },
    hover: { background: 'var(--accent-primary-hover)' },
    active: { background: 'var(--accent-primary-press)' },
  },
  secondary: {
    base: { background: 'transparent', color: 'var(--text-heading)', border: '1px solid var(--border-default)' },
    hover: { background: 'var(--bg-hover)', borderColor: 'var(--border-accent)', color: 'var(--text-heading)' },
    active: { background: 'var(--bg-active)' },
  },
  ghost: {
    base: { background: 'transparent', color: 'var(--text-body)', border: '1px solid transparent' },
    hover: { background: 'var(--bg-hover)', color: 'var(--text-heading)' },
    active: { background: 'var(--bg-active)' },
  },
  danger: {
    base: { background: 'var(--status-danger)', color: 'var(--paper-050)', border: '1px solid transparent' },
    hover: { background: '#f05257' },
    active: { background: '#c93b40' },
  },
};

export function Button({
  children, variant = 'primary', size = 'md', iconLeft, iconRight,
  disabled = false, notch = false, full = false, type = 'button', style, onClick, ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const [press, setPress] = React.useState(false);
  const v = VARIANTS[variant] || VARIANTS.primary;
  const s = SIZES[size] || SIZES.md;
  const styles = {
    display: full ? 'flex' : 'inline-flex', width: full ? '100%' : undefined,
    alignItems: 'center', justifyContent: 'center', gap: s.gap,
    height: s.height, padding: s.padding,
    fontFamily: 'var(--font-display)', fontWeight: 'var(--weight-semibold)',
    fontSize: s.fontSize, letterSpacing: 'var(--tracking-wider)', textTransform: 'uppercase',
    whiteSpace: 'nowrap', cursor: disabled ? 'not-allowed' : 'pointer',
    borderRadius: notch ? 0 : 'var(--radius-sm)',
    clipPath: notch ? 'var(--notch-clip-tr)' : undefined,
    transition: 'var(--transition-control)',
    opacity: disabled ? 0.4 : 1,
    ...v.base,
    ...(!disabled && hover ? v.hover : null),
    ...(!disabled && press ? v.active : null),
    ...style,
  };
  return (
    <button
      type={type} disabled={disabled} onClick={disabled ? undefined : onClick} style={styles}
      onMouseEnter={() => setHover(true)} onMouseLeave={() => { setHover(false); setPress(false); }}
      onMouseDown={() => setPress(true)} onMouseUp={() => setPress(false)}
      {...rest}
    >
      {iconLeft}{children}{iconRight}
    </button>
  );
}
