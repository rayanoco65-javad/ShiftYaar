using ShiftYar.Application.Features.ShiftModel.OrTools.Models;
using ShiftYar.Application.Features.ShiftModel.OrTools;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftModel.Hybrid
{
    /// <summary>
    /// پیاده‌سازی الگوریتم ترکیبی که OR-Tools CP-SAT و Simulated Annealing را ترکیب می‌کند
    /// </summary>
    public class HybridScheduler // زمان‌بند ترکیبی (OR-Tools + SA)
    {
        private readonly ShiftConstraints _saConstraints; // محدودیت‌های SA
        private readonly OrToolsConstraints _ortoolsConstraints; // محدودیت‌های OR-Tools
        private readonly SimulatedAnnealingParameters _saParameters; // پارامترهای SA
        private readonly OrToolsParameters _ortoolsParameters; // پارامترهای OR-Tools
        private readonly HybridParameters _hybridParameters; // پارامترهای Hybrid
        private readonly HybridStatistics _statistics; // آمار اجرای Hybrid

        public HybridScheduler(
            ShiftConstraints saConstraints,
            OrToolsConstraints ortoolsConstraints,
            SimulatedAnnealingParameters saParameters,
            OrToolsParameters ortoolsParameters,
            HybridParameters hybridParameters) // سازنده با ورودی قیود و پارامترها
        {
            _saConstraints = saConstraints;
            _ortoolsConstraints = ortoolsConstraints;
            _saParameters = saParameters;
            _ortoolsParameters = ortoolsParameters;
            _hybridParameters = hybridParameters;
            _statistics = new HybridStatistics();
        }

        /// <summary>
        /// اجرای الگوریتم ترکیبی
        /// </summary>
        public HybridSolution Optimize() // اجرای الگوریتم ترکیبی مطابق استراتژی
        {
            var stopwatch = Stopwatch.StartNew();
            var solution = new HybridSolution();

            try
            {
                switch (_hybridParameters.Strategy)
                {
                    case HybridStrategy.OrToolsFirst:
                        solution = OptimizeOrToolsFirst();
                        break;
                    case HybridStrategy.SimulatedAnnealingFirst:
                        solution = OptimizeSimulatedAnnealingFirst();
                        break;
                    case HybridStrategy.Parallel:
                        solution = OptimizeParallel();
                        break;
                    case HybridStrategy.Iterative:
                        solution = OptimizeIterative();
                        break;
                    case HybridStrategy.Adaptive:
                        solution = OptimizeAdaptive();
                        break;
                    default:
                        solution = OptimizeOrToolsFirst();
                        break;
                }

                stopwatch.Stop();
                solution.TotalExecutionTime = stopwatch.Elapsed;
                _statistics.TotalExecutionTime = stopwatch.Elapsed;

                return solution;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.Errors.Add($"Hybrid optimization error: {ex.Message}");

                return new HybridSolution
                {
                    TotalExecutionTime = stopwatch.Elapsed,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// استراتژی: ابتدا OR-Tools، سپس بهبود با Simulated Annealing
        /// </summary>
        private HybridSolution OptimizeOrToolsFirst() // ابتدا OR-Tools سپس بهبود SA
        {
            var solution = new HybridSolution();
            var phase1Stopwatch = Stopwatch.StartNew();

            // فاز 1: حل با OR-Tools
            _statistics.Phase1StartTime = DateTime.Now;
            var ortoolsScheduler = new OrToolsCPSatScheduler(_ortoolsConstraints, _ortoolsParameters);
            var ortoolsSolution = ortoolsScheduler.Optimize();
            phase1Stopwatch.Stop();

            solution.OrToolsSolution = ortoolsSolution;
            solution.Phase1ExecutionTime = phase1Stopwatch.Elapsed;
            _statistics.Phase1ExecutionTime = phase1Stopwatch.Elapsed;
            _statistics.Phase1Status = ortoolsSolution.Status.ToString();

            if (ortoolsSolution.Status == OrToolsSolverStatus.Optimal || ortoolsSolution.Status == OrToolsSolverStatus.Feasible)
            {
                // فاز 2: بهبود با Simulated Annealing
                var phase2Stopwatch = Stopwatch.StartNew();
                _statistics.Phase2StartTime = DateTime.Now;

                // تبدیل راه‌حل OR-Tools به فرمت Simulated Annealing
                var initialSolution = ConvertOrToolsToSimulatedAnnealing(ortoolsSolution);

                // تنظیم پارامترهای SA برای بهبود محلی
                var improvedSaParameters = CreateImprovedSAParameters();
                var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, improvedSaParameters);

                // استفاده از راه‌حل OR-Tools به عنوان نقطه شروع
                var improvedSolution = saScheduler.OptimizeWithInitialSolution(initialSolution);

                phase2Stopwatch.Stop();

                solution.SimulatedAnnealingSolution = improvedSolution;
                solution.Phase2ExecutionTime = phase2Stopwatch.Elapsed;
                _statistics.Phase2ExecutionTime = phase2Stopwatch.Elapsed;

                // انتخاب بهترین راه‌حل
                solution.FinalSolution = SelectBestSolution(ortoolsSolution, improvedSolution);
            }
            else
            {
                // اگر OR-Tools موفق نبود، فقط از Simulated Annealing استفاده کن
                var fallbackStopwatch = Stopwatch.StartNew();
                var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, _saParameters);
                var fallbackSolution = saScheduler.Optimize();
                fallbackStopwatch.Stop();

                solution.SimulatedAnnealingSolution = fallbackSolution;
                solution.FallbackExecutionTime = fallbackStopwatch.Elapsed;
                solution.FinalSolution = fallbackSolution;
                _statistics.FallbackUsed = true;
            }

            return solution;
        }

        /// <summary>
        /// استراتژی: ابتدا Simulated Annealing، سپس بهبود با OR-Tools
        /// </summary>
        private HybridSolution OptimizeSimulatedAnnealingFirst() // ابتدا SA سپس بهبود OR-Tools
        {
            var solution = new HybridSolution();
            var phase1Stopwatch = Stopwatch.StartNew();

            // فاز 1: حل با Simulated Annealing
            _statistics.Phase1StartTime = DateTime.Now;
            var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, _saParameters);
            var saSolution = saScheduler.Optimize();
            phase1Stopwatch.Stop();

            solution.SimulatedAnnealingSolution = saSolution;
            solution.Phase1ExecutionTime = phase1Stopwatch.Elapsed;
            _statistics.Phase1ExecutionTime = phase1Stopwatch.Elapsed;

            // فاز 2: بهبود با OR-Tools (اگر راه‌حل معتبر باشد)
            if (IsValidSolution(saSolution))
            {
                var phase2Stopwatch = Stopwatch.StartNew();
                _statistics.Phase2StartTime = DateTime.Now;

                // تنظیم محدودیت‌های OR-Tools بر اساس راه‌حل SA
                var refinedConstraints = RefineOrToolsConstraints(saSolution);

                // حل مجدد با OR-Tools
                var ortoolsScheduler = new OrToolsCPSatScheduler(refinedConstraints, _ortoolsParameters);
                var ortoolsSolution = ortoolsScheduler.Optimize();

                phase2Stopwatch.Stop();

                solution.OrToolsSolution = ortoolsSolution;
                solution.Phase2ExecutionTime = phase2Stopwatch.Elapsed;
                _statistics.Phase2ExecutionTime = phase2Stopwatch.Elapsed;
                _statistics.Phase2Status = ortoolsSolution.Status.ToString();

                // انتخاب بهترین راه‌حل
                solution.FinalSolution = SelectBestSolution(saSolution, ortoolsSolution);
            }
            else
            {
                solution.FinalSolution = saSolution;
            }

            return solution;
        }

        /// <summary>
        /// استراتژی: اجرای موازی هر دو الگوریتم
        /// </summary>
        private HybridSolution OptimizeParallel() // اجرای موازی هر دو الگوریتم
        {
            var solution = new HybridSolution();
            var parallelStopwatch = Stopwatch.StartNew();

            _statistics.Phase1StartTime = DateTime.Now;

            // اجرای موازی
            var saTask = Task.Run(() =>
            {
                var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, _saParameters);
                return saScheduler.Optimize();
            });

            var ortoolsTask = Task.Run(() =>
            {
                var ortoolsScheduler = new OrToolsCPSatScheduler(_ortoolsConstraints, _ortoolsParameters);
                return ortoolsScheduler.Optimize();
            });

            // انتظار برای تکمیل هر دو
            Task.WaitAll(saTask, ortoolsTask);

            parallelStopwatch.Stop();

            solution.SimulatedAnnealingSolution = saTask.Result;
            solution.OrToolsSolution = ortoolsTask.Result;
            solution.ParallelExecutionTime = parallelStopwatch.Elapsed;
            _statistics.ParallelExecutionTime = parallelStopwatch.Elapsed;
            _statistics.Phase1Status = "Parallel";

            // انتخاب بهترین راه‌حل
            solution.FinalSolution = SelectBestSolution(saTask.Result, ortoolsTask.Result);

            return solution;
        }

        /// <summary>
        /// استراتژی: بهبود تکراری
        /// </summary>
        private HybridSolution OptimizeIterative() // بهبود تکراری بین دو الگوریتم
        {
            var solution = new HybridSolution();
            var totalStopwatch = Stopwatch.StartNew();

            // شروع با OR-Tools
            var ortoolsScheduler = new OrToolsCPSatScheduler(_ortoolsConstraints, _ortoolsParameters);
            var currentSolution = ortoolsScheduler.Optimize();
            solution.OrToolsSolution = currentSolution;

            var bestScore = currentSolution.CalculateScore();
            var iterations = 0;
            var maxIterations = _hybridParameters.MaxIterations;

            while (iterations < maxIterations)
            {
                iterations++;
                _statistics.Iterations.Add(iterations);

                // بهبود با Simulated Annealing
                var initialSolution = ConvertOrToolsToSimulatedAnnealing(currentSolution);
                var improvedSaParameters = CreateImprovedSAParameters();
                var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, improvedSaParameters);
                var improvedSolution = saScheduler.OptimizeWithInitialSolution(initialSolution);

                var improvedScore = improvedSolution.Score;

                if (improvedScore < bestScore)
                {
                    bestScore = improvedScore;
                    currentSolution = ConvertSimulatedAnnealingToOrTools(improvedSolution);
                    _statistics.Improvements.Add(iterations);
                }
                else
                {
                    // اگر بهبودی نداشت، متوقف شو
                    break;
                }
            }

            totalStopwatch.Stop();
            solution.IterativeExecutionTime = totalStopwatch.Elapsed;
            solution.FinalSolution = currentSolution;
            _statistics.TotalIterations = iterations;

            return solution;
        }

        /// <summary>
        /// استراتژی: تطبیقی (انتخاب الگوریتم بر اساس پیچیدگی مسئله)
        /// </summary>
        private HybridSolution OptimizeAdaptive() // انتخاب استراتژی بر اساس پیچیدگی
        {
            var solution = new HybridSolution();
            var complexity = CalculateProblemComplexity();

            if (complexity < _hybridParameters.ComplexityThreshold)
            {
                // مسئله ساده: فقط از OR-Tools استفاده کن
                var ortoolsScheduler = new OrToolsCPSatScheduler(_ortoolsConstraints, _ortoolsParameters);
                var ortoolsSolution = ortoolsScheduler.Optimize();

                solution.OrToolsSolution = ortoolsSolution;
                solution.FinalSolution = ortoolsSolution;
                solution.StrategyUsed = "OR-Tools Only";
            }
            else if (complexity > _hybridParameters.ComplexityThreshold * 2)
            {
                // مسئله پیچیده: فقط از Simulated Annealing استفاده کن
                var saScheduler = new SimulatedAnnealingScheduler(_saConstraints, _saParameters);
                var saSolution = saScheduler.Optimize();

                solution.SimulatedAnnealingSolution = saSolution;
                solution.FinalSolution = saSolution;
                solution.StrategyUsed = "Simulated Annealing Only";
            }
            else
            {
                // مسئله متوسط: از ترکیب استفاده کن
                solution = OptimizeOrToolsFirst();
                solution.StrategyUsed = "Hybrid";
            }

            _statistics.ProblemComplexity = complexity;
            _statistics.StrategyUsed = solution.StrategyUsed;

            return solution;
        }

        /// <summary>
        /// تبدیل راه‌حل OR-Tools به فرمت Simulated Annealing
        /// </summary>
        private ShiftSolution ConvertOrToolsToSimulatedAnnealing(OrToolsShiftSolution ortoolsSolution) // نگاشت نتیجه OR-Tools به SA
        {
            var saSolution = new ShiftSolution();

            foreach (var assignment in ortoolsSolution.Assignments.Values)
            {
                saSolution.AddAssignment(
                    assignment.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall
                );
            }

            saSolution.Score = ortoolsSolution.CalculateScore();
            return saSolution;
        }

        /// <summary>
        /// تبدیل راه‌حل Simulated Annealing به فرمت OR-Tools
        /// </summary>
        private OrToolsShiftSolution ConvertSimulatedAnnealingToOrTools(ShiftSolution saSolution) // نگاشت نتیجه SA به OR-Tools
        {
            var ortoolsSolution = new OrToolsShiftSolution();

            foreach (var assignment in saSolution.Assignments.Values)
            {
                ortoolsSolution.AddAssignment(
                    assignment.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall
                );
            }

            ortoolsSolution.ObjectiveValue = saSolution.Score;
            ortoolsSolution.Status = OrToolsSolverStatus.Feasible;
            return ortoolsSolution;
        }

        /// <summary>
        /// انتخاب بهترین راه‌حل از بین دو راه‌حل
        /// </summary>
        private object SelectBestSolution(object solution1, object solution2) // انتخاب راه‌حل بهتر براساس امتیاز
        {
            double score1 = 0, score2 = 0;

            if (solution1 is OrToolsShiftSolution ortools1)
            {
                score1 = ortools1.CalculateScore();
            }
            else if (solution1 is ShiftSolution sa1)
            {
                score1 = sa1.Score;
            }

            if (solution2 is OrToolsShiftSolution ortools2)
            {
                score2 = ortools2.CalculateScore();
            }
            else if (solution2 is ShiftSolution sa2)
            {
                score2 = sa2.Score;
            }

            return score1 <= score2 ? solution1 : solution2;
        }

        /// <summary>
        /// بررسی معتبر بودن راه‌حل
        /// </summary>
        private bool IsValidSolution(ShiftSolution solution) // بررسی اعتبار راه‌حل SA
        {
            return solution != null && solution.Score < double.MaxValue && solution.Violations.Count == 0;
        }

        /// <summary>
        /// تنظیم محدودیت‌های OR-Tools بر اساس راه‌حل SA
        /// </summary>
        private OrToolsConstraints RefineOrToolsConstraints(ShiftSolution saSolution) // تنظیم قیود OR-Tools با توجه به SA
        {
            // کپی محدودیت‌های اصلی
            var refinedConstraints = new OrToolsConstraints
            {
                DepartmentId = _ortoolsConstraints.DepartmentId,
                StartDate = _ortoolsConstraints.StartDate,
                EndDate = _ortoolsConstraints.EndDate,
                UserConstraints = new List<OrToolsUserConstraint>(_ortoolsConstraints.UserConstraints),
                ShiftRequirements = new List<OrToolsShiftRequirement>(_ortoolsConstraints.ShiftRequirements),
                GlobalConstraints = _ortoolsConstraints.GlobalConstraints,
                HardRules = _ortoolsConstraints.HardRules,
                SoftWeights = _ortoolsConstraints.SoftWeights
            };

            // تنظیم محدودیت‌ها بر اساس راه‌حل SA
            // اینجا می‌توانید منطق پیچیده‌تری برای تنظیم محدودیت‌ها اضافه کنید

            return refinedConstraints;
        }

        /// <summary>
        /// ایجاد پارامترهای بهبود یافته برای Simulated Annealing
        /// </summary>
        private SimulatedAnnealingParameters CreateImprovedSAParameters() // پارامترهای سبک‌تر برای بهبود محلی
        {
            return new SimulatedAnnealingParameters
            {
                InitialTemperature = _saParameters.InitialTemperature * 0.5, // دما کمتر
                FinalTemperature = _saParameters.FinalTemperature,
                CoolingRate = _saParameters.CoolingRate,
                MaxIterations = _saParameters.MaxIterations / 2, // تکرار کمتر
                MaxIterationsWithoutImprovement = _saParameters.MaxIterationsWithoutImprovement / 2,
                PenaltyWeight = _saParameters.PenaltyWeight
            };
        }

        /// <summary>
        /// محاسبه پیچیدگی مسئله
        /// </summary>
        private double CalculateProblemComplexity() // محاسبه پیچیدگی تقریبی مسأله
        {
            var numUsers = _ortoolsConstraints.NumUsers;
            var numShifts = _ortoolsConstraints.NumShifts;
            var numDays = _ortoolsConstraints.NumDays;
            var numConstraints = _ortoolsConstraints.UserConstraints.Sum(u => u.UnavailableDateIndices.Count);

            // فرمول ساده برای محاسبه پیچیدگی
            return (numUsers * numShifts * numDays * numConstraints) / 1000.0;
        }

        /// <summary>
        /// دریافت آمارهای الگوریتم ترکیبی
        /// </summary>
        public HybridStatistics GetStatistics() // دریافت آمار اجرای Hybrid
        {
            return _statistics;
        }
    }

    /// <summary>
    /// راه‌حل ترکیبی
    /// </summary>
    public class HybridSolution // نتیجه اجرای الگوریتم ترکیبی
    {
        public OrToolsShiftSolution OrToolsSolution { get; set; } // نتیجه OR-Tools (در صورت وجود)
        public ShiftSolution SimulatedAnnealingSolution { get; set; } // نتیجه SA (در صورت وجود)
        public object FinalSolution { get; set; } // راه‌حل نهایی انتخاب‌شده
        public string StrategyUsed { get; set; } = "Unknown"; // استراتژی استفاده‌شده

        public TimeSpan TotalExecutionTime { get; set; } // زمان کل اجرا
        public TimeSpan Phase1ExecutionTime { get; set; } // زمان فاز اول
        public TimeSpan Phase2ExecutionTime { get; set; } // زمان فاز دوم
        public TimeSpan ParallelExecutionTime { get; set; } // زمان اجرای موازی
        public TimeSpan IterativeExecutionTime { get; set; } // زمان اجرای تکراری
        public TimeSpan FallbackExecutionTime { get; set; } // زمان مسیر جایگزین

        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// پارامترهای الگوریتم ترکیبی
    /// </summary>
    public class HybridParameters // پارامترهای کنترل الگوریتم ترکیبی
    {
        public HybridStrategy Strategy { get; set; } = HybridStrategy.OrToolsFirst; // استراتژی ترکیب
        public int MaxIterations { get; set; } = 5; // سقف تکرار در حالت Iterative
        public double ComplexityThreshold { get; set; } = 100.0; // آستانه پیچیدگی برای Adaptive
        public bool EnableFallback { get; set; } = true; // فعال‌سازی مسیر جایگزین
        public bool EnableParallelExecution { get; set; } = true; // فعال‌سازی اجرای موازی
        public int MaxParallelTimeInSeconds { get; set; } = 300; // سقف زمان در حالت موازی
    }

    /// <summary>
    /// استراتژی‌های الگوریتم ترکیبی
    /// </summary>
    public enum HybridStrategy // حالت‌های مختلف اجرای Hybrid
    {
        OrToolsFirst,           // ابتدا OR-Tools، سپس SA
        SimulatedAnnealingFirst, // ابتدا SA، سپس OR-Tools
        Parallel,               // اجرای موازی
        Iterative,              // بهبود تکراری
        Adaptive                // تطبیقی
    }

    /// <summary>
    /// آمارهای الگوریتم ترکیبی
    /// </summary>
    public class HybridStatistics // آمار اجرای Hybrid
    {
        public DateTime Phase1StartTime { get; set; } // زمان شروع فاز اول
        public DateTime Phase2StartTime { get; set; } // زمان شروع فاز دوم
        public TimeSpan Phase1ExecutionTime { get; set; } // مدت فاز اول
        public TimeSpan Phase2ExecutionTime { get; set; } // مدت فاز دوم
        public TimeSpan ParallelExecutionTime { get; set; } // مدت اجرای موازی
        public TimeSpan TotalExecutionTime { get; set; } // مدت کل
        public string Phase1Status { get; set; } = ""; // وضعیت فاز اول
        public string Phase2Status { get; set; } = ""; // وضعیت فاز دوم
        public bool FallbackUsed { get; set; } // استفاده از مسیر جایگزین
        public int TotalIterations { get; set; } // تعداد تکرارها
        public List<int> Iterations { get; set; } = new List<int>(); // تاریخچه تکرارها
        public List<int> Improvements { get; set; } = new List<int>(); // گام‌های بهبود
        public double ProblemComplexity { get; set; } // پیچیدگی محاسبه‌شده
        public string StrategyUsed { get; set; } = ""; // استراتژی ثبت‌شده
        public List<string> Errors { get; set; } = new List<string>(); // خطاهای رخ‌داده
    }
}
