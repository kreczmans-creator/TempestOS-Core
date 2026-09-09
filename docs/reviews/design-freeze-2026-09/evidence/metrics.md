# Design Freeze Review - Quantitative Evidence
## TempestOS Repository Metrics

Generated: 2026-09-08

---

## Section 1: Source Lines Per Project

| Project | .cs Files | Non-Blank Lines |
|---------|-----------|-----------------|
| src/Tempest.Core | 760 | 72,529 |
| src/Tempest.Desktop | 91 | 19,130 |
| src/Tempest.Harness | 2 | 684 |
| src/Tempest.Workspace | 217 | 19,121 |
| src/Samples/Tempest.Samples | 79 | 5,868 |
| src/Templates/Tempest.Templates.Module | 1 | 34 |
| src/Validation/Tempest.Validation | 1 | 90 |
| src/Frozen | 41 | 2,839 |
| **src (excl. Frozen)** | **1,151** | **117,456** |
| **src/Frozen** | **41** | **2,839** |

Command:
```bash
find "src" -name "*.cs" -not -path "*/Frozen/*" -not -path "*/bin/*" -not -path "*/obj/*" | wc -l
find "src" -name "*.cs" -not -path "*/Frozen/*" -not -path "*/bin/*" -not -path "*/obj/*" -exec cat {} \; | grep -v '^[[:space:]]*$' | wc -l
```

---

## Section 2: Test Lines and Test Methods

| Test Project | .cs Files | Non-Blank Lines | [Fact] | [Theory] | [AvaloniaFact] | Total Tests |
|--------------|-----------|-----------------|--------|----------|----------------|-------------|
| tests/Tempest.Core.Tests | 369 | 16,529 | 3,553 | 145 | 0 | 3,698 |
| tests/Tempest.Desktop.Tests | 72 | 18,172 | 73 | 3 | 391 | 467 |

Command:
```bash
grep -r "\[Fact\]" tests/Tempest.Core.Tests --include="*.cs" | wc -l
grep -r "\[Theory\]" tests/Tempest.Core.Tests --include="*.cs" | wc -l
grep -r "\[AvaloniaFact\]" tests/Tempest.Core.Tests --include="*.cs" | wc -l
```

---

## Section 3: Largest 25 .cs Files by Line Count (src excluding Frozen)

| # | Lines | Path |
|---|-------|------|
| 1 | 1,162 | src/Tempest.Core/Runtime/TempestHost.cs |
| 2 | 1,112 | src/Tempest.Desktop/MainWindow.cs |
| 3 | 976 | src/Tempest.Desktop/Editors/ObjectEditorView.cs |
| 4 | 791 | src/Tempest.Desktop/Views/EngineeringCalculationView.cs |
| 5 | 776 | src/Tempest.Desktop/DigitalThread/DigitalThreadGraphView.cs |
| 6 | 766 | src/Tempest.Desktop/Views/ProjectExplorerView.cs |
| 7 | 761 | src/Tempest.Core/Persistence/SqlitePersistenceStore.cs |
| 8 | 708 | src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs |
| 9 | 690 | src/Tempest.Core/Persistence/PersistenceStore.cs |
| 10 | 645 | src/Tempest.Harness/WorkspaceShell.cs |
| 11 | 640 | src/Tempest.Core/Plugins/PluginManifestDiscoveryService.cs |
| 12 | 635 | src/Tempest.Workspace/Workspace/EngineeringCockpit.cs |
| 13 | 615 | src/Tempest.Desktop/Views/RibbonView.cs |
| 14 | 556 | src/Tempest.Core/Requirements/RequirementsService.cs |
| 15 | 534 | src/Tempest.Desktop/DigitalThread/DigitalThreadGraphModel.cs |
| 16 | 509 | src/Tempest.Workspace/Engineering/BracketCalculationWorkbench.cs |
| 17 | 481 | src/Tempest.Desktop/Views/ProjectRisksView.cs |
| 18 | 481 | src/Tempest.Desktop/Views/CockpitView.cs |
| 19 | 472 | src/Tempest.Core/EngineeringDomain/Implementation/GovernanceRisk.cs |
| 20 | 453 | src/Tempest.Core/BusinessOperations/Crm/CrmCatalogs.cs |
| 21 | 452 | src/Tempest.Workspace/Engineering/EngineeringCalculationRegister.cs |
| 22 | 449 | src/Tempest.Core/EngineeringIntelligence/RuleEngine.cs |
| 23 | 437 | src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectStateStore.cs |
| 24 | 425 | src/Tempest.Core/CommercialIntelligence/Estimating/EstimatingValidation.cs |
| 25 | 422 | src/Tempest.Desktop/Viewing/DocumentViewerView.cs |

