/** Single-choice control for 2–3 visible options. The one round element in the system. */
export interface RadioProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: React.ReactNode;
  description?: React.ReactNode;
  name?: string;
  value?: string;
  checked?: boolean;
  disabled?: boolean;
}
export function Radio(props: RadioProps): JSX.Element;
