/** Native select with Tempest chrome and a chevron affordance. */
export interface SelectOption { value: string; label: string }
export interface SelectProps extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, 'size'> {
  label?: string;
  hint?: string;
  error?: string;
  /** Strings or {value,label} pairs. */
  options?: (string | SelectOption)[];
  size?: 'sm' | 'md' | 'lg';
  containerStyle?: React.CSSProperties;
}
export function Select(props: SelectProps): JSX.Element;
