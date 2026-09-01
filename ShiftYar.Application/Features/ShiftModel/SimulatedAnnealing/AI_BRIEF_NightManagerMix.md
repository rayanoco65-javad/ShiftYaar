# Brief for another AI: Night shift-manager mix still unmet (ShiftYar / Pediatrics)

This file is a self-contained briefing. Goal: diagnose and propose a **root-cause** fix so **every staffed Night (and Evening) slot** satisfies the shift-manager skill-mix, **without** breaking the other hard constraints that previous patches kept trading off.

If you work in this repo, search the identifiers below; do not invent a second scheduler. Production optimize currently runs **Simulated Annealing**, then a long **post-hoc guard pipeline**. There is also Google OR-Tools CP-SAT, but it is not the path the user is failing on unless they switch algorithm.

---

## 1. What the user sees (latest)

Department: **Pediatrics (اطفال), DeptId = 2**. Month: **Shahrivar 1405** ≈ **2026-08-23 .. 2026-09-22**.

Reported as «نقض محدودیت‌ها» (constraint violations on the result DTO). Mix is **not** thrown by `EnsureHardDailyRulesOrThrow`; it is appended to `ShiftSolution.Violations` at the end of `ApplyMandatoryConstraints` and shown in the UI.

Latest messages (2026-09-01):

1. `Shift manager mix unmet for Night on 2026-08-23 (need total≥2, level1≥1).`
2. `Shift manager mix unmet for Night on 2026-09-01 (need total≥2, level1≥1).`
3. `Shift manager mix unmet for Night on 2026-09-08 (need total≥2, level1≥1).`
4. `Shift manager mix unmet for Night on 2026-09-13 (need total≥2, level1≥1).`
5. `Shift manager mix unmet for Night on 2026-09-22 (need total≥2, level1≥1).`

Meaning: those Night shifts **are staffed** (empty slots are skipped by `ShiftManagerMixGuard.GetViolations`). Among regular (non-on-call) assignees there are **not** at least **2 managers**, including **at least 1 Level-1**.

Previous iterations of the same month also failed mix on 2026-08-28, 08-29, 09-07, 09-22, plus adjacency, ON, quotas, and consecutive-day errors. The set of failing Night dates **moves around**; that is a symptom of oscillating post-processors, not of five permanently impossible calendar days.

---

## 2. Product and stack

- Repo: `ShiftYar` (.NET 8, layered: Domain / Application / API).
- Scheduling entry: `ShiftSchedulingService.OptimizeWithSimulatedAnnealingAsync` → `SimulatedAnnealingScheduler.Optimize()` → `ApplyMandatoryConstraints`.
- Result DTO field: `Violations` (string list). Mix strings come from `ShiftManagerMixGuard.GetViolations`.
- Deploy: production is Liara; **code changes do nothing until the API is redeployed**. If the user still sees old errors after a local fix, first confirm the running binary.
- Flutter app exists (`flutter_shiftyaar1`) but this bug is **backend rostering**, not UI.

---

## 3. Domain model (what a “solution” is)

A `ShiftSolution` is a set of `SaShiftAssignment`: `(UserId, ShiftId, Date, ShiftLabel, IsOnCall)`.

Shifts in this department (from tests / typical Pediatrics seed):

| ShiftId | Label | Duration | Coverage (specialty 2) | ManagerRequiredCount | ManagerMinLevel1Count |
|--------:|-------|----------|------------------------|----------------------|------------------------|
| 4 | Morning | 12h | ~4 regular | 0 | 0 |
| 5 | Evening | 12h | ~3 regular | **2** | **1** |
| 6 | Night | 12h | ~4 regular (weekday often 11 total slots across labels; holiday 10 — confirm live DB) | **2** | **1** |

`MaxShiftsPerDay = 1` (one assignment per user per calendar day).

User skill for “in-charge”:

