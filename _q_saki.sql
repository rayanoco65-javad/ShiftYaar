SELECT u.Id, u.FullName, u.ShiftType, u.ShiftSubType
FROM Users u WHERE u.FullName LIKE N'%فرشته%' OR u.Id = 12;

SELECT CONVERT(varchar(10), sd.Date, 23) AS D, sd.PersianDate, s.Label, sa.IsOnCall, sa.Id AS AssignmentId
FROM ShiftAssignments sa
JOIN Users u ON u.Id = sa.UserId
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.Id = 12
  AND sd.PersianDate LIKE N'1405/06/%'
ORDER BY sd.Date, s.Label;
