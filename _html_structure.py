# -*- coding: utf-8 -*-
import re, html
from pathlib import Path
from collections import Counter

TEXT = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405 (1).html").read_text(encoding="utf-8")
rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', TEXT, re.S)
OUT = Path(r"D:\Hampadco\RealProjects\ShiftYar\_html_structure.txt")
lines = []

multi = []
for r in rows:
    nm = html.unescape(re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', r).group(1).strip())
    tds = re.findall(r'<td([^>]*)>(.*?)</td>', r, re.S)
    for attrs, body in tds:
        if 'hw-cell' not in attrs or 'hw-sum' in attrs:
            continue
        cm = 'hw-mark-m' in body
        ce = 'hw-mark-e' in body
        cn = 'hw-mark-n' in body
        c = sum([cm, ce, cn])
        if c > 1:
            multi.append((nm, attrs, body[:200]))

lines.append(f"multi-mark cells: {len(multi)}")
for x in multi[:20]:
    lines.append(str(x))

# per-cell: elif vs independent
for r in rows:
    nm = html.unescape(re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', r).group(1).strip())
    tds = re.findall(r'<td([^>]*)>(.*?)</td>', r, re.S)
    elif_m = elif_e = elif_n = 0
    ind_m = ind_e = ind_n = 0
    span_m = span_e = span_n = 0
    for attrs, body in tds:
        if 'hw-cell' not in attrs or 'hw-sum' in attrs:
            continue
        if 'hw-mark-m' in body: elif_m += 1
        elif 'hw-mark-e' in body: elif_e += 1
        elif 'hw-mark-n' in body: elif_n += 1
        if 'hw-mark-m' in body: ind_m += 1
        if 'hw-mark-e' in body: ind_e += 1
        if 'hw-mark-n' in body: ind_n += 1
        span_m += len(re.findall(r'class="hw-mark hw-mark-m"', body))
        span_e += len(re.findall(r'class="hw-mark hw-mark-e"', body))
        span_n += len(re.findall(r'class="hw-mark hw-mark-n"', body))
    if (elif_m, elif_e, elif_n) != (span_m, span_e, span_n):
        lines.append(f"DIFF {nm}: elif={elif_m}/{elif_e}/{elif_n} span={span_m}/{span_e}/{span_n} ind={ind_m}/{ind_e}/{ind_n}")

# daily coverage with correct counts
lines.append("\n=== daily coverage (span count) ===")
daily = {i: Counter() for i in range(1, 32)}
for r in rows:
    tds = re.findall(r'<td([^>]*)>(.*?)</td>', r, re.S)
    for attrs, body in tds:
        dm = re.search(r'data-day="(\d+)"', attrs)
        if not dm:
            continue
        day = int(dm.group(1))
        if 'hw-mark-m' in body: daily[day]['M'] += 1
        if 'hw-mark-e' in body: daily[day]['E'] += 1
        if 'hw-mark-n' in body: daily[day]['N'] += 1

bad = 0
for d in range(1, 32):
    c = daily[d]
    issues = []
    if c['M'] != 4: issues.append(f"M={c['M']}")
    if c['E'] != 3: issues.append(f"E={c['E']}")
    if c['N'] != 4: issues.append(f"N={c['N']}")
    if issues:
        bad += 1
        lines.append(f"day {d}: {', '.join(issues)}")
lines.append(f"bad days: {bad}/31")
lines.append(f"totals M={sum(daily[d]['M'] for d in daily)} E={sum(daily[d]['E'] for d in daily)} N={sum(daily[d]['N'] for d in daily)}")

OUT.write_text("\n".join(lines), encoding='utf-8')
print('ok')
