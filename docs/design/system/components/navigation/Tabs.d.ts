/**
 * Underline tab bar for switching views within a panel or page.
 */
export interface TabItem { value: string; label: string; icon?: string; count?: number }
export interface TabsProps {
  items: TabItem[];
  /** Currently selected item value. */
  value?: string;
  onChange?: (value: string) => void;
  size?: 'sm' | 'md';
  style?: React.CSSProperties;
}
export function Tabs(props: TabsProps): JSX.Element;
