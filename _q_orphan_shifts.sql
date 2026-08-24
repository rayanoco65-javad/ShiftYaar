SELECT Id, DepartmentId, Label FROM Shifts WHERE Id IN (1,2,3,4,5,6) ORDER BY Id;

-- How many orphan dept2-user assignments on non-dept2 shifts in shahrivar?
SELECT s.Id AS ShiftId, s.DepartmentId AS ShiftDept, s.Label, COUNT(*) AS Cnt
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.DepartmentId = 2
  AND sd.PersianDate LIKE N'1405/06/%'
  AND (s.DepartmentId IS NULL OR s.DepartmentId <> 2)
GROUP BY s.Id, s.DepartmentId, s.Label;
