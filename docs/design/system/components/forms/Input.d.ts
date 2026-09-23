/**
 * Single-line text field: sunken fill, hairline border, cyan focus ring.
 */
export interface InputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'> {
  /** Uppercase display-font label above the field. */
  label?: string;
  /** Helper text below. */
  hint?: string;
  /** Error message; also turns the border red. Overrides hint. */
  error?: string;
  /** Leading Lucide icon name. */
  icon?: string;
  /** Trailing unit or affix, set in mono (e.g. "kW", "ms"). */
  suffix?: React.ReactNode;
  size?: 'sm' | 'md' | 'lg';
  /** Set the value in Space Mono — for IDs, keys, coordinates. @default false */
  mono?: boolean;
  containerStyle?: React.CSSProperties;
}
export function Input(props: InputProps): JSX.Element;
