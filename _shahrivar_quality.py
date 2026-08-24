# -*- coding: utf-8 -*-
"""Analyze Shahrivar 1405 pediatrics schedule quality from DB exports."""
import re
from collections import Counter, defaultdict
from pathlib import Path

USERS = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_users.txt")
ASG = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_asg.txt")
OUT = Path(r"D:\Hampadco\RealProjects\ShiftYar\_shahrivar_quality.txt")

LABEL = {0: "M", 1: "E", 2: "N"}

def parse_users():
    users = {}
    for line in USERS.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("-") or line.startswith("Id|") or "rows affected" in line:
            continue
        parts = line.split("|")
        if len(parts) < 4:
            continue
        try:
            uid = int(parts[0])
        except ValueError:
            continue
        users[uid] = {
            "name": parts[1],
            "shift_type": int(parts[2]),
            "shift_sub": int(parts[3]),
        }
    return users

def parse_asg():
    # unique (user, day, label)
    rows = set()
    raw_count = 0
    for line in ASG.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("-") or line.startswith("UserId|") or "rows affected" in line:
            continue
        parts = line.split("|")
        if len(parts) < 5:
            continue
        try:
            uid = int(parts[0])
            day = int(parts[1].split("/")[-1])
            label = int(parts[4])
        except ValueError:
            continue
        raw_count += 1
        rows.add((uid, day, label))
    return rows, raw_count

def runs_of(bits):
    """bits: list of 0/1 length 31. Return list of (start_day, end_day, length, value)."""
    out = []
    i = 0
    n = len(bits)
    while i < n:
        j = i
        while j < n and bits[j] == bits[i]:
            j += 1
        out.append((i + 1, j, j - i, bits[i]))
        i = j
    return out

