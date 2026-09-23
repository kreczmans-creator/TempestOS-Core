/**
 * Tempest action button — uppercase Chakra Petch label, wide tracking, 3px corners.
 */
export interface ButtonProps {
  children?: React.ReactNode;
  /** primary = cyan fill (one per view); secondary = hairline outline; ghost = bare; danger = destructive. @default "primary" */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  /** 28 / 36 / 44px control heights. @default "md" */
  size?: 'sm' | 'md' | 'lg';
  /** Leading node, normally an <Icon />. */
  iconLeft?: React.ReactNode;
  iconRight?: React.ReactNode;
  disabled?: boolean;
  /** Cut the top-right corner like the TEMPEST logotype. Hero CTAs only. @default false */
  notch?: boolean;
  /** Stretch to container width. @default false */
  full?: boolean;
  type?: 'button' | 'submit' | 'reset';
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  style?: React.CSSProperties;
}
export function Button(props: ButtonProps): JSX.Element;
