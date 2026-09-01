# Brief for another AI: Approach A implemented — Night manager mix STILL failing (Pediatrics / ShiftYar)

**Language:** Persian context, English identifiers. Use this file alone to understand the problem and all code changes already made.

**Repo:** `D:\Hampadco\RealProjects\ShiftYar` (.NET 8, Clean Architecture)  
**Department:** Pediatrics (اطفال), `DeptId = 2`  
**Month:** Shahrivar 1405 ≈ `2026-08-23` .. `2026-09-22`  
**Algorithm in production UI:** Simulated Annealing (`SimulatedAnnealingScheduler.Optimize`)

---

## 1. What the user still sees (latest run)

Reported in UI as «نقض محدودیت‌ها» (`ShiftSolution.Violations`). Job may complete (`AlgorithmStatus = Completed`); mix is **not** thrown by `EnsureHardDailyRulesOrThrow`.

| # | Message |
|---|---------|
| 1 | Shift manager mix unmet for Night on **2026-08-23** (need total≥2, level1≥1). |
| 2 | Shift manager mix unmet for Night on **2026-08-29** (need total≥2, level1≥1). |
| 3 | Shift manager mix unmet for Night on **2026-09-01** (need total≥2, level1≥1). |
| 4 | Shift manager mix unmet for Night on **2026-09-05** (need total≥2, level1≥1). |
| 5 | Shift manager mix unmet for Night on **2026-09-08** (need total≥2, level1≥1). |
| 6 | Shift manager mix unmet for Night on **2026-09-22** (need total≥2, level1≥1). |

**Meaning:** `ShiftManagerMixGuard.GetViolations` skips **empty** slots. These Nights **are staffed** (juniors/L2 only or 1 manager) but do **not** have ≥2 managers including ≥1 Level-1.

**Dates moved since last report:** 2026-09-13 gone; **2026-08-29** and **2026-09-05** appeared. Oscillation of failing dates is a symptom of post-hoc guards still overwriting the manager layer.

---

## 2. Required constraint (unchanged)

For Evening and Night (`ShiftId` 5 / 6 in tests):

- `ManagerRequiredCount = 2`
- `ManagerMinLevel1Count = 1`
- Morning: no mix requirement

`ShiftManagerRules.IsSatisfied`: among **regular** (non-on-call) assignees, count managers (Level 1 or 2); need ≥2 managers and ≥1 Level-1.

All of these must hold **together** (user requirement):

- Mix on every staffed Evening/Night
- Exact night quotas per user
- Full daily coverage (specialty counts)
- Adjacency: no Evening+Night same day; no Night→Morning next day; settings: no Night→Night / Night→Evening unless approved ON on later slot
- Approved ON/OFF last word (`ApprovedRequestGuard.ForceApply`)
- Max consecutive workdays from department settings (default 2; Pediatrics observed **4** for some users); approved ON days exempt
- `MaxShiftsPerDay = 1`

---

## 3. What was implemented: “Approach A” (True two-stage + skeleton)

A full implementation pass was done. **It did not fix production** for the dates above (257/258 unit tests pass; production month with live DB still fails mix on 6 nights).

### 3.1 New model flag: `IsSkeleton`

**File:** `SimulatedAnnealing/Models/ShiftSolution.cs`

- `SaShiftAssignment.IsSkeleton` — protected manager-layer assignment
- `AddAssignment(..., isSkeleton: bool)`
- `RemoveAssignment(..., force: bool)` — skeleton removed only with `force: true` (e.g. approved ON via `ApprovedRequestGuard`)
- `ClearAllSkeletonFlags()`, `MarkSkeleton(...)`

### 3.2 Pre-SA feasibility check

**File:** `ManagerMixFeasibilityChecker.cs`

- Called at start of `SimulatedAnnealingScheduler.Optimize()` → `ValidateOrThrow`
- Greedy simulation: place L1 on every Night then Evening with strict rest/consecutive rules
- If impossible → Persian `InvalidOperationException` (shortage of L1 / excessive requirements)
- **Note:** If production Optimize **runs without throwing**, this check believes the month is **feasible** at L1-calendar level — yet final snapshot still has mix holes → failure is in **pipeline after SA**, not global infeasibility (or checker is too weak).

### 3.3 Phase 1 — Reserved manager skeleton

