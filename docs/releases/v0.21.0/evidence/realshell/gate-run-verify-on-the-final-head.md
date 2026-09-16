> The runner's own report from the campaign's gate run on the final code head (product code `37264671`, runner `83458b13`), display :105, 2026-09-16 01:38–01:42 UTC. Screenshots of this run were not committed (the 25 committed PNGs are from the package's own run); the step tables are the record.

# Real-shell journey (verify)

- Started: 2026-09-16 01:42:11, finished 01:42:43
- Display: `:105`, X window `2097167`
- Persistence root: `/tmp/claude-0/-home-user-TempestOS-Core/5db835a4-6634-545f-aa24-3893beef3321/scratchpad/gate/realshell3/run`
- Real-shell journey (verify): 8 steps - 8 Verified, 0 Inferred, 0 Unknown, 0 not exercised, 0 FAILED.

Every action below was delivered to the application as real X11 input
(`xdotool` pointer moves, button presses, key events and typed characters).
The visual tree was read only to locate controls and to assert what is on
screen. No service, view-model or command was called directly.

| # | Step | Action | Input | Expected | Observed | Verdict | Screenshot |
|---|---|---|---|---|---|---|---|
| 1 | restart | Relaunch on the same persistence root | process start | The application starts on the same data and the shell composes | the shell composed; the status bar's own project segment reads 'No project' | Verified | `01-restart.png` |
| 2 | project-survives | Projects → Open | mouse | The project created in the first session is listed, by code and name | listed under Closed: "P-0001 Apollo Pump Redesign" | Verified | `02-project-survives.png` |
| 3 | quote-survives | Open the project → Quote tab | mouse | The quotation is still Accepted, with both lines and its own total | "Q-2026-001 — Accepted"; "Total £7,300.00"; "Concept design and pump sizing  •  40 × £120.00  =  £4,800.00" | Verified | `03-quote-survives.png` |
| 4 | deliverables-survive | Deliverables tab | mouse | Both deliverables are there, one of them completed | "2 deliverable(s), 1 completed." | Verified | `04-deliverables-survive.png` |
| 5 | evidence-survives | Evidence tab | mouse | The evidence record and its attached file are still there | "Calculation  —  evidence-sample  •  Draft" | Verified | `05-evidence-survives.png` |
| 6 | calculation-survives | Engineering → Engineering Calculations | mouse | The named calculation and its runs are still recorded against the project | "Apollo discharge beam check  ·  Apollo Pump Redesign  ·  Recorded  ·  rev 1  ·  2026-09-16 01:40 UTC" | Verified | `06-calculation-survives.png` |
| 7 | reference-survives | Engineering → Reference data | mouse | The released material and the released person survived the restart at their released revision | "mat-s355j2 — S355J2 non-alloy structural steel  •  rev 5  •  Released  •  Siderticino SA, S355J2 (S355) technical specifications - Non-alloy structural steels, Mechanical properties by thickness table"; "person-dana-whitfield — Dana Whitfield (Principal Engineer)  •  rev 5  •  Released  •  (no source citation)" | Verified | `07-reference-survives.png` |
| 8 | store | Read tempest.db directly | sqlite (Microsoft.Data.Sqlite) | The project, its milestone, both deliverables, the completion, the quotation and the evidence are all stored | tempest.db holds Calculation×1, Deliverable×2, DeliverableCompletion×1, Evidence×1, InvoiceRequest×1, Milestone×1, Project×1, Quotation×1 | Verified | `08-store.png` |

## Runner log

```
01:42:13  Window 2097167 on display :105; shell composed: True.
01:42:43  The application's own process loop returned; the window is closed.
```
