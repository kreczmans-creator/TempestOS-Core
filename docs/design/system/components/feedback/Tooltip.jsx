import React from 'react';

export function Tooltip({ children, content, placement = 'top', style, ...rest }) {
  const [show, setShow] = React.useState(false);
  const pos = {
    top: { bottom: '100%', left: '50%', transform: 'translate(-50%,-6px)' },
    bottom: { top: '100%', left: '50%', transform: 'translate(-50%,6px)' },
    left: { right: '100%', top: '50%', transform: 'translate(-6px,-50%)' },
    right: { left: '100%', top: '50%', transform: 'translate(6px,-50%)' },
  }[placement];
  return (
    <span
      style={{ position: 'relative', display: 'inline-flex', ...style }}
      onMouseEnter={() => setShow(true)} onMouseLeave={() => setShow(false)}
      onFocus={() => setShow(true)} onBlur={() => setShow(false)}
      {...rest}
    >
      {children}
      {show && (
        <span role="tooltip" style={{
          position: 'absolute', zIndex: 50, ...pos, padding: '5px 8px', maxWidth: 240, width: 'max-content',
          background: 'var(--navy-900)', border: '1px solid var(--border-default)', borderRadius: 'var(--radius-xs)',
          color: 'var(--paper-050)', fontFamily: 'var(--font-mono)', fontSize: 'var(--text-3xs)', lineHeight: 1.4,
          pointerEvents: 'none', boxShadow: 'var(--shadow-md)',
        }}>{content}</span>
      )}
    </span>
  );
}
