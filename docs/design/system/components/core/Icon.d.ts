/**
 * Lucide glyph masked to currentColor — the Tempest icon primitive.
 */
export interface IconProps {
  /** Lucide icon name, kebab-case (e.g. "activity", "gauge", "cloud-lightning"). */
  name: string;
  /** Pixel box. 14 / 16 in dense UI, 20 / 24 for standalone. @default 16 */
  size?: number;
  /** Any CSS color; defaults to inheriting text color. @default "currentColor" */
  color?: string;
  /** Accessible label; falls back to name. */
  title?: string;
  style?: React.CSSProperties;
}
export function Icon(props: IconProps): JSX.Element;
