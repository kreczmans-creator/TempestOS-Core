/** Transient notification: left status rule, optional monospaced timestamp. */
export interface ToastProps {
  title?: React.ReactNode;
  children?: React.ReactNode;
  tone?: 'info' | 'success' | 'warning' | 'danger';
  /** Single inline action, usually <Button variant="ghost" size="sm">. */
  action?: React.ReactNode;
  onClose?: () => void;
  /** Mono timestamp, e.g. "14:02:11Z". */
  timestamp?: string;
  style?: React.CSSProperties;
}
export function Toast(props: ToastProps): JSX.Element;
