using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Features.ShiftModel.Hybrid;
using ShiftYar.Application.Features.ShiftModel.OrTools;
using ShiftYar.Application.Features.ShiftModel.OrTools.Models;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Interfaces.ShiftModel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.PerformanceComparison
{
    /// <summary>
    /// ابزار مقایسه عملکرد الگوریتم‌های مختلف شیفت‌بندی
    /// </summary>
    public class AlgorithmPerformanceComparator
    {
        private readonly ILogger<AlgorithmPerformanceComparator> _logger; // logger برای ثبت رویدادها و خطاها
        private readonly IShiftSchedulingService _schedulingService; // سرویس شیفت‌بندی که داده‌ها را از DB می‌خواند

        public AlgorithmPerformanceComparator(ILogger<AlgorithmPerformanceComparator> logger, IShiftSchedulingService schedulingService)
        {
            _logger = logger; // تزریق لاگر
            _schedulingService = schedulingService; // تزریق سرویس برای اجرای الگوریتم‌ها
        }

        /// <summary>
        /// مقایسه عملکرد تمام الگوریتم‌ها
        /// </summary>
        public async Task<PerformanceComparisonResult> CompareAllAlgorithmsAsync(ShiftSchedulingRequestDto request) // مقایسه همه الگوریتم‌ها روی ورودی واحد
        {
            var result = new PerformanceComparisonResult
            {
                Request = request,
                ComparisonStartTime = DateTime.Now
            };

            try
            {
                _logger.LogInformation("Starting performance comparison for department {DepartmentId}", request.DepartmentId);

                // اجرای الگوریتم‌ها
                var tasks = new List<Task<AlgorithmResult>>();

                // Simulated Annealing
                tasks.Add(Task.Run(() => RunViaServiceAsync(request, SchedulingAlgorithm.SimulatedAnnealing)));

                // OR-Tools CP-SAT
                tasks.Add(Task.Run(() => RunViaServiceAsync(request, SchedulingAlgorithm.OrToolsCPSat)));

                // Hybrid - OrToolsFirst
                tasks.Add(Task.Run(() => RunViaServiceAsync(CreateHybridRequest(request, ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel.HybridStrategy.OrToolsFirst), SchedulingAlgorithm.Hybrid)));

                // Hybrid - SimulatedAnnealingFirst
                tasks.Add(Task.Run(() => RunViaServiceAsync(CreateHybridRequest(request, ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel.HybridStrategy.SimulatedAnnealingFirst), SchedulingAlgorithm.Hybrid)));

                // Hybrid - Parallel
                tasks.Add(Task.Run(() => RunViaServiceAsync(CreateHybridRequest(request, ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel.HybridStrategy.Parallel), SchedulingAlgorithm.Hybrid)));

                // انتظار برای تکمیل همه
                var results = await Task.WhenAll(tasks);

                // تجزیه و تحلیل نتایج
                result.AlgorithmResults = results.ToList();
                result.ComparisonEndTime = DateTime.Now;
                result.TotalComparisonTime = result.ComparisonEndTime - result.ComparisonStartTime;

                // تحلیل آماری
                AnalyzeResults(result);

                _logger.LogInformation("Performance comparison completed in {Duration}ms", result.TotalComparisonTime.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance comparison");
                result.Errors.Add($"Comparison error: {ex.Message}");
                result.ComparisonEndTime = DateTime.Now;
                return result;
            }
        }

        /// <summary>
        /// مقایسه عملکرد دو الگوریتم خاص
        /// </summary>
        public async Task<PerformanceComparisonResult> CompareTwoAlgorithmsAsync( // مقایسه دو الگوریتم مشخص
            ShiftSchedulingRequestDto request,
            SchedulingAlgorithm algorithm1,
            SchedulingAlgorithm algorithm2)
        {
            var result = new PerformanceComparisonResult
            {
                Request = request,
                ComparisonStartTime = DateTime.Now
            };

            try
            {
                var tasks = new List<Task<AlgorithmResult>>();

                tasks.Add(Task.Run(() => RunViaServiceAsync(request, algorithm1)));

                tasks.Add(Task.Run(() => RunViaServiceAsync(request, algorithm2)));

                var results = await Task.WhenAll(tasks);
                result.AlgorithmResults = results.ToList();
                result.ComparisonEndTime = DateTime.Now;
                result.TotalComparisonTime = result.ComparisonEndTime - result.ComparisonStartTime;

                AnalyzeResults(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during two-algorithm comparison");
                result.Errors.Add($"Comparison error: {ex.Message}");
                return result;
            }
        }

        private async Task<AlgorithmResult> RunViaServiceAsync(ShiftSchedulingRequestDto baseRequest, SchedulingAlgorithm algorithm) // اجرای یک الگوریتم از طریق سرویس
        {
            var request = new ShiftSchedulingRequestDto
            {
                DepartmentId = baseRequest.DepartmentId,
                StartDate = baseRequest.StartDate,
                EndDate = baseRequest.EndDate,
                Algorithm = algorithm
            };

            var sw = Stopwatch.StartNew();
            var res = await _schedulingService.OptimizeShiftScheduleAsync(request);
            sw.Stop();

            var result = new AlgorithmResult
            {
                Algorithm = algorithm,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                ExecutionTime = sw.Elapsed,
                Success = res.IsSuccess,
                Error = res.IsSuccess ? "" : res.Message
            };

            if (res.IsSuccess && res.Data != null)
            {
                result.FinalScore = res.Data.FinalScore;
                result.Violations = res.Data.Violations.Count;
                result.TotalIterations = res.Data.TotalIterations;
                result.AlgorithmStatus = res.Data.AlgorithmStatus;
            }

            return result;
        }

        private ShiftSchedulingRequestDto CreateHybridRequest(ShiftSchedulingRequestDto baseRequest, ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel.HybridStrategy strategy) // ساخت درخواست Hybrid با استراتژی دلخواه
        {
            return new ShiftSchedulingRequestDto
            {
                DepartmentId = baseRequest.DepartmentId,
                StartDate = baseRequest.StartDate,
                EndDate = baseRequest.EndDate,
                Algorithm = SchedulingAlgorithm.Hybrid
            };
        }

        /// <summary>
        /// تحلیل آماری نتایج
        /// </summary>
        private void AnalyzeResults(PerformanceComparisonResult result)
        {
            var successfulResults = result.AlgorithmResults.Where(r => r.Success).ToList();

            if (!successfulResults.Any())
            {
                result.Analysis = "No successful results to analyze";
                return;
            }

            // بهترین امتیاز
            var bestScore = successfulResults.Min(r => r.FinalScore);
            var bestAlgorithm = successfulResults.First(r => r.FinalScore == bestScore);
            result.BestAlgorithm = bestAlgorithm.Algorithm.ToString();
            result.BestScore = bestScore;

            // سریع‌ترین الگوریتم
            var fastestTime = successfulResults.Min(r => r.ExecutionTime);
            var fastestAlgorithm = successfulResults.First(r => r.ExecutionTime == fastestTime);
            result.FastestAlgorithm = fastestAlgorithm.Algorithm.ToString();
            result.FastestTime = fastestTime;

            // آمارهای کلی
            result.AverageScore = successfulResults.Average(r => r.FinalScore);
            result.AverageExecutionTime = TimeSpan.FromMilliseconds(successfulResults.Average(r => r.ExecutionTime.TotalMilliseconds));
            result.TotalSuccessfulAlgorithms = successfulResults.Count;

            // تحلیل تفاوت‌ها
            var scoreRange = successfulResults.Max(r => r.FinalScore) - successfulResults.Min(r => r.FinalScore);
            var timeRange = successfulResults.Max(r => r.ExecutionTime) - successfulResults.Min(r => r.ExecutionTime);

            result.ScoreVariation = scoreRange;
            result.TimeVariation = timeRange;

            // توصیه‌ها
            result.Recommendations = GenerateRecommendations(result, successfulResults);
        }

        /// <summary>
        /// تولید توصیه‌ها بر اساس نتایج
        /// </summary>
        private List<string> GenerateRecommendations(PerformanceComparisonResult result, List<AlgorithmResult> successfulResults)
        {
            var recommendations = new List<string>();

            // اگر تفاوت امتیاز کم است، سرعت را در نظر بگیر
            if (result.ScoreVariation < 100)
            {
                recommendations.Add("تفاوت امتیاز بین الگوریتم‌ها کم است. الگوریتم سریع‌تر را انتخاب کنید.");
            }

            // اگر تفاوت زمان کم است، کیفیت را در نظر بگیر
            if (result.TimeVariation.TotalSeconds < 30)
            {
                recommendations.Add("تفاوت زمان اجرا کم است. الگوریتم با بهترین امتیاز را انتخاب کنید.");
            }

            // توصیه بر اساس پیچیدگی مسئله
            var hybridResults = successfulResults.Where(r => r.Algorithm == SchedulingAlgorithm.Hybrid).ToList();
            if (hybridResults.Any())
            {
                var hybridResult = hybridResults.First();
                if (hybridResult.Statistics.ContainsKey("ProblemComplexity"))
                {
                    var complexity = (double)hybridResult.Statistics["ProblemComplexity"];
                    if (complexity > 1000)
                    {
                        recommendations.Add("مسئله پیچیده است. الگوریتم ترکیبی با استراتژی تطبیقی توصیه می‌شود.");
                    }
                    else if (complexity < 100)
                    {
                        recommendations.Add("مسئله ساده است. OR-Tools CP-SAT برای مسائل کوچک مناسب‌تر است.");
                    }
                }
            }

            // توصیه بر اساس تعداد نقض محدودیت‌ها
            var minViolations = successfulResults.Min(r => r.Violations);
            var bestViolationAlgorithm = successfulResults.First(r => r.Violations == minViolations);
            if (minViolations == 0)
            {
                recommendations.Add($"الگوریتم {bestViolationAlgorithm.Algorithm} تمام محدودیت‌ها را رعایت کرده است.");
            }
            else
            {
                recommendations.Add($"الگوریتم {bestViolationAlgorithm.Algorithm} کمترین نقض محدودیت را دارد ({minViolations} مورد).");
            }

            return recommendations;
        }

        // حذف لودرهای شبیه‌سازی شده؛ داده‌ها از سرویس و دیتابیس خوانده می‌شوند
    }

    /// <summary>
    /// نتیجه مقایسه عملکرد
    /// </summary>
    public class PerformanceComparisonResult
    {
        public ShiftSchedulingRequestDto Request { get; set; } = new ShiftSchedulingRequestDto();
        public DateTime ComparisonStartTime { get; set; }
        public DateTime ComparisonEndTime { get; set; }
        public TimeSpan TotalComparisonTime { get; set; }
        public List<AlgorithmResult> AlgorithmResults { get; set; } = new List<AlgorithmResult>();
        public List<string> Errors { get; set; } = new List<string>();

        // تحلیل آماری
        public string BestAlgorithm { get; set; } = "";
        public double BestScore { get; set; }
        public string FastestAlgorithm { get; set; } = "";
        public TimeSpan FastestTime { get; set; }
        public double AverageScore { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public int TotalSuccessfulAlgorithms { get; set; }
        public double ScoreVariation { get; set; }
        public TimeSpan TimeVariation { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public string Analysis { get; set; } = "";
    }

    /// <summary>
    /// نتیجه یک الگوریتم
    /// </summary>
    public class AlgorithmResult
    {
        public SchedulingAlgorithm Algorithm { get; set; }
        public string Strategy { get; set; } = "";
        public string StrategyUsed { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan Phase1ExecutionTime { get; set; }
        public TimeSpan Phase2ExecutionTime { get; set; }
        public TimeSpan ParallelExecutionTime { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; } = "";
        public double FinalScore { get; set; }
        public int TotalIterations { get; set; }
        public string AlgorithmStatus { get; set; } = "";
        public int Violations { get; set; }
        public Dictionary<string, object> Statistics { get; set; } = new Dictionary<string, object>();
        public List<string> Errors { get; set; } = new List<string>();
    }
}
