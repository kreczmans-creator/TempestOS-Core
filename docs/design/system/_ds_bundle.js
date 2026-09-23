/* @ds-bundle: {"format":4,"namespace":"TempestEngineeringDesignSystem_1d4355","components":[{"name":"Badge","sourcePath":"components/core/Badge.jsx"},{"name":"Button","sourcePath":"components/core/Button.jsx"},{"name":"Card","sourcePath":"components/core/Card.jsx"},{"name":"Icon","sourcePath":"components/core/Icon.jsx"},{"name":"IconButton","sourcePath":"components/core/IconButton.jsx"},{"name":"Tag","sourcePath":"components/core/Tag.jsx"},{"name":"Dialog","sourcePath":"components/feedback/Dialog.jsx"},{"name":"Toast","sourcePath":"components/feedback/Toast.jsx"},{"name":"Tooltip","sourcePath":"components/feedback/Tooltip.jsx"},{"name":"Checkbox","sourcePath":"components/forms/Checkbox.jsx"},{"name":"Input","sourcePath":"components/forms/Input.jsx"},{"name":"Radio","sourcePath":"components/forms/Radio.jsx"},{"name":"Select","sourcePath":"components/forms/Select.jsx"},{"name":"Switch","sourcePath":"components/forms/Switch.jsx"},{"name":"Textarea","sourcePath":"components/forms/Textarea.jsx"},{"name":"Tabs","sourcePath":"components/navigation/Tabs.jsx"}],"sourceHashes":{"components/core/Badge.jsx":"95ecb91b1258","components/core/Button.jsx":"1fa266942951","components/core/Card.jsx":"5a9eb5568abb","components/core/Icon.jsx":"738a02b04e17","components/core/IconButton.jsx":"a021c04c7072","components/core/Tag.jsx":"96bebae5d1ae","components/feedback/Dialog.jsx":"f9e18db8fe44","components/feedback/Toast.jsx":"84554d75e312","components/feedback/Tooltip.jsx":"c27dcc8503dd","components/forms/Checkbox.jsx":"1c1a34085ea1","components/forms/Input.jsx":"0fd0574e2ce1","components/forms/Radio.jsx":"7e052dcfc22a","components/forms/Select.jsx":"3e714cab4c76","components/forms/Switch.jsx":"e65c5abb9e87","components/forms/Textarea.jsx":"5dfba1671250","components/navigation/Tabs.jsx":"3606b2c5fcc4","ui_kits/tempest-os/AlertsScreen.jsx":"94845ec8fcbe","ui_kits/tempest-os/AppShell.jsx":"2327315f74da","ui_kits/tempest-os/LandingIntro.jsx":"b318c0735de6","ui_kits/tempest-os/LoginScreen.jsx":"b6d3cb642c8f","ui_kits/tempest-os/OverviewScreen.jsx":"0ac5167cbcbb","ui_kits/tempest-os/SplashScreen.jsx":"def94b7cafec","ui_kits/tempest-os/TelemetryScreen.jsx":"1bd78f70c644","ui_kits/tempest-os/parts.jsx":"d64dc920e887","ui_kits/tempest-os/tempest-boot.js":"dae45b6a7bd5","ui_kits/tempest-web/Capabilities.jsx":"1f47aea9767c","ui_kits/tempest-web/Hero.jsx":"b8e25f9a8d39","ui_kits/tempest-web/Numbers.jsx":"b54f86a71730","ui_kits/tempest-web/SiteFooter.jsx":"dcbbc2349db5","ui_kits/tempest-web/SiteHeader.jsx":"4107850b0d66"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.TempestEngineeringDesignSystem_1d4355 = window.TempestEngineeringDesignSystem_1d4355 || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/core/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const SIZES = {
  sm: {
    height: 'var(--control-h-sm)',
    padding: '0 12px',
    fontSize: 'var(--text-2xs)',
    gap: 6
  },
  md: {
    height: 'var(--control-h-md)',
    padding: '0 16px',
    fontSize: 'var(--text-xs)',
    gap: 8
  },
  lg: {
    height: 'var(--control-h-lg)',
    padding: '0 24px',
    fontSize: 'var(--text-sm)',
    gap: 10
  }
};
const VARIANTS = {
  primary: {
    base: {
      background: 'var(--accent-primary)',
      color: 'var(--on-accent)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--accent-primary-hover)'
    },
    active: {
      background: 'var(--accent-primary-press)'
    }
  },
  secondary: {
    base: {
      background: 'transparent',
      color: 'var(--text-heading)',
      border: '1px solid var(--border-default)'
    },
    hover: {
      background: 'var(--bg-hover)',
      borderColor: 'var(--border-accent)',
      color: 'var(--text-heading)'
    },
    active: {
      background: 'var(--bg-active)'
    }
  },
  ghost: {
    base: {
      background: 'transparent',
      color: 'var(--text-body)',
      border: '1px solid transparent'
    },
    hover: {
      background: 'var(--bg-hover)',
      color: 'var(--text-heading)'
    },
    active: {
      background: 'var(--bg-active)'
    }
  },
  danger: {
    base: {
      background: 'var(--status-danger)',
      color: 'var(--paper-050)',
      border: '1px solid transparent'
    },
    hover: {
      background: '#f05257'
    },
    active: {
      background: '#c93b40'
    }
  }
};
function Button({
  children,
  variant = 'primary',
  size = 'md',
  iconLeft,
  iconRight,
  disabled = false,
  notch = false,
  full = false,
  type = 'button',
  style,
  onClick,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const [press, setPress] = React.useState(false);
  const v = VARIANTS[variant] || VARIANTS.primary;
  const s = SIZES[size] || SIZES.md;
  const styles = {
    display: full ? 'flex' : 'inline-flex',
    width: full ? '100%' : undefined,
    alignItems: 'center',
    justifyContent: 'center',
    gap: s.gap,
    height: s.height,
    padding: s.padding,
    fontFamily: 'var(--font-display)',
    fontWeight: 'var(--weight-semibold)',
    fontSize: s.fontSize,
    letterSpacing: 'var(--tracking-wider)',
    textTransform: 'uppercase',
    whiteSpace: 'nowrap',
    cursor: disabled ? 'not-allowed' : 'pointer',
    borderRadius: notch ? 0 : 'var(--radius-sm)',
    clipPath: notch ? 'var(--notch-clip-tr)' : undefined,
    transition: 'var(--transition-control)',
    opacity: disabled ? 0.4 : 1,
    ...v.base,
    ...(!disabled && hover ? v.hover : null),
    ...(!disabled && press ? v.active : null),
    ...style
  };
  return /*#__PURE__*/React.createElement("button", _extends({
    type: type,
    disabled: disabled,
    onClick: disabled ? undefined : onClick,
    style: styles,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => {
      setHover(false);
      setPress(false);
    },
    onMouseDown: () => setPress(true),
    onMouseUp: () => setPress(false)
  }, rest), iconLeft, children, iconRight);
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Button.jsx", error: String((e && e.message) || e) }); }

// components/core/Card.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const PADS = {
  none: 0,
  sm: 'var(--space-5)',
  md: 'var(--space-7)',
  lg: 'var(--space-8)'
};
function Card({
  children,
  title,
  eyebrow,
  actions,
  padding = 'md',
  accent,
  interactive = false,
  notch = false,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  return /*#__PURE__*/React.createElement("section", _extends({
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      position: 'relative',
      background: 'var(--surface-card)',
      border: `1px solid ${interactive && hover ? 'var(--border-accent)' : 'var(--surface-card-border)'}`,
      borderRadius: notch ? 0 : 'var(--radius-md)',
      clipPath: notch ? 'var(--notch-clip-tr)' : undefined,
      borderTop: accent ? `2px solid ${accent === true ? 'var(--accent-primary)' : accent}` : undefined,
      transition: 'var(--transition-control)',
      cursor: interactive ? 'pointer' : undefined,
      ...style
    }
  }, rest), (title || eyebrow || actions) && /*#__PURE__*/React.createElement("header", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      gap: 'var(--space-5)',
      padding: `var(--space-5) ${PADS[padding] === 0 ? 'var(--space-5)' : PADS[padding]}`,
      borderBottom: '1px solid var(--border-subtle)'
    }
  }, /*#__PURE__*/React.createElement("div", null, eyebrow && /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-3xs)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-widest)',
      textTransform: 'uppercase',
      color: 'var(--text-accent)',
      marginBottom: 6
    }
  }, eyebrow), title && /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-lg)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-tight)',
      color: 'var(--text-heading)'
    }
  }, title)), actions && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--space-2)',
      flex: '0 0 auto'
    }
  }, actions)), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: PADS[padding]
    }
  }, children));
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Card.jsx", error: String((e && e.message) || e) }); }

// components/core/Icon.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const CDN = 'https://unpkg.com/lucide-static@0.428.0/icons/';

/* Tempest has no proprietary glyph set in the brand pack. Iconography is Lucide
   (round caps + joins, matching the logo mark's stroke terminals), masked so the
   glyph inherits currentColor. */
function Icon({
  name,
  size = 16,
  color = 'currentColor',
  title,
  style,
  ...rest
}) {
  const mask = `url("${CDN}${name}.svg") center / contain no-repeat`;
  return /*#__PURE__*/React.createElement("span", _extends({
    role: "img",
    "aria-label": title || name,
    style: {
      display: 'inline-block',
      width: size,
      height: size,
      flex: '0 0 auto',
      backgroundColor: color,
      WebkitMask: mask,
      mask,
      ...style
    }
  }, rest));
}
Object.assign(__ds_scope, { Icon });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Icon.jsx", error: String((e && e.message) || e) }); }

// components/core/Badge.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const TONES = {
  neutral: {
    color: 'var(--text-muted)',
    border: 'var(--border-default)',
    bg: 'transparent',
    dot: 'var(--slate-500)'
  },
  info: {
    color: 'var(--cyan-400)',
    border: 'var(--cyan-a40)',
    bg: 'var(--cyan-a12)',
    dot: 'var(--cyan-500)'
  },
  success: {
    color: 'var(--green-500)',
    border: 'rgba(18,185,129,.4)',
    bg: 'rgba(18,185,129,.12)',
    dot: 'var(--green-500)'
  },
  warning: {
    color: 'var(--amber-500)',
    border: 'rgba(245,165,36,.4)',
    bg: 'rgba(245,165,36,.12)',
    dot: 'var(--amber-500)'
  },
  danger: {
    color: 'var(--red-500)',
    border: 'rgba(229,72,77,.42)',
    bg: 'rgba(229,72,77,.12)',
    dot: 'var(--red-500)'
  },
  brand: {
    color: 'var(--violet-400)',
    border: 'rgba(108,41,217,.45)',
    bg: 'var(--violet-a16)',
    dot: 'var(--violet-500)'
  }
};
function Badge({
  children,
  tone = 'neutral',
  dot = false,
  icon,
  style,
  ...rest
}) {
  const t = TONES[tone] || TONES.neutral;
  return /*#__PURE__*/React.createElement("span", _extends({
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 6,
      height: 20,
      padding: '0 8px',
      borderRadius: 'var(--radius-xs)',
      border: `1px solid ${t.border}`,
      background: t.bg,
      color: t.color,
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-3xs)',
      fontWeight: 'var(--weight-semibold)',
      letterSpacing: 'var(--tracking-wider)',
      textTransform: 'uppercase',
      whiteSpace: 'nowrap',
      ...style
    }
  }, rest), dot && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 5,
      height: 5,
      borderRadius: '50%',
      background: t.dot,
      flex: '0 0 auto'
    }
  }), icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: 11
  }), children);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Badge.jsx", error: String((e && e.message) || e) }); }

