"""Renders the ADR register and the technical debt register (BACKLOG.md) as
HTML documents in the Tempest document style, into docs/governance/html/.

Usage (from anywhere):  python3 scripts/generate-registers.py
Re-run after the ADR Register or BACKLOG.md changes (each release).
"""
import re, html, sys, datetime, collections, pathlib
ROOT = str(pathlib.Path(__file__).resolve().parent.parent)
OUT = sys.argv[1] if len(sys.argv) > 1 else f'{ROOT}/docs/governance/html'
logo = open(f'{ROOT}/docs/design/system/assets/logo/derived/logo-os-horizontal-transparent-ink.svg').read()
logo = re.sub(r'<\?xml[^>]*\?>', '', logo)
logo = re.sub(r'<svg ', '<svg class="logo" aria-label="TempestOS" ', logo, count=1)

def inline(t):
    t = html.escape(t, quote=False)
    t = re.sub(r'`([^`]+)`', r'<code>\1</code>', t)
    t = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', t)
    t = re.sub(r'(?<![\w*])\*([^*\n]+)\*(?![\w*])', r'<em>\1</em>', t)
    t = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'\1', t)
    return t

def cells(line):
    s = line.strip()
    if s.startswith('|'): s = s[1:]
    if s.endswith('|'): s = s[:-1]
    out, buf, tick = [], '', False
    for ch in s:
        if ch == '`': tick = not tick
        if ch == '|' and not tick: out.append(buf.strip()); buf = ''
        else: buf += ch
    out.append(buf.strip())
    return [c.replace('\\|', '|') for c in out]

def tables_by_section(md):
    sections, cur, h2, rows, header = [], None, None, [], None
    lines = md.split('\n')
    i = 0
    result = []  # (h2, h3, header, rows, intro)
    h2 = h3 = None; intro = []
    while i < len(lines):
        l = lines[i]
        if l.startswith('## '): h2 = l[3:].strip(); h3 = None; intro = []
        elif l.startswith('### '): h3 = l[4:].strip(); intro = []
        elif l.startswith('|') and i + 1 < len(lines) and re.match(r'^\|\s*:?-{3,}', lines[i+1]):
            header = cells(l); rows = []; i += 2
            while i < len(lines) and lines[i].startswith('|'):
                rows.append(cells(lines[i])); i += 1
            result.append((h2, h3, header, rows, ' '.join(intro).strip()))
            intro = []
            continue
        elif l.strip() and not l.startswith('#'):
            intro.append(l.strip())
        else:
            if not l.strip(): pass
        i += 1
    return result

