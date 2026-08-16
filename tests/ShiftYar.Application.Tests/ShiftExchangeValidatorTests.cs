using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Enums.ShiftExchangeModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class ShiftExchangeValidatorTests
{
    [Fact]
    public void ValidateSwapOfferingAssignment_RequiresOfferingShiftId()
    {
        Assert.NotNull(ShiftExchangeValidator.ValidateSwapOfferingAssignment(null));
        Assert.NotNull(ShiftExchangeValidator.ValidateSwapOfferingAssignment(0));
        Assert.Null(ShiftExchangeValidator.ValidateSwapOfferingAssignment(42));
    }

    [Fact]
    public void ValidateTransferOfferingAssignment_RejectsOfferingShiftId()
    {
        Assert.Null(ShiftExchangeValidator.ValidateTransferOfferingAssignment(null));
        Assert.NotNull(ShiftExchangeValidator.ValidateTransferOfferingAssignment(42));
    }

    [Fact]
    public void IsDuplicateTransfer_MatchesSamePendingTransfer()
    {
        var duplicate = ShiftExchangeValidator.IsDuplicateTransfer(
            ExchangeType.Transfer,
            ExchangeStatus.Pending,
            existingRequestingUserId: 1,
            existingOfferingUserId: 2,
            existingRequestingAssignmentId: 100,
            requestingUserId: 1,
            offeringUserId: 2,
            requestingShiftAssignmentId: 100);

        Assert.True(duplicate);
    }

    [Fact]
    public void IsDuplicateSwap_MatchesReverseDirection()
    {
        var duplicate = ShiftExchangeValidator.IsDuplicateSwap(
            ExchangeType.Swap,
            ExchangeStatus.Approved,
            existingRequestingUserId: 2,
            existingOfferingUserId: 1,
            existingRequestingAssignmentId: 200,
            existingOfferingAssignmentId: 100,
            requestingUserId: 1,
            offeringUserId: 2,
            requestingShiftAssignmentId: 100,
            offeringShiftAssignmentId: 200);

        Assert.True(duplicate);
    }
}
