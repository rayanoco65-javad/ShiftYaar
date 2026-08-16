using ShiftYar.Domain.Enums.ShiftExchangeModel;

namespace ShiftYar.Application.Common.Utilities;

public static class ShiftExchangeValidator
{
    public static bool IsActiveExchangeStatus(ExchangeStatus? status) =>
        status is ExchangeStatus.Pending or ExchangeStatus.Approved;

    public static ExchangeType ResolveExchangeType(ExchangeType? exchangeType) =>
        exchangeType ?? ExchangeType.Swap;

    public static string? ValidateUsersAreDifferent(int requestingUserId, int offeringUserId)
    {
        if (requestingUserId == offeringUserId)
        {
            return "امکان جابجایی یا واگذاری شیفت با خودتان وجود ندارد.";
        }

        return null;
    }

    public static string? ValidateSameDepartment(int? requestingDepartmentId, int? offeringDepartmentId)
    {
        if (!requestingDepartmentId.HasValue || !offeringDepartmentId.HasValue ||
            requestingDepartmentId.Value != offeringDepartmentId.Value)
        {
            return "کاربر دریافت‌کننده باید از همان دپارتمان کاربر واگذارکننده باشد.";
        }

        return null;
    }

    public static string? ValidateSwapOfferingAssignment(int? offeringShiftAssignmentId)
    {
        if (!offeringShiftAssignmentId.HasValue || offeringShiftAssignmentId.Value <= 0)
        {
            return "برای جابجایی دوطرفه، شناسه شیفت کاربر مقابل الزامی است.";
        }

        return null;
    }

    public static string? ValidateTransferOfferingAssignment(int? offeringShiftAssignmentId)
    {
        if (offeringShiftAssignmentId.HasValue && offeringShiftAssignmentId.Value > 0)
        {
            return "در واگذاری شیفت به کاربر آزاد، نباید شیفت برای کاربر مقابل انتخاب شود.";
        }

        return null;
    }

    public static bool IsDuplicateSwap(
        ExchangeType? existingType,
        ExchangeStatus? existingStatus,
        int? existingRequestingUserId,
        int? existingOfferingUserId,
        int? existingRequestingAssignmentId,
        int? existingOfferingAssignmentId,
        int requestingUserId,
        int offeringUserId,
        int requestingShiftAssignmentId,
        int offeringShiftAssignmentId)
    {
        if (!IsActiveExchangeStatus(existingStatus))
        {
            return false;
        }

        if (ResolveExchangeType(existingType) != ExchangeType.Swap)
        {
            return false;
        }

        return (existingRequestingUserId == requestingUserId &&
                existingOfferingUserId == offeringUserId &&
                existingRequestingAssignmentId == requestingShiftAssignmentId &&
                existingOfferingAssignmentId == offeringShiftAssignmentId) ||
               (existingRequestingUserId == offeringUserId &&
                existingOfferingUserId == requestingUserId &&
                existingRequestingAssignmentId == offeringShiftAssignmentId &&
                existingOfferingAssignmentId == requestingShiftAssignmentId);
    }

    public static bool IsDuplicateTransfer(
        ExchangeType? existingType,
        ExchangeStatus? existingStatus,
        int? existingRequestingUserId,
        int? existingOfferingUserId,
        int? existingRequestingAssignmentId,
        int requestingUserId,
        int offeringUserId,
        int requestingShiftAssignmentId)
    {
        if (!IsActiveExchangeStatus(existingStatus))
        {
            return false;
        }

        if (ResolveExchangeType(existingType) != ExchangeType.Transfer)
        {
            return false;
        }

        return existingRequestingUserId == requestingUserId &&
               existingOfferingUserId == offeringUserId &&
               existingRequestingAssignmentId == requestingShiftAssignmentId;
    }
}
