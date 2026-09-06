# ADR-0133: A Working Day Is Not a Duration, and Lead Times Refuse to Convert Between the Two

## Status

Accepted — `Group D` (P03 Commercial Intelligence), 2026-09-06.

## Context

Suppliers quote lead times in whatever unit suits them: "3 weeks", "15
working days", "20 days", "same day". A platform that wants to compare
them needs them in one unit, and the obvious move is to convert
everything to elapsed time.

Five working days is not seven-fifths of a calendar week. How much
elapsed time five working days represent depends on which country's
public holidays apply, whether the supplier works Saturdays, whether it
shuts for two weeks in August, and where in the week the clock started.
TempestOS holds none of that. A five-day working week is an assumption,
and an assumption that turns "15 working days" into "3 weeks" produces a
delivery date somebody will plan around.

This is the same problem `ADR-0125` met with affine units — where a
temperature difference and a temperature are not interchangeable — and
the same one `ADR-0130` met with currency conversion. In all three cases
the honest answer is to refuse.

## Decision

`LeadTimeDuration` carries its own unit — `Unspecified`, `Hour`,
`CalendarDay`, `WorkingDay`, `Week`, `Month` — and is not a
`Quantity<Duration>`.

- `ToElapsed()` returns a `Quantity<Duration>` for calendar units and
  `null` for working days. There is no flag to make it guess.
- `IsComparableWith` is false across the calendar/working boundary, and
  `CompareTo` throws rather than ordering incomparable figures.
- Anything that ranks or aggregates lead times handles incomparability
  explicitly: `CostEstimate.LongestLeadTime` returns `null` where the
  lines disagree on unit rather than picking one; `LeadTimeQuery`'s
  `NoLongerThan` ceiling excludes a record it cannot compare rather than
  admitting it; `D5` warns where candidates' lead times cannot be ranked
  against each other.
- `DurationUnits` gains `Day` (86,400 s) and `Week` (604,800 s),
  documented as *calendar* units, so the calendar side of the boundary
  converts freely and correctly.

`LeadTimeKind` is a second axis, again orthogonal: `Estimated`, `Typical`,
`Historical`, `Quoted`, `Committed`, `Actual`. A committed lead time
outranks a quoted one, which outranks an observed history, which outranks
somebody's estimate — and `FindApplicableAsync` returns them in that
order, strongest claim first.

## Consequences

**Some comparisons simply cannot be made**, and the caller is told so
rather than given a number. A user who wants them compared must record
the lead times in comparable units, which is a decision they can defend.

**"Which supplier is quickest?" can return "nobody can tell from this."**
That is a better answer than a ranking built on an invented shift
pattern.

**The strongest claim is not the most realistic one.** A supplier's
commitment outranks its own delivery history in `LeadTimeKind`, and the
history is frequently the number worth believing — which is why
`FindApplicableAsync` returns the whole ordered set rather than only the
head.
