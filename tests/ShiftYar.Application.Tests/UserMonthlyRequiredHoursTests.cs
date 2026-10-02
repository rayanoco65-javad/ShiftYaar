using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.DTOs.UserModel;
using ShiftYar.Application.Features.UserModel.Filters;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.DepartmentModel;
using ShiftYar.Domain.Entities.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ShiftYar.Application.Tests
{
    public class UserMonthlyRequiredHoursTests
    {
        [Fact]
        public void Filter_WithParameters_BuildsValidExpression()
        {
            var filter = new UserMonthlyRequiredHourFilter
            {
                DepartmentId = 5,
                UserId = 12,
                PersianYear = 1405,
                PersianMonth = 7,
                IsManuallyEdited = true
            };

            var expr = filter.GetExpression().Compile();

            var matchingItem = new UserMonthlyRequiredHour
            {
                DepartmentId = 5,
                UserId = 12,
                PersianYear = 1405,
                PersianMonth = 7,
                IsManuallyEdited = true
            };

            var nonMatchingItem = new UserMonthlyRequiredHour
            {
                DepartmentId = 5,
                UserId = 13,
                PersianYear = 1405,
                PersianMonth = 7,
                IsManuallyEdited = false
            };

            Assert.True(expr(matchingItem));
            Assert.False(expr(nonMatchingItem));
        }

        [Fact]
        public void BulkUpsertDto_ValidatesRanges()
        {
            var validDto = new UserMonthlyRequiredHourBulkUpsertDto
            {
                DepartmentId = 1,
                PersianYear = 1405,
                PersianMonth = 8,
                Items = new List<UserMonthlyRequiredHourItemDto>
                {
                    new() { UserId = 10, ApprovedHours = 160m, Notes = "تأییدشده با کارگزینی" }
                }
            };

            Assert.Equal(1, validDto.DepartmentId);
            Assert.Equal(1405, validDto.PersianYear);
            Assert.Equal(8, validDto.PersianMonth);
            Assert.Single(validDto.Items);
            Assert.Equal(160m, validDto.Items[0].ApprovedHours);
            Assert.Equal("تأییدشده با کارگزینی", validDto.Items[0].Notes);
        }

        [Fact]
        public void UserMonthlyRequiredHour_PropertiesSetCorrectly()
        {
            var record = new UserMonthlyRequiredHour
            {
                Id = 100,
                UserId = 25,
                DepartmentId = 2,
                PersianYear = 1405,
                PersianMonth = 1,
                CalculatedHours = 142.5m,
                ApprovedHours = 140m,
                IsManuallyEdited = true,
                Notes = "کسر ۲.۵ ساعت مرخصی اداری",
                ConfirmedAt = DateTime.UtcNow,
                ConfirmedByUserId = 1
            };

            Assert.Equal(100, record.Id);
            Assert.Equal(25, record.UserId);
            Assert.Equal(2, record.DepartmentId);
            Assert.Equal(1405, record.PersianYear);
            Assert.Equal(1, record.PersianMonth);
            Assert.Equal(142.5m, record.CalculatedHours);
            Assert.Equal(140m, record.ApprovedHours);
            Assert.True(record.IsManuallyEdited);
            Assert.Equal("کسر ۲.۵ ساعت مرخصی اداری", record.Notes);
            Assert.NotNull(record.ConfirmedAt);
            Assert.Equal(1, record.ConfirmedByUserId);
        }
    }
}