CSS = '''
:root{--paper-000:#fff;--paper-050:#f5f6fa;--paper-100:#eff0f5;--paper-200:#dfe0e4;--paper-300:#bec1c9;--slate-500:#82848e;--slate-600:#4b5160;--slate-700:#31343f;--ink-900:#16181d;--indigo-600:#1c2d97;--indigo-500:#2a3fbe;--cyan-600:#2b7fa5;--cyan-500:#40a2ce;--green-500:#12b981;--amber-500:#f5a524;--red-500:#e5484d;--ink-a08:rgba(22,24,29,.08);--ink-a14:rgba(22,24,29,.14);--cyan-a12:rgba(64,162,206,.12);
--font-display:"Chakra Petch",system-ui,sans-serif;--font-body:"Inter","Helvetica Neue",system-ui,sans-serif;--font-mono:"Space Mono",ui-monospace,monospace;
--bg:var(--paper-050);--sheet:var(--paper-000);--text:var(--slate-700);--strong:var(--ink-900);--muted:var(--slate-600);--rule:var(--ink-a08);--rule2:var(--ink-a14);--head:var(--paper-100);--accent:var(--indigo-600);--id:var(--cyan-600)}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#0b0d12;--sheet:#12151c;--text:#c9ccd6;--strong:#f5f6fa;--muted:#9195a2;--rule:rgba(245,246,250,.08);--rule2:rgba(245,246,250,.14);--head:#191d27;--accent:#7f8cff;--id:#68bde2}.logo{filter:invert(1) hue-rotate(180deg)}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font-family:var(--font-body);font-size:13.5px;line-height:1.5}
.sheet{max-width:1180px;margin:24px auto;background:var(--sheet);padding:32px 36px 24px;box-shadow:0 1px 3px var(--rule2)}
header.doc{display:flex;align-items:center;justify-content:space-between;gap:24px;border-bottom:2px solid var(--accent);padding-bottom:12px;flex-wrap:wrap}
.logo{height:38px;width:auto;display:block}
.eyebrow{font-family:var(--font-display);font-weight:600;font-size:13px;letter-spacing:.18em;color:var(--accent);text-align:right}
.ref{font-family:var(--font-mono);font-size:10.5px;letter-spacing:.08em;color:var(--muted);margin-top:4px;text-align:right}
h1{font-family:var(--font-display);font-weight:700;font-size:26px;letter-spacing:-.01em;color:var(--strong);margin:22px 0 4px}
h2{font-family:var(--font-display);font-weight:600;font-size:17px;color:var(--strong);margin:30px 0 6px;padding-bottom:4px;border-bottom:1px solid var(--rule2)}
h3{font-family:var(--font-display);font-weight:600;font-size:14px;color:var(--strong);margin:20px 0 6px}
.lede{color:var(--muted);max-width:820px;margin:0 0 6px}
.kpis{display:flex;gap:10px;flex-wrap:wrap;margin:16px 0 4px}
.kpi{border:1px solid var(--rule2);padding:10px 14px;min-width:120px}
.kpi b{display:block;font-family:var(--font-display);font-size:24px;color:var(--strong);font-weight:600;line-height:1.1}
.kpi span{font-family:var(--font-mono);font-size:10px;letter-spacing:.1em;color:var(--muted);text-transform:uppercase}
.tools{display:flex;gap:10px;align-items:center;margin:16px 0 8px;flex-wrap:wrap}
.tools input{font:inherit;padding:7px 10px;border:1px solid var(--rule2);background:var(--sheet);color:var(--strong);min-width:280px;flex:1;max-width:460px}
.tools select{font:inherit;padding:7px 8px;border:1px solid var(--rule2);background:var(--sheet);color:var(--strong)}
.count{font-family:var(--font-mono);font-size:11px;color:var(--muted)}
.wrap{overflow-x:auto}
table{width:100%;border-collapse:collapse;font-size:12.5px;table-layout:fixed}
td{overflow-wrap:anywhere}
th{text-align:left;padding:7px 8px;font-family:var(--font-mono);font-size:10px;letter-spacing:.1em;color:var(--muted);font-weight:400;border-bottom:1px solid var(--rule2);background:var(--head);text-transform:uppercase;position:sticky;top:0}
td{padding:7px 8px;border-bottom:1px solid var(--rule);vertical-align:top}
td.id{font-family:var(--font-mono);color:var(--id);white-space:nowrap}
td.t{color:var(--strong)}
td.date{font-family:var(--font-mono);font-size:11px;white-space:nowrap}
code{font-family:var(--font-mono);font-size:.88em;background:var(--cyan-a12);padding:0 3px}
.chip{font-family:var(--font-mono);font-size:10px;letter-spacing:.06em;padding:2px 6px;white-space:nowrap;display:inline-block;border:1px solid var(--rule2)}
.chip.ok{background:rgba(18,185,129,.14);border-color:rgba(18,185,129,.4)}
.chip.warn{background:rgba(245,165,36,.16);border-color:rgba(245,165,36,.45)}
.chip.bad{background:rgba(229,72,77,.14);border-color:rgba(229,72,77,.4)}
details summary{cursor:pointer;color:var(--accent);font-family:var(--font-mono);font-size:10.5px;letter-spacing:.06em}
details[open] summary{margin-bottom:4px}
.detail{color:var(--text);font-size:12px;max-width:760px}
footer.doc{display:flex;justify-content:space-between;gap:16px;border-top:1px solid var(--rule2);padding-top:8px;margin-top:28px;font-family:var(--font-mono);font-size:10px;letter-spacing:.06em;color:var(--muted);flex-wrap:wrap}
@media (max-width:640px){.sheet{margin:0;padding:18px 16px}.eyebrow,.ref{text-align:left}h1{font-size:21px}.tools input{min-width:0}}
@media print{body{background:#fff}.sheet{box-shadow:none;margin:0;max-width:none}.tools{display:none}details{display:block}details>summary{display:none}details>.detail{display:block}th{position:static}}
'''
FONTS = '<link rel="preconnect" href="https://fonts.googleapis.com"><link href="https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@400;600;700&family=Inter:wght@400;500;600&family=Space+Mono&display=swap" rel="stylesheet">'
JS = '''<script>
document.querySelectorAll('[data-filter]').forEach(function(box){
  var input=box.querySelector('input'), sel=box.querySelector('select'), count=box.querySelector('.count');
  var rows=[].slice.call(document.querySelectorAll(box.getAttribute('data-filter')));
  function apply(){var q=(input.value||'').toLowerCase(), s=sel?sel.value:'', n=0;
    rows.forEach(function(r){var ok=(!q||r.textContent.toLowerCase().indexOf(q)>=0)&&(!s||r.getAttribute('data-k')===s);r.style.display=ok?'':'none';if(ok)n++});
    if(count)count.textContent=n+' of '+rows.length+' shown';}
  input.addEventListener('input',apply); if(sel)sel.addEventListener('change',apply); apply();
});
</script>'''
today = datetime.date.today().strftime('%d/%m/%Y')

