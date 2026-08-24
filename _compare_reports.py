from pathlib import Path
import re, html

def extract(path):
    t = Path(path).read_text(encoding='utf-8')
    src = re.search(r'hw-report-source[^>]*>([^<]+)', t)
    rows = re.findall(r'<tr[^>]*class="hw-row[^"]*"[^>]*>(.*?)</tr>', t, re.S)
    marks = []
    for r in rows:
        for cell in re.findall(r'<td[^>]*>(.*?)</td>', r, re.S):
            if 'hw-mark-m' in cell: marks.append('M')
            elif 'hw-mark-e' in cell: marks.append('E')
            elif 'hw-mark-n' in cell: marks.append('N')
    return html.unescape(src.group(1).strip()) if src else '?', len(rows), ''.join(marks)

files = [
    r'C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405.html',
    r'C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405 (1).html',
]
results = []
for p in files:
    if Path(p).exists():
        results.append((Path(p).name, extract(p)))
    else:
        results.append((Path(p).name, None))

lines = []
for name, data in results:
    lines.append(f"{name}: {data}")

if len(results) == 2 and all(r[1] for r in results):
    same = results[0][1][2] == results[1][1][2]
    lines.append(f"SAME_SCHEDULE: {same}")

Path(r"D:\Hampadco\RealProjects\ShiftYar\_compare_reports_out.txt").write_text("\n".join(lines), encoding="utf-8")