Command:
```bash
find "src" -name "*.cs" -not -path "*/Frozen/*" -not -path "*/bin/*" -not -path "*/obj/*" -type f | while read f; do
  lines=$(cat "$f" | grep -v '^[[:space:]]*$' | wc -l)
  echo "$lines|$f"
done | sort -rn | head -25
```

---

## Section 4: Namespace/Folder Size Analysis

### src/Tempest.Core - Top-Level Subfolder Lines

| Folder | Non-Blank Lines |
|--------|-----------------|
| Audit | 543 |
| BackgroundServices | 546 |
| Bearings | 1,403 |
| BusinessGovernance | 7,632 |
| BusinessOperations | 3,005 |
| Calculations | 1,824 |
| Commands | 1,577 |
| CommercialIntelligence | 5,892 |
| Components | 1,373 |
| Concurrency | 179 |
| Configuration | 451 |
| Constants | 778 |
| DependencyInjection | 753 |
| Diagnostics | 161 |
| EngineeringAssets | 3,658 |
| EngineeringData | 610 |
| EngineeringDomain | 5,880 |
| EngineeringIntelligence | 7,622 |
| Events | 247 |
| ExportImport | 653 |
| Fasteners | 1,314 |
| Identity | 800 |
| Input | 250 |
| Knowledge | 2,958 |
| Logging | 927 |
| Macros | 349 |
| Manufacturing | 1,247 |
| Materials | 1,034 |
| Models | 21 |
| Modules | 1,575 |
| Navigation | 395 |
| Notifications | 329 |
| Persistence | 1,777 |
| Plugins | 1,485 |
| ReferenceData | 5,349 |
| Reporting | 342 |
| Requirements | 1,631 |
| Runtime | 1,677 |
| Settings | 595 |
| Standards | 1,151 |
| UnitsAndQuantities | 1,974 |
| Verification | 388 |
| Versioning | 173 |

### src/Tempest.Workspace - Top-Level Subfolder Lines

| Folder | Non-Blank Lines |
|--------|-----------------|
| Composition | 208 |
| Engineering | 1,833 |
| Projects | 2,320 |
| Shell | 674 |
| Workspace | 14,069 |

### src/Tempest.Desktop - Top-Level Subfolder Lines

| Folder | Non-Blank Lines |
|--------|-----------------|
| Branding | 222 |
| Composition | 2,476 |
| Diagnostics | 180 |
| DigitalThread | 1,310 |
| Docking | 1,123 |
| Editors | 976 |
| History | 43 |
| Icons | 227 |
| Input | 185 |
| Tasks | 140 |
| Theming | 1,244 |
| Viewing | 963 |
| Views | 8,045 |

Command:
```bash
cd src/Tempest.Core && find . -maxdepth 1 -type d | sort | while read dir; do
  if [ "$dir" != "." ] && [ "$dir" != ".." ]; then
    dirname=$(echo "$dir" | sed 's/^\.\///')
    line_count=$(find "$dirname" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" -exec cat {} \; 2>/dev/null | grep -v '^[[:space:]]*$' | wc -l)
    if [ "$line_count" -gt 0 ]; then
      echo "$dirname|$line_count"
    fi
  fi
done
```

