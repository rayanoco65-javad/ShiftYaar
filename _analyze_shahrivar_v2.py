# -*- coding: utf-8 -*-
import re, html
from pathlib import Path

TEXT = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405 (1).html").read_text(encoding="utf-8")
OUT = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_report_review_v2.txt")

QUOTAS = {
    "سپیده دریکوند": 9, "فاطمه مدهنی": 9, "فاطمه یوسفی کشکولی": 9,
    "فاطمه رازانی": 8, "حدیث کاظمی": 8, "مریم امیدی منش": 8,
    "فاطمه دبستانیان": 8, "مریم کرمی": 8, "نازنین سبزواری": 8,
    "گلنوش باقری": 8, "الناز رستمی چگنی": 8, "شکیبا موسیوند": 7,
    "فاطمه رضایی": 5, "عاطفه رحیمی منفرد": 5,
    "خدیجه متقی": 4, "فاطمه سلیمی": 4, "کیمیا کاظمی": 4, "زهرا درخشانی الوار": 4,
}

def parse_row(row_html):
    name_m = re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', row_html)
    name = html.unescape(name_m.group(1).strip()) if name_m else "?"
    tds = re.findall(r'<td([^>]*)>(.*?)</td>', row_html, re.S)
    day_cells = [body for attrs, body in tds if 'hw-cell' in attrs and 'hw-sum' not in attrs]
    days = []
    for cell in day_cells[:31]:
        if 'hw-mark-m' in cell: days.append('M')
        elif 'hw-mark-e' in cell: days.append('E')
        elif 'hw-mark-n' in cell: days.append('N')
        else: days.append('-')
    return name, days

source = re.search(r'hw-report-source[^>]*>([^<]+)', TEXT)
source_txt = html.unescape(source.group(1).strip()) if source else "?"

rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', TEXT, re.S)
users = [parse_row(r) for r in rows if parse_row(r)[0] != "?"]

lines = [
    "=== تحلیل شیفت‌بندی شهریور 1405 (نسخه جدید) ===",
    source_txt,
    f"پرسنل: {len(users)}",
    "",
    "=== ۱. پوشش روزانه ===",
]
bad = 0
for i in range(31):
    dm = sum(1 for _, d in users if len(d) > i and d[i] == 'M')
    de = sum(1 for _, d in users if len(d) > i and d[i] == 'E')
    dn = sum(1 for _, d in users if len(d) > i and d[i] == 'N')
    issues = []
    if dm != 4: issues.append(f"صبح={dm}")
    if de != 3: issues.append(f"عصر={de}")
    if dn != 4: issues.append(f"شب={dn}")
    if issues:
        bad += 1
        lines.append(f"روز {i+1}: {', '.join(issues)}")
lines.append(f"{'✓ 31/31 روز کامل' if bad == 0 else f'⚠ {bad}/31 روز ناقص'}")
tm, te, tn = sum(d.count('M') for _,d in users), sum(d.count('E') for _,d in users), sum(d.count('N') for _,d in users)
lines.append(f"جمع: M={tm}/124 E={te}/93 N={tn}/124")

lines += ["", "=== ۲. سهمیه شب ==="]
ok = under = over = 0
for name, days in sorted(users, key=lambda x: -x[1].count('N')):
    n = days.count('N')
    q = QUOTAS.get(name)
    if q is None:
        if n: lines.append(f"• {name}: {n} شب")
        continue
    if n < q:
        under += 1; lines.append(f"❌ {name}: {n}/{q} (-{q-n})")
    elif n > q:
        over += 1; lines.append(f"⚠ {name}: {n}/{q} (+{n-q})")
    else:
        ok += 1; lines.append(f"✓ {name}: {n}/{q}")
lines.append(f"خلاصه: {ok} OK | {under} کمبود | {over} مازاد")

lines += ["", "=== ۳. شب متوالی (>2) ==="]
cons = []
for name, days in users:
    run = mx = 0
    for s in days:
        run = run + 1 if s == 'N' else 0
        mx = max(mx, run)
    if mx > 2: cons.append(f"⚠ {name}: {mx}")
lines += cons or ["✓ OK"]

lines += ["", "=== ۴. شب→صبح ==="]
nm = sum(1 for _, d in users for i in range(len(d)-1) if d[i]=='N' and d[i+1]=='M')
lines.append("✓ OK" if nm == 0 else f"❌ {nm} مورد")

lines += ["", "=== ۵. پرسنل ==="]
for name, days in sorted(users, key=lambda x: -(x[1].count('M')+x[1].count('E')+x[1].count('N'))):
    m,e,n = days.count('M'), days.count('E'), days.count('N')
    if m+e+n: lines.append(f"{name}: M={m} E={e} N={n} T={m+e+n}")

OUT.write_text("\n".join(lines), encoding='utf-8')
print('OK')