- `ShiftManagerLevel`: `null` = not a manager, `1` = Level-1 (senior / charge), `2` = Level-2 (can count toward total managers).
- `CanBeShiftManager` with null level is treated as Level-1 (`ShiftManagerRules.EffectiveLevel`).
- Fixed-morning users (`ShiftType = FixedShift`, `FixedMorning`) **cannot take Night**.

Typical rotating L1 IDs seen in tests / production names:

- 14 بهاره بهاری پور (often has **approved Morning ON**, e.g. 2026-08-23)
- 17, 18 خدیجه متقی, 19, 20, 21 عاطفه رحیمی منفرد
- 12, 15 often **fixed morning L1** — useless for Night mix

L2 examples: 22, 23, 24, 25, 26. Juniors: 27–31.

Satisfaction (`ShiftManagerRules.IsSatisfied`):

```
managers.Count >= requiredTotal (2)
AND count(Level1) >= min(minLevel1, requiredTotal)  →  ≥ 1 L1
```

On-call assignments **do not** count.

---

## 4. Hard constraints that MUST hold together

These are the constraints that previous “fixes” kept swapping. A valid Night mix that violates any of these is **not** acceptable; the UI will just show a different error next run.

### 4.1 Shift-manager mix (the current failure)

- Evening and Night: `ManagerRequiredCount = 2`, `ManagerMinLevel1Count = 1`.
- Morning: 0 (no mix).
- Code: `ShiftManagerRules`, `ShiftManagerMixGuard`, `PlaceManagerSkeleton`, `EnsureShiftManagerMixForSlot`.
- **Important:** `GetViolations` **ignores dates with zero regular assignees**. Mix is only reported when the shift is already staffed.

### 4.2 Adjacency / rest after night (department settings)

`AllowEveningAfterNightShift = false`, `AllowNightShiftAfterNightShift = false`.

Always forbidden (physical):

- Evening → Night **same calendar day**
- Night → Morning **next calendar day** (12h night into morning = >12h continuous work)

Settings-controlled (department; **approved ON of the LATER slot can waive reporting**):

- Night → Evening next day
- Night → Night next day

Code: `AdjacentShiftRestRules`, `AdjacentShiftRestGuard`.

Strip policy (`ChooseRemovable`): for settings pairs, prefer dropping the later assignment unless the later Night is mix-critical (then drop earlier). Waived if later is approved ON. Both-ON settings pairs are kept and **not reported**.

`MinDaysBetweenNightShifts` default **1** (same as no consecutive nights). Manager install used to **ignore** this (`relaxNightSpacing` / `mandatoryInstall`); Night→Night/Evening ignore was removed except when the **target** is an approved required slot.

### 4.3 Approved shift requests (ON / OFF) — absolute last word

- `RequiredShiftSlots` = approved ON for a date+label (optional ShiftId).
- `RequiredPresenceDates` = must work that day (any label).
- `UnavailableDates` / `UnavailableShiftSlots` = OFF.

`ApprovedRequestGuard.ForceApply` must leave ON placed. Validation `EnsureApprovedRequestsOrThrow` runs **before** night-quota and adjacency throws.

Example that already bit production: user 14 must be Morning on 2026-08-23. Manager repair used to steal her to Evening/Night same day (`MaxShiftsPerDay=1`).

ON days are **excluded** from consecutive-workday counting (`MaxConsecutiveWorkdayRules.IsApprovedOnWorkDay`).

### 4.4 Exact night quotas

Many users have `ExactNightShiftCount` (e.g. 8 or 9). `ExactNightQuotaGuard` adds/moves nights. It must not:

- create Night→Night / Night→Evening (unless later is ON)
- steal mix-critical nights (`IsSticky` = ON or `IsCriticalForManagerMix`)
- hang the job (`ForceSatisfyAllDeficits` is bounded)

Quota restore after manager swaps: `RestoreAnyDroppedNightQuotas`.

### 4.5 Coverage / capacity

