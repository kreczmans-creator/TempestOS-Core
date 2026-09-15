/** Mono-type token for filters, labels and metadata. Removable or selectable. */
export interface TagProps {
  children?: React.ReactNode;
  /** Show a trailing × and call this on click. */
  onRemove?: (e: React.MouseEvent) => void;
  /** Makes the whole tag clickable (filter chips). */
  onClick?: (e: React.MouseEvent) => void;
  /** Cyan-tinted selected state. @default false */
  selected?: boolean;
  style?: React.CSSProperties;
}
export function Tag(props: TagProps): JSX.Element;
