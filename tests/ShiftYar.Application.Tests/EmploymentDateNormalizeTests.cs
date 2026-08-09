using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using Xunit;

namespace ShiftYar.Application.Tests;

public class EmploymentDateNormalizeTests
{
    [Theory]
    [InlineData("1380/01/04")]
    [InlineData("1380/06/01")]
    [InlineData("1381/04/01")]
    [InlineData("1390/03/01")]
    [InlineData("1390/04/01")]
    [InlineData("1391/04/01")]
    [InlineData("1391/09/09")]
    [InlineData("1404/03/01")]
    public void NormalizeEmploymentDate_ConvertsMisstoredPersianComponents(string persian)
    {
        var parts = persian.Split('/');
        var stored = new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        var expected = DateConverter.ConvertToGregorianDate(persian);

        Assert.True(StaffEmploymentInfo.LooksLikeMisstoredPersianComponents(stored));
        Assert.Equal(expected, StaffEmploymentInfo.NormalizeEmploymentDate(stored));
        Assert.Equal(expected, DateConverter.NormalizeEmploymentDate(stored));
        Assert.Equal(persian, DateConverter.EmploymentDateToPersianString(stored));
    }

    [Fact]
    public void NormalizeEmploymentDate_LeavesRealGregorianUnchanged()
    {
        var real = DateConverter.ConvertToGregorianDate("1380/01/04");
        Assert.Equal(real, StaffEmploymentInfo.NormalizeEmploymentDate(real));
        Assert.False(StaffEmploymentInfo.LooksLikeMisstoredPersianComponents(real));
    }

    [Fact]
    public void FromUser_NormalizesEmploymentDate()
    {
        var user = new User
        {
            Id = 11,
            FullName = "Test",
            DateOfEmployment = new DateTime(1404, 3, 1)
        };

        var info = StaffEmploymentInfo.FromUser(user);
        Assert.Equal(DateConverter.ConvertToGregorianDate("1404/03/01"), info.DateOfEmployment);
    }

    [Fact]
    public void ResolveYearsOfService_UsesNormalizedGregorianDate()
    {
        // استخدام «۱۴۰۴/۰۳/۰۱» که به‌اشتباه 1404-03-01 ذخیره شده
        var info = new StaffEmploymentInfo
        {
            StaffId = 1,
            DateOfEmployment = new DateTime(1404, 3, 1)
        };

        // حدود یک سال بعد از استخدام واقعی (۲۰۲۵-۰۵-۲۲)
        var years = info.ResolveYearsOfService(new DateTime(2026, 7, 23));
        Assert.Equal(1, years);
    }
}
