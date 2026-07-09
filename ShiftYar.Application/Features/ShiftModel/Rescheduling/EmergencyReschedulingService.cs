using Microsoft.Extensions.Logging;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Interfaces.ShiftModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Rescheduling
{
    public class EmergencyReschedulingService : IEmergencyReschedulingService
    {
        private readonly IShiftSchedulingService _shiftSchedulingService;
        private readonly ILogger<EmergencyReschedulingService> _logger;

        public EmergencyReschedulingService(
            IShiftSchedulingService shiftSchedulingService,
            ILogger<EmergencyReschedulingService> logger)
        {
            _shiftSchedulingService = shiftSchedulingService;
            _logger = logger;
        }

        public async Task<ApiResponse<RollingHorizonRescheduleResultDto>> RescheduleAsync(EmergencyReschedulingRequestDto request)
        {
            try
            {
                var start = DateConverter.ConvertToGregorianDate(request.StartDate);
                var end = DateConverter.ConvertToGregorianDate(request.EndDate);

                if (end < start)
                {
                    return ApiResponse<RollingHorizonRescheduleResultDto>.Fail("End date must be after start date.");
                }

                var windows = BuildWindows(start, end, request.WindowSizeDays, request.OverlapDays);
                if (windows.Count == 0)
                {
                    return ApiResponse<RollingHorizonRescheduleResultDto>.Fail("No scheduling windows could be generated.");
                }

                var aggregatedAssignments = new Dictionary<string, ShiftAssignmentDto>();
                var windowResults = new List<RollingHorizonWindowResultDto>();
                var totalSolveTime = TimeSpan.Zero;
                var hasConflicts = false;
                var impactedOnly = request.ImpactedUserIds?.Count > 0;

                var windowIndex = 1;
                foreach (var window in windows)
                {
                    var internalRequest = new ShiftSchedulingRequestInternalDto
                    {
                        DepartmentId = request.DepartmentId,
                        StartDate = window.Start,
                        EndDate = window.End,
                        Algorithm = request.Algorithm
                    };

                    var optimizationResponse = await _shiftSchedulingService.OptimizeShiftScheduleInternalAsync(internalRequest);
                    if (!optimizationResponse.IsSuccess || optimizationResponse.Data == null)
                    {
                        var message = optimizationResponse.Message ?? "Unknown optimization failure.";
                        return ApiResponse<RollingHorizonRescheduleResultDto>.Fail($"Window {windowIndex} failed: {message}");
                    }

                    var windowResult = new RollingHorizonWindowResultDto
                    {
                        WindowIndex = windowIndex,
                        StartDate = window.Start,
                        EndDate = window.End,
                        AlgorithmStatus = optimizationResponse.Data.AlgorithmStatus,
                        AssignmentCount = optimizationResponse.Data.Assignments.Count,
                        ProductivityComplianceRate = optimizationResponse.Data.Statistics?.ProductivityComplianceRate ?? 0,
                        Violations = optimizationResponse.Data.Violations
                    };

                    windowResults.Add(windowResult);
                    totalSolveTime += optimizationResponse.Data.ExecutionTime;
                    hasConflicts |= (optimizationResponse.Data.Violations?.Count ?? 0) > 0;

                    foreach (var assignment in optimizationResponse.Data.Assignments)
                    {
                        if (impactedOnly && !request.ImpactedUserIds.Contains(assignment.UserId))
                        {
                            continue;
                        }

                        var key = $"{assignment.UserId}_{assignment.ShiftId}_{assignment.Date:yyyyMMdd}";
                        aggregatedAssignments[key] = assignment;
                    }

                    windowIndex++;
                }

                var result = new RollingHorizonRescheduleResultDto
                {
                    AggregatedAssignments = aggregatedAssignments.Values
                        .OrderBy(a => a.Date)
                        .ThenBy(a => a.UserId)
                        .ToList(),
                    Windows = windowResults,
                    TotalSolveTime = totalSolveTime,
                    HasConflicts = hasConflicts,
                    Notes = BuildNotes(windows.Count, request, hasConflicts)
                };

                return ApiResponse<RollingHorizonRescheduleResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Emergency rescheduling failed for department {DepartmentId}", request.DepartmentId);
                return ApiResponse<RollingHorizonRescheduleResultDto>.Fail($"Error: {ex.Message}");
            }
        }

        private static List<(DateTime Start, DateTime End)> BuildWindows(DateTime start, DateTime end, int windowSizeDays, int overlapDays)
        {
            var windows = new List<(DateTime Start, DateTime End)>();
            var step = Math.Max(1, windowSizeDays - overlapDays);
            var currentStart = start;

            while (currentStart <= end)
            {
                var currentEnd = currentStart.AddDays(windowSizeDays - 1);
                if (currentEnd > end)
                {
                    currentEnd = end;
                }

                windows.Add((currentStart, currentEnd));

                if (currentEnd >= end)
                {
                    break;
                }

                currentStart = currentStart.AddDays(step);
            }

            return windows;
        }

        private static List<string> BuildNotes(int windowCount, EmergencyReschedulingRequestDto request, bool hasConflicts)
        {
            var notes = new List<string>
            {
                $"Windows executed: {windowCount}",
                $"WindowSizeDays={request.WindowSizeDays}, OverlapDays={request.OverlapDays}",
                hasConflicts ? "Violations detected in at least one window." : "All windows satisfied constraints."
            };

            if (request.ImpactedUserIds?.Count > 0)
            {
                notes.Add($"Impacted users: {string.Join(",", request.ImpactedUserIds)}");
            }

            return notes;
        }
    }
}


