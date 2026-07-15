using ShiftYar.Domain.Entities.ShiftModel;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Common.Utilities;

/// <summary>
/// نرمال‌سازی مقدار ShiftLabel در درخواست‌ها.
/// Label معتبر (0/1/2) همیشه Label است؛ فقط مقادیر خارج از enum (مثل Id=3) به‌عنوان Shift.Id تفسیر می‌شوند.
/// </summary>
public static class ShiftLabelResolver
{
    public static (ShiftLabel Label, int? ShiftId) Resolve(
        int? rawLabelValue,
        IReadOnlyList<Shift> departmentShifts,
        Func<Shift, ShiftLabel>? inferLabelFromTime = null)
    {
        if (!rawLabelValue.HasValue)
        {
            return (ShiftLabel.Morning, null);
        }

        var raw = rawLabelValue.Value;

        if (Enum.IsDefined(typeof(ShiftLabel), raw))
        {
            return ((ShiftLabel)raw, null);
        }

        var shiftById = departmentShifts.FirstOrDefault(s => s.Id == raw);
        if (shiftById != null)
        {
            var label = shiftById.Label
                        ?? inferLabelFromTime?.Invoke(shiftById)
                        ?? InferLabelFromStartTime(shiftById);
            return (label, shiftById.Id);
        }

        return ((ShiftLabel)raw, null);
    }

    public static ShiftLabel InferLabelFromStartTime(Shift shift)
    {
        var start = shift.StartTime ?? TimeSpan.Zero;
        if (start >= TimeSpan.FromHours(5) && start < TimeSpan.FromHours(13))
            return ShiftLabel.Morning;
        if (start >= TimeSpan.FromHours(13) && start < TimeSpan.FromHours(20))
            return ShiftLabel.Evening;
        return ShiftLabel.Night;
    }
}