**File:** `SimulatedAnnealingScheduler.cs`

| Method | Behavior |
|--------|----------|
| `BuildReservedManagerSkeleton` | `ClearAllSkeletonFlags()` then `PlaceManagerLayer(strictPhase: true)` |
| `RepairManagerMix` / `PlaceManagerSkeleton` | `PlaceManagerLayer(strictPhase: false)` without clearing flags |
| `PlaceManagerLayer` | For each Night then Evening date with coverage demand: `EnsureShiftManagerMixForSlot(..., markSkeleton: true)` then `SkeletonAssignmentGuard.MarkSlotManagersAsSkeleton` |

**Strict phase:** `relaxNightSpacing = false`, `mandatoryInstall = false` on manager install (rest rules enforced when building skeleton).

**Fairness:** `RankManagerCandidatePool` orders by fewer same-label assignments, penalizes consecutive Night for same user.

### 3.4 Skeleton marking rules

**File:** `SkeletonAssignmentGuard.cs`

- `MarkSlotManagersAsSkeleton`: for each **manager** in slot, `IsSkeleton = true` **only if** `ShiftManagerRules.IsSatisfied` for that slot **right now**
- If mix broken → managers in slot get `IsSkeleton = false` (allows replace in repair)
- `IsLevel1MixSkeleton`: skeleton **and** user is Level-1 — used for **adjacency** protection (L2 skeleton is **not** adjacency-protected)

**On install:** `AddShiftAssignmentForManagerInstall(..., markSkeleton)` sets `IsSkeleton = markSkeleton && ShiftManagerRules.IsManager(user)` (both L1 and L2 when marked).

### 3.5 Phase 2 — Guard immunity (partial)

Guards updated to **not remove** `IsSkeleton` assignments (with nuances):

| Guard | Skeleton behavior |
|-------|-------------------|
| `ShiftCoverageGuard.StripExcess` | `IsProtectedAssignment` includes `IsSkeleton` |
| `AdjacentShiftRestGuard` | Only **L1** skeleton protected (`IsLevel1MixSkeleton`); L2 skeleton may be stripped for Night→Night; both mix-critical → prefer drop **later** on settings pairs |
| `MaxConsecutiveWorkdayGuard` | Skips skeleton; calls `RepairManagerMix` callback if skeleton blocked clearing |
| `ExactNightQuotaGuard` | `IsSticky` includes `IsSkeleton`; donors prefer non-L1; skip skeleton donors |
| `DailyDuplicateAssignmentGuard` | Skip removing skeleton extras |
| `ShiftEligibilityGuard` | Skip stripping skeleton |
| `ProductivityHourFillGuard` | `IsProtectedAssignment` includes `IsSkeleton` |
| `ShiftSeniorityDistributionGuard` | `IsProtected` includes `IsSkeleton` |
| `MorningEveningBalanceGuard` / `HolidayMorningEveningFairnessGuard` | skeleton skip |
| SA neighborhood moves | `IsProtectedAssignment` includes skeleton |
| `ApprovedRequestGuard` | Uses `force: true` on remove (can break skeleton for ON) |

**Gap:** Many guards still **never** had skeleton checks: `ExactDayShiftQuotaGuard`, `ExactComboShiftQuotaGuard`, `MorningEveningBalanceGuard` **swap** paths, parts of `ProductivityHourFillGuard`, etc. Any `RemoveAssignment` without skeleton check can still break mix **indirectly** (e.g. move user who was manager elsewhere).

### 3.6 Final mix enforcement loop

**File:** `SimulatedAnnealingScheduler.ApplyMandatoryConstraints` (end of pipeline)

Order (simplified):

1. `ForceApply`, strips, **`BuildReservedManagerSkeleton`**
2. Quotas, **`ShiftCoverageGuard.Enforce`** (×多次), productivity, seniority, fairness, … (~40 guard steps)
3. `FinalizeMandatoryConstraints` → `FinishWithCoverageManagersAndQuotas` (repair passes, quotas, seal)
4. `SealApprovedRequestsThenAdjacency` → `ForceApply` + **`RepairManagerMix`** + adjacency strip
5. **`FinalizeManagerMixMandatory`** — up to 10 rounds: repair broken slots, seal, on round 9 **`BuildReservedManagerSkeleton`** if still broken
6. Adjacency strip, **`ExactNightQuotaGuard.Enforce`**, **`RepairManagerMix`**, **`RefreshManagerSkeletonFlags`**, adjacency again
7. Collect violations including **`ShiftManagerMixGuard.GetViolations`**

