/** Hover/focus label in Space Mono — units, truncated IDs, icon-button names. */
export interface TooltipProps {
  children?: React.ReactNode;
  content?: React.ReactNode;
  placement?: 'top' | 'bottom' | 'left' | 'right';
  style?: React.CSSProperties;
}
export function Tooltip(props: TooltipProps): JSX.Element;
