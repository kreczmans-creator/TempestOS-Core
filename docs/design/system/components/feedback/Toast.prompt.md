Transient system notification, bottom-right stack.

```jsx
<Toast tone="warning" title="Wind shear detected" timestamp="14:02:11Z" onClose={fn}
  action={<Button variant="ghost" size="sm">View array</Button>}>
  Array 04 feathered automatically.
</Toast>
```

Alerts that persist belong in an inline panel, not a toast.
