SELECT u.Id AS UserId, sd.PersianDate, CONVERT(varchar(10), sd.Date, 23) AS GregorianDate,
       CAST(sd.IsHoliday AS int) AS IsHoliday, s.Label AS ShiftLabel
FROM ShiftAssignments sa
INNER JOIN Users u ON u.Id = sa.UserId
INNER JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
INNER JOIN Shifts s ON s.Id = sa.ShiftId
WHERE u.DepartmentId = 2
  AND sd.PersianDate LIKE N'1405/06/%'
  AND ISNULL(sa.IsOnCall, 0) = 0
ORDER BY u.Id, sd.Date, s.Label;
