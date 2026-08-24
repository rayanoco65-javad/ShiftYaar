import re
from pathlib import Path

text = Path(r"C:\Users\Paria\Downloads\گزارش_گروهی_شیفت_بیمارستان_آیت_اله_مدنی_اورژانس_اطفال_شهریور_1405.html").read_text(encoding="utf-8")
lines = []

for target in ["فاطمه رازانی", "شکیبا موسیوند", "صبا حاتمی", "فاطمه یوسفی"]:
    idx = text.find(target)
    snippet = text[idx:idx+5000]
    row_end = snippet.find("</tr>")
    row = snippet[:row_end]
    marks_in_row = re.findall(r'hw-mark-([men])"', row)
    cells = re.findall(r'<td[^>]*>(.*?)</td>', row, re.S)
    lines.append(f"\n=== {target} ===")
    lines.append(f"all marks: {marks_in_row.count('m')}M {marks_in_row.count('e')}E {marks_in_row.count('n')}N")
    lines.append(f"td count: {len(cells)}")
    day_shifts = []
    for cell in cells:
        if 'hw-mark-m' in cell: day_shifts.append('M')
        elif 'hw-mark-e' in cell: day_shifts.append('E')
        elif 'hw-mark-n' in cell: day_shifts.append('N')
        elif 'hw-name' in cell: day_shifts.append('N')
        else: day_shifts.append('.')
    lines.append(f"cells pattern len={len(day_shifts)}: {''.join(day_shifts)}")

Path(r"D:\Hampadco\RealProjects\ShiftYar\_parse_snippet_out.txt").write_text("\n".join(lines), encoding="utf-8")