---

## Section 5: Project References Graph

| Project | ProjectReference Targets |
|---------|--------------------------|
| src/Tempest.Core | (none) |
| src/Tempest.Workspace | src/Tempest.Core/Tempest.Core.csproj |
| src/Tempest.Desktop | src/Tempest.Workspace/Tempest.Workspace.csproj |
| src/Tempest.Harness | src/Tempest.Workspace/Tempest.Workspace.csproj |
| src/Samples/Tempest.Samples | src/Tempest.Core/Tempest.Core.csproj |
| src/Templates/Tempest.Templates.Module | src/Tempest.Core/Tempest.Core.csproj |
| src/Validation/Tempest.Validation | src/Tempest.Core/Tempest.Core.csproj |
| tests/Tempest.Core.Tests | src/Tempest.Core, src/Tempest.Workspace, src/Tempest.Harness, src/Samples/Tempest.Samples, src/Validation/Tempest.Validation |
| tests/Tempest.Desktop.Tests | src/Tempest.Core, src/Tempest.Workspace, src/Tempest.Desktop, src/Samples/Tempest.Samples |

Command:
```bash
grep -o 'ProjectReference.*Include="[^"]*"' *.csproj | sed 's/.*Include="//' | sed 's/"//'
```

---

## Section 6: Key Concept Counts

| Concept | Count |
|---------|-------|
| Command Descriptors (RegisterDescriptor calls) | 96 |
| Desktop Views (public sealed class ... View : UserControl) | 21 |
| IProjectExplorerNodeProvider implementations | 6 |
| IPropertyFacetProvider implementations | 6 |
| IHas interfaces (EngineeringDomain) | 8 |
| EngineeringDomain .cs files | 67 |
| Distinct TD- Work Packages (src + docs) | 176 |
| ADR files | 147 |
| Archive files (all types) | 835 |

Command:
```bash
grep -r "RegisterDescriptor(new CommandDescriptor(" src --include="*.cs" --exclude-dir=Frozen | wc -l
grep -r "public sealed class .*View : UserControl" src --include="*.cs" --exclude-dir=Frozen | wc -l
grep -r "class .* : IProjectExplorerNodeProvider" src --include="*.cs" --exclude-dir=Frozen | wc -l
grep -r "class .* : IPropertyFacetProvider" src --include="*.cs" --exclude-dir=Frozen | wc -l
grep -r "interface IHas" src/Tempest.Core/EngineeringDomain --include="*.cs" | wc -l
grep -r "TD-[0-9]\+" src docs --include="*.cs" --include="*.md" | grep -o "TD-[0-9]\+" | sort -u | wc -l
```

---

## Section 7: Markdown Documentation Volume

| Location | Non-Blank Lines |
|----------|-----------------|
| docs/ (excl. archive) | 38,740 |
| docs/archive/ | 0 |
| repo root .md files | 1,083 |
| **Total Documentation** | **39,823** |
| src (excl. Frozen) code | 117,456 |
| **Documentation : Code Ratio** | **1 : 2.95** |

Command:
```bash
find "docs" -name "*.md" -not -path "*/archive/*" -not -path "*/design-freeze*" -exec cat {} \; | grep -v '^[[:space:]]*$' | wc -l
```

---

## Section 8: Git History Analysis

| Metric | Value |
|--------|-------|
| Total commits | 528 |
| Repository start date | 2026-07-15 |
| Most recent commit | 2026-09-08 |

### Commits Per Month

| Month | Count |
|-------|-------|
| 2026-07 | 94 |
| 2026-08 | 117 |
| 2026-09 | 317 |

### Top 30 Most-Changed Files