Fill every specialty `RequiredTotalCount` for Morning/Evening/Night. Do not exceed capacity (`StripExcessCoverage`). Weekday vs holiday counts differ (live Pediatrics often 11 weekday / 10 holiday **total slots across the day**, not per shift — confirm DB `ShiftRequiredSpecialty`).

### 4.6 Max consecutive workdays

Read from **department scheduling settings**. Default builder: `MaxConsecutiveShifts = 2`. Live Pediatrics recently reported **ceiling 4** for user 18 (5-day run 2026-08-30..09-03).

`EnforceMaxConsecutiveShifts` from dept settings. Approved ON days exempt. Guard: `MaxConsecutiveWorkdayGuard` (clears a rest day). Mix-critical no longer blocks clearing (so consecutive can drop an L1 night; skeleton is supposed to put another L1).

### 4.7 Daily uniqueness

`ForbidDuplicateDailyAssignments`, `MaxShiftsPerDay = 1`. Morning+Evening same day **not** allowed under max=1. Evening+Night always illegal.

### 4.8 Eligibility

`ShiftEligibilityResolver.MayEverTakeLabel` / `AllowedShiftPermissions`. Fixed morning cannot take Night.

### 4.9 What is thrown vs warning

After SA, `ShiftSchedulingService` **throws** on:

- unmet approved requests
- unmet exact night / day quotas
- adjacency + daily-duplicate (`EnsureHardDailyRulesOrThrow`)
- over-capacity

**Mix and consecutive** are typically **warnings on `Violations`**, not throws — but the product treats them as failure («نقض محدودیت‌ها»). Fix mix **in the assignment set**, not by hiding the string.

---

## 5. Algorithm pipeline (current)

### 5.1 SA

`SimulatedAnnealingScheduler.Optimize`:

1. `GenerateFeasibleInitialSolution` / `GenerateInitialSolution`
2. Annealing neighborhood moves
3. **`ApplyMandatoryConstraints(bestSolution)`** — this is where mix is supposed to become true

`GenerateInitialSolution` order (simplified):

1. Hard required ON
2. Exact night / day / combo quotas
3. **`PlaceManagerSkeleton`** (Night shifts first, then Evening)
4. Random fill Night, then Morning/Evening
5. Quotas again

### 5.2 `PlaceManagerSkeleton`

Intended two-stage (senior layer first):

- For each manager-required shift, each date with coverage demand: `EnsureShiftManagerMixForSlot`
- Night ordered before Evening
- Then `RestoreDeficitNightQuotas`

`EnsureShiftManagerMixForSlot` tries `TryAddManagerToSlot` if the slot is empty **or** has spare specialty capacity, else **replace** a non-L1 occupant (`TryReplaceForManagerMix` → direct install / same-day swap / non-adjacent night swap).

Direct install: iterate `RankManagerCandidatePool`, `TryClearAdjacencyForManagerInstall` (delete unprotected prev-night / next-day forbidden labels), then `IsUserAvailableForManagerInstall` (now includes **consecutive** and does **not** ignore settings adjacency unless target is ON).

### 5.3 `ApplyMandatoryConstraints` (long; this is the oscillation engine)

Rough order:

1. `ForceApply` ON
2. Strip duplicates / ineligible / adjacency
3. **`PlaceManagerSkeleton`**
4. Night/day quotas
5. `ShiftCoverageGuard.Enforce` (strip excess + fill) — **can bury L1 calendars with Morning/Evening**
6. Productivity hour fill, holiday fairness, morning/evening balance, seniority redistribution — **many add/move assignments**
7. Quotas, `ForceApply`, strip excess, coverage again
8. `FinalizeMandatoryConstraints` (loop: ForceApply, fill, manager repair, quota, strip adj, strip dup, **consecutive**, ForceApply)
9. `FinishWithCoverageManagersAndQuotas` (strip, fill, manager, quota, seal, manager, fill, quota, seal, consecutive, skeleton, seal)
10. `SealApprovedRequestsThenAdjacency` = `ForceApply` + `StripForbiddenAdjacencies`
11. Collect `Violations` including mix