**Critical:** Mix is only **reported** in step 7. `ShiftSchedulingService` does **not** throw on mix; it throws on ON, quotas, adjacency, capacity.

### 3.7 Service layer

**File:** `ShiftSchedulingService.OptimizeWithSimulatedAnnealingAsync`

- Does **not** call `ManagerMixFeasibilityChecker` directly (only inside `scheduler.Optimize()`)
- After Optimize: `EnsureApprovedRequestsOrThrow`, `EnsureExactNightQuotasOrThrow`, `EnsureHardDailyRulesOrThrow`, … — **no throw for mix**
- Result DTO includes `Violations` from solution

---

## 4. Complete changelog (files touched)

| File | Change summary |
|------|----------------|
| `Models/ShiftSolution.cs` | `IsSkeleton`, forced remove, clone |
| `ManagerMixFeasibilityChecker.cs` | **New** — pre-SA L1 feasibility |
| `SkeletonAssignmentGuard.cs` | **New** — mark/protect skeleton; L1 vs L2 adjacency |
| `SimulatedAnnealingScheduler.cs` | Build/Repair skeleton, FinalizeManagerMixMandatory, RefreshManagerSkeletonFlags, strict/repair phases, end-of-pipeline mix loop |
| `AdjacentShiftRestGuard.cs` | L1 skeleton protection; both-critical N→N drop later |
| `MaxConsecutiveWorkdayGuard.cs` | Skeleton skip + repair callback |
| `ShiftCoverageGuard.cs` | Skeleton in StripExcess protection |
| `ExactNightQuotaGuard.cs` | Skeleton sticky; donor ranking non-L1 first |
| `ProductivityHourFillGuard.cs` | Skeleton protected from hour-balance moves |
| `ShiftSeniorityDistributionGuard.cs` | Skeleton protected |
| `MorningEveningBalanceGuard.cs` | Skeleton protected |
| `HolidayMorningEveningFairnessGuard.cs` | Skip skeleton moves |
| `DailyDuplicateAssignmentGuard.cs` | Skip skeleton |
| `ShiftEligibilityGuard.cs` | Skip skeleton |
| `ApprovedRequestGuard.cs` | `force: true` on removals for ON |
| `AI_BRIEF_NightManagerMix.md` | Original problem brief (pre-Approach A) |

**Tests:** 257/258 `ShiftYar.Application.Tests` pass. Key mix tests pass (`FixesAllReportedNightManagerMixDates`, `PlacesNightL1_WhenSlotHasOnlyJuniors`). One adjacency+quota edge test fails (`FillsNightQuotaDeficits_WithoutBreakingManagerMix` — user 22 Night adjacency in September).

---

## 5. Why Approach A likely still fails in production

### Hypothesis A — Skeleton never “sticks” because mix is rarely satisfied at mark time

`MarkSlotManagersAsSkeleton` sets `IsSkeleton = satisfied`. If after guards the slot has 4 juniors + 0 managers, `satisfied = false` → **no skeleton** → next guard pass treats managers like normal staff → L2 stripped by productivity/seniority.

`FinalizeManagerMixMandatory` tries to repair but:

- `EnsureShiftManagerMixForSlot` may **fail** when no legal L1 exists (ON conflict, adjacency, consecutive cap, all L1s busy on adjacent days)
- `TryReplaceForManagerMix` skips `IsSkeleton` occupants — if wrongly marked, deadlock
- Slot **full** with juniors: replace must evict non-manager; ranking may pick wrong donor; quota restore rolls back swap

### Hypothesis B — `BuildReservedManagerSkeleton` too early, destroyed by middle pipeline

Called at line ~1518 **before** ~35 guard steps (coverage, productivity, seniority, quota × N). Even with skeleton immunity, **immunity only applies when `IsSkeleton == true`**, which requires mix satisfied at refresh time. Middle pipeline can leave slots with juniors only → no protection → same as before Approach A.

### Hypothesis C — L1 pool tight + fixed constraints on specific dates

Known hard dates from history:

