using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ShiftModel;
using Xunit;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.Tests;

public class ShiftLabelResolverTests
{
    private static List<Shift> DepartmentShifts() =>
    [
        new() { Id = 1, Label = ShiftLabel.Morning, StartTime = TimeSpan.FromHours(8) },
        new() { Id = 2, Label = ShiftLabel.Evening, StartTime = TimeSpan.FromHours(14) },
        new() { Id = 3, Label = ShiftLabel.Night, StartTime = TimeSpan.FromHours(20) },
    ];

    [Theory]
    [InlineData(0, ShiftLabel.Morning)]
    [InlineData(1, ShiftLabel.Evening)]
    [InlineData(2, ShiftLabel.Night)]
    public void Resolve_ValidLabel_KeepsLabel_EvenWhenShiftIdCollides(int raw, ShiftLabel expected)
    {
        var (label, shiftId) = ShiftLabelResolver.Resolve(raw, DepartmentShifts());

        Assert.Equal(expected, label);
        Assert.Null(shiftId);
    }

    [Fact]
    public void Resolve_ShiftIdOutsideEnum_MapsToRealLabel()
    {
        var (label, shiftId) = ShiftLabelResolver.Resolve(3, DepartmentShifts());

        Assert.Equal(ShiftLabel.Night, label);
        Assert.Equal(3, shiftId);
    }
}