**Any step after skeleton can remove the Night L1** (adjacency strip, consecutive rest, quota rebalance, coverage excess, productivity). Later repair often cannot put an L1 back because:

- all L1s have Night yesterday or Evening/Morning tomorrow
- consecutive cap
- Morning ON that day
- fixed-morning L1s
- `MaxShiftsPerDay=1` and they already work another label

That is the core failure mode: **skeleton is not reserved; it is a suggestion that later guards overwrite.**

---

## 6. Why greedy / SA+repair fails (classic NRP skill-mix)

This is the nurse rostering **skill-mix / charge-nurse** constraint.

What went wrong architecturally:

1. **Fill all staff first, repair mix later.** Juniors occupy Night seats; L1s are already used on Evening/Morning or adjacent nights. Replacement needs an L1 who is legally free. Often nobody is.
2. **Conflicting post-processors with no global feasibility.** Each guard is locally greedy:
   - Mix wants an L1 on this Night
   - Adjacency deletes that L1’s next Evening or consecutive Night
   - Quota puts a Night on D-1, making D illegal
   - Consecutive deletes a work day that was the only L1 Night
   - ON ForceApply last restores Morning and kicks the L1 off Night
3. **`GetViolations` only looks at the final snapshot.** Intermediate mix-OK states are discarded.
4. **Feasibility of the L1 pool is never checked up front.** Evening+Night each need 1 L1 per day ≈ **62 L1-shifts / 31-day month**. Rotating L1s who can work Night are few (~5–6). With no Night-after-Night, no Evening-after-Night, no Morning-after-Night, `MaxShiftsPerDay=1`, and consecutive ≤4 (or 2), the L1 calendar is extremely tight. If the instance is **infeasible**, SA cannot succeed; the solver should say so.

### Feasibility sketch (must be computed from live data)

Let  
- \(D\) = number of dates in range (≈31)  
- \(E, N\) = dates where Evening/Night have `RequiredTotalCount > 0`  
- \(L\) = count of **Night-eligible Level-1** (exclude fixed-morning, exclude users with no Night permission)

Lower bound L1-shifts: \(|E| + |N|\) if every Evening and Night needs ≥1 L1.

Each L1 available days after rest-after-night: at most \(\lceil D/2 \rceil\) Nights if they only work Night every other day, **and** they also need to cover Evening L1 unless other L1s do evenings.

**If bound > supply, do not patch guards — add charge nurses or reduce mix requirement.**

---

## 7. History of patches (do not repeat blindly)

| Attempt | Effect |
|---------|--------|
| Repair mix after quotas | Oscillated with quota deficits |
| `ForceApply` last | Fixed ON (user 14 Morning 2026-08-23) then mix/adjacency broke |
| Strip adjacency last | Fixed user 21 Night→Night→Evening; mix-critical later Night was dropped |
| Don’t ignore settings adjacency on manager install | Mix dates lost L1s who were adjacent |
| Clear unprotected adjacency then install L1 | Helped unit tests; production still has holes |
| Keep mix-critical Night when stripping Night→Night | Helped some dates; others appeared |
| “Two-stage” `PlaceManagerSkeleton` before coverage fill | Skeleton is still destroyed by later guards in `ApplyMandatoryConstraints` |
| Consecutive respected on manager install; consecutive can clear mix-critical | User 18 5-day run; mix holes moved to 08-23, 09-01, 09-08, 09-13, 09-22 |

Unit tests in `ShiftManagerRulesTests` / `AdjacentShiftRestRulesTests` pass **toy** instances. They do **not** replay the full Pediatrics month with live ON requests, live quotas, and live consecutive=4.

---

## 8. Key files (absolute from repo root)

