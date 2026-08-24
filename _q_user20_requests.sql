SELECT sr.Id, sr.RequestType, sr.RequestAction, sr.ShiftLabel, sr.Status,
       CONVERT(varchar(10), sr.RequestDate, 23) AS RequestDate,
       sr.Reason
FROM ShiftRequests sr
WHERE sr.UserId = 20
  AND sr.RequestDate >= '2026-08-23'
  AND sr.RequestDate <= '2026-09-22'
ORDER BY sr.RequestDate, sr.ShiftLabel, sr.RequestAction;

SELECT CONVERT(varchar(10), sd.Date, 23) AS D, sd.PersianDate, s.Label, sa.IsOnCall
FROM ShiftAssignments sa
JOIN ShiftDates sd ON sd.Id = sa.ShiftDateId
JOIN Shifts s ON s.Id = sa.ShiftId
WHERE sa.UserId = 20
  AND sd.Date >= '2026-08-23' AND sd.Date <= '2026-09-22'
ORDER BY sd.Date, s.Label;