| Date | Likely conflict |
|------|-----------------|
| **2026-08-23** | User **14** approved **Morning ON** — L1 cannot take Night same day (`MaxShiftsPerDay=1`); steals L1 from Night if forced to Morning |
| **2026-09-01, 09-08, 09-22** | End/start of month / quota rebalance — L1 nights removed for quota or consecutive (user **18** had 5-day run) |
| **2026-08-29, 09-05** | New failures — likely adjacency/quota moved L1/L2 off Night after repair |

Feasibility checker passes → greedy L1 placement **without** full quota/productivity/SA state may be optimistically feasible.

### Hypothesis D — Mix not a hard constraint in service layer

Pipeline can return `Completed` with mix in `Violations`. No final `throw` or CP-SAT re-solve. User sees failure; algorithm considers itself done.

### Hypothesis E — Deploy / code path

If Liara runs old binary, none of Approach A runs. **Verify deployed assembly** includes `IsSkeleton`, `FinalizeManagerMixMandatory`, `ManagerMixFeasibilityChecker`.

### Hypothesis F — `RunShiftManagerRepairPasses` uses `markSkeleton: false`

Intermediate repairs during `FinalizeMandatoryConstraints` do **not** mark new managers as skeleton; only final loops mark. Repairs can be undone by next guard in same loop.

---

## 6. Architecture diagram (current pipeline)

```
Optimize()
  ManagerMixFeasibilityChecker.ValidateOrThrow
  GenerateFeasibleInitialSolution
    ForceApply (partial)
    quotas
    BuildReservedManagerSkeleton     ← Phase 1 strict
    fill juniors (random)
  SA loop (neighbor moves; skeleton protected in IsProtectedAssignment)
  ApplyMandatoryConstraints
    ForceApply → strips → BuildReservedManagerSkeleton   ← again, then destroyed by below
    Coverage.Enforce × many
    ProductivityHourFill × many        ← skeleton protected IF IsSkeleton set
    Seniority / fairness / quotas × many
    FinalizeMandatoryConstraints
      FinishWithCoverageManagersAndQuotas
        RunShiftManagerRepairPasses (markSkeleton: false)
        PlaceManagerSkeleton / RepairManagerMix
    SealApprovedRequestsThenAdjacency + RepairManagerMix
    FinalizeManagerMixMandatory (10 rounds, rebuild skeleton round 9)
    quota + RepairManagerMix + adjacency
    Violations += ShiftManagerMixGuard   ← mix only WARNed here
```

**Design flaw:** Manager layer is built **before** the heavy middle, not **after** or as **locked variables** in SA/CP-SAT.

---

## 7. Key code entry points (search these)

```
SimulatedAnnealingScheduler.ApplyMandatoryConstraints
SimulatedAnnealingScheduler.BuildReservedManagerSkeleton
SimulatedAnnealingScheduler.FinalizeManagerMixMandatory
SimulatedAnnealingScheduler.EnsureShiftManagerMixForSlot
SimulatedAnnealingScheduler.TryReplaceForManagerMix
SkeletonAssignmentGuard.MarkSlotManagersAsSkeleton
ShiftManagerMixGuard.GetViolations
ShiftManagerRules.IsSatisfied / IsCriticalForManagerMix
ManagerMixFeasibilityChecker.ValidateOrThrow
ShiftSchedulingService.OptimizeWithSimulatedAnnealingAsync
ExactNightQuotaGuard.IsSticky
ProductivityHourFillGuard.IsProtectedAssignment
AdjacentShiftRestGuard.ChooseRemovable
```

---

## 8. What unit tests prove vs do not prove

**Prove:**

- Toy full-month Pediatrics fixture can repair mix on historical failing dates when solution pre-filled with juniors + ApplyMandatoryConstraints
- Single-day L1 install, user 18 consecutive break, user 14 Morning ON preserved in specific tests
- RepairShiftManagers can swap L2 at quota cap for L1 in small fixtures

**Do NOT prove:**

- Live DB month with real ON requests, productivity plans, 31 days, all guards — **production parity**
- That `FinalizeManagerMixMandatory` always converges when L1 legally unavailable on 6 specific dates
- That Liara deploy matches local branch

---

## 9. Recommended directions for next AI (prioritized)

### 9.1 Make mix a **hard** constraint (strongest)

