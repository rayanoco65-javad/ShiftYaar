# -*- coding: utf-8 -*-
import re, html
from pathlib import Path

TEXT = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405.html").read_text(encoding="utf-8")
OUT = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_report_review.txt")

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

rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', TEXT, re.S)
users = [parse_row(r) for r in rows]
users = [(n, d) for n, d in users if n != "?"]

lines = [
    "=== تحلیل شیفت‌بندی شهریور 1405 — اورژانس اطفال ===",
    "منبع: برنامه ذخیره‌شده (1405/06/01 23:54)",
    f"پرسنل: {len(users)} نفر",
    "",
    "=== ۱. پوشش روزانه (هدف: 4 صبح، 3 عصر، 4 شب) ===",
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
lines.append(f"{'✓ همه 31 روز پوشش کامل' if bad == 0 else f'⚠ {bad} روز ناقص از 31'}")
lines.append(f"جمع ماه: صبح={sum(d.count('M') for _,d in users)} عصر={sum(d.count('E') for _,d in users)} شب={sum(d.count('N') for _,d in users)}")

lines += ["", "=== ۲. سهمیه شب ==="]
ok = under = over = 0
for name, days in sorted(users, key=lambda x: (-days.count('N'), x[0]) if False else (-len([1 for dd in x[1] if dd=='N']), x[0])):
    n = days.count('N')
    q = QUOTAS.get(name)
    if q is None:
        if n > 0:
            lines.append(f"• {name}: {n} شب (بدون سهمیه/ثابت)")
        continue
    if n < q:
        under += 1
        lines.append(f"❌ {name}: {n}/{q} (کمبود {q-n})")
    elif n > q:
        over += 1
        lines.append(f"⚠ {name}: {n}/{q} (مازاد {n-q})")
    else:
        ok += 1
        lines.append(f"✓ {name}: {n}/{q}")
lines.append(f"خلاصه: {ok} مطابق، {under} کمبود، {over} مازاد")

lines += ["", "=== ۳. شب متوالی (سقف dept: 2) ==="]
cons_issues = []
for name, days in users:
    run = mx = 0
    for s in days:
        if s == 'N':
            run += 1
            mx = max(mx, run)
        else:
            run = 0
    if mx > 2:
        cons_issues.append(f"⚠ {name}: {mx} شب پشت‌سرهم")
lines += cons_issues or ["✓ بیش از 2 شب متوالی نیست"]

lines += ["", "=== ۴. شب → صبح روز بعد (ممنوع) ==="]
nm = [f"❌ {n}: روز {i+1}→{i+2}" for n, d in users for i in range(len(d)-1) if d[i]=='N' and d[i+1]=='M']
lines += nm[:15] if nm else ["✓ موردی نیست"]
if len(nm) > 15:
    lines.append(f"... و {len(nm)-15} مورد دیگر")

lines += ["", "=== ۵. توزیع بار (گردشی) ==="]
for name, days in sorted(users, key=lambda x: -(x[1].count('M')+x[1].count('E')+x[1].count('N'))):
    m, e, n = days.count('M'), days.count('E'), days.count('N')
    if m + e + n == 0:
        continue
    lines.append(f"{name}: M={m} E={e} N={n} | جمع={m+e+n}")

lines += ["", "=== ۶. جمع‌بندی ==="]
if under == 0 and bad == 0 and not cons_issues and not nm:
    lines.append("✅ از نظر سهمیه شب و پوشش روزانه قابل قبول است.")
else:
    if under: lines.append(f"• {under} نفر سهمیه شب کامل نشده")
    if bad: lines.append(f"• {bad} روز پوشش ناقص")
    if cons_issues: lines.append(f"• {len(cons_issues)} نفر بیش از 2 شب متوالی")
    if nm: lines.append(f"• {len(nm)} مورد شب→صبح")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("written")
