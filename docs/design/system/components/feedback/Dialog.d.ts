/** Modal panel: dimmed + blurred backdrop, cyan top rule, right-aligned footer actions. */
export interface DialogProps {
  open?: boolean;
  title?: React.ReactNode;
  eyebrow?: React.ReactNode;
  children?: React.ReactNode;
  /** Footer action row — put the primary Button last. */
  footer?: React.ReactNode;
  /** Called by the close button and backdrop click. */
  onClose?: () => void;
  /** Max width in px. @default 480 */
  width?: number;
  style?: React.CSSProperties;
}
export function Dialog(props: DialogProps): JSX.Element | null;
