# Working Hours Calculator

This feature contains the productivity rules mandated by the *Regulation of Productivity Promotion of Clinical Employees* in Iran.

## Calculation Overview

- **Base weekly hours:** 44
- **Weekly reduction caps:** 5 (seniority) + 2 (hardship) + 1 (rotating) → max 8
- **Monthly base formula:** `BaseWeekly × WeeksInMonth`
- **Night/holiday multiplier:** `1.5 × ReportedNightHolidayHours`

The final obligation is computed as:

```
FinalMonthly = (BaseWeekly × Weeks) − (WeeklyReductions × Weeks) − NightHolidayCredit
NightHolidayCredit = (NightHolidayHours × 1.5) − NightHolidayHours
```

Use `StaffEmploymentInfoDto` (or `StaffEmploymentInfo.FromUser(user)` inside the Application layer) to adapt existing user aggregates without leaking domain types to the API surface. When special policies are needed, populate `WorkingHoursCalculationRequestDto.RuleOverrides` (all properties are optional) instead of instantiating domain configs. Finally call `IWorkingHoursCalculator.CalculateMonthlyHours` to obtain the detailed breakdown (base, deductions, multiplier credit, and final requirement).

## Manual override in scheduling

User entity field **`MaxProductivityRequiredHours`** (`decimal?`):

- `null` or `0` → automatic calculation via `WorkingHoursCalculator` / `ProductivityRequiredHoursResolver`
- positive value → used as the monthly required hours during shift scheduling (automatic calculation skipped)

See `ShiftYar.Application/Common/Utilities/ProductivityRequiredHoursResolver.cs`.

