using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;

/// <summary>
/// اولویت پر کردن ساعت موظفی: پرسنل غیرطرحی قبل از پرسنل طرحی.
/// </summary>
public static class ProjectPersonnelProductivityPriority
{
    public static bool IsProjectPersonnel(UserConstraint user) => user.IsProjectPersonnel == true;

    /// <summary>۰ = غیرطرحی (اول)، ۱ = طرحی (بعد)</summary>
    public static int FillTier(UserConstraint user) => IsProjectPersonnel(user) ? 1 : 0;

    public static IEnumerable<UserConstraint> SplitNonProjectFirst(IEnumerable<UserConstraint> users)
    {
        var list = users as IList<UserConstraint> ?? users.ToList();
        foreach (var user in list.Where(u => !IsProjectPersonnel(u)))
        {
            yield return user;
        }

        foreach (var user in list.Where(IsProjectPersonnel))
        {
            yield return user;
        }
    }
}
