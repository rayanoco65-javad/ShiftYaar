namespace ShiftYar.Application.DTOs.ProductivityModel
{
    /// <summary>
    /// Result of the working hours calculation.
    /// </summary>
    public class WorkingHoursCalculationResultDto
    {
        public decimal BaseMonthlyHours { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal FinalMonthlyRequiredHours { get; set; }
        /// <summary>ساعت موظفی خالص نهایی (همگام با FinalMonthlyRequiredHours).</summary>
        public decimal NetRequiredHours
        {
            get => FinalMonthlyRequiredHours;
            set => FinalMonthlyRequiredHours = value;
        }
        public WorkingHoursCalculationBreakdownDto Breakdown { get; set; } = new WorkingHoursCalculationBreakdownDto();
    }
}

