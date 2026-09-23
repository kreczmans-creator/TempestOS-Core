/**
 * Panel surface: flat fill, single hairline, 5px corners. No drop shadow on dark.
 */
export interface CardProps {
  children?: React.ReactNode;
  /** Display-font panel title; renders the header row. */
  title?: React.ReactNode;
  /** Uppercase cyan micro-label above the title. */
  eyebrow?: React.ReactNode;
  /** Right-aligned header controls (IconButton, Button size="sm"). */
  actions?: React.ReactNode;
  padding?: 'none' | 'sm' | 'md' | 'lg';
  /** true for a 2px cyan top rule, or any CSS color. */
  accent?: boolean | string;
  /** Hairline turns cyan on hover. @default false */
  interactive?: boolean;
  /** Cut top-right corner. @default false */
  notch?: boolean;
  style?: React.CSSProperties;
}
export function Card(props: CardProps): JSX.Element;
