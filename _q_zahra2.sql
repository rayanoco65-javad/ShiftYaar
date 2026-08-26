SELECT Id, FullName, ShiftType, ShiftSubType, TwoShiftRotationPattern, AllowedShiftPermissions
FROM Users WHERE Id = 13;

-- Who worked evening/morning on the gap days?
SELECT sd.PersianDate, s.Label, u.FullName, u.Id
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
JOIN Users u ON u.Id = sa.UserId
WHERE u.DepartmentId = 2
  AND sd.PersianDate IN (N'1405/06/06', N'1405/06/10', N'1405/06/14', N'1405/06/18', N'1405/06/22')
  AND ISNULL(sa.IsOnCall,0)=0
ORDER BY sd.PersianDate, s.Label, u.FullName;

-- Productivity tables
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Emp%' OR TABLE_NAME LIKE '%Prod%' OR TABLE_NAME LIKE '%Staff%';
