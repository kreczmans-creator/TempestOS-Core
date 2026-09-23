Masked Lucide glyph that inherits `currentColor` — use for every icon in Tempest UI; never hand-roll SVG.

```jsx
<Icon name="cloud-lightning" size={20} />
<Icon name="activity" size={14} color="var(--accent-primary)" />
```

Sizes in use: 14 (table rows, tags), 16 (buttons, menu items), 20 (toolbars), 24+ (empty states, feature marks). Loads from the lucide-static CDN, so it needs network access; in offline decks fall back to type.
