/** Immediate-effect toggle (applies on flip — no save step). */
export interface SwitchProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type' | 'size'> {
  checked?: boolean;
  label?: React.ReactNode;
  disabled?: boolean;
  size?: 'sm' | 'md';
}
export function Switch(props: SwitchProps): JSX.Element;