// components/core/IconButton.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const BOX = {
  sm: 28,
  md: 36,
  lg: 44
};
const GLYPH = {
  sm: 14,
  md: 16,
  lg: 20
};
function IconButton({
  icon,
  label,
  size = 'md',
  variant = 'ghost',
  active = false,
  disabled = false,
  onClick,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const box = BOX[size] || BOX.md;
  const outline = variant === 'outline';
  return /*#__PURE__*/React.createElement("button", _extends({
    type: "button",
    "aria-label": label,
    title: label,
    disabled: disabled,
    onClick: disabled ? undefined : onClick,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: box,
      height: box,
      flex: '0 0 auto',
      padding: 0,
      borderRadius: 'var(--radius-sm)',
      border: outline ? '1px solid var(--border-default)' : '1px solid transparent',
      background: active ? 'var(--bg-selected)' : hover && !disabled ? 'var(--bg-hover)' : 'transparent',
      color: active ? 'var(--text-accent)' : hover && !disabled ? 'var(--text-heading)' : 'var(--text-muted)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.4 : 1,
      transition: 'var(--transition-control)',
      ...style
    }
  }, rest), typeof icon === 'string' ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: GLYPH[size] || 16
  }) : icon);
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/core/Tag.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Tag({
  children,
  onRemove,
  onClick,
  selected = false,
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(false);
  const interactive = !!onClick;
  return /*#__PURE__*/React.createElement("span", _extends({
    onClick: onClick,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 6,
      height: 24,
      padding: onRemove ? '0 4px 0 10px' : '0 10px',
      borderRadius: 'var(--radius-sm)',
      border: `1px solid ${selected ? 'var(--border-accent)' : 'var(--border-subtle)'}`,
      background: selected ? 'var(--bg-selected)' : hover && interactive ? 'var(--bg-hover)' : 'var(--bg-surface-raised)',
      color: selected ? 'var(--text-accent)' : 'var(--text-body)',
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--text-2xs)',
      letterSpacing: 'var(--tracking-normal)',
      cursor: interactive ? 'pointer' : 'default',
      transition: 'var(--transition-control)',
      ...style
    }
  }, rest), children, onRemove && /*#__PURE__*/React.createElement("button", {
    type: "button",
    "aria-label": "Remove",
    onClick: e => {
      e.stopPropagation();
      onRemove(e);
    },
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: 16,
      height: 16,
      padding: 0,
      border: 0,
      background: 'transparent',
      color: 'var(--text-faint)',
      cursor: 'pointer',
      borderRadius: 'var(--radius-xs)'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "x",
    size: 11
  })));
}
Object.assign(__ds_scope, { Tag });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/core/Tag.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Dialog.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Dialog({
  open = false,
  title,
  eyebrow,
  children,
  footer,
  onClose,
  width = 480,
  style,
  ...rest
}) {
  if (!open) return null;
  return /*#__PURE__*/React.createElement("div", {
    role: "presentation",
    onClick: onClose,
    style: {
      position: 'fixed',
      inset: 0,
      zIndex: 100,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: 'var(--space-8)',
      background: 'var(--surface-overlay)',
      backdropFilter: 'blur(3px)'
    }
  }, /*#__PURE__*/React.createElement("div", _extends({
    role: "dialog",
    "aria-modal": "true",
    "aria-label": typeof title === 'string' ? title : undefined,
    onClick: e => e.stopPropagation(),
    style: {
      width: '100%',
      maxWidth: width,
      background: 'var(--bg-surface-raised)',
      border: '1px solid var(--border-default)',
      borderTop: '2px solid var(--accent-primary)',
      borderRadius: 'var(--radius-md)',
      boxShadow: 'var(--shadow-panel)',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement("header", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      justifyContent: 'space-between',
      gap: 'var(--space-5)',
      padding: 'var(--space-6) var(--space-7)',
      borderBottom: '1px solid var(--border-subtle)'
    }
  }, /*#__PURE__*/React.createElement("div", null, eyebrow && /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-3xs)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-widest)',
      textTransform: 'uppercase',
      color: 'var(--text-accent)',
      marginBottom: 6
    }
  }, eyebrow), /*#__PURE__*/React.createElement("h2", {
    style: {
      margin: 0,
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-xl)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-tight)',
      color: 'var(--text-heading)'
    }
  }, title)), onClose && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "x",
    label: "Close",
    size: "sm",
    onClick: onClose
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 'var(--space-7)',
      fontSize: 'var(--text-sm)',
      color: 'var(--text-body)'
    }
  }, children), footer && /*#__PURE__*/React.createElement("footer", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end',
      gap: 'var(--space-3)',
      padding: 'var(--space-5) var(--space-7)',
      borderTop: '1px solid var(--border-subtle)',
      background: 'var(--bg-surface)'
    }
  }, footer)));
}
Object.assign(__ds_scope, { Dialog });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Dialog.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Toast.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const TONES = {
  info: {
    accent: 'var(--cyan-500)',
    icon: 'info'
  },
  success: {
    accent: 'var(--green-500)',
    icon: 'check-circle'
  },
  warning: {
    accent: 'var(--amber-500)',
    icon: 'alert-triangle'
  },
  danger: {
    accent: 'var(--red-500)',
    icon: 'alert-octagon'
  }
};
function Toast({
  title,
  children,
  tone = 'info',
  action,
  onClose,
  timestamp,
  style,
  ...rest
}) {
  const t = TONES[tone] || TONES.info;
  return /*#__PURE__*/React.createElement("div", _extends({
    role: "status",
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 'var(--space-4)',
      width: 380,
      padding: 'var(--space-5)',
      background: 'var(--bg-surface-raised)',
      border: '1px solid var(--border-default)',
      borderLeft: `2px solid ${t.accent}`,
      borderRadius: 'var(--radius-sm)',
      boxShadow: 'var(--shadow-lg)',
      ...style
    }
  }, rest), /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: t.icon,
    size: 16,
    color: t.accent,
    style: {
      marginTop: 1
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'baseline',
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-sm)',
      fontWeight: 600,
      color: 'var(--text-heading)'
    }
  }, title), timestamp && /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--text-3xs)',
      color: 'var(--text-faint)'
    }
  }, timestamp)), children && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 4,
      fontSize: 'var(--text-xs)',
      color: 'var(--text-muted)',
      lineHeight: 'var(--leading-normal)'
    }
  }, children), action && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 'var(--space-4)'
    }
  }, action)), onClose && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "x",
    label: "Dismiss",
    size: "sm",
    onClick: onClose
  }));
}
Object.assign(__ds_scope, { Toast });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Toast.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Tooltip.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Tooltip({
  children,
  content,
  placement = 'top',
  style,
  ...rest
}) {
  const [show, setShow] = React.useState(false);
  const pos = {
    top: {
      bottom: '100%',
      left: '50%',
      transform: 'translate(-50%,-6px)'
    },
    bottom: {
      top: '100%',
      left: '50%',
      transform: 'translate(-50%,6px)'
    },
    left: {
      right: '100%',
      top: '50%',
      transform: 'translate(-6px,-50%)'
    },
    right: {
      left: '100%',
      top: '50%',
      transform: 'translate(6px,-50%)'
    }
  }[placement];
  return /*#__PURE__*/React.createElement("span", _extends({
    style: {
      position: 'relative',
      display: 'inline-flex',
      ...style
    },
    onMouseEnter: () => setShow(true),
    onMouseLeave: () => setShow(false),
    onFocus: () => setShow(true),
    onBlur: () => setShow(false)
  }, rest), children, show && /*#__PURE__*/React.createElement("span", {
    role: "tooltip",
    style: {
      position: 'absolute',
      zIndex: 50,
      ...pos,
      padding: '5px 8px',
      maxWidth: 240,
      width: 'max-content',
      background: 'var(--navy-900)',
      border: '1px solid var(--border-default)',
      borderRadius: 'var(--radius-xs)',
      color: 'var(--paper-050)',
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--text-3xs)',
      lineHeight: 1.4,
      pointerEvents: 'none',
      boxShadow: 'var(--shadow-md)'
    }
  }, content));
}
Object.assign(__ds_scope, { Tooltip });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Tooltip.jsx", error: String((e && e.message) || e) }); }

// components/forms/Checkbox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Checkbox({
  label,
  description,
  checked,
  indeterminate = false,
  disabled = false,
  onChange,
  style,
  ...rest
}) {
  const on = checked || indeterminate;
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'flex-start',
      gap: 10,
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.45 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    type: "checkbox",
    checked: !!checked,
    disabled: disabled,
    onChange: onChange,
    style: {
      position: 'absolute',
      opacity: 0,
      width: 0,
      height: 0
    }
  }, rest)), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": "true",
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: 16,
      height: 16,
      marginTop: 2,
      flex: '0 0 auto',
      borderRadius: 'var(--radius-xs)',
      border: `1px solid ${on ? 'var(--accent-primary)' : 'var(--border-default)'}`,
      background: on ? 'var(--accent-primary)' : 'var(--bg-input)',
      color: 'var(--on-accent)',
      transition: 'var(--transition-control)'
    }
  }, indeterminate ? /*#__PURE__*/React.createElement("span", {
    style: {
      width: 8,
      height: 2,
      background: 'var(--on-accent)'
    }
  }) : checked ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check",
    size: 11
  }) : null), /*#__PURE__*/React.createElement("span", null, label && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      fontSize: 'var(--text-sm)',
      color: 'var(--text-heading)'
    }
  }, label), description && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      fontSize: 'var(--text-2xs)',
      color: 'var(--text-faint)',
      marginTop: 2
    }
  }, description)));
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/forms/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const H = {
  sm: 'var(--control-h-sm)',
  md: 'var(--control-h-md)',
  lg: 'var(--control-h-lg)'
};
function Input({
  label,
  hint,
  error,
  icon,
  suffix,
  size = 'md',
  mono = false,
  id,
  style,
  containerStyle,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      ...containerStyle
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-2xs)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-wider)',
      textTransform: 'uppercase',
      color: 'var(--text-muted)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      height: H[size] || H.md,
      padding: '0 10px',
      background: 'var(--bg-input)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-sm)',
      boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none',
      transition: 'var(--transition-control)'
    }
  }, icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: 14,
    color: "var(--text-faint)"
  }), /*#__PURE__*/React.createElement("input", _extends({
    id: fid,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      flex: 1,
      minWidth: 0,
      height: '100%',
      border: 0,
      background: 'transparent',
      outline: 'none',
      color: 'var(--text-heading)',
      fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)',
      fontSize: size === 'sm' ? 'var(--text-xs)' : 'var(--text-sm)',
      ...style
    }
  }, rest)), suffix && /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--font-mono)',
      fontSize: 'var(--text-2xs)',
      color: 'var(--text-faint)'
    }
  }, suffix)), (hint || error) && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-2xs)',
      color: error ? 'var(--status-danger)' : 'var(--text-faint)'
    }
  }, error || hint));
}
Object.assign(__ds_scope, { Input });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Input.jsx", error: String((e && e.message) || e) }); }

// components/forms/Radio.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Radio({
  label,
  description,
  name,
  value,
  checked,
  disabled = false,
  onChange,
  style,
  ...rest
}) {
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'flex-start',
      gap: 10,
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.45 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    type: "radio",
    name: name,
    value: value,
    checked: !!checked,
    disabled: disabled,
    onChange: onChange,
    style: {
      position: 'absolute',
      opacity: 0,
      width: 0,
      height: 0
    }
  }, rest)), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": "true",
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: 16,
      height: 16,
      marginTop: 2,
      flex: '0 0 auto',
      borderRadius: '50%',
      border: `1px solid ${checked ? 'var(--accent-primary)' : 'var(--border-default)'}`,
      background: 'var(--bg-input)',
      transition: 'var(--transition-control)'
    }
  }, checked && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 7,
      height: 7,
      borderRadius: '50%',
      background: 'var(--accent-primary)'
    }
  })), /*#__PURE__*/React.createElement("span", null, label && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      fontSize: 'var(--text-sm)',
      color: 'var(--text-heading)'
    }
  }, label), description && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      fontSize: 'var(--text-2xs)',
      color: 'var(--text-faint)',
      marginTop: 2
    }
  }, description)));
}
Object.assign(__ds_scope, { Radio });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Radio.jsx", error: String((e && e.message) || e) }); }

