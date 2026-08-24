SELECT DepartmentId, MaxConsecutiveShifts, EnforceMaxConsecutiveShifts, MaxShiftsPerDay, MaxShiftsPerWeek,
       FairShiftCountBalanceWeight, ShiftLabelBalanceWeight,
       AllowEveningAfterNightShift, AllowNightShiftAfterNightShift, MaxConsecutiveNightShifts
FROM DepartmentSchedulingSettings WHERE DepartmentId = 2;

SELECT Id, FullName, ShiftType, ShiftSubType
FROM Users WHERE DepartmentId = 2 ORDER BY Id;
