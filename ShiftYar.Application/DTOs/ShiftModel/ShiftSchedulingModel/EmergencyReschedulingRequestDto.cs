using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ShiftYar.Application.Common.Utilities;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// Request for emergency rescheduling of shifts using the rolling horizon algorithm.
    /// </summary>
    public class EmergencyReschedulingRequestDto : IValidatableObject
    {
        private const int MaxWindowLengthDays = 21;

        [Range(1, int.MaxValue, ErrorMessage = "شناسه دپارتمان معتبر نیست.")]
        public int DepartmentId { get; set; }

        [Required]
        [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")]
        public string StartDate { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")]
        public string EndDate { get; set; } = string.Empty;

        [Range(1, MaxWindowLengthDays, ErrorMessage = "طول پنجره باید بین 1 تا 21 روز باشد.")]
        public int WindowSizeDays { get; set; } = 7;

        [Range(0, 14, ErrorMessage = "همپوشانی باید بین 0 تا 14 روز باشد.")]
        public int OverlapDays { get; set; } = 1;

        public List<int> ImpactedUserIds { get; set; } = new List<int>();

        public SchedulingAlgorithm Algorithm { get; set; } = SchedulingAlgorithm.SimulatedAnnealing;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!TryParsePersianDate(StartDate, nameof(StartDate), out var start, out var startError))
            {
                yield return startError;
                yield break;
            }

            if (!TryParsePersianDate(EndDate, nameof(EndDate), out var end, out var endError))
            {
                yield return endError;
                yield break;
            }

            if (end < start)
            {
                yield return new ValidationResult("تاریخ پایان باید بعد از تاریخ شروع باشد.", new[] { nameof(EndDate) });
            }

            if (WindowSizeDays <= OverlapDays)
            {
                yield return new ValidationResult("همپوشانی باید کوچکتر از طول پنجره باشد.", new[] { nameof(OverlapDays) });
            }

            if (ImpactedUserIds.Count != ImpactedUserIds.Distinct().Count())
            {
                yield return new ValidationResult("شناسه کاربران تکراری است.", new[] { nameof(ImpactedUserIds) });
            }
        }

        private static bool TryParsePersianDate(string value, string fieldName, out DateTime date, out ValidationResult error)
        {
            try
            {
                date = DateConverter.ConvertToGregorianDate(value);
                error = ValidationResult.Success!;
                return true;
            }
            catch
            {
                date = default;
                error = new ValidationResult($"قالب {fieldName} معتبر نیست.", new[] { fieldName });
                return false;
            }
        }
    }

    public class RollingHorizonRescheduleResultDto
    {
        public List<ShiftAssignmentDto> AggregatedAssignments { get; set; } = new List<ShiftAssignmentDto>();
        public List<RollingHorizonWindowResultDto> Windows { get; set; } = new List<RollingHorizonWindowResultDto>();
        public TimeSpan TotalSolveTime { get; set; }
        public bool HasConflicts { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }

    public class RollingHorizonWindowResultDto
    {
        public int WindowIndex { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string AlgorithmStatus { get; set; } = string.Empty;
        public int AssignmentCount { get; set; }
        public double ProductivityComplianceRate { get; set; }
        public List<string> Violations { get; set; } = new List<string>();
    }
}

