# Working Hours Calculator

This feature contains the productivity rules mandated by the *Regulation of Productivity Promotion of Clinical Employees* in Iran.

## Calculation Overview

- **Daily base working hours:** `22 / 3` (7 hours and 20 minutes ≈ 7.3333 hours)
- **Working days:** `WorkingDays = TotalDays − (FridaysCount + OfficialHolidaysCount)` (Thursdays are standard working days in 24/7 healthcare facilities)
- **Gross monthly base hours:** `BaseMonthlyHours = WorkingDays × (22 / 3)`
- **Standard month cap (optional):** When `CapBaseHoursToStandardMonth = true`, working days are capped at 24 and base hours at 176.0 (4 weeks × 44h).
- **Weekly reduction caps (Ministry of Health Executive Directive):**
  - **Seniority:** 0–4 years = 1.0h/week (new recruits / طرحی included; never 0), 4y 1m – 8y = 2.0h, 8y 1m – 12y = 3.0h, 12y 1m – 16y = 4.0h, >16y = 5.0h.
  - **Hardship (max 2.0h/week):** Dual support for percentage (8–25%: 0.5h, 26–50%: 1.0h, 51–75%: 1.5h, 76–100%: 2.0h) or Civil Service Management Law points (0–375: 0.5h, 376–750: 1.0h, 751–1000: 1.5h, >1000: 2.0h) with strict Mutually Exclusive (XOR) validation.
  - **Article 4 Clinical Management Exception:** Supervisors, Head Nurses, Metrons, and Directors automatically receive 2.0h hardship reduction regardless of recorded percentage or points.
  - **Rotating shift:** 1.0h/week for all rotating/unconventional shift personnel (10-year seniority condition completely removed). Fixed day = 0.0h.
  - **Total weekly reduction cap:** `min(8.0, Seniority + Hardship + RotatingShift)`.
- **Monthly reduction formula:** `MonthlyReductions = (TotalDays / 7) × WeeklyReduction` (in standard 31-day months, 1h weekly maps to 5h monthly reduction).

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