| Path | Role |
|------|------|
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/SimulatedAnnealingScheduler.cs` | SA + `ApplyMandatoryConstraints` + skeleton + manager install |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/ShiftManagerMixGuard.cs` | Mix violations |
| `ShiftYar.Application/Common/Utilities/ShiftManagerRules.cs` | Mix math |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/AdjacentShiftRestGuard.cs` | Strip/report adjacency |
| `ShiftYar.Application/Common/Utilities/AdjacentShiftRestRules.cs` | Forbidden pairs |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/ApprovedRequestGuard.cs` | ON/OFF |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/ExactNightQuotaGuard.cs` | Night quotas; `IsSticky` |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/ShiftCoverageGuard.cs` | Fill/strip coverage; L1 priority if mix unmet |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/MaxConsecutiveWorkdayGuard.cs` | Break long runs |
| `ShiftYar.Application/Common/Utilities/MaxConsecutiveWorkdayRules.cs` | Consecutive counting (ON exempt) |
| `ShiftYar.Application/Features/ShiftModel/Services/ShiftSchedulingService.cs` | Load constraints from DB, run SA, throw/return violations |
| `ShiftYar.Application/Common/Utilities/DepartmentSchedulingDefaultSettingsBuilder.cs` | Defaults: consecutive=2, no Eve/Night after Night |
| `ShiftYar.Application/Features/ShiftModel/OrTools/OrToolsCPSatScheduler.cs` | Alternate CP-SAT path (not currently failing in UI unless selected) |
| `tests/ShiftYar.Application.Tests/ShiftManagerRulesTests.cs` | Mix unit tests (`BuildPediatricsConstraints`) |

---

## 9. What a correct fix must look like

Do **not** add another pass at the end that “tries harder” with the same replace/strip loop. That is what already failed.

### Approach A — True two-stage with a **reserved L1 calendar** (heuristic, matches this codebase)

1. **Feasibility check** before SA: count Night-eligible L1s vs Evening+Night L1 demand under adjacency+consecutive. If infeasible, return a clear Persian error (need more Level-1 or lower mix counts).
2. **Phase 1 only L1/L2 managers**, dates ordered Night then Evening:
   - Assign L1 to every Night, then every Evening, **without** filling junior seats yet.
   - Build L1 assignments as a **locked set** (`IsSkeleton` / do not strip except ON conflict).
   - Enforce rest-after-night and consecutive **inside this layer only**.
   - Rotate L1s (fairness) so the same person is not used on D and D+1.
3. **Phase 2** fill remaining demand with all staff, **forbidden to remove or relocate skeleton assignments** except if ON requires it (then immediately re-place another L1 on that Night).
4. Quotas: prefer putting extra nights on **non-L1** or on L1 dates that are already skeleton Nights (don’t add a new adjacent Night).
5. Consecutive: may not delete a skeleton Night unless another L1 is installed in the same transaction.

Implementation hint: mark skeleton assignments in `SaShiftAssignment` (new flag) or a `HashSet<(user, shiftId, date)>` on the solution, and make `AdjacentShiftRestGuard`, `MaxConsecutiveWorkdayGuard`, `ExactNightQuotaGuard`, `ShiftCoverageGuard.StripExcess` refuse to remove them.

### Approach B — CP-SAT hard constraint (most stable)

`OrToolsCPSatScheduler` already exists. Add:

```
∀ d, ∀ s ∈ {Evening, Night} with demand > 0:
  sum_{n in Level1} x[n,d,s] >= 1
  sum_{n in Managers} x[n,d,s] >= 2
