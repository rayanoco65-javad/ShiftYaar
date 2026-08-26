SELECT Id, FullName, ShiftType, ShiftSubType FROM Users WHERE Id = 14;

SELECT CONVERT(varchar(10), sd.Date, 23) AS D, sd.PersianDate, s.Label, sa.IsOnCall
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE sa.UserId = 14
  AND sd.PersianDate LIKE N'1405/06/%'
ORDER BY sd.Date, s.Label;

SELECT sr.Id, sr.RequestType, sr.RequestAction, sr.ShiftLabel, sr.Status,
       CONVERT(varchar(10), sr.RequestDate, 23) AS RequestDate
FROM ShiftRequests sr
WHERE sr.UserId = 14
  AND sr.RequestDate >= '2026-08-23' AND sr.RequestDate <= '2026-09-22'
  AND sr.Status = 1
ORDER BY sr.RequestDate;

SELECT MaxConsecutiveShifts, EnforceMaxConsecutiveShifts
FROM DepartmentSchedulingSettings WHERE DepartmentId = 2;
