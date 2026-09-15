/** Square 16px checkbox; cyan fill when on. Supports indeterminate. */
export interface CheckboxProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: React.ReactNode;
  /** Secondary line under the label. */
  description?: React.ReactNode;
  checked?: boolean;
  /** Mixed state (partial group selection). @default false */
  indeterminate?: boolean;
  disabled?: boolean;
}
export function Checkbox(props: CheckboxProps): JSX.Element;
