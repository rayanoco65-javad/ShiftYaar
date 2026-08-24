SELECT s.Id AS ShiftId, s.Label, s.StartTime, s.EndTime,
       rs.SpecialtyId,
       rs.RequiredTotalCount, rs.RequiredMaleCount, rs.RequiredFemaleCount,
       rs.OnCallTotalCount, rs.OnCallMaleCount, rs.OnCallFemaleCount,
       rs.HolidayRequiredTotalCount, rs.HolidayRequiredMaleCount, rs.HolidayRequiredFemaleCount,
       rs.HolidayOnCallTotalCount, rs.HolidayOnCallMaleCount, rs.HolidayOnCallFemaleCount
FROM ShiftRequiredSpecialties rs
JOIN Shifts s ON s.Id = rs.ShiftId
WHERE s.DepartmentId = 2
ORDER BY s.Label, rs.SpecialtyId;
