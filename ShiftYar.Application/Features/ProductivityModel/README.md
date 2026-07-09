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

🚧 A quick audit of the three scheduling engines (`SimulatedAnnealingScheduler`, `HybridScheduler`, `OrToolsCPSatScheduler`) shows that they currently consume only the legacy workload inputs and are not yet wired to the productivity calculator. Injecting those deductions requires feeding the calculator’s output into the constraint-building step for each engine.

