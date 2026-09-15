Single-line field with optional label, icon, unit suffix and error state.

```jsx
<Input label="Site name" placeholder="North Ridge" />
<Input label="Rated output" suffix="MW" mono defaultValue="12.4" />
<Input label="API key" icon="key" error="Key not recognised" />
```

Use `mono` whenever the value is machine data. Labels are uppercase; keep them two words or fewer.
