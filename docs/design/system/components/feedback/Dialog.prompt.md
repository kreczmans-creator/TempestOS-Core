Modal for confirmations and short focused forms.

```jsx
<Dialog open={open} eyebrow="Confirm" title="Take array offline?" onClose={close}
  footer={<><Button variant="ghost" onClick={close}>Cancel</Button><Button variant="danger">Take offline</Button></>}>
  Output will drop to zero for roughly 40 minutes.
</Dialog>
```
