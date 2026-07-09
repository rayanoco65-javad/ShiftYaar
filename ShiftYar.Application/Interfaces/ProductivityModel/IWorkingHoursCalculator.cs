using ShiftYar.Application.DTOs.ProductivityModel;

namespace ShiftYar.Application.Interfaces.ProductivityModel
{
    /// <summary>
    /// Interface for the working hours calculator.
    /// </summary>
    public interface IWorkingHoursCalculator
    {
        WorkingHoursCalculationResultDto CalculateMonthlyHours(WorkingHoursCalculationRequestDto request);
    }
}