// components/forms/Select.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const H = {
  sm: 'var(--control-h-sm)',
  md: 'var(--control-h-md)',
  lg: 'var(--control-h-lg)'
};
function Select({
  label,
  hint,
  error,
  options = [],
  size = 'md',
  id,
  style,
  containerStyle,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      ...containerStyle
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-2xs)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-wider)',
      textTransform: 'uppercase',
      color: 'var(--text-muted)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'flex',
      alignItems: 'center'
    }
  }, /*#__PURE__*/React.createElement("select", _extends({
    id: fid,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      appearance: 'none',
      width: '100%',
      height: H[size] || H.md,
      padding: '0 32px 0 10px',
      background: 'var(--bg-input)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-sm)',
      boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none',
      color: 'var(--text-heading)',
      fontFamily: 'var(--font-body)',
      fontSize: size === 'sm' ? 'var(--text-xs)' : 'var(--text-sm)',
      outline: 'none',
      cursor: 'pointer',
      transition: 'var(--transition-control)',
      ...style
    }
  }, rest), options.map(o => {
    const opt = typeof o === 'string' ? {
      value: o,
      label: o
    } : o;
    return /*#__PURE__*/React.createElement("option", {
      key: opt.value,
      value: opt.value
    }, opt.label);
  })), /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "chevron-down",
    size: 14,
    color: "var(--text-faint)",
    style: {
      position: 'absolute',
      right: 10,
      pointerEvents: 'none'
    }
  })), (hint || error) && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-2xs)',
      color: error ? 'var(--status-danger)' : 'var(--text-faint)'
    }
  }, error || hint));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Select.jsx", error: String((e && e.message) || e) }); }