```

plus existing: at most one shift/day, Night→Morning forbidden, Night→Evening/Night forbidden unless ON, consecutive, quotas, coverage.

If **INFEASIBLE**, report that — do not return a roster with mix holes.

Switching the Pediatrics job to OR-Tools is a product decision; SA would still need Approach A if SA remains the default.

### Approach C — Do not staff a Night until mix is possible

If no legal L1 exists for date D, **leave Night empty** and fail coverage — currently mix is only checked when staffed, so the algorithm prefers “full Night with four juniors” over “empty Night”. That hides the real infeasibility and produces exactly these messages.

---

## 10. Acceptance tests a real fix must pass together

On Pediatrics 2026-08-23..2026-09-22 (or the same constraints exported from DB):

1. **Zero** `Shift manager mix unmet` for Evening and Night on every date with regular assignees.
2. **Zero** `AdjacentShiftRestGuard.GetViolations` (except waived ON pairs).
3. All `RequiredShiftSlots` met (`ApprovedRequestGuard.GetUnmetViolations` empty), including user 14 Morning if still requested.
4. Exact night quotas met for every user with `ExactNightShiftCount`.
5. No user over `MaxConsecutiveShifts` countable days (ON exempt).
6. No Evening+Night same day; coverage not over specialty capacity.
7. Job must **finish** (no hang / pending forever). `ForceSatisfyAllDeficits` must stay bounded.

Regression unit tests already exist; they are **insufficient** alone. Add a full-month test that loads realistic L1/L2/junior counts, consecutive=4, adjacency flags false, mix 2/1, and asserts 1–6.

---

## 11. How to inspect a failing Night (debug recipe)

For each failing date D (08-23, 09-01, 09-08, 09-13, 09-22):

1. List Night assignees: userId, level, labels on D-1 and D+1.
2. List all Level-1 users: assignment on D, D-1, D+1; consecutive run if D were added; ON/OFF that day; FixedMorning?
3. Ask: after `TryClearAdjacencyForManagerInstall`, would any L1 be legal? If yes, why didn’t `EnsureShiftManagerMixForSlot` keep them until `Violations` were collected? Trace **which later guard removed** the L1 Night (strip, consecutive, quota, coverage excess, ForceApply).
4. If no L1 is legal, the date is locally infeasible given the rest of the month — Phase 1 calendar must change **other** days, not only D.

---

## 12. Constraints dump (defaults vs live)

From `DepartmentSchedulingDefaultSettingsBuilder` (may be overridden in DB):

- `MaxShiftsPerDay`: 1 unless same-day multi-shift profile
- `MaxConsecutiveShifts`: **2** default; live Pediatrics recently **4**
- `AllowEveningAfterNightShift`: false
- `AllowNightShiftAfterNightShift`: false
- `MaxConsecutiveNightShifts`: 1 if department has nights
- `MinDaysBetweenNightShifts`: 1 when consecutive nights disabled

From `ShiftSchedulingService` user load:

- `MaxConsecutiveShifts` starts at 2, then dept setting if `> 0`
- `MinDaysBetweenNightShifts = 1` unless consecutive nights allowed
- `MaxNightShiftsPerMonth = 8` default if no exact quota

Confirm live rows: `DepartmentSchedulingSettings`, `Shift.ManagerRequiredCount` / `ManagerMinLevel1Count`, users’ `ShiftManagerLevel`, `ShiftRequest` approved ON for the month.

---

## 13. Out of scope / do not do

- Do not ignore Night→Morning or Evening+Night same day.
- Do not steal approved ON (especially user 14 Morning).
- Do not re-enable `ignoreSettingsControlledAfterNight` for all manager installs.
- Do not unbounded-recurse quota ↔ mix (job stays `Pending`).
- Do not “fix” mix by clearing `Violations` without changing assignments.
- Do not assume unit tests on a **single day** equal a 31-day roster.

---

## 14. One-sentence problem statement

**Pediatrics Night (and Evening) require 2 managers including 1 Level-1 every day, but Simulated Annealing fills the roster as one layer and then a stack of greedy guards fight over the same L1 calendars (adjacency, quotas, consecutive days, ON, coverage), so several Nights end staffed only with Level-2/juniors; the mix check runs on that final snapshot and reports unmet.**

The durable fix is either **lock a senior/charge layer first (and never overwrite it)** or **encode mix as a CP-SAT hard constraint**, after proving the L1 pool is mathematically sufficient.
