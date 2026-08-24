-- Count morning duplicates and evening leftovers for Saki
SELECT s.Label, COUNT(*) AS Cnt
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.Id = 12 AND sd.PersianDate LIKE N'1405/06/%'
GROUP BY s.Label;

-- How many evening assignments for fixed morning users in dept 2 shahrivar?
SELECT u.Id, u.FullName, u.ShiftType, u.ShiftSubType, s.Label, COUNT(*) AS Cnt
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.DepartmentId = 2
  AND sd.PersianDate LIKE N'1405/06/%'
  AND u.ShiftType = 0
GROUP BY u.Id, u.FullName, u.ShiftType, u.ShiftSubType, s.Label
ORDER BY u.Id, s.Label;

-- Duplicate (UserId, ShiftDateId, ShiftId) rows
SELECT sa.UserId, sa.ShiftDateId, sa.ShiftId, COUNT(*) AS Cnt
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
WHERE u.DepartmentId = 2 AND sd.PersianDate LIKE N'1405/06/%'
GROUP BY sa.UserId, sa.ShiftDateId, sa.ShiftId
HAVING COUNT(*) > 1
ORDER BY Cnt DESC;
