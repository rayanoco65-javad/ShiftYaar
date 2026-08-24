-- Clean orphan assignments: pediatrics users stored on other department's shifts (1/2)
DELETE sa
FROM ShiftAssignments sa
INNER JOIN Users u ON u.Id = sa.UserId
INNER JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
INNER JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.DepartmentId = 2
  AND sd.PersianDate LIKE N'1405/06/%'
  AND (s.DepartmentId IS NULL OR s.DepartmentId <> 2);

-- Verify Saki has no evenings left
SELECT CONVERT(varchar(10), sd.Date, 23) AS D, s.Label, sa.ShiftId, COUNT(*) AS Cnt
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.Id = 12 AND sd.PersianDate LIKE N'1405/06/%'
GROUP BY sd.Date, s.Label, sa.ShiftId
ORDER BY sd.Date, s.Label;