// components/forms/Switch.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Switch({
  checked = false,
  onChange,
  label,
  disabled = false,
  size = 'md',
  style,
  ...rest
}) {
  const w = size === 'sm' ? 30 : 38,
    h = size === 'sm' ? 16 : 20,
    k = h - 6;
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 10,
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.45 : 1,
      ...style
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    type: "checkbox",
    role: "switch",
    checked: checked,
    disabled: disabled,
    onChange: onChange,
    style: {
      position: 'absolute',
      opacity: 0,
      width: 0,
      height: 0
    }
  }, rest)), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": "true",
    style: {
      position: 'relative',
      width: w,
      height: h,
      flex: '0 0 auto',
      borderRadius: 'var(--radius-pill)',
      background: checked ? 'var(--accent-primary)' : 'var(--bg-input)',
      border: `1px solid ${checked ? 'var(--accent-primary)' : 'var(--border-default)'}`,
      transition: 'var(--transition-control)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      top: 2,
      left: checked ? w - k - 4 : 2,
      width: k,
      height: k,
      borderRadius: '50%',
      background: checked ? 'var(--navy-900)' : 'var(--slate-400)',
      transition: `left var(--dur-fast) var(--ease-standard), background-color var(--dur-fast) var(--ease-standard)`
    }
  })), label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-sm)',
      color: 'var(--text-heading)'
    }
  }, label));
}
Object.assign(__ds_scope, { Switch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Switch.jsx", error: String((e && e.message) || e) }); }

// components/forms/Textarea.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Textarea({
  label,
  hint,
  error,
  rows = 4,
  mono = false,
  id,
  style,
  containerStyle,
  ...rest
}) {
  const [focus, setFocus] = React.useState(false);
  const fid = id || React.useId();
  const borderColor = error ? 'var(--status-danger)' : focus ? 'var(--border-accent)' : 'var(--border-default)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 6,
      ...containerStyle
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      fontFamily: 'var(--font-display)',
      fontSize: 'var(--text-2xs)',
      fontWeight: 600,
      letterSpacing: 'var(--tracking-wider)',
      textTransform: 'uppercase',
      color: 'var(--text-muted)'
    }
  }, label), /*#__PURE__*/React.createElement("textarea", _extends({
    id: fid,
    rows: rows,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      padding: '10px 12px',
      background: 'var(--bg-input)',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--radius-sm)',
      boxShadow: focus ? '0 0 0 3px var(--cyan-a12)' : 'none',
      color: 'var(--text-heading)',
      fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)',
      fontSize: 'var(--text-sm)',
      lineHeight: 'var(--leading-normal)',
      outline: 'none',
      resize: 'vertical',
      transition: 'var(--transition-control)',
      ...style
    }
  }, rest)), (hint || error) && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--text-2xs)',
      color: error ? 'var(--status-danger)' : 'var(--text-faint)'
    }
  }, error || hint));
}
Object.assign(__ds_scope, { Textarea });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Textarea.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Tabs.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
function Tabs({
  items = [],
  value,
  onChange,
  size = 'md',
  style,
  ...rest
}) {
  const [hover, setHover] = React.useState(null);
  const pad = size === 'sm' ? '0 10px' : '0 14px';
  const h = size === 'sm' ? 30 : 38;
  return /*#__PURE__*/React.createElement("div", _extends({
    role: "tablist",
    style: {
      display: 'flex',
      alignItems: 'stretch',
      gap: 2,
      borderBottom: '1px solid var(--border-subtle)',
      ...style
    }
  }, rest), items.map(it => {
    const active = it.value === value;
    return /*#__PURE__*/React.createElement("button", {
      key: it.value,
      role: "tab",
      "aria-selected": active,
      type: "button",
      onClick: () => onChange && onChange(it.value),
      onMouseEnter: () => setHover(it.value),
      onMouseLeave: () => setHover(null),
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: 7,
        height: h,
        padding: pad,
        border: 0,
        borderBottom: `2px solid ${active ? 'var(--accent-primary)' : 'transparent'}`,
        background: !active && hover === it.value ? 'var(--bg-hover)' : 'transparent',
        color: active ? 'var(--text-heading)' : 'var(--text-muted)',
        fontFamily: 'var(--font-display)',
        fontSize: size === 'sm' ? 'var(--text-2xs)' : 'var(--text-xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-wider)',
        textTransform: 'uppercase',
        cursor: 'pointer',
        marginBottom: -1,
        transition: 'var(--transition-control)'
      }
    }, it.icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      name: it.icon,
      size: 14
    }), it.label, it.count != null && /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)',
        letterSpacing: 0
      }
    }, it.count));
  }));
}
Object.assign(__ds_scope, { Tabs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Tabs.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/AlertsScreen.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Card,
    Badge,
    Button,
    IconButton,
    Tag,
    Input,
    Textarea,
    Icon,
    Tooltip
  } = window.TempestEngineeringDesignSystem_1d4355;
  const ALERTS = [{
    id: 'ALR-4192',
    tone: 'warning',
    sev: 'Warning',
    title: 'Wind shear above threshold',
    site: 'north-ridge',
    asset: 'array-04',
    t: '14:02:11Z',
    owner: 'auto',
    detail: 'Shear reached 18.4 m/s against a 16.0 m/s limit. Units 04-03 and 04-04 feathered automatically; dispatch re-planned to hold 26.3 MW fleet output.'
  }, {
    id: 'ALR-4191',
    tone: 'danger',
    sev: 'Critical',
    title: 'Site link lost',
    site: 'selkie-bank',
    asset: 'breaker B2',
    t: '13:51:47Z',
    owner: 'm.okafor',
    detail: 'No telemetry for 11 minutes. Last known state: 36 units running, 8.9 MW. Local controller is assumed to be in island mode.'
  }, {
    id: 'ALR-4188',
    tone: 'warning',
    sev: 'Warning',
    title: 'Gearbox temperature drift',
    site: 'brae-point',
    asset: 'unit 09-14',
    t: '12:20:03Z',
    owner: 'j.reyes',
    detail: 'Gearbox temperature 4.1°C above the 30-day band for this unit at comparable load. Maintenance window suggested within 14 days.'
  }, {
    id: 'ALR-4180',
    tone: 'neutral',
    sev: 'Info',
    title: 'Curtailment window closed',
    site: 'calder-flats',
    asset: 'site',
    t: '11:04:55Z',
    owner: 'auto',
    detail: 'Grid operator lifted the curtailment instruction. Output ramped back to schedule over 6 minutes.'
  }];
  function AlertsScreen({
    onConfirmOffline
  }) {
    const [sel, setSel] = React.useState(ALERTS[0].id);
    const [filter, setFilter] = React.useState('all');
    const list = filter === 'all' ? ALERTS : ALERTS.filter(a => a.tone === filter);
    const active = ALERTS.find(a => a.id === sel);
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 400px',
        gap: 'var(--space-7)',
        maxWidth: 1240,
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      padding: "none",
      title: "Alert queue",
      actions: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(Input, {
        size: "sm",
        icon: "search",
        placeholder: "Filter",
        containerStyle: {
          width: 180
        }
      }), /*#__PURE__*/React.createElement(IconButton, {
        icon: "refresh-cw",
        label: "Refresh",
        size: "sm"
      }))
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 8,
        padding: 'var(--space-5)',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, [['all', 'all'], ['danger', 'critical'], ['warning', 'warning'], ['neutral', 'info']].map(([k, label]) => /*#__PURE__*/React.createElement(Tag, {
      key: k,
      selected: filter === k,
      onClick: () => setFilter(k)
    }, label))), /*#__PURE__*/React.createElement("div", null, list.map(a => {
      const on = a.id === sel;
      return /*#__PURE__*/React.createElement("button", {
        key: a.id,
        type: "button",
        onClick: () => setSel(a.id),
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: 'var(--space-5)',
          width: '100%',
          padding: 'var(--space-5)',
          border: 0,
          borderBottom: '1px solid var(--border-subtle)',
          borderLeft: `2px solid ${on ? 'var(--cyan-500)' : 'transparent'}`,
          background: on ? 'var(--bg-selected)' : 'transparent',
          cursor: 'pointer',
          textAlign: 'left'
        }
      }, /*#__PURE__*/React.createElement(Badge, {
        tone: a.tone,
        dot: true
      }, a.sev), /*#__PURE__*/React.createElement("span", {
        style: {
          flex: 1,
          minWidth: 0
        }
      }, /*#__PURE__*/React.createElement("span", {
        style: {
          display: 'block',
          fontSize: 'var(--text-sm)',
          color: 'var(--text-heading)'
        }
      }, a.title), /*#__PURE__*/React.createElement("span", {
        style: {
          display: 'block',
          fontFamily: 'var(--font-mono)',
          fontSize: 'var(--text-3xs)',
          color: 'var(--text-faint)',
          marginTop: 3
        }
      }, a.id, " \xB7 ", a.site, " \xB7 ", a.asset)), /*#__PURE__*/React.createElement("span", {
        style: {
          fontFamily: 'var(--font-mono)',
          fontSize: 'var(--text-3xs)',
          color: 'var(--text-faint)'
        }
      }, a.t));
    }))), /*#__PURE__*/React.createElement(Card, {
      eyebrow: active.id,
      title: active.title,
      accent: active.tone === 'danger' ? 'var(--red-500)' : 'var(--amber-500)',
      actions: /*#__PURE__*/React.createElement(Tooltip, {
        content: "Acknowledge"
      }, /*#__PURE__*/React.createElement(IconButton, {
        icon: "check",
        label: "Acknowledge",
        size: "sm",
        variant: "outline"
      }))
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: 8,
        marginBottom: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Tag, null, active.site), /*#__PURE__*/React.createElement(Tag, null, active.asset), /*#__PURE__*/React.createElement(Tag, null, "owner:", active.owner)), /*#__PURE__*/React.createElement("p", {
      style: {
        margin: 0,
        fontSize: 'var(--text-sm)',
        lineHeight: 'var(--leading-relaxed)',
        color: 'var(--text-body)'
      }
    }, active.detail), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 'var(--space-6)',
        paddingTop: 'var(--space-5)',
        borderTop: '1px solid var(--border-subtle)',
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Textarea, {
      label: "Operator note",
      rows: 3,
      placeholder: "What did you do?"
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 'var(--space-3)'
      }
    }, /*#__PURE__*/React.createElement(Button, {
      variant: "secondary",
      size: "sm",
      full: true
    }, "Assign to me"), /*#__PURE__*/React.createElement(Button, {
      variant: "danger",
      size: "sm",
      full: true,
      onClick: onConfirmOffline
    }, "Take offline")))));
  }
  Object.assign(window, {
    AlertsScreen
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/AlertsScreen.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/AppShell.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Icon,
    IconButton,
    Badge,
    Tooltip
  } = window.TempestEngineeringDesignSystem_1d4355;
  const NAV = [{
    key: 'overview',
    icon: 'gauge',
    label: 'Overview'
  }, {
    key: 'telemetry',
    icon: 'activity',
    label: 'Telemetry'
  }, {
    key: 'alerts',
    icon: 'bell',
    label: 'Alerts'
  }, {
    key: 'dispatch',
    icon: 'calendar-clock',
    label: 'Dispatch'
  }, {
    key: 'assets',
    icon: 'wind',
    label: 'Assets'
  }, {
    key: 'settings',
    icon: 'settings',
    label: 'Settings'
  }];
  function Rail({
    screen,
    onNavigate
  }) {
    return /*#__PURE__*/React.createElement("nav", {
      style: {
        width: 60,
        flex: '0 0 auto',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 4,
        padding: '14px 0',
        background: 'var(--navy-900)',
        borderRight: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-appicon-navy.png",
      alt: "Tempest OS",
      style: {
        width: 30,
        height: 30,
        marginBottom: 14
      }
    }), NAV.map(n => {
      const active = n.key === screen;
      return /*#__PURE__*/React.createElement(Tooltip, {
        key: n.key,
        content: n.label,
        placement: "right"
      }, /*#__PURE__*/React.createElement("button", {
        type: "button",
        onClick: () => onNavigate(n.key),
        "aria-label": n.label,
        style: {
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          width: 40,
          height: 40,
          border: 0,
          borderRadius: 'var(--radius-sm)',
          background: active ? 'var(--bg-selected)' : 'transparent',
          color: active ? 'var(--cyan-500)' : 'var(--text-faint)',
          cursor: 'pointer',
          transition: 'var(--transition-control)'
        }
      }, /*#__PURE__*/React.createElement(Icon, {
        name: n.icon,
        size: 18
      }), active && /*#__PURE__*/React.createElement("span", {
        style: {
          position: 'absolute',
          left: -10,
          top: 10,
          bottom: 10,
          width: 2,
          background: 'var(--cyan-500)'
        }
      })));
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-social-avatar.png",
      alt: "M. Okafor",
      style: {
        width: 28,
        height: 28,
        borderRadius: '50%'
      }
    }));
  }
  function TopBar({
    title,
    breadcrumb,
    right
  }) {
    return /*#__PURE__*/React.createElement("header", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-5)',
        height: 56,
        flex: '0 0 auto',
        padding: '0 var(--space-7)',
        background: 'var(--navy-800)',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        minWidth: 0
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, breadcrumb), /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-lg)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-tight)',
        color: 'var(--text-heading)'
      }
    }, title)), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-4)'
      }
    }, right));
  }
  function StatusStrip() {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-6)',
        height: 28,
        flex: '0 0 auto',
        padding: '0 var(--space-7)',
        background: 'var(--navy-900)',
        borderTop: '1px solid var(--border-subtle)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--green-500)'
      }
    }, "\u25CF link ok"), /*#__PURE__*/React.createElement("span", null, "latency 41ms"), /*#__PURE__*/React.createElement("span", null, "tempest-os 4.2.1"), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement("span", null, "14:02:12Z"));
  }
  function AppShell({
    screen,
    onNavigate,
    title,
    breadcrumb,
    headerRight,
    children,
    toasts
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        height: '100%',
        minHeight: 0,
        background: 'var(--navy-800)'
      }
    }, /*#__PURE__*/React.createElement(Rail, {
      screen: screen,
      onNavigate: onNavigate
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        minWidth: 0
      }
    }, /*#__PURE__*/React.createElement(TopBar, {
      title: title,
      breadcrumb: breadcrumb,
      right: headerRight
    }), /*#__PURE__*/React.createElement("main", {
      style: {
        flex: 1,
        overflow: 'auto',
        padding: 'var(--space-7)',
        backgroundColor: 'var(--navy-800)',
        backgroundImage: 'var(--bg-grid)'
      }
    }, children), /*#__PURE__*/React.createElement(StatusStrip, null)), /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'fixed',
        right: 20,
        bottom: 44,
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        zIndex: 200
      }
    }, toasts));
  }
  Object.assign(window, {
    AppShell,
    Rail,
    TopBar,
    StatusStrip,
    OS_NAV: NAV
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/AppShell.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/LandingIntro.jsx
try { (() => {
/* scoped — Tempest Engineering website landing intro.
   Same charge/ripple and blade-group fades as the OS splash, but the mark
   slides HORIZONTALLY left as the TEMPEST ENGINEERING wordmark reveals to its
   right, then the whole lockup travels to the top-left corner as the site
   fades in beneath it. Geometry is computed in px from the viewport so the
   docked lockup lands exactly in the site header at every breakpoint.
   Pure presentation; onDone fires after dock. */
(() => {
  const RM = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const L = '../../assets/logo/derived/';
  const LAYERS = [['hub', 'hub'], ['indigo', 'g1'], ['cyan', 'g2'], ['violet', 'g3']];
  const EASE = 'cubic-bezier(.2,0,.2,1)';
  const ORD = ['start', 'ripple', 'hub', 'g1', 'g2', 'g3', 'slide', 'word', 'dock', 'site'];
  function LandingIntro({
    onDone,
    demo
  }) {
    const [t, setT] = React.useState('start');
    const rootRef = React.useRef(null);
    const [vp, setVp] = React.useState({
      w: innerWidth,
      h: innerHeight
    });
    const at = s => ORD.indexOf(t) >= ORD.indexOf(s);
    React.useEffect(() => {
      const m = () => {
        const el = rootRef.current;
        if (el) setVp({
          w: el.clientWidth,
          h: el.clientHeight
        });
      };
      m();
      addEventListener('resize', m);
      return () => removeEventListener('resize', m);
    }, []);
    React.useEffect(() => {
      const steps = RM ? [['hub', 60], ['g1', 60], ['g2', 60], ['g3', 60], ['slide', 60], ['word', 60], ['dock', 120], ['site', 400]] : [['ripple', 80], ['hub', 2200], ['g1', 3000], ['g2', 3750], ['g3', 4500], ['slide', 5600], ['word', 5950], ['dock', 7100], ['site', 7500]];
      const ids = steps.map(([s, ms]) => setTimeout(() => setT(s), ms));
      return () => ids.forEach(clearTimeout);
    }, []);
    React.useEffect(() => {
      if (t === 'site' && !demo && onDone) {
        const id = setTimeout(onDone, 700);
        return () => clearTimeout(id);
      }
    }, [t, demo]);

    /* ---- shared geometry (px) — the header and the docked lockup both derive from this ---- */
    const mobile = vp.w < 640,
      tablet = vp.w >= 640 && vp.w < 1024;
    const PAD_X = mobile ? 20 : tablet ? 26 : 32;
    const PAD_Y = mobile ? 16 : tablet ? 22 : 28;
    const vmin = Math.min(vp.w, vp.h);
    const icon = Math.round(Math.max(mobile ? 96 : 130, Math.min(0.21 * vmin, 240))); // centred build size
    const GAP_R = 0.17,
      WORD_R = 1.5; // gap and wordmark width as ratios of icon
    const lockW = icon * (1 + GAP_R + WORD_R); // full lockup width at scale 1
    const dockIcon = mobile ? 30 : tablet ? 36 : 40; // docked icon height = header lockup height
    const scale = dockIcon / icon;
    const headerH = Math.round(dockIcon * 1.15); // header row sized off the docked lockup
    const docked = RM || at('dock');
    const slid = RM || at('slide');
    return /*#__PURE__*/React.createElement("div", {
      ref: rootRef,
      "aria-label": "Tempest Engineering",
      style: {
        position: 'relative',
        height: '100%',
        overflow: 'hidden',
        background: 'var(--navy-800)'
      }
    }, /*#__PURE__*/React.createElement("style", null, `@keyframes tmpst-gather{0%{r:4.5vmax;opacity:0}30%{opacity:.75}100%{r:0.45vmax;opacity:0}}@keyframes tmpst-core{0%{r:0.05vmax;opacity:0}45%{r:0.62vmax;opacity:1}80%{r:0.5vmax;opacity:.9}100%{r:0.12vmax;opacity:0}}@keyframes tmpst-glow{0%{r:0.6vmax;opacity:.55}100%{r:62vmax;opacity:0}}@keyframes tmpst-ripple{0%{r:0.4vmax;opacity:0}5%{opacity:1}45%{opacity:.7}100%{r:78vmax;opacity:0}}.tmpst-rp{fill:none;opacity:0}`), !RM && at('ripple') && !at('g1') && /*#__PURE__*/React.createElement("svg", {
      "aria-hidden": "true",
      style: {
        position: 'absolute',
        inset: 0,
        width: '100%',
        height: '100%'
      }
    }, /*#__PURE__*/React.createElement("defs", null, /*#__PURE__*/React.createElement("radialGradient", {
      id: "tmpst-lg"
    }, /*#__PURE__*/React.createElement("stop", {
      offset: "0%",
      stopColor: "var(--indigo-600)",
      stopOpacity: ".8"
    }), /*#__PURE__*/React.createElement("stop", {
      offset: "65%",
      stopColor: "var(--indigo-600)",
      stopOpacity: ".22"
    }), /*#__PURE__*/React.createElement("stop", {
      offset: "100%",
      stopColor: "var(--indigo-600)",
      stopOpacity: "0"
    }))), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "1.25",
      style: {
        animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "2",
      style: {
        animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 140ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "50%",
      cy: "50%",
      fill: "#f5f6fa",
      style: {
        opacity: 0,
        animation: 'tmpst-core 700ms cubic-bezier(.3,0,.4,1) 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "50%",
      cy: "50%",
      fill: "url(#tmpst-lg)",
      style: {
        opacity: 0,
        animation: 'tmpst-glow 1500ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "3",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "2.25",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 800ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "1.25",
      style: {
        opacity: 0,
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 980ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "1",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 1160ms 1 forwards'
      }
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'absolute',
        top: docked ? PAD_Y : (vp.h - icon) / 2,
        left: docked ? PAD_X : (vp.w - lockW) / 2,
        transform: `scale(${docked ? scale : 1})`,
        transformOrigin: 'top left',
        transition: RM ? 'none' : `top 900ms ${EASE}, left 900ms ${EASE}, transform 900ms ${EASE}`
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: icon * GAP_R
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'relative',
        width: icon,
        aspectRatio: '1',
        flex: '0 0 auto',
        transform: slid ? 'translateX(0)' : `translateX(${icon * (GAP_R + WORD_R) / 2}px)`,
        transition: RM ? 'none' : `transform 640ms ${EASE}`
      }
    }, LAYERS.map(([name, stage]) => /*#__PURE__*/React.createElement("img", {
      key: name,
      src: `${L}mark-${name}.svg`,
      alt: "",
      draggable: "false",
      style: {
        position: 'absolute',
        inset: 0,
        width: '100%',
        height: '100%',
        opacity: RM || at(stage) ? 1 : 0,
        transition: RM ? 'none' : `opacity ${name === 'hub' ? 680 : 750}ms ${EASE}`
      }
    }))), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: '0 0 auto',
        width: icon * WORD_R,
        opacity: at('word') ? 1 : 0,
        transform: at('word') ? 'translateX(0)' : `translateX(${icon * -0.12}px)`,
        transition: RM ? 'none' : `opacity 620ms ${EASE}, transform 620ms ${EASE}`
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontWeight: 700,
        fontSize: icon * 0.339,
        letterSpacing: '-0.01em',
        lineHeight: 1,
        color: 'var(--paper-050)',
        whiteSpace: 'nowrap'
      }
    }, "TEMPEST"), /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontWeight: 600,
        fontSize: icon * 0.089,
        color: 'var(--slate-400)',
        marginTop: icon * 0.045,
        display: 'flex',
        justifyContent: 'space-between',
        whiteSpace: 'nowrap'
      }
    }, 'ENGINEERING'.split('').map((c, i) => /*#__PURE__*/React.createElement("span", {
      key: i
    }, c)))))), /*#__PURE__*/React.createElement("div", {
      "aria-hidden": !at('site'),
      style: {
        position: 'absolute',
        inset: 0,
        padding: `${PAD_Y}px ${PAD_X}px`,
        display: 'flex',
        flexDirection: 'column',
        opacity: at('site') ? 1 : 0,
        transition: RM ? 'none' : `opacity 700ms ${EASE} 150ms`,
        pointerEvents: at('site') ? 'auto' : 'none'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        justifyContent: 'flex-end',
        alignItems: 'center',
        gap: mobile ? 20 : 36,
        height: headerH,
        paddingLeft: lockW * scale + 24,
        fontFamily: 'var(--font-mono)',
        fontSize: 11,
        letterSpacing: '.22em',
        color: 'var(--slate-400)'
      }
    }, mobile ? /*#__PURE__*/React.createElement("span", {
      "aria-label": "Menu",
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 5,
        padding: 6
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        width: 22,
        height: 2,
        background: 'var(--slate-400)'
      }
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        width: 22,
        height: 2,
        background: 'var(--slate-400)'
      }
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        width: 14,
        height: 2,
        background: 'var(--cyan-500)'
      }
    })) : /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("span", null, "SERVICES"), /*#__PURE__*/React.createElement("span", null, "PROJECTS"), /*#__PURE__*/React.createElement("span", null, "ABOUT"), /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--cyan-500)'
      }
    }, "CONTACT"))), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        maxWidth: 880
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: mobile ? 10 : 12,
        letterSpacing: '.28em',
        color: 'var(--cyan-500)',
        marginBottom: mobile ? 16 : 22
      }
    }, "DESIGN ENGINEERING CONSULTANCY"), /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontWeight: 700,
        fontSize: 'clamp(34px, 6.4vw, 64px)',
        letterSpacing: '-0.02em',
        lineHeight: 1.08,
        color: 'var(--paper-050)',
        textWrap: 'pretty'
      }
    }, "Engineering that holds up"), /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-body)',
        fontSize: 'clamp(15px, 2.2vw, 19px)',
        lineHeight: 1.6,
        color: 'var(--slate-400)',
        marginTop: mobile ? 18 : 24,
        maxWidth: 560
      }
    }, "[One-paragraph positioning \u2014 sectors served, the kind of problems solved, and the standard the work is held to.]")), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: 16,
        fontFamily: 'var(--font-mono)',
        fontSize: mobile ? 8.5 : 10,
        letterSpacing: '.2em',
        color: 'var(--text-faint)',
        paddingBottom: 4,
        whiteSpace: 'nowrap'
      }
    }, /*#__PURE__*/React.createElement("span", null, "TEMPEST DESIGN ENGINEERING LTD"), /*#__PURE__*/React.createElement("span", null, "WWW.TEMPEST-ENGINEERING.CO.UK"))));
  }
  Object.assign(window, {
    LandingIntro
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/LandingIntro.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/LoginScreen.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Button,
    Input,
    Checkbox,
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  function LoginScreen({
    onSignIn
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        height: '100%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: "url('../../assets/imagery/tempest-desktop-wallpaper-3840x2160.png') center/cover"
      }
    }, /*#__PURE__*/React.createElement("form", {
      onSubmit: e => {
        e.preventDefault();
        onSignIn();
      },
      style: {
        width: 360,
        padding: 'var(--space-8)',
        background: 'rgba(11,14,30,.82)',
        backdropFilter: 'blur(8px)',
        border: '1px solid var(--border-default)',
        borderTop: '2px solid var(--cyan-500)',
        borderRadius: 'var(--radius-md)',
        boxShadow: 'var(--shadow-panel)'
      }
    }, /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-os-logo-dark.png",
      alt: "Tempest OS",
      style: {
        height: 34,
        marginBottom: 'var(--space-8)'
      }
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Input, {
      label: "Operator ID",
      icon: "user",
      defaultValue: "m.okafor",
      mono: true
    }), /*#__PURE__*/React.createElement(Input, {
      label: "Passphrase",
      type: "password",
      icon: "lock",
      defaultValue: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"
    }), /*#__PURE__*/React.createElement(Checkbox, {
      label: "Trust this workstation",
      description: "Skips MFA for 12 hours",
      checked: true,
      onChange: () => {}
    }), /*#__PURE__*/React.createElement(Button, {
      type: "submit",
      full: true,
      size: "lg",
      iconRight: /*#__PURE__*/React.createElement(Icon, {
        name: "arrow-right",
        size: 15
      })
    }, "Sign in")), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 'var(--space-7)',
        paddingTop: 'var(--space-5)',
        borderTop: '1px solid var(--border-subtle)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)',
        display: 'flex',
        justifyContent: 'space-between'
      }
    }, /*#__PURE__*/React.createElement("span", null, "site: north-ridge"), /*#__PURE__*/React.createElement("span", null, "4.2.1"))));
  }
  Object.assign(window, {
    LoginScreen
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/LoginScreen.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/OverviewScreen.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Card,
    Badge,
    Button,
    IconButton,
    Tag,
    Icon,
    Tabs
  } = window.TempestEngineeringDesignSystem_1d4355;
  const SITES = [['north-ridge', 'Array 01–04', 48, '12.4', 'Nominal', 'success'], ['calder-flats', 'Array 05–07', 36, '9.1', 'Nominal', 'success'], ['brae-point', 'Array 08–09', 24, '4.8', 'Degraded', 'warning'], ['selkie-bank', 'Array 10–12', 36, '0.0', 'Offline', 'danger']];
  function OverviewScreen({
    onOpenAlerts
  }) {
    const [tab, setTab] = React.useState('sites');
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-7)',
        maxWidth: 1240
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Readout, {
      label: "Fleet output",
      value: "26.3",
      unit: "MW",
      delta: "+4.2% vs 1h"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Availability",
      value: "98.6",
      unit: "%",
      delta: "-0.4pt",
      tone: "down"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Units reporting",
      value: "142",
      unit: "/ 144"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Open alerts",
      value: "3",
      delta: "1 new",
      tone: "down"
    })), /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Last 6 hours",
      title: "Fleet output",
      accent: true,
      actions: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(Tag, null, "1h"), /*#__PURE__*/React.createElement(Tag, {
        selected: true,
        onClick: () => {}
      }, "6h"), /*#__PURE__*/React.createElement(Tag, null, "24h"), /*#__PURE__*/React.createElement(IconButton, {
        icon: "download",
        label: "Export",
        size: "sm"
      }))
    }, /*#__PURE__*/React.createElement(Sparkband, {
      seed: 2.1,
      height: 150
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        justifyContent: 'space-between',
        marginTop: 'var(--space-4)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", null, "08:00Z"), /*#__PURE__*/React.createElement("span", null, "10:00Z"), /*#__PURE__*/React.createElement("span", null, "12:00Z"), /*#__PURE__*/React.createElement("span", null, "14:00Z"))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1.6fr 1fr',
        gap: 'var(--space-7)',
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      padding: "none",
      title: "Sites",
      actions: /*#__PURE__*/React.createElement(Button, {
        variant: "secondary",
        size: "sm",
        iconLeft: /*#__PURE__*/React.createElement(Icon, {
          name: "plus",
          size: 12
        })
      }, "Add site")
    }, /*#__PURE__*/React.createElement(Tabs, {
      value: tab,
      onChange: setTab,
      size: "sm",
      style: {
        padding: '0 var(--space-5)'
      },
      items: [{
        value: 'sites',
        label: 'Sites',
        count: 4
      }, {
        value: 'arrays',
        label: 'Arrays',
        count: 12
      }]
    }), /*#__PURE__*/React.createElement("table", {
      style: {
        width: '100%',
        borderCollapse: 'collapse',
        marginTop: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement(Th, null, "Site"), /*#__PURE__*/React.createElement(Th, null, "Arrays"), /*#__PURE__*/React.createElement(Th, {
      align: "right"
    }, "Units"), /*#__PURE__*/React.createElement(Th, {
      align: "right"
    }, "Output"), /*#__PURE__*/React.createElement(Th, null, "State"), /*#__PURE__*/React.createElement(Th, null))), /*#__PURE__*/React.createElement("tbody", null, SITES.map(([id, arrays, units, out, state, tone]) => /*#__PURE__*/React.createElement("tr", {
      key: id
    }, /*#__PURE__*/React.createElement(Td, {
      mono: true
    }, id), /*#__PURE__*/React.createElement(Td, {
      dim: true
    }, arrays), /*#__PURE__*/React.createElement(Td, {
      align: "right",
      mono: true
    }, units), /*#__PURE__*/React.createElement(Td, {
      align: "right",
      mono: true
    }, out, " MW"), /*#__PURE__*/React.createElement(Td, null, /*#__PURE__*/React.createElement(Badge, {
      tone: tone,
      dot: true
    }, state)), /*#__PURE__*/React.createElement(Td, {
      align: "right"
    }, /*#__PURE__*/React.createElement(IconButton, {
      icon: "chevron-right",
      label: `Open ${id}`,
      size: "sm"
    }))))))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-7)'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Live",
      title: "Event log",
      padding: "sm",
      actions: /*#__PURE__*/React.createElement(IconButton, {
        icon: "pause",
        label: "Pause stream",
        size: "sm"
      })
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column'
      }
    }, /*#__PURE__*/React.createElement(LogLine, {
      t: "14:02:11Z",
      level: "WARN",
      msg: "array-04 wind_shear=18.4m/s \u2192 feather"
    }), /*#__PURE__*/React.createElement(LogLine, {
      t: "14:02:12Z",
      level: "INFO",
      msg: "dispatch replan ok (142/144)"
    }), /*#__PURE__*/React.createElement(LogLine, {
      t: "13:58:04Z",
      level: "OK",
      msg: "deploy tempest-os 4.2.1 complete"
    }), /*#__PURE__*/React.createElement(LogLine, {
      t: "13:51:47Z",
      level: "ERR",
      msg: "selkie-bank link lost (breaker B2)"
    }), /*#__PURE__*/React.createElement(LogLine, {
      t: "13:44:02Z",
      level: "INFO",
      msg: "curtailment window closed"
    }), /*#__PURE__*/React.createElement(LogLine, {
      t: "13:40:19Z",
      level: "INFO",
      msg: "grid signal 50.02Hz nominal"
    }))), /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Requires action",
      title: "Alerts",
      padding: "sm",
      accent: "var(--amber-500)",
      actions: /*#__PURE__*/React.createElement(Button, {
        variant: "ghost",
        size: "sm",
        onClick: onOpenAlerts
      }, "Open queue")
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-4)'
      }
    }, [['warning', 'Wind shear — array 04', '14:02Z'], ['danger', 'Link lost — selkie-bank', '13:51Z'], ['warning', 'Gearbox temp — unit 09-14', '12:20Z']].map(([tone, label, t]) => /*#__PURE__*/React.createElement("div", {
      key: label,
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-4)'
      }
    }, /*#__PURE__*/React.createElement(Badge, {
      tone: tone,
      dot: true
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        flex: 1,
        fontSize: 'var(--text-sm)',
        color: 'var(--text-heading)'
      }
    }, label), /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, t))))))));
  }
  Object.assign(window, {
    OverviewScreen
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/OverviewScreen.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/SplashScreen.jsx
try { (() => {
/* scoped — Tempest OS launch splash, per approved motion reference
   (uploads/TempestOS_launch_8_second_ROYAL_BLUE_OSCILLATING_RIPPLE.gif):
   black → royal-blue oscillating ripple exits frame → hub fades in → indigo,
   cyan, violet stroke groups fade in (opacity only — no growth, no rotation,
   no lock pulse) → the one icon lifts → TEMPEST OS wordmark below.
   Presentation only: startup state comes from window.TempestBoot (boot.js).
   The mark is never redrawn — layers are exact color-separated crops of the
   canonical appicon (assets/logo/derived/tempest-mark-layer-*.png). */
(() => {
  const {
    Button
  } = window.TempestEngineeringDesignSystem_1d4355;
  const RM = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const L = '../../assets/logo/derived/';
  const LAYERS = [['hub', 'hub'], ['indigo', 'g1'], ['cyan', 'g2'], ['violet', 'g3']];
  const WORD = L + 'tempest-os-wordmark-dark.png';
  const EASE = 'cubic-bezier(.2,0,.2,1)';
  const ORD = ['start', 'ripple', 'hub', 'g1', 'g2', 'g3', 'lift', 'word', 'done'];
  function SplashScreen({
    boot,
    onDone,
    demo
  }) {
    const [t, setT] = React.useState('start');
    const [bs, setBs] = React.useState(boot.getState());
    const [exiting, setExiting] = React.useState(false);
    const at = s => ORD.indexOf(t) >= ORD.indexOf(s);
    React.useEffect(() => boot.subscribe(setBs), [boot]);
    React.useEffect(() => {
      const steps = RM ? [['hub', 60], ['g1', 60], ['g2', 60], ['g3', 60], ['lift', 60], ['word', 120], ['done', 700]] : [['ripple', 80], ['hub', 2200], ['g1', 3000], ['g2', 3750], ['g3', 4500], ['lift', 5600], ['word', 5950], ['done', 6800]];
      const ids = steps.map(([s, ms]) => setTimeout(() => setT(s), ms));
      return () => ids.forEach(clearTimeout);
    }, []);
    React.useEffect(() => {
      if (t === 'done' && bs.status === 'ready' && !demo) {
        setExiting(true);
        const id = setTimeout(onDone, RM ? 100 : 380);
        return () => clearTimeout(id);
      }
    }, [t, bs.status, demo]);
    const ICON = 'clamp(180px, 26vmin, 280px)';
    const failed = bs.status === 'failed';
    const lifted = RM || at('lift');
    return /*#__PURE__*/React.createElement("div", {
      "aria-label": "Tempest OS starting",
      style: {
        position: 'relative',
        height: '100%',
        overflow: 'hidden',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'var(--navy-800)',
        opacity: exiting ? 0 : 1,
        transition: `opacity 360ms ${EASE}`
      }
    }, /*#__PURE__*/React.createElement("style", null, `@keyframes tmpst-gather{0%{r:4.5vmax;opacity:0}30%{opacity:.75}100%{r:0.45vmax;opacity:0}}@keyframes tmpst-core{0%{r:0.05vmax;opacity:0}45%{r:0.62vmax;opacity:1}80%{r:0.5vmax;opacity:.9}100%{r:0.12vmax;opacity:0}}@keyframes tmpst-glow{0%{r:0.6vmax;opacity:.55}100%{r:62vmax;opacity:0}}@keyframes tmpst-ripple{0%{r:0.4vmax;opacity:0}5%{opacity:1}45%{opacity:.7}100%{r:78vmax;opacity:0}}@keyframes tmpst-status-in{from{opacity:0}to{opacity:1}}@keyframes tmpst-tick{0%,100%{opacity:.25}50%{opacity:1}}.tmpst-rp{fill:none;opacity:0}`), !RM && at('ripple') && !at('g1') && /*#__PURE__*/React.createElement("svg", {
      "aria-hidden": "true",
      style: {
        position: 'absolute',
        inset: 0,
        width: '100%',
        height: '100%'
      }
    }, /*#__PURE__*/React.createElement("defs", null, /*#__PURE__*/React.createElement("radialGradient", {
      id: "tmpst-g"
    }, /*#__PURE__*/React.createElement("stop", {
      offset: "0%",
      stopColor: "var(--indigo-600)",
      stopOpacity: ".8"
    }), /*#__PURE__*/React.createElement("stop", {
      offset: "65%",
      stopColor: "var(--indigo-600)",
      stopOpacity: ".22"
    }), /*#__PURE__*/React.createElement("stop", {
      offset: "100%",
      stopColor: "var(--indigo-600)",
      stopOpacity: "0"
    }))), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "1.25",
      style: {
        animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "2",
      style: {
        animation: 'tmpst-gather 560ms cubic-bezier(.55,0,.85,.35) 140ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "50%",
      cy: "50%",
      fill: "#f5f6fa",
      style: {
        opacity: 0,
        animation: 'tmpst-core 700ms cubic-bezier(.3,0,.4,1) 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      cx: "50%",
      cy: "50%",
      fill: "url(#tmpst-g)",
      style: {
        opacity: 0,
        animation: 'tmpst-glow 1500ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "3",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 620ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "2.25",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 800ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--cyan-500)",
      strokeWidth: "1.25",
      style: {
        opacity: 0,
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 980ms 1 forwards'
      }
    }), /*#__PURE__*/React.createElement("circle", {
      className: "tmpst-rp",
      cx: "50%",
      cy: "50%",
      stroke: "var(--indigo-600)",
      strokeWidth: "1",
      style: {
        animation: 'tmpst-ripple 1550ms cubic-bezier(.2,.45,.55,1) 1160ms 1 forwards'
      }
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        transform: lifted ? 'translateY(0)' : 'translateY(calc(clamp(180px, 26vmin, 280px) * 0.135))',
        transition: RM ? 'none' : `transform 640ms ${EASE}`
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'relative',
        width: ICON,
        aspectRatio: '1',
        flex: '0 0 auto'
      }
    }, LAYERS.map(([name, stage]) => /*#__PURE__*/React.createElement("img", {
      key: name,
      src: `${L}mark-${name}.svg`,
      alt: "",
      draggable: "false",
      style: {
        position: 'absolute',
        inset: 0,
        width: '100%',
        height: '100%',
        opacity: RM || at(stage) ? 1 : 0,
        transition: RM ? 'none' : `opacity ${name === 'hub' ? 680 : 750}ms ${EASE}`
      }
    }))), /*#__PURE__*/React.createElement("img", {
      src: WORD,
      alt: "Tempest OS",
      draggable: "false",
      style: {
        width: `calc(${ICON} * 1.02)`,
        marginTop: `calc(${ICON} * 0.13)`,
        opacity: at('word') ? 1 : 0,
        transition: RM ? 'none' : `opacity 520ms ${EASE}`
      }
    })), /*#__PURE__*/React.createElement("div", {
      role: "status",
      "aria-live": "polite",
      style: {
        position: 'absolute',
        bottom: 44,
        left: 0,
        right: 0,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 10,
        fontFamily: 'var(--font-mono)',
        fontSize: 10,
        letterSpacing: '.28em',
        textTransform: 'uppercase',
        color: 'var(--text-faint)',
        opacity: at('word') || failed ? 1 : 0,
        transition: `opacity 260ms ${EASE}`
      }
    }, failed ? /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--red-500)'
      }
    }, "\u25CF"), /*#__PURE__*/React.createElement("span", null, "Startup halted \u2014 ", bs.error), /*#__PURE__*/React.createElement(Button, {
      size: "sm",
      variant: "ghost",
      onClick: () => boot.retry()
    }, "Retry")) : /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("span", {
      "aria-hidden": "true",
      style: {
        width: 4,
        height: 4,
        borderRadius: '50%',
        background: bs.status === 'ready' ? 'var(--green-500)' : 'var(--cyan-500)',
        animation: bs.status === 'ready' || RM ? 'none' : 'tmpst-tick 1200ms ease-in-out infinite'
      }
    }), /*#__PURE__*/React.createElement("span", {
      key: bs.label,
      style: {
        animation: RM ? 'none' : 'tmpst-status-in 140ms ease-out'
      }
    }, bs.label))));
  }
  Object.assign(window, {
    SplashScreen
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/SplashScreen.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/TelemetryScreen.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Card,
    Badge,
    Button,
    IconButton,
    Tabs,
    Switch,
    Select,
    Tooltip,
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  const CHANNELS = [['rotor_speed', 'rpm', '14.2', 'success'], ['wind_speed', 'm/s', '11.8', 'success'], ['wind_shear', 'm/s', '18.4', 'warning'], ['nacelle_temp', '°C', '41.2', 'success'], ['gearbox_temp', '°C', '78.9', 'warning'], ['pitch_angle', 'deg', '6.4', 'success'], ['grid_freq', 'Hz', '50.02', 'success'], ['power_out', 'kW', '3140', 'success']];
  function TelemetryScreen() {
    const [tab, setTab] = React.useState('signals');
    const [live, setLive] = React.useState(true);
    const [sel, setSel] = React.useState('wind_shear');
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '260px 1fr',
        gap: 'var(--space-7)',
        maxWidth: 1240,
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      padding: "none",
      title: "Channels",
      actions: /*#__PURE__*/React.createElement(IconButton, {
        icon: "search",
        label: "Search channels",
        size: "sm"
      })
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column'
      }
    }, CHANNELS.map(([name, unit, val, tone]) => {
      const active = name === sel;
      return /*#__PURE__*/React.createElement("button", {
        key: name,
        type: "button",
        onClick: () => setSel(name),
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          padding: '10px var(--space-5)',
          border: 0,
          borderLeft: `2px solid ${active ? 'var(--cyan-500)' : 'transparent'}`,
          background: active ? 'var(--bg-selected)' : 'transparent',
          cursor: 'pointer',
          textAlign: 'left'
        }
      }, /*#__PURE__*/React.createElement("span", {
        style: {
          width: 5,
          height: 5,
          borderRadius: '50%',
          background: tone === 'warning' ? 'var(--amber-500)' : 'var(--green-500)'
        }
      }), /*#__PURE__*/React.createElement("span", {
        style: {
          flex: 1,
          fontFamily: 'var(--font-mono)',
          fontSize: 'var(--text-2xs)',
          color: active ? 'var(--text-heading)' : 'var(--text-muted)'
        }
      }, name), /*#__PURE__*/React.createElement("span", {
        style: {
          fontFamily: 'var(--font-mono)',
          fontSize: 'var(--text-2xs)',
          color: 'var(--text-faint)'
        }
      }, val, " ", unit));
    }))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-7)'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      padding: "sm",
      eyebrow: "north-ridge / array-04",
      title: sel,
      accent: true,
      actions: /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement(Switch, {
        size: "sm",
        checked: live,
        onChange: () => setLive(!live),
        label: "Live"
      }), /*#__PURE__*/React.createElement(Select, {
        size: "sm",
        options: ['6h', '24h', '7d'],
        containerStyle: {
          width: 96
        }
      }), /*#__PURE__*/React.createElement(IconButton, {
        icon: "maximize-2",
        label: "Expand",
        size: "sm"
      }))
    }, /*#__PURE__*/React.createElement(Tabs, {
      value: tab,
      onChange: setTab,
      size: "sm",
      items: [{
        value: 'signals',
        label: 'Signal'
      }, {
        value: 'residual',
        label: 'Residual'
      }, {
        value: 'spectrum',
        label: 'Spectrum'
      }],
      style: {
        marginBottom: 'var(--space-6)'
      }
    }), /*#__PURE__*/React.createElement(Sparkband, {
      seed: sel.length + 3,
      height: 180,
      color: sel === 'wind_shear' ? 'var(--amber-500)' : 'var(--cyan-500)',
      bars: 80
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        justifyContent: 'space-between',
        marginTop: 'var(--space-4)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", null, "08:00Z"), /*#__PURE__*/React.createElement("span", null, "10:00Z"), /*#__PURE__*/React.createElement("span", null, "12:00Z"), /*#__PURE__*/React.createElement("span", null, "14:00Z"))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3,1fr)',
        gap: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Readout, {
      label: "Current",
      value: "18.4",
      unit: "m/s",
      delta: "+6.1 in 10m",
      tone: "down"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Threshold",
      value: "16.0",
      unit: "m/s"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Time over",
      value: "4m 12s"
    })), /*#__PURE__*/React.createElement(Card, {
      padding: "sm",
      title: "Unit breakdown",
      actions: /*#__PURE__*/React.createElement(Badge, {
        tone: "warning",
        dot: true
      }, "2 feathered")
    }, /*#__PURE__*/React.createElement("table", {
      style: {
        width: '100%',
        borderCollapse: 'collapse'
      }
    }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", null, /*#__PURE__*/React.createElement(Th, null, "Unit"), /*#__PURE__*/React.createElement(Th, {
      align: "right"
    }, "Output"), /*#__PURE__*/React.createElement(Th, {
      align: "right"
    }, "Rotor"), /*#__PURE__*/React.createElement(Th, {
      align: "right"
    }, "Pitch"), /*#__PURE__*/React.createElement(Th, null, "State"))), /*#__PURE__*/React.createElement("tbody", null, [['04-01', '3140', '14.2', '6.4', 'Running', 'success'], ['04-02', '3090', '14.0', '6.8', 'Running', 'success'], ['04-03', '0', '0.4', '88.0', 'Feathered', 'warning'], ['04-04', '0', '0.2', '88.0', 'Feathered', 'warning']].map(r => /*#__PURE__*/React.createElement("tr", {
      key: r[0]
    }, /*#__PURE__*/React.createElement(Td, {
      mono: true
    }, r[0]), /*#__PURE__*/React.createElement(Td, {
      align: "right",
      mono: true
    }, r[1], " kW"), /*#__PURE__*/React.createElement(Td, {
      align: "right",
      mono: true
    }, r[2]), /*#__PURE__*/React.createElement(Td, {
      align: "right",
      mono: true
    }, r[3], "\xB0"), /*#__PURE__*/React.createElement(Td, null, /*#__PURE__*/React.createElement(Badge, {
      tone: r[5],
      dot: true
    }, r[4])))))))));
  }
  Object.assign(window, {
    TelemetryScreen
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/TelemetryScreen.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/parts.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Icon,
    Badge
  } = window.TempestEngineeringDesignSystem_1d4355;

  /* Shared readouts and table primitives used across Tempest OS screens. */
  function Readout({
    label,
    value,
    unit,
    delta,
    tone
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        minWidth: 0,
        padding: 'var(--space-5)',
        background: 'var(--surface-card)',
        border: '1px solid var(--surface-card-border)',
        borderRadius: 'var(--radius-md)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-3xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-widest)',
        textTransform: 'uppercase',
        color: 'var(--text-faint)'
      }
    }, label), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'baseline',
        gap: 5,
        marginTop: 8
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-3xl)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-tight)',
        lineHeight: 1,
        color: 'var(--text-heading)'
      }
    }, value), unit && /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-xs)',
        color: 'var(--text-faint)'
      }
    }, unit)), delta && /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 5,
        marginTop: 8,
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: tone === 'down' ? 'var(--red-500)' : 'var(--green-500)'
      }
    }, /*#__PURE__*/React.createElement(Icon, {
      name: tone === 'down' ? 'trending-down' : 'trending-up',
      size: 12
    }), delta));
  }

  /* Deterministic pseudo-random series so the chart band is stable across renders. */
  function series(seed, n) {
    const out = [];
    for (let i = 0; i < n; i++) out.push(0.45 + 0.4 * Math.abs(Math.sin(seed + i * 0.37)) + 0.08 * Math.abs(Math.cos(seed * 2 + i)));
    return out;
  }
  function Sparkband({
    seed = 1,
    height = 132,
    color = 'var(--cyan-500)',
    bars = 64
  }) {
    const data = series(seed, bars);
    return /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'relative',
        height,
        display: 'flex',
        alignItems: 'flex-end',
        gap: 2
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'absolute',
        inset: 0,
        backgroundImage: 'linear-gradient(var(--paper-a08) 1px, transparent 1px)',
        backgroundSize: '100% 33%'
      }
    }), data.map((v, i) => /*#__PURE__*/React.createElement("div", {
      key: i,
      style: {
        flex: 1,
        height: `${Math.round(v * 100)}%`,
        background: i === bars - 1 ? 'var(--paper-050)' : color,
        opacity: i === bars - 1 ? 1 : 0.55 + 0.45 * v,
        borderRadius: 1
      }
    })));
  }
  function Th({
    children,
    align
  }) {
    return /*#__PURE__*/React.createElement("th", {
      style: {
        textAlign: align || 'left',
        padding: '0 var(--space-5) var(--space-3)',
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-3xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-wider)',
        textTransform: 'uppercase',
        color: 'var(--text-faint)',
        borderBottom: '1px solid var(--border-subtle)',
        whiteSpace: 'nowrap'
      }
    }, children);
  }
  function Td({
    children,
    align,
    mono = false,
    dim = false
  }) {
    return /*#__PURE__*/React.createElement("td", {
      style: {
        textAlign: align || 'left',
        padding: 'var(--space-4) var(--space-5)',
        fontFamily: mono ? 'var(--font-mono)' : 'var(--font-body)',
        fontSize: mono ? 'var(--text-xs)' : 'var(--text-sm)',
        color: dim ? 'var(--text-muted)' : 'var(--text-heading)',
        borderBottom: '1px solid var(--border-subtle)',
        whiteSpace: 'nowrap'
      }
    }, children);
  }
  function LogLine({
    t,
    level,
    msg
  }) {
    const c = {
      WARN: 'var(--amber-500)',
      INFO: 'var(--cyan-500)',
      ERR: 'var(--red-500)',
      OK: 'var(--green-500)'
    }[level];
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 10,
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-2xs)',
        lineHeight: 1.9,
        whiteSpace: 'nowrap'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--text-faint)'
      }
    }, t), /*#__PURE__*/React.createElement("span", {
      style: {
        color: c,
        width: 34,
        flex: '0 0 auto'
      }
    }, level), /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--text-body)',
        overflow: 'hidden',
        textOverflow: 'ellipsis'
      }
    }, msg));
  }
  Object.assign(window, {
    Readout,
    Sparkband,
    Th,
    Td,
    LogLine,
    series
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/parts.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-os/tempest-boot.js
try { (() => {
/* TempestBoot — real startup lifecycle for the Tempest OS console. Presentation-free:
   the splash subscribes to this state; it never invents progress.
   Defines window.TempestBoot unconditionally: this file is the source of truth even
   if a stale copy was evaluated earlier (e.g. baked into a cached bundle). */
(() => {
  const NS = 'TempestEngineeringDesignSystem_1d4355';
  const asset = p => new Promise((res, rej) => {
    let tries = 0;
    (function go() {
      const i = new Image();
      i.onload = res;
      i.onerror = () => ++tries < 3 ? setTimeout(go, 300) : rej(new Error('asset ' + p));
      i.src = p + (tries ? '?r=' + tries : '');
    })();
  });
  const poll = (fn, timeout, what) => new Promise((res, rej) => {
    const t0 = performance.now();
    (function tick() {
      if (fn()) return res();
      if (performance.now() - t0 > timeout) return rej(new Error(what));
      setTimeout(tick, 40);
    })();
  });
  const PHASES = [{
    key: 'runtime',
    label: 'RUNTIME HOST',
    run: () => poll(() => window.React && window.ReactDOM, 8000, 'runtime host')
  }, {
    key: 'ds',
    label: 'DESIGN SYSTEM',
    run: () => poll(() => window[NS], 8000, 'design system bundle')
  }, {
    key: 'type',
    label: 'TYPE SYSTEM',
    run: () => Promise.race([Promise.all([document.fonts.load('600 16px "Chakra Petch"'), document.fonts.load('16px Inter'), document.fonts.load('16px "Space Mono"')]).then(() => document.fonts.ready), new Promise(r => setTimeout(r, 3000)) // fonts are non-fatal: cap the wait
    ])
  }, {
    key: 'assets',
    label: 'BRAND ASSETS',
    run: () => Promise.all(['../../assets/logo/derived/mark-hub.svg', '../../assets/logo/derived/mark-indigo.svg', '../../assets/logo/derived/mark-cyan.svg', '../../assets/logo/derived/mark-violet.svg', '../../assets/logo/derived/tempest-os-wordmark-dark.png', '../../assets/imagery/tempest-desktop-wallpaper-3840x2160.png'].map(asset))
  }, {
    key: 'modules',
    label: 'MODULE SYSTEM',
    run: () => poll(() => window.AppShell && window.LoginScreen && window.OverviewScreen && window.TelemetryScreen && window.AlertsScreen, 8000, 'module system')
  }, {
    key: 'shell',
    label: 'UI SHELL',
    run: () => new Promise(r => {
      let done = false;
      const fin = () => {
        if (!done) {
          done = true;
          r();
        }
      };
      requestAnimationFrame(() => requestAnimationFrame(fin));
      setTimeout(fin, 300);
    })
  }];
  let state = {
    status: 'initialising',
    index: 0,
    label: PHASES[0].label,
    error: null
  };
  const subs = new Set();
  const set = p => {
    state = {
      ...state,
      ...p
    };
    subs.forEach(f => f(state));
  };
  async function sequence() {
    set({
      status: 'initialising',
      error: null
    });
    for (let i = 0; i < PHASES.length; i++) {
      set({
        index: i,
        label: PHASES[i].label
      });
      try {
        await PHASES[i].run();
      } catch (e) {
        set({
          status: 'failed',
          error: PHASES[i].label
        });
        return;
      }
    }
    set({
      status: 'ready',
      label: 'READY'
    });
  }
  let started = false;
  window.TempestBoot = {
    start() {
      if (!started) {
        started = true;
        sequence();
      }
      return this;
    },
    getState: () => state,
    subscribe(f) {
      subs.add(f);
      f(state);
      return () => subs.delete(f);
    },
    retry() {
      sequence();
    }
  };
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-os/tempest-boot.js", error: String((e && e.message) || e) }); }

// ui_kits/tempest-web/Capabilities.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Card,
    Icon,
    Button,
    Badge,
    Tag
  } = window.TempestEngineeringDesignSystem_1d4355;
  const ITEMS = [{
    icon: 'cpu',
    title: 'Dispatch control',
    body: 'Closed-loop scheduling against grid signals, curtailment instructions and forecast, recalculated every 30 seconds.'
  }, {
    icon: 'activity',
    title: 'Telemetry',
    body: 'Every channel from every unit, retained at full resolution for 13 months and queryable from the console.'
  }, {
    icon: 'shield-check',
    title: 'Safety & compliance',
    body: 'Protection logic and audit trails built to IEC 61400-25, validated on hardware before it reaches a site.'
  }];
  function Capabilities() {
    return /*#__PURE__*/React.createElement("section", {
      style: {
        padding: 'var(--section-y) var(--space-8)',
        background: 'var(--navy-800)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        maxWidth: 1240,
        margin: '0 auto'
      }
    }, /*#__PURE__*/React.createElement("div", {
      className: "t-eyebrow"
    }, "What we build"), /*#__PURE__*/React.createElement("h2", {
      style: {
        margin: '12px 0 var(--space-9)',
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-3xl)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-tightest)',
        color: 'var(--text-heading)',
        maxWidth: '24ch'
      }
    }, "Three systems, one control loop"), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3,1fr)',
        gap: 'var(--space-7)'
      }
    }, ITEMS.map(it => /*#__PURE__*/React.createElement(Card, {
      key: it.title,
      interactive: true,
      padding: "lg"
    }, /*#__PURE__*/React.createElement(Icon, {
      name: it.icon,
      size: 22,
      color: "var(--cyan-500)"
    }), /*#__PURE__*/React.createElement("h3", {
      style: {
        margin: '18px 0 10px',
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-xl)',
        fontWeight: 600,
        color: 'var(--text-heading)'
      }
    }, it.title), /*#__PURE__*/React.createElement("p", {
      style: {
        margin: 0,
        fontSize: 'var(--text-sm)',
        lineHeight: 'var(--leading-relaxed)',
        color: 'var(--text-body)'
      }
    }, it.body)))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 'var(--space-11)',
        alignItems: 'center',
        marginTop: 'var(--space-13)',
        padding: 'var(--space-10)',
        background: 'var(--navy-700)',
        border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-md)'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-os-logo-dark.png",
      alt: "Tempest OS",
      style: {
        height: 30,
        marginBottom: 'var(--space-6)'
      }
    }), /*#__PURE__*/React.createElement("p", {
      style: {
        margin: 0,
        fontSize: 'var(--text-base)',
        lineHeight: 'var(--leading-relaxed)',
        color: 'var(--text-body)',
        maxWidth: '46ch'
      }
    }, "The console operators actually use. One number that matters, and the path to change it \u2014 on the wall in the control room or on a laptop in a field van."), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 8,
        marginTop: 'var(--space-7)',
        flexWrap: 'wrap'
      }
    }, /*#__PURE__*/React.createElement(Tag, null, "on-prem"), /*#__PURE__*/React.createElement(Tag, null, "hosted"), /*#__PURE__*/React.createElement(Tag, null, "offline-capable")), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 'var(--space-8)'
      }
    }, /*#__PURE__*/React.createElement(Button, {
      variant: "secondary",
      iconRight: /*#__PURE__*/React.createElement(Icon, {
        name: "arrow-right",
        size: 14
      })
    }, "Read the docs"))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: 'var(--space-5)'
      }
    }, /*#__PURE__*/React.createElement(Readout, {
      label: "Sites live",
      value: "9"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Units managed",
      value: "144"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Uptime, 12mo",
      value: "99.98",
      unit: "%"
    }), /*#__PURE__*/React.createElement(Readout, {
      label: "Replan cycle",
      value: "30",
      unit: "s"
    })))));
  }
  Object.assign(window, {
    Capabilities
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-web/Capabilities.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-web/Hero.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Button,
    Badge,
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  function Hero() {
    return /*#__PURE__*/React.createElement("section", {
      style: {
        position: 'relative',
        padding: '120px var(--space-8) 96px',
        backgroundColor: 'var(--navy-800)',
        backgroundImage: 'var(--bg-grid)',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        maxWidth: 1240,
        margin: '0 auto',
        display: 'grid',
        gridTemplateColumns: '1.1fr .9fr',
        gap: 'var(--space-11)',
        alignItems: 'center'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        marginBottom: 'var(--space-7)'
      }
    }, /*#__PURE__*/React.createElement(Badge, {
      tone: "info",
      dot: true
    }, "Tempest OS 4.2 is live")), /*#__PURE__*/React.createElement("h1", {
      style: {
        margin: 0,
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-6xl)',
        fontWeight: 600,
        lineHeight: 'var(--leading-tight)',
        letterSpacing: 'var(--tracking-tightest)',
        color: 'var(--text-heading)'
      }
    }, "Control systems for grid-scale wind"), /*#__PURE__*/React.createElement("p", {
      style: {
        marginTop: 'var(--space-7)',
        maxWidth: '52ch',
        fontSize: 'var(--text-lg)',
        lineHeight: 'var(--leading-relaxed)',
        color: 'var(--text-body)'
      }
    }, "We build the dispatch, telemetry and safety software that keeps turbine fleets producing. Nine sites, 144 units, one control loop."), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 'var(--space-4)',
        marginTop: 'var(--space-9)'
      }
    }, /*#__PURE__*/React.createElement(Button, {
      size: "lg",
      notch: true,
      iconRight: /*#__PURE__*/React.createElement(Icon, {
        name: "arrow-right",
        size: 15
      })
    }, "Talk to engineering"), /*#__PURE__*/React.createElement(Button, {
      size: "lg",
      variant: "secondary",
      iconLeft: /*#__PURE__*/React.createElement(Icon, {
        name: "play",
        size: 15
      })
    }, "See Tempest OS")), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        gap: 'var(--space-8)',
        marginTop: 'var(--space-11)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-2xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", null, "IEC 61400-25 compliant"), /*#__PURE__*/React.createElement("span", null, "SOC 2 Type II"), /*#__PURE__*/React.createElement("span", null, "On-prem or hosted"))), /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'relative',
        aspectRatio: '4 / 3',
        border: '1px solid var(--border-default)',
        borderTop: '2px solid var(--cyan-500)',
        borderRadius: 'var(--radius-md)',
        background: 'var(--navy-700)',
        overflow: 'hidden',
        boxShadow: 'var(--shadow-panel)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        height: 34,
        padding: '0 12px',
        borderBottom: '1px solid var(--border-subtle)',
        background: 'var(--navy-900)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        width: 6,
        height: 6,
        borderRadius: '50%',
        background: 'var(--green-500)'
      }
    }), "tempest-os / north-ridge"), /*#__PURE__*/React.createElement("div", {
      style: {
        padding: 20
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 10,
        fontWeight: 600,
        letterSpacing: '.28em',
        textTransform: 'uppercase',
        color: 'var(--text-faint)'
      }
    }, "Fleet output"), /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-4xl)',
        fontWeight: 600,
        color: 'var(--text-heading)',
        letterSpacing: 'var(--tracking-tight)',
        lineHeight: 1.1
      }
    }, "26.3 ", /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--font-mono)',
        fontSize: 16,
        color: 'var(--text-faint)'
      }
    }, "MW")), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 18
      }
    }, /*#__PURE__*/React.createElement(Sparkband, {
      seed: 2.1,
      height: 120
    }))))));
  }
  Object.assign(window, {
    Hero
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-web/Hero.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-web/Numbers.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  function Numbers() {
    const rows = [['26.3 MW', 'managed at peak'], ['13 months', 'full-resolution telemetry'], ['30 s', 'dispatch replan cycle'], ['2011', 'engineering since']];
    return /*#__PURE__*/React.createElement("section", {
      style: {
        padding: 'var(--space-11) var(--space-8)',
        backgroundColor: 'var(--navy-900)',
        backgroundImage: 'var(--bg-grid)',
        borderTop: '1px solid var(--border-subtle)',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        maxWidth: 1240,
        margin: '0 auto',
        display: 'grid',
        gridTemplateColumns: 'repeat(4,1fr)',
        gap: 'var(--space-8)'
      }
    }, rows.map(([n, l]) => /*#__PURE__*/React.createElement("div", {
      key: l
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-3xl)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-tight)',
        color: 'var(--cyan-500)',
        lineHeight: 1
      }
    }, n), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 10,
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-2xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-wider)',
        textTransform: 'uppercase',
        color: 'var(--text-muted)'
      }
    }, l)))));
  }
  Object.assign(window, {
    Numbers
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-web/Numbers.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-web/SiteFooter.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Button,
    Input,
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  const COLS = [['Platform', ['Dispatch control', 'Telemetry', 'Safety & compliance', 'Integrations']], ['Tempest OS', ['Overview', 'Release notes', 'Documentation', 'Status']], ['Company', ['About', 'Field services', 'Careers', 'Contact']]];
  function SiteFooter() {
    return /*#__PURE__*/React.createElement("footer", {
      className: "t-light",
      style: {
        background: 'var(--bg-page)',
        color: 'var(--text-body)',
        padding: 'var(--space-12) var(--space-8) var(--space-8)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        maxWidth: 1240,
        margin: '0 auto'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1.2fr repeat(3,1fr)',
        gap: 'var(--space-9)'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-logo-vertical-paper.png",
      alt: "Tempest Engineering",
      style: {
        height: 110,
        marginLeft: -8
      }
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 'var(--space-6)',
        display: 'flex',
        gap: 8,
        maxWidth: 300
      }
    }, /*#__PURE__*/React.createElement(Input, {
      placeholder: "work email",
      containerStyle: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement(Button, {
      size: "md"
    }, "Subscribe"))), COLS.map(([head, links]) => /*#__PURE__*/React.createElement("div", {
      key: head
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-2xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-widest)',
        textTransform: 'uppercase',
        color: 'var(--text-heading)'
      }
    }, head), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        marginTop: 'var(--space-5)'
      }
    }, links.map(l => /*#__PURE__*/React.createElement("a", {
      key: l,
      href: "#",
      style: {
        fontSize: 'var(--text-sm)',
        color: 'var(--text-body)',
        borderBottom: 0
      }
    }, l)))))), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-7)',
        marginTop: 'var(--space-10)',
        paddingTop: 'var(--space-6)',
        borderTop: '1px solid var(--border-subtle)',
        fontFamily: 'var(--font-mono)',
        fontSize: 'var(--text-3xs)',
        color: 'var(--text-faint)'
      }
    }, /*#__PURE__*/React.createElement("span", null, "\xA9 2026 Tempest Engineering"), /*#__PURE__*/React.createElement("span", null, "Privacy"), /*#__PURE__*/React.createElement("span", null, "Terms"), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'flex',
        gap: 12,
        color: 'var(--text-muted)'
      }
    }, /*#__PURE__*/React.createElement(Icon, {
      name: "github",
      size: 15
    }), /*#__PURE__*/React.createElement(Icon, {
      name: "linkedin",
      size: 15
    }), /*#__PURE__*/React.createElement(Icon, {
      name: "rss",
      size: 15
    })))));
  }
  Object.assign(window, {
    SiteFooter
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-web/SiteFooter.jsx", error: String((e && e.message) || e) }); }

// ui_kits/tempest-web/SiteHeader.jsx
try { (() => {
/* scoped */
(() => {
  const {
    Button,
    Icon
  } = window.TempestEngineeringDesignSystem_1d4355;
  const LINKS = ['Platform', 'Tempest OS', 'Field services', 'About'];
  function SiteHeader() {
    const [open, setOpen] = React.useState(null);
    return /*#__PURE__*/React.createElement("header", {
      style: {
        position: 'sticky',
        top: 0,
        zIndex: 40,
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-9)',
        height: 72,
        padding: '0 var(--space-8)',
        background: 'rgba(11,14,30,.86)',
        backdropFilter: 'blur(10px)',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("img", {
      src: "../../assets/logo/tempest-logo-horizontal-navy.png",
      alt: "Tempest Engineering",
      style: {
        height: 42
      }
    }), /*#__PURE__*/React.createElement("nav", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--space-7)'
      }
    }, LINKS.map(l => /*#__PURE__*/React.createElement("a", {
      key: l,
      href: "#",
      onMouseEnter: () => setOpen(l),
      onMouseLeave: () => setOpen(null),
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-wider)',
        textTransform: 'uppercase',
        color: open === l ? 'var(--paper-050)' : 'var(--text-muted)',
        borderBottom: 0,
        transition: 'var(--transition-control)'
      }
    }, l))), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1
      }
    }), /*#__PURE__*/React.createElement("a", {
      href: "#",
      style: {
        fontFamily: 'var(--font-display)',
        fontSize: 'var(--text-xs)',
        fontWeight: 600,
        letterSpacing: 'var(--tracking-wider)',
        textTransform: 'uppercase',
        color: 'var(--text-muted)',
        borderBottom: 0
      }
    }, "Sign in"), /*#__PURE__*/React.createElement(Button, {
      size: "md",
      iconRight: /*#__PURE__*/React.createElement(Icon, {
        name: "arrow-right",
        size: 14
      })
    }, "Talk to engineering"));
  }
  Object.assign(window, {
    SiteHeader
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/tempest-web/SiteHeader.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.Button = __ds_scope.Button;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.Icon = __ds_scope.Icon;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.Tag = __ds_scope.Tag;

__ds_ns.Dialog = __ds_scope.Dialog;

__ds_ns.Toast = __ds_scope.Toast;

__ds_ns.Tooltip = __ds_scope.Tooltip;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Radio = __ds_scope.Radio;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Switch = __ds_scope.Switch;

__ds_ns.Textarea = __ds_scope.Textarea;

__ds_ns.Tabs = __ds_scope.Tabs;

})();
