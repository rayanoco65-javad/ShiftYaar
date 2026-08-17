using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Domain.Entities.UserModel;
using Xunit;

namespace ShiftYar.Application.Tests;

public class ProductivityRequiredHoursResolverTests
{
    [Fact]
    public void HasManualOverride_TrueWhenPositiveValue()
    {
        var user = new User { MaxProductivityRequiredHours = 120m };
        Assert.True(ProductivityRequiredHoursResolver.HasManualOverride(user));
    }

    [Fact]
    public void HasManualOverride_FalseWhenNull()
    {
        var user = new User { MaxProductivityRequiredHours = null };
        Assert.False(ProductivityRequiredHoursResolver.HasManualOverride(user));
    }

    [Fact]
    public void HasManualOverride_FalseWhenZero()
    {
        var user = new User { MaxProductivityRequiredHours = 0m };
        Assert.False(ProductivityRequiredHoursResolver.HasManualOverride(user));
    }

    [Fact]
    public void HasManualOverride_FalseWhenNegative()
    {
        var user = new User { MaxProductivityRequiredHours = -5m };
        Assert.False(ProductivityRequiredHoursResolver.HasManualOverride(user));
    }

    [Fact]
    public void ApplyToUserConstraint_UsesManualHoursAndOverridesCalculatedSnapshot()
    {
        var user = new User { MaxProductivityRequiredHours = 100m };
        var constraint = new UserConstraint { UserId = 1 };
        var calculated = new WorkingHoursCalculationResultDto
        {
            BaseMonthlyHours = 176,
            TotalDeductions = 16,
            FinalMonthlyRequiredHours = 160
        };

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calculated);

        Assert.True(constraint.IncludedInProductivityPlan);
        Assert.Equal(100m, constraint.ProductivityRequiredHours);
        Assert.Equal(100m, constraint.ProductivitySnapshot!.FinalMonthlyRequiredHours);
        Assert.Contains(
            constraint.ProductivitySnapshot.Breakdown!.Notes!,
            n => n.Contains("حداکثر ساعت موظفی"));
    }

    [Fact]
    public void ApplyToUserConstraint_ManualOnlyWhenNoCalculatedSnapshot()
    {
        var user = new User { MaxProductivityRequiredHours = 80m };
        var constraint = new UserConstraint { UserId = 2 };

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calculatedSnapshot: null);

        Assert.True(constraint.IncludedInProductivityPlan);
        Assert.Equal(80m, constraint.ProductivityRequiredHours);
        Assert.Equal(80m, constraint.ProductivitySnapshot!.FinalMonthlyRequiredHours);
    }

    [Fact]
    public void ApplyToUserConstraint_UsesCalculatedWhenNoManualOverride()
    {
        var user = new User { MaxProductivityRequiredHours = null };
        var constraint = new UserConstraint { UserId = 3 };
        var calculated = new WorkingHoursCalculationResultDto
        {
            BaseMonthlyHours = 176,
            TotalDeductions = 16,
            FinalMonthlyRequiredHours = 160
        };

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calculated);

        Assert.True(constraint.IncludedInProductivityPlan);
        Assert.Equal(160m, constraint.ProductivityRequiredHours);
        Assert.Same(calculated, constraint.ProductivitySnapshot);
    }

    [Fact]
    public void ApplyToUserConstraint_DoesNothingWhenNoManualAndNoCalculated()
    {
        var user = new User();
        var constraint = new UserConstraint { UserId = 4 };

        ProductivityRequiredHoursResolver.ApplyToUserConstraint(user, constraint, calculatedSnapshot: null);

        Assert.False(constraint.IncludedInProductivityPlan);
        Assert.Null(constraint.ProductivityRequiredHours);
        Assert.Null(constraint.ProductivitySnapshot);
    }
}
