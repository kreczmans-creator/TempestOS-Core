/** Multi-line field — incident notes, commit messages, config blobs. */
export interface TextareaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  hint?: string;
  error?: string;
  /** @default 4 */
  rows?: number;
  /** Space Mono value — use for config / logs. @default false */
  mono?: boolean;
  containerStyle?: React.CSSProperties;
}
export function Textarea(props: TextareaProps): JSX.Element;
