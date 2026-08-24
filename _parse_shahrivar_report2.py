# -*- coding: utf-8 -*-
import re, html
from pathlib import Path
from collections import Counter

PATH = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405 (1).html")
OUT = Path(r"D:\Hampadco\RealProjects\ShiftYar\_parse_snippet_out.txt")
TEXT = PATH.read_text(encoding="utf-8")

USER = {
    "زهرا درخشانی الوار": (0, 14),
    "حدیث کاظمی": (5, 5),
    "فاطمه رازانی": (8, 3),
    "مریم کرمی": (8, 4),
    "نازنین سبزواری": (8, 4),
    "فاطمه یوسفی کشکولی": (9, 4),
    "فاطمه دبستانیان": (8, 4),
    "مریم امیدی منش": (8, 4),
    "سپیده دریکوند": (9, 3),
    "الناز رستمی چگنی": (8, 4),
    "گلنوش باقری": (8, 5),
    "خدیجه متقی": (4, 5),
    "فاطمه رضایی": (4, 5),
    "کیمیا کاظمی": (8, 5),
    "بهاره بهاری پور": (4, 2),
    "شکیبا موسیوند": (7, 3),
    "عاطفه رحیمی منفرد": (5, 6),
    "فاطمه سلیمی": (4, 8),
    "فاطمه مدهنی": (9, 5),
}

lines = []
lines.append(f"file marks: M={TEXT.count('hw-mark-m')} E={TEXT.count('hw-mark-e')} N={TEXT.count('hw-mark-n')}")

# legend section marks
legend = re.search(r'hw-legend-block.*?</div></div>', TEXT, re.S)
if legend:
    L = legend.group(0)
    lines.append(f"legend marks: M={L.count('hw-mark-m')} E={L.count('hw-mark-e')} N={L.count('hw-mark-n')}")

rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', TEXT, re.S)
lines.append(f"rows={len(rows)}")

# Inspect first user row: all td class attrs
r0 = rows[0]
name0 = html.unescape(re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', r0).group(1).strip())
lines.append(f"sample user: {name0}")

# Use a better td splitter that handles nested tags? Simple regex may break on nested td (unlikely).
# Check if marks sit outside hw-cell
for label in ["hw-mark-m", "hw-mark-e", "hw-mark-n"]:
    positions = [m.start() for m in re.finditer(label, r0)]
    lines.append(f"{name0} {label} count in row={len(positions)}")

# For each mark in row, find nearest enclosing td class
def enclosing_td_class(row, pos):
    before = row[:pos]
    opens = list(re.finditer(r'<td([^>]*)>', before))
    if not opens:
        return "?"
    return opens[-1].group(1)

r0_marks = []
for m in re.finditer(r'hw-mark-([men])', r0):
    attrs = enclosing_td_class(r0, m.start())
    r0_marks.append((m.group(1), 'hw-cell' in attrs, 'hw-sum' in attrs, attrs[:100]))
lines.append(f"first row mark contexts ({len(r0_marks)}):")
ctx_counter = Counter((lab, cell, sm) for lab, cell, sm, _ in r0_marks)
lines.append(str(dict(ctx_counter)))
# show any mark NOT in hw-cell
for lab, cell, sm, attrs in r0_marks:
    if not cell:
        lines.append(f"  NON-cell mark {lab}: {attrs}")

# Try counting marks per row directly (not via day cells)
lines.append("\n=== per-user mark counts in row HTML ===")
tot = Counter()
match_ok = 0
for r in rows:
    nm = re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', r)
    name = html.unescape(nm.group(1).strip()) if nm else "?"
    m = len(re.findall(r'hw-mark-m', r))
    e = len(re.findall(r'hw-mark-e', r))
    n = len(re.findall(r'hw-mark-n', r))
    tot['M'] += m; tot['E'] += e; tot['N'] += n

    # old broken method
    tds = re.findall(r'<td([^>]*)>(.*?)</td>', r, re.S)
    day_cells = [body for attrs, body in tds if 'hw-cell' in attrs and 'hw-sum' not in attrs]
    om = oe = on = 0
    for cell in day_cells[:31]:
        if 'hw-mark-m' in cell: om += 1
        elif 'hw-mark-e' in cell: oe += 1
        elif 'hw-mark-n' in cell: on += 1

    user = USER.get(name)
    if user is None:
        for uk, uv in USER.items():
            if uk.split()[0] in name and uk.split()[-1] in name:
                user = uv
                break
    if user:
        un, ue = user
        ok = (n == un and e == ue)
        if ok: match_ok += 1
        lines.append(f"{'OK' if ok else 'DIFF'} {name}: row N/E={n}/{e} old={on}/{oe} user={un}/{ue} day_cells={len(day_cells)}")
    else:
        lines.append(f"-- {name}: row N/E/M={n}/{e}/{m} old={on}/{oe}/{om} day_cells={len(day_cells)}")

lines.append(f"\nrow-sum totals M/E/N={tot['M']}/{tot['E']}/{tot['N']}")
lines.append(f"user matches: {match_ok}/{len(USER)}")

# Why day_cells wrong? Check td count and classes
lines.append("\n=== td class inventory sample user ===")
tds = re.findall(r'<td([^>]*)>(.*?)</td>', r0, re.S)
class_counts = Counter()
for attrs, body in tds:
    cm = re.search(r'class="([^"]*)"', attrs)
    cls = cm.group(1) if cm else "(none)"
    has = ("M" if "hw-mark-m" in body else "") + ("E" if "hw-mark-e" in body else "") + ("N" if "hw-mark-n" in body else "")
    class_counts[cls] += 1
    if has and "hw-cell" not in cls:
        lines.append(f"mark in non-hw-cell: class={cls} has={has} body={body[:80]}")
lines.append(f"td class counts: {dict(class_counts)}")
lines.append(f"total tds={len(tds)}")

# Check if nested spans break </td> regex - look for day with both?
# Also check: maybe HTML uses self-closing or different structure for some days
# Extract day index from data-day or title?
data_attrs = sorted(set(re.findall(r'\b(data-[a-zA-Z0-9-]+)=', TEXT)))
lines.append(f"data attrs: {data_attrs}")

# Look at a few empty vs filled cells raw
filled = [(i, attrs, body[:150]) for i, (attrs, body) in enumerate(tds) if "hw-mark-" in body][:3]
empty = [(i, attrs, body[:80]) for i, (attrs, body) in enumerate(tds) if "hw-cell" in attrs and "hw-mark-" not in body][:3]
lines.append(f"filled samples: {filled}")
lines.append(f"empty samples: {empty}")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("OK wrote", OUT)
