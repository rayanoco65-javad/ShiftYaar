# Working Hours Calculator

This feature contains the productivity rules mandated by the *Regulation of Productivity Promotion of Clinical Employees* in Iran.

## Calculation Overview

- **Daily base working hours:** `22 / 3` (7 hours and 20 minutes ≈ 7.3333 hours)
- **Working days:** `WorkingDays = TotalDays − (FridaysCount + OfficialHolidaysCount)` (Thursdays are standard working days in 24/7 healthcare facilities)
- **Gross monthly base hours:** `BaseMonthlyHours = WorkingDays × (22 / 3)`
- **Standard month cap (optional):** When `CapBaseHoursToStandardMonth = true`, working days are capped at 24 and base hours at 176.0 (4 weeks × 44h).
- **Weekly reduction caps:** Seniority (0h for <5y, 1h for 5-12y, 2h for 13-17y, up to 3h for 18+y) + Hardship (2h for special sections, 0h for general) + Rotating shift (1h for 3-shift rotating with ≥10y seniority) → max 8h weekly.
- **Monthly reduction formula:** `MonthlyReductions = (TotalDays / 7) × WeeklyReduction` (in 31-day months, 1h weekly maps to 5h monthly reduction).

The final monthly duty hours obligation is computed as:

```
FinalMonthlyRequiredHours = max(0, BaseMonthlyHours − MonthlyReductions)
```

> **Important architectural separation:**  
> The 1.5× multiplier for night and holiday shifts applies exclusively to **effective worked hours** during schedule evaluation (`ProductivityWorkedHoursCalculator`). It rewards clinical staff for working anti-social hours towards their target; it is **never** deducted from the monthly required hours target (`FinalMonthlyRequiredHours`).

Use `StaffEmploymentInfoDto` (or `StaffEmploymentInfo.FromUser(user)` inside the Application layer) to adapt existing user aggregates without leaking domain types to the API surface. When special policies are needed, populate `WorkingHoursCalculationRequestDto.RuleOverrides` (all properties are optional) instead of instantiating domain configs. Finally call `IWorkingHoursCalculator.CalculateMonthlyHours` to obtain the detailed breakdown (base, deductions, multiplier credit, and final requirement).

## Manual override in scheduling

User entity field **`MaxProductivityRequiredHours`** (`decimal?`):

- `null` or `0` → automatic calculation via `WorkingHoursCalculator` / `ProductivityRequiredHoursResolver`
- positive value → used as the monthly required hours during shift scheduling (automatic calculation skipped)

See `ShiftYar.Application/Common/Utilities/ProductivityRequiredHoursResolver.cs`.

