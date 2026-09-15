/** Square icon-only control for toolbars, table rows and panel headers. */
export interface IconButtonProps {
  /** Lucide icon name or a node. */
  icon: string | React.ReactNode;
  /** Required accessible label; also the tooltip. */
  label: string;
  size?: 'sm' | 'md' | 'lg';
  /** ghost = bare (default); outline = hairline box. @default "ghost" */
  variant?: 'ghost' | 'outline';
  /** Toggled-on state: cyan tint + cyan glyph. @default false */
  active?: boolean;
  disabled?: boolean;
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  style?: React.CSSProperties;
}
export function IconButton(props: IconButtonProps): JSX.Element;