def main():
    users = parse_users()
    asg, raw_count = parse_asg()
    lines = []
    def log(s=""):
        lines.append(s)

    log("=== تحلیل کیفیت شیفت‌بندی شهریور ۱۴۰۵ — اورژانس اطفال (از دیتابیس) ===")
    log(f"پرسنل دپارتمان: {len(users)}")
    log(f"ردیف خام انتساب: {raw_count} | یکتا (کاربر،روز،برچسب): {len(asg)}")

    by_user = defaultdict(lambda: defaultdict(set))
    for uid, day, label in asg:
        by_user[uid][day].add(LABEL.get(label, "?"))

    # coverage
    daily = {d: Counter() for d in range(1, 32)}
    for uid, days in by_user.items():
        for d, labs in days.items():
            for lab in labs:
                daily[d][lab] += 1

    log("\n=== ۱. پوشش روزانه (هدف M=4 E=3 N=4) — بدون شمارش تکراری ===")
    bad = 0
    for d in range(1, 32):
        c = daily[d]
        issues = []
        if c["M"] != 4: issues.append(f"M={c['M']}")
        if c["E"] != 3: issues.append(f"E={c['E']}")
        if c["N"] != 4: issues.append(f"N={c['N']}")
        if issues:
            bad += 1
            log(f"روز {d}: {', '.join(issues)}")
    tot = Counter()
    for c in daily.values():
        tot.update(c)
    log(f"{'✓ همه روزها کامل' if bad == 0 else f'⚠ {bad}/31 ناقص/مازاد'} | جمع M={tot['M']} E={tot['E']} N={tot['N']}")

    # per-user quality
    log("\n=== ۲. خوشه‌بندی کار / استراحت ===")
    log("W = روز کاری (حداقل یک شیفت) | . = OFF")
    log("هشدار: ≥۴ روز کار متوالی یا ≥۴ روز OFF متوالی\n")

    results = []
    for uid in sorted(by_user.keys()):
        u = users.get(uid, {"name": f"#{uid}", "shift_type": None, "shift_sub": None})
        days_map = by_user[uid]
        work = [1 if d in days_map else 0 for d in range(1, 32)]
        pattern = "".join("W" if w else "." for w in work)
        run_list = runs_of(work)
        work_runs = [(s, e, L) for s, e, L, v in run_list if v == 1]
        off_runs = [(s, e, L) for s, e, L, v in run_list if v == 0]
        max_work = max((L for *_, L in work_runs), default=0)
        max_off = max((L for *_, L in off_runs), default=0)
        long_work = [x for x in work_runs if x[2] >= 4]
        long_off = [x for x in off_runs if x[2] >= 4]
        m = sum(1 for d in range(1, 32) if "M" in days_map.get(d, ()))
        e = sum(1 for d in range(1, 32) if "E" in days_map.get(d, ()))
        n = sum(1 for d in range(1, 32) if "N" in days_map.get(d, ()))
        work_days = sum(work)
        transitions = sum(1 for i in range(30) if work[i] != work[i + 1])
        work_day_list = [d for d in range(1, 32) if work[d - 1]]
        gaps = [work_day_list[i + 1] - work_day_list[i] for i in range(len(work_day_list) - 1)]
        avg_gap = sum(gaps) / len(gaps) if gaps else 0
        max_gap = max(gaps) if gaps else 0
        # night spacing
        nights = [d for d in range(1, 32) if "N" in days_map.get(d, ())]
        ngaps = [nights[i + 1] - nights[i] for i in range(len(nights) - 1)]
        results.append({
            "uid": uid, "name": u["name"], "st": u["shift_type"], "sst": u["shift_sub"],
            "work_days": work_days, "m": m, "e": e, "n": n,
            "max_work": max_work, "max_off": max_off,
            "long_work": long_work, "long_off": long_off,
            "transitions": transitions, "avg_gap": avg_gap, "max_gap": max_gap,
            "pattern": pattern, "nights": nights, "ngaps": ngaps,
            "fixed_morning": u["shift_type"] == 0,
        })

    results.sort(key=lambda r: (-(0 if r["fixed_morning"] else 1), -r["max_work"], -r["max_off"]))

    for r in results:
        kind = "فیکس" if r["fixed_morning"] else ("گردشی۲" if r["sst"] == 2 else "گردشی۳")
        flags = []
        if not r["fixed_morning"]:
            if r["max_work"] >= 4: flags.append(f"کار‌متوالی={r['max_work']}")
            if r["max_off"] >= 4: flags.append(f"OFF‌متوالی={r['max_off']}")
        mark = "⚠" if flags else ("·" if not r["fixed_morning"] else "○")
        log(f"{mark} {r['name']} [{kind}]: کار={r['work_days']} (M={r['m']} E={r['e']} N={r['n']}) | "
            f"بیشینه‌کار‌متوالی={r['max_work']} | بیشینه‌OFF={r['max_off']} | "
            f"جابجایی={r['transitions']} | فاصله‌میانگین={r['avg_gap']:.1f}")
        for s, e, L in r["long_work"]:
            log(f"    └ خوشه کار {L}روزه: {s}–{e}")
        for s, e, L in r["long_off"]:
            if not r["fixed_morning"]:
                log(f"    └ خوشه OFF {L}روزه: {s}–{e}")
        log(f"    {r['pattern']}")

    cohort = [r for r in results if not r["fixed_morning"]]
    log("\n=== ۳. خلاصه کیفیت (فقط پرسنل غیر فیکس صبح) ===")
    n = len(cohort)
    if n:
        log(f"تعداد: {n}")
        log(f"میانگین بیشینه کار متوالی: {sum(r['max_work'] for r in cohort)/n:.1f}")
        log(f"میانگین بیشینه OFF متوالی: {sum(r['max_off'] for r in cohort)/n:.1f}")
        log(f"میانگین جابجایی کار/OFF: {sum(r['transitions'] for r in cohort)/n:.1f} (ایده‌آل تقریبی برای ~۱۳ روز کار ≈ ۲۴)")
        log(f"افراد با ≥۴ روز کار پشت‌سرهم: {sum(1 for r in cohort if r['max_work']>=4)}/{n}")
        log(f"افراد با ≥۵ روز کار پشت‌سرهم: {sum(1 for r in cohort if r['max_work']>=5)}/{n}")
        log(f"افراد با ≥۴ روز OFF پشت‌سرهم: {sum(1 for r in cohort if r['max_off']>=4)}/{n}")
        log(f"افراد با ≥۶ روز OFF پشت‌سرهم: {sum(1 for r in cohort if r['max_off']>=6)}/{n}")
        log(f"افراد با هر دو (≥۴ کار و ≥۴ OFF): {sum(1 for r in cohort if r['max_work']>=4 and r['max_off']>=4)}/{n}")

        # worst offenders
        log("\nبدترین خوشه‌های کار:")
        for r in sorted(cohort, key=lambda x: -x["max_work"])[:8]:
            if r["max_work"] >= 4:
                clusters = ", ".join(f"{s}–{e}({L}د)" for s, e, L in r["long_work"])
                log(f"  {r['name']}: {r['max_work']} — {clusters}")
        log("بدترین خوشه‌های OFF:")
        for r in sorted(cohort, key=lambda x: -x["max_off"])[:8]:
            if r["max_off"] >= 4:
                clusters = ", ".join(f"{s}–{e}({L}د)" for s, e, L in r["long_off"])
                log(f"  {r['name']}: {r['max_off']} — {clusters}")

    log("\n=== ۴. فاصله شب‌ها ===")
    for r in sorted(cohort, key=lambda x: -x["n"]):
        if r["n"] == 0:
            continue
        ng = r["ngaps"]
        mn = min(ng) if ng else "-"
        mx = max(ng) if ng else "-"
        cons = 0
        run = 1
        for g in ng:
            if g == 1:
                run += 1
                cons = max(cons, run)
            else:
                run = 1
        flag = "⚠" if (isinstance(mx, int) and mx >= 8) else "·"
        log(f"{flag} {r['name']}: {r['n']} شب | {r['nights']} | فاصله min/max={mn}/{mx} | شب‌متوالی={cons}")

    log("\n=== ۵. دو شیفت در یک روز ===")
    doubles = 0
    for uid, days in by_user.items():
        name = users.get(uid, {}).get("name", str(uid))
        for d, labs in sorted(days.items()):
            if len(labs) > 1:
                doubles += 1
                log(f"· {name} روز {d}: {'+'.join(sorted(labs))}")
    log(f"جمع: {doubles}")

    log("\n=== ۶. جمع‌بندی کیفی ===")
    if n:
        bad_w = sum(1 for r in cohort if r["max_work"] >= 4)
        bad_o = sum(1 for r in cohort if r["max_off"] >= 4)
        both = sum(1 for r in cohort if r["max_work"] >= 4 and r["max_off"] >= 4)
        log(f"پوشش و سهمیه ظاهراً برقرار است، اما پراکندگی زمانی ضعیف است:")
        log(f"- {bad_w}/{n} نفر خوشه کار ≥۴ روز دارند")
        log(f"- {bad_o}/{n} نفر خوشه OFF ≥۴ روز دارند")
        log(f"- {both}/{n} نفر هر دو را همزمان دارند (الگوی «چند روز پشت‌سرهم کار / چند روز پشت‌سرهم استراحت»)")
        log("این معمولاً یعنی الگوریتم قیود سخت (پوشش، سهمیه شب، استراحت بعد از شب) را ارضا کرده،")
        log("ولی جریمه پراکندگی روزهای کاری / جلوگیری از خوشه‌بندی کار-استراحت ضعیف یا غایب است.")

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print("OK", OUT)

if __name__ == "__main__":
    main()
