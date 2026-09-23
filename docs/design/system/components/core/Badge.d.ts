/** Status pill: uppercase micro-label, optional status dot. Read-only. */
export interface BadgeProps {
  children?: React.ReactNode;
  tone?: 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'brand';
  /** Leading 5px status dot — use for live system state. @default false */
  dot?: boolean;
  /** Lucide icon name rendered at 11px. */
  icon?: string;
  style?: React.CSSProperties;
}
export function Badge(props: BadgeProps): JSX.Element;
