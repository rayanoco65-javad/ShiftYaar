SELECT Id, FullName, ShiftType, ShiftSubType, AllowedShiftPermissions, IsActive
FROM Users WHERE FullName LIKE N'%زهرا درخشانی%' OR Id = 13;

-- Assignments Shahrivar
SELECT CONVERT(varchar(10), sd.Date, 23) AS D, sd.PersianDate, s.Label, sa.IsOnCall
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE sa.UserId = 13 AND sd.PersianDate LIKE N'1405/06/%'
ORDER BY sd.Date, s.Label;

-- All approved requests Shahrivar
SELECT sr.Id, sr.RequestType, sr.RequestAction, sr.ShiftLabel, sr.Status,
       CONVERT(varchar(10), sr.RequestDate, 23) AS RequestDate, sr.Reason
FROM ShiftRequests sr
WHERE sr.UserId = 13
  AND sr.RequestDate >= '2026-08-23' AND sr.RequestDate <= '2026-09-22'
ORDER BY sr.RequestDate, sr.RequestAction, sr.ShiftLabel;

-- Day shift / night quotas
SELECT * FROM UserMonthlyDayShiftQuotas WHERE UserId = 13 AND PersianYear = 1405 AND PersianMonth = 6;
SELECT * FROM UserMonthlyNightQuotas WHERE UserId = 13 AND PersianYear = 1405 AND PersianMonth = 6;

-- Productivity
SELECT TOP 5 * FROM StaffEmploymentInfos WHERE UserId = 13;
