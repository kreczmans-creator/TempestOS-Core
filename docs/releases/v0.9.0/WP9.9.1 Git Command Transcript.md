# WP 9.9.1 — Git Command Transcript

**Work Package:** `WP 9.9.1` — Product Owner Release Execution
**Date:** 2026-08-07

This is a faithful transcript of the commands actually run to prepare
the `v0.9.0` release, in order, with their material output. Read-only
inspection commands used purely to confirm state are included where
they informed a decision; routine repeated status checks are
summarized rather than reproduced verbatim.

---

## 1. Pre-flight: confirm history and conventions

```
$ git log --oneline -10
447c368 WP 9.1B: stop hardcoding this Work Package's own commit hash
7d6b493 WP 9.1B: Development Baseline Consolidation
71b49ea WP 9.0A-9.1A: Mechanical Foundation consolidation (v0.9.0, development baseline)
183d2ef Release: TempestOS v0.8.0
28e41e8 Merge branch 'feature/v0.8.0-engineering-workspace' into main
...

$ git tag -l
v0.3.0
v0.4.0
v0.5.0
v0.6.0
v0.7.0
v0.8.0

$ git show v0.8.0 --no-patch
tag v0.8.0
Tagger: kreczmans-creator <kreczmans@gmail.com>
...
TempestOS v0.8.0 - Engineering Workspace
...
    Release: TempestOS v0.8.0
    Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
```

Confirmed the established conventions to mirror: annotated tags,
`Release: TempestOS vX.Y.Z` commit subject, `kreczmans-creator
<kreczmans@gmail.com>` author identity, `Co-Authored-By` trailer.

## 2. Pre-flight: confirm working tree and branch state

```
$ cat VERSION
0.8.0
$ git branch -a
* main
  remotes/origin/HEAD -> origin/main
  remotes/origin/main
$ git status --short | wc -l
101
```

Confirmed: only `main` exists (no feature branch); 101 pending changes,
all inspected and confirmed to be the expected `WP 9.2A`–`WP 9.9.0`
(both passes)/`WP 9.8B` release content — no stray build artifacts,
temp files, or `bin`/`obj` output among them (`.gitignore` confirmed
present and effective).

## 3. Pre-commit sanity build

```
$ dotnet build src/TempestOS.slnx -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## 4. Update VERSION

```
$ printf '0.9.0' > VERSION
$ cat VERSION
0.9.0
```

(No trailing newline, matching the existing file's format exactly.)

## 5. Stage all release changes

```
$ git add -A
$ git status --short | awk '{print substr($0,1,2)}' | sort | uniq -c
    146 A
     15 M
$ git diff --stat | tail -3
(empty — no unstaged changes remain)
```

## 6. Create the release commit

```
$ git commit -F <message file>
[main 2128bba] Release: TempestOS v0.9.0
 Committer: Steven Kreczman <stevenk@tempest-engineering.co.uk>
 [... Git identity auto-detection warning ...]
 161 files changed, 22205 insertions(+), 120 deletions(-)
```

Commit message body (in full):

```
Release: TempestOS v0.9.0

v0.9.0 ("Engineering Domain Completion") - ten Work Packages
(WP 9.0A-9.9.0, plus reconciliation WP 9.8B): the Mechanical
Foundation consolidation, five further real Engineering Disciplines
(Calculations, Verification Management, Engineering Documents,
Manufacturing - alongside the earlier Requirements/Mechanical work),
a Platform Service governance reconciliation, and a two-pass release
preparation and product baseline review (WP 9.9.0 First Pass, which
found one disclosed governance gap; WP 9.8B, which closed it; and a
WP 9.9.0 Second Pass, which independently re-confirmed the closure
and formally registered one new, non-blocking finding, TD-34).

Recommendation from both release-preparation passes: APPROVED.
See docs/releases/v0.9.0/ReleaseNotes.md and
"WP9.9.0 Product Approval Report (Second Pass).md" for the complete
account.

Disclosed, unresolved as of this commit (see PROJECT_STATUS.md for
full detail): PROJECT_STATUS.md's own historical narrative describes
work happening on a feature/v0.9.0-calculations-workspace branch;
no such branch exists in this repository - all v0.9.0 work has, in
fact, been committed directly to main. No merge is required for this
release as a result; this is recorded here rather than silently
reconciled.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
```

## 7. Disclosed correction: commit identity

```
$ git log -1 --format="Author: %an <%ae>%nCommitter: %cn <%ce>"
Author: Steven Kreczman <stevenk@tempest-engineering.co.uk>
Committer: Steven Kreczman <stevenk@tempest-engineering.co.uk>
```

This did not match the repository's established identity
(`kreczmans-creator <kreczmans@gmail.com>`, used by every prior
commit). Corrected before proceeding:

```
$ git config --local user.name "kreczmans-creator"
$ git config --local user.email "kreczmans@gmail.com"
$ git commit --amend --reset-author --no-edit
[main 9f258f1] Release: TempestOS v0.9.0
 161 files changed, 22205 insertions(+), 120 deletions(-)
$ git log -1 --format="Author: %an <%ae>%nCommitter: %cn <%ce>"
Author: kreczmans-creator <kreczmans@gmail.com>
Committer: kreczmans-creator <kreczmans@gmail.com>
```

Note the amend changed the commit hash from `2128bba` to `9f258f1`
(tree and message unchanged; only the author/committer identity and
resulting hash changed). **`9f258f1` (in full,
`9f258f16e07b89f6030d1bdcd90b799527fdcb8c`) is the actual, final
release commit hash.**

## 8. Merge into `main`

Not performed. `git branch -a` (re-run after the commit, §10) confirms
`main` remains the only branch. There is nothing to merge — see the
Product Owner Release Summary, §3, for the full disclosure. No `git
merge` command was run.

## 9. Create the annotated tag

```
$ git tag -a v0.9.0 -m "TempestOS v0.9.0 - Engineering Domain Completion"
$ git tag -l -n1 | grep v0.9.0
v0.9.0          TempestOS v0.9.0 - Engineering Domain Completion
```

## 10. Post-tag verification

```
$ git status
On branch main
Your branch is ahead of 'origin/main' by 1 commit.
  (use "git push" to publish your local commits)

nothing to commit, working tree clean

$ git log --oneline -3
9f258f1 Release: TempestOS v0.9.0
447c368 WP 9.1B: stop hardcoding this Work Package's own commit hash
7d6b493 WP 9.1B: Development Baseline Consolidation

$ git rev-parse v0.9.0^{commit}
9f258f16e07b89f6030d1bdcd90b799527fdcb8c
$ git rev-parse HEAD
9f258f16e07b89f6030d1bdcd90b799527fdcb8c
# (tag target == HEAD, confirmed identical)

$ git branch -a
* main
  remotes/origin/HEAD -> origin/main
  remotes/origin/main

$ cat VERSION
0.9.0

$ git rev-list --left-right --count origin/main...HEAD
0	1
# (0 behind, 1 ahead — not pushed)

$ git diff --stat
$ git diff --cached --stat
# (both empty — working tree fully clean, nothing staged or unstaged)
```

## 11. Commands explicitly not run

No command in this transcript wrote to any remote. In particular, none
of the following were run at any point in this Work Package:

```
git push
git push origin main
git push origin v0.9.0
git push --tags
gh release create ...
git branch -d / git branch -D  (no branches existed to delete)
```

---

**Final state:** `main` @ `9f258f16e07b89f6030d1bdcd90b799527fdcb8c`,
tagged `v0.9.0` (annotated, same commit), working tree clean, 1 commit
ahead of `origin/main`, 0 behind, nothing pushed.
