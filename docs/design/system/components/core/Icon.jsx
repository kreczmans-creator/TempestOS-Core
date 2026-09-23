import React from 'react';

const CDN = 'https://unpkg.com/lucide-static@0.428.0/icons/';

/* Tempest has no proprietary glyph set in the brand pack. Iconography is Lucide
   (round caps + joins, matching the logo mark's stroke terminals), masked so the
   glyph inherits currentColor. */
export function Icon({ name, size = 16, color = 'currentColor', title, style, ...rest }) {
  const mask = `url("${CDN}${name}.svg") center / contain no-repeat`;
  return (
    <span
      role="img"
      aria-label={title || name}
      style={{ display: 'inline-block', width: size, height: size, flex: '0 0 auto', backgroundColor: color, WebkitMask: mask, mask, ...style }}
      {...rest}
    />
  );
}