After `ApplyMandatoryConstraints`, if `ShiftManagerMixGuard.GetViolations` non-empty → **throw** with Persian message listing dates OR run dedicated repair until zero or prove infeasible.

Alternatively switch Pediatrics to **`OrToolsCPSatScheduler`** with hard constraints:

```
∀ date d with Night demand:
  sum_{u in L1} x[u,d,Night] >= 1
  sum_{u in Managers} x[u,d,Night] >= 2
```

### 9.2 True locked calendar (fix Approach A properly)

1. **Phase 1 only:** assign all L1+L2 manager slots for all Nights/Evenings; store in `HashSet<(user,shiftId,date)>` or permanent `IsSkeleton=true` until ON conflict
2. **Phase 2 only:** fill juniors; **forbid** any guard from touching skeleton set (no “satisfied at mark time” gate)
3. Run Phase 1 **after** quotas/ON are known, or re-run Phase 1 **once** at very end before violations
4. On ON conflict: atomic `{ remove skeleton if conflict; ForceApply ON; re-place L1 on affected Nights in same transaction }`

### 9.3 Strengthen feasibility checker

Include in simulation: exact night quotas, approved ON slots, productivity caps, full user list from DB. If fails → clear error before SA. If passes but final fails → checker incomplete.

### 9.4 Debug recipe for each failing date D

For each of: 2026-08-23, 08-29, 09-01, 09-05, 09-08, 09-22:

1. Log Night assignees: userId, level, IsSkeleton, labels on D±1
2. Log all L1 users: availability on D per adjacency/consecutive/ON
3. Trace whether `EnsureShiftManagerMixForSlot` returns false and why (`TryReplaceForManagerMix` exhausted)
4. Check if `FinalizeManagerMixMandatory` still has `HasUnmetManagerMix` after round 10

Add temporary logging or a diagnostic API endpoint returning this for production month.

### 9.5 Do NOT repeat (already tried)

- End-only `RunShiftManagerRepairPasses` without skeleton lock → oscillates
- `relaxNightSpacing` / ignore all adjacency on manager install → adjacency errors
- Only L1 skeleton without L2 → `total≥2` fails when L2 stripped (partially fixed; still failing in prod)
- `BuildReservedManagerSkeleton` only at start of ApplyMandatoryConstraints without end lock

---

## 10. Acceptance criteria (unchanged)

For Pediatrics 2026-08-23..2026-09-22:

1. **Zero** `Shift manager mix unmet` for Night and Evening on every staffed date
2. Zero adjacency violations (except waived ON pairs)
3. All approved ON met (incl. user 14 Morning 2026-08-23 if still in DB)
4. Exact night quotas satisfied
5. Max consecutive workdays respected (ON exempt)
6. Job completes without hang
7. **Production:** after code fix, **Liara redeploy** required

---

## 11. Persian summary for product owner

**مشکل:** شب‌های پر شده بدون ترکیب مسئول (۲ نفر شامل ۱ سطح‌۱) گزارش می‌شوند.  
**کار انجام‌شده:** Approach A با پرچم `IsSkeleton`، فاز اول اسکلت، گارد feasibility، حلقهٔ نهایی `FinalizeManagerMixMandatory`، محافظت L2 در productivity — در تست واحد OK، در production هنوز ۶ تاریخ Night خراب.  
**احتمال قوی:** لایهٔ مسئول هنوز در میانهٔ pipeline (پوشش، موظفی، سهمیه) عملاً overwrite می‌شود چون اسکلت فقط وقتی mix **آن لحظه** برقرار است فعال می‌شود؛ یا جایگزین L1 قانونی برای آن تاریخ وجود ندارد ولی سرویس خطا نمی‌دهد.  
**راه‌حل پایدار پیشنهادی:** CP-SAT با قید سخت mix، یا Phase 1 با قفل واقعی (بدون وابستگی به satisfied لحظه‌ای) + throw اگر ترمیم نشد.

---

## 12. Related files in repo

- Original brief: `AI_BRIEF_NightManagerMix.md` (problem statement before Approach A)
- **This file:** `AI_BRIEF_ApproachA_Implementation_And_Remaining_Failures.md`
- Tests: `tests/ShiftYar.Application.Tests/ShiftManagerRulesTests.cs`

---

*Generated for handoff to another AI — includes implementation status as of Approach A merge attempt; production still failing on 6 Night dates listed in section 1.*
