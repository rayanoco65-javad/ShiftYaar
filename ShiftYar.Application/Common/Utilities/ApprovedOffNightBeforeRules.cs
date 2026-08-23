using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Linq;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// درخواست OFF تأییدشده برای صبح یا کل‌روز → شبِ روز قبل ممنوع
/// (شب D-1 با صبح D استراحت اجباری است).
/// </summary>
public static class ApprovedOffNightBeforeRules
{
    public static bool RequiresNightBeforeBlock(RequestType requestType, ShiftLabel shiftLabel) =>
        requestType == RequestType.FullDay || shiftLabel == ShiftLabel.Morning;

    public static bool RequiresNightBeforeBlock(RequestType requestType, ShiftLabel? shiftLabel) =>
        requestType == RequestType.FullDay ||
        (shiftLabel.HasValue && shiftLabel.Value == ShiftLabel.Morning);

    public static void ApplyNightBeforeOffConstraint(
        UserConstraint user,
        DateTime offDate,
        IReadOnlyList<Shift> departmentShifts,
        Func<Shift, ShiftLabel>? inferLabelFromTime = null)
    {
        var previousDay = offDate.Date.AddDays(-1);

        if (user.UnavailableShiftSlots.Any(s =>
                s.Date.Date == previousDay.Date && s.ShiftLabel == ShiftLabel.Night))
        {
            return;
        }

        // ON صریح شب همان روز اولویت دارد — OFF صبح روز بعد نباید آن را باطل کند
        if (user.RequiredShiftSlots.Any(s =>
                s.Date.Date == previousDay.Date && s.ShiftLabel == ShiftLabel.Night))
        {
            return;
        }

        var nightShift = departmentShifts.FirstOrDefault(s =>
        {
            var label = s.Label
                        ?? inferLabelFromTime?.Invoke(s)
                        ?? ShiftLabelResolver.InferLabelFromStartTime(s);
            return label == ShiftLabel.Night;
        });

        user.UnavailableShiftSlots.Add(new ShiftSlotConstraint
        {
            Date = previousDay,
            ShiftLabel = ShiftLabel.Night,
            ShiftId = nightShift?.Id
        });
    }

    public static bool IsNightBlockedByApprovedOff(UserConstraint user, DateTime nightDate) =>
        user.UnavailableShiftSlots.Any(s =>
            s.Date.Date == nightDate.Date && s.ShiftLabel == ShiftLabel.Night);
}
