-- Shifts for dept 2
SELECT Id, DepartmentId, Label, StartTime, EndTime FROM Shifts WHERE DepartmentId = 2 ORDER BY Label, Id;

-- Saki day 1: both assignment details
SELECT sa.Id, sa.ShiftDateId, sa.ShiftId, s.Label, sd.PersianDate, CONVERT(varchar(10), sd.Date, 23) AS D
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE sa.UserId = 12 AND sd.PersianDate = N'1405/06/01';

-- Duplicate ShiftDates for shahrivar?
SELECT PersianDate, COUNT(*) AS Cnt
FROM ShiftDates
WHERE PersianDate LIKE N'1405/06/%'
GROUP BY PersianDate
HAVING COUNT(*) > 1;

-- Saki evenings details
SELECT sa.Id, sa.ShiftDateId, sa.ShiftId, s.Label, sd.PersianDate, CONVERT(varchar(10), sd.Date, 23) AS D, sa.Notes
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE sa.UserId = 12 AND s.Label = 1 AND sd.PersianDate LIKE N'1405/06/%'
ORDER BY sd.Date;
