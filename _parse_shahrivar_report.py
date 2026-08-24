import re
import html
from pathlib import Path

p = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405.html")
text = p.read_text(encoding="utf-8")

rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', text, re.S)
print(f"rows={len(rows)}")

users = []
for row in rows:
    name_m = re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', row)
    if not name_m:
        continue
    name = html.unescape(name_m.group(1).strip())
    marks = re.findall(r'hw-mark-([men])"[^>]*>([^<]*)', row)
    m = sum(1 for t, _ in marks if t == "m")
    e = sum(1 for t, _ in marks if t == "e")
    n = sum(1 for t, _ in marks if t == "n")
    users.append({"name": name, "M": m, "E": e, "N": n, "total": m + e + n})

out = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_analysis.txt")
lines = []
lines.append(f"rows={len(rows)}")

users.sort(key=lambda x: (-x["N"], x["name"]))
for u in users:
    lines.append(f"{u['name']}|M={u['M']}|E={u['E']}|N={u['N']}|T={u['total']}")

# daily column totals from footer
footer = re.search(r'hw-footer.*?</tr>', text, re.S)
if footer:
    fm = re.findall(r'hw-sum-([men])"[^>]*>(\d+)', footer.group(0))
    lines.append("FOOTER_SUMS " + str(fm))

lines.append("")
lines.append("CONSECUTIVE_NIGHTS:")
for row in rows:
    name_m = re.search(r'class="hw-name[^"]*"[^>]*>([^<]+)', row)
    if not name_m:
        continue
    name = html.unescape(name_m.group(1).strip())
    cells = re.findall(r'hw-mark-([men])"', row)
    runs = []
    run = 0
    for c in cells:
        if c == "n":
            run += 1
        else:
            if run > 1:
                runs.append(run)
            run = 0
    if run > 1:
        runs.append(run)
    if runs:
        lines.append(f"{name}: max_consecutive={max(runs)} runs={runs}")

out.write_text("\n".join(lines), encoding="utf-8")
print("written", out)