| # | Commits | File |
|---|---------|------|
| 1 | 144 | PROJECT_STATUS.md |
| 2 | 107 | docs/governance/Quality/Technical Debt Register.md |
| 3 | 82 | docs/governance/Documentation/Academy Register.md |
| 4 | 72 | docs/governance/Architecture/ADR Register.md |
| 5 | 70 | docs/academy/Academy Index.md |
| 6 | 62 | docs/governance/Documentation/Documentation Register.md |
| 7 | 53 | src/Tempest.Core/Runtime/TempestHost.cs |
| 8 | 44 | docs/governance/Engineering/Namespace Register.md |
| 9 | 44 | docs/architecture/Platform Service Map.md |
| 10 | 33 | src/Tempest.Desktop/MainWindow.cs |
| 11 | 32 | docs/governance/Quality/Test Register.md |
| 12 | 32 | docs/governance/Delivery/Release Register.md |
| 13 | 31 | docs/governance/Future Capability Register.md |
| 14 | 30 | docs/governance/Engineering/Interface Register.md |
| 15 | 28 | docs/governance/Engineering/Exception Register.md |
| 16 | 27 | docs/governance/Governance Index.md |
| 17 | 26 | tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs |
| 18 | 24 | docs/governance/Engineering/Platform Services Register.md |
| 19 | 24 | docs/governance/Architecture/Architecture Document Register.md |
| 20 | 21 | docs/governance/Engineering/Dependency Injection Register.md |
| 21 | 20 | src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs |
| 22 | 20 | docs/releases/v0.16.0/WorkPackages.md |
| 23 | 20 | docs/governance/Quality/Repository Metrics Register.md |
| 24 | 19 | src/Tempest.Desktop/WorkspaceHost.cs |
| 25 | 17 | docs/releases/v0.4.0/WorkPackages.md |
| 26 | 17 | VERSION |
| 27 | 16 | docs/releases/v0.4.0/CHANGELOG.md |
| 28 | 16 | docs/architecture/Engineering Glossary.md |
| 29 | 16 | .github/workflows/ci.yml |
| 30 | 15 | tests/Tempest.Core.Tests/Samples/EngineeringDomainSampleIntegrationTests.cs |

### Work Package Activity (Last 60 Commits)

| Metric | Value |
|--------|-------|
| Commits mentioning 'WP' | 67 |
| Distinct WP numbers | 4 |
| WP numbers touched | WP 10, WP 16, WP 17, WP 18 |

### Lines Added/Deleted (Last 100 Commits)

| Metric | Value |
|--------|-------|
| Lines added | 40,330 |
| Lines deleted | 18,832 |
| Net change | +21,498 |

Command:
```bash
git rev-list --count HEAD
git log --date=format:%Y-%m --format=%ad | sort | uniq -c
git log --format= --name-only | sort | uniq -c | sort -rn | head -30
git log -60 --format=%B | grep -oE "WP [0-9]+" | sort -u | wc -l
git log -100 --shortstat --format= | awk '/file/ { matched+=1; if (match($0, /([0-9]+) insertions/, a)) added+=a[1]; if (match($0, /([0-9]+) deletions/, d)) deleted+=d[1] } END { print "Added: " added "\nDeleted: " deleted }'
```

---

## Section 9: Churn of Key Files

| File | Commits |
|------|---------|
| src/Tempest.Desktop/MainWindow.cs | 33 |
| src/Tempest.Desktop/Editors/ObjectEditorView.cs | 8 |
| src/Tempest.Desktop/Views/EngineeringCalculationView.cs | 10 |
| src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectBase.cs | 20 |
| src/Tempest.Core/Persistence/SqlitePersistenceStore.cs | 4 |
| src/Tempest.Workspace/Workspace/WorkspaceManager.cs | 3 |
| PROJECT_STATUS.md | 144 |
| BACKLOG.md | 5 |

Command:
```bash
git log --format= --name-only -- "src/Tempest.Desktop/MainWindow.cs" | wc -l
```

---

Generated: 2026-09-08