def page(title, eyebrow, ref, body):
    return f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{html.escape(title)}</title>{FONTS}<style>{CSS}</style></head><body><div class="sheet">
<header class="doc">{logo}<div><div class="eyebrow">{eyebrow}</div><div class="ref">{ref}</div></div></header>
{body}
<footer class="doc"><span>Tempest Design Engineering Ltd · Company No. 17349874</span><span>TempestOS · generated {today} from the repository</span></footer>
</div>{JS}</body></html>'''

def chip_cls(status):
    s = status.lower()
    if 'supersed' in s or 'reject' in s or 'withdrawn' in s: return 'bad'
    if 'proposed' in s or 'amended' in s or 'partial' in s: return 'warn'
    return 'ok'

# ---------- ADR register ----------
md = open(f'{ROOT}/docs/governance/Architecture/ADR Register.md').read()
tabs = [t for t in tables_by_section(md) if t[2] and t[2][0] == 'ADR']
rows = [r for t in tabs for r in t[3] if r and r[0].startswith('ADR-')]
statuses = collections.Counter(re.match(r'[A-Za-z]+', re.sub(r'[*`]', '', r[2]).strip()).group(0) for r in rows)
trs = []
for r in rows:
    adr, title, status, origin, date, ver = (r + ['']*6)[:6]
    st = re.sub(r'\*', '', status)
    key = re.match(r'[A-Za-z]+', st.replace('`','').strip()).group(0)
    trs.append(f'<tr class="adr" data-k="{html.escape(key)}"><td class="id">{html.escape(adr)}</td><td class="t">{inline(title)}<details><summary>ORIGIN &amp; SCOPE</summary><div class="detail">{inline(origin)}</div></details></td><td><span class="chip {chip_cls(st)}">{inline(status)}</span></td><td class="date">{inline(date)}</td><td>{inline(ver)}</td></tr>')
opts = ''.join(f'<option value="{html.escape(k)}">{html.escape(k)} ({v})</option>' for k, v in statuses.most_common())
kpis = f'<div class="kpi"><b>{len(rows)}</b><span>ADRs</span></div>' + ''.join(f'<div class="kpi"><b>{v}</b><span>{html.escape(k)}</span></div>' for k, v in statuses.most_common(4))
body = f'''<h1>Architecture Decision Record register</h1>
<p class="lede">Every architecture decision TempestOS has recorded, from <code>docs/adr/</code>, with its status, date and the work package that produced it. Open <em>Origin &amp; scope</em> on any row for the decision's full summary.</p>
<div class="kpis">{kpis}</div>
<div class="tools" data-filter="tr.adr"><input type="search" placeholder="Search ADRs — number, title, scope…" aria-label="Search ADRs"><select aria-label="Status"><option value="">All statuses</option>{opts}</select><span class="count"></span></div>
<div class="wrap"><table><thead><tr><th style="width:96px">ADR</th><th>Title</th><th style="width:190px">Status</th><th style="width:200px">Date</th><th style="width:80px">Verified</th></tr></thead><tbody>{''.join(trs)}</tbody></table></div>'''
open(f'{OUT}/ADR Register.html', 'w').write(page('ADR Register', 'ADR REGISTER', f'TDE-OS-GOV-ADR · {len(rows)} RECORDS · ISSUED {today}', body))

# ---------- Technical debt register (BACKLOG.md) ----------
md = open(f'{ROOT}/BACKLOG.md').read()
tabs = tables_by_section(md)
groups = collections.OrderedDict()
for h2, h3, header, rows_, intro in tabs:
    if not header or header[0] != 'ID': continue
    groups.setdefault(h2, []).append((h3, header, rows_, intro))
def count(h2): return sum(len(r) for _, _, r, _ in groups.get(h2, []))
names = list(groups)
live = [n for n in names if n.startswith('Live Backlog')]
owned = [n for n in names if n.startswith('Owned')]
arch = [n for n in names if n.startswith('Archived')]
allrows = sum(count(n) for n in names)
kpis = ''.join(f'<div class="kpi"><b>{c}</b><span>{l}</span></div>' for l, c in [('Open (live backlog)', sum(count(n) for n in live)), ('Closed', sum(count(n) for n in owned)), ('Archived with a layer', sum(count(n) for n in arch)), ('Total rows', allrows)])
parts = []
for n in names:
    label = 'Open' if n in live else 'Closed' if n in owned else 'Archived'
    nice = re.sub(r'\s*\(.*\)$', '', n)
    parts.append(f'<h2>{inline(nice)} <span class="count">· {count(n)} row(s)</span></h2>')
    for h3, header, rows_, intro in groups[n]:
        if h3: parts.append(f'<h3>{inline(h3)}</h3>')
        th = ''.join(f'<th{" style=\"width:120px\"" if i == 0 else " style=\"width:36%\"" if i == 1 else ""}>{inline(h)}</th>' for i, h in enumerate(header))
        trs = []
        for r in rows_:
            r = (r + [''] * len(header))[:len(header)]
            tds = [f'<td class="id">{inline(r[0])}</td>', f'<td class="t">{inline(r[1])}</td>'] + [f'<td>{inline(c)}</td>' for c in r[2:]]
            trs.append(f'<tr class="td" data-k="{label}">{"".join(tds)}</tr>')
        parts.append(f'<div class="wrap"><table><thead><tr>{th}</tr></thead><tbody>{"".join(trs)}</tbody></table></div>')
body = f'''<h1>Technical debt register</h1>
<p class="lede">The live technical-debt list (<code>BACKLOG.md</code>), which replaced the frozen 170-row register in <code>WP 17.0B</code>: what is still open, what each work package closed and how, and what was archived with a frozen layer.</p>
<div class="kpis">{kpis}</div>
<div class="tools" data-filter="tr.td"><input type="search" placeholder="Search debt — ID, title, owner, fix…" aria-label="Search the register"><select aria-label="State"><option value="">Open, closed and archived</option><option value="Open">Open only</option><option value="Closed">Closed only</option><option value="Archived">Archived only</option></select><span class="count"></span></div>
{''.join(parts)}'''
open(f'{OUT}/Technical Debt Register.html', 'w').write(page('Technical Debt Register', 'TECHNICAL DEBT REGISTER', f'TDE-OS-GOV-TDR · {allrows} ROWS · ISSUED {today}', body))
print(len(rows), 'adr;', allrows, 'td rows;', [(n, count(n)) for n in names])
