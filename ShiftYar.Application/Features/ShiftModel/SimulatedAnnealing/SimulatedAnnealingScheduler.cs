using ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.SimulatedAnnealing
{
    /// <summary>
    /// پیاده‌سازی الگوریتم Simulated Annealing برای بهینه‌سازی شیفت‌بندی
    /// </summary>
    public class SimulatedAnnealingScheduler
    {
        private readonly Random _random;
        private readonly ShiftConstraints _constraints;
        private readonly SimulatedAnnealingParameters _parameters;
        private readonly AlgorithmStatistics _statistics;
        private readonly Dictionary<int, double> _shiftDurationLookup;

        public SimulatedAnnealingScheduler(ShiftConstraints constraints, SimulatedAnnealingParameters parameters)
        {
            _constraints = constraints;
            _parameters = parameters;
            _random = new Random();
            _statistics = new AlgorithmStatistics();
            _shiftDurationLookup = constraints.ShiftRequirements
                .GroupBy(s => s.ShiftId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().DurationHours > 0 ? g.First().DurationHours : 8);
        }

        /// <summary>
        /// اجرای الگوریتم Simulated Annealing
        /// </summary>
        public ShiftSolution Optimize()
        {
            var stopwatch = Stopwatch.StartNew();

            var currentSolution = GenerateFeasibleInitialSolution();
            var bestSolution = currentSolution.Clone();

            _statistics.BestScore = currentSolution.Score;
            _statistics.CurrentScore = currentSolution.Score;

            RunAnnealingLoop(ref currentSolution, ref bestSolution);

            // تضمین نهایی رعایت درخواست‌های تأییدشده و الزام مدیر شیفت
            // (حتی اگر حلقه SA نتوانسته باشد راه‌حل کاملاً معتبر پیدا کند)
            EnforceHardRequestConstraints(bestSolution);

            stopwatch.Stop();
            _statistics.ExecutionTime = stopwatch.Elapsed;

            return bestSolution;
        }

        /// <summary>
        /// اجرای الگوریتم Simulated Annealing با راه‌حل اولیه مشخص
        /// </summary>
        public ShiftSolution OptimizeWithInitialSolution(ShiftSolution initialSolution)
        {
            var stopwatch = Stopwatch.StartNew();

            var currentSolution = initialSolution.Clone();
            EnforceHardRequestConstraints(currentSolution);
            currentSolution.Score = CalculateSolutionScore(currentSolution);
            var bestSolution = currentSolution.Clone();

            _statistics.BestScore = currentSolution.Score;
            _statistics.CurrentScore = currentSolution.Score;

            RunAnnealingLoop(ref currentSolution, ref bestSolution);

            EnforceHardRequestConstraints(bestSolution);

            stopwatch.Stop();
            _statistics.ExecutionTime = stopwatch.Elapsed;

            return bestSolution;
        }

        private void RunAnnealingLoop(ref ShiftSolution currentSolution, ref ShiftSolution bestSolution)
        {
            double temperature = _parameters.InitialTemperature;
            int iterationsWithoutImprovement = 0;

            for (int iteration = 0; iteration < _parameters.MaxIterations; iteration++)
            {
                _statistics.TotalIterations = iteration + 1;
                _statistics.CurrentTemperature = temperature;

                var neighborSolution = GenerateNeighbor(currentSolution);
                if (!IsFeasible(neighborSolution))
                {
                    _statistics.RejectedMoves++;
                    iterationsWithoutImprovement++;
                    temperature *= _parameters.CoolingRate;
                    _statistics.CurrentScore = currentSolution.Score;
                    _statistics.ScoreHistory.Add(currentSolution.Score);
                    _statistics.TemperatureHistory.Add(temperature);
                    if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement ||
                        temperature <= _parameters.FinalTemperature)
                    {
                        break;
                    }
                    continue;
                }

                double deltaScore = neighborSolution.Score - currentSolution.Score;
                bool acceptMove = deltaScore < 0 || _random.NextDouble() < Math.Exp(-deltaScore / temperature);

                if (acceptMove)
                {
                    currentSolution = neighborSolution;
                    _statistics.AcceptedMoves++;

                    if (currentSolution.Score < bestSolution.Score)
                    {
                        bestSolution = currentSolution.Clone();
                        _statistics.BestScore = bestSolution.Score;
                        iterationsWithoutImprovement = 0;
                    }
                    else
                    {
                        iterationsWithoutImprovement++;
                    }
                }
                else
                {
                    _statistics.RejectedMoves++;
                    iterationsWithoutImprovement++;
                }

                _statistics.CurrentScore = currentSolution.Score;
                _statistics.ScoreHistory.Add(currentSolution.Score);
                _statistics.TemperatureHistory.Add(temperature);

                temperature *= _parameters.CoolingRate;

                if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement ||
                    temperature <= _parameters.FinalTemperature)
                {
                    break;
                }
            }
        }

        private ShiftSolution GenerateFeasibleInitialSolution()
        {
            const int maxAttempts = 8;
            ShiftSolution best = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = GenerateInitialSolution();
                if (IsFeasible(candidate))
                {
                    return candidate;
                }

                if (best == null || candidate.Score < best.Score)
                {
                    best = candidate;
                }
            }

            return best ?? GenerateInitialSolution();
        }


        /// <summary>
        /// تولید راه‌حل اولیه
        /// </summary>
        private ShiftSolution GenerateInitialSolution()
        {
            var solution = new ShiftSolution();

            // ابتدا انتساب‌های اجباری (درخواست‌های تأییدشده حضور) اعمال می‌شوند
            // تا پر کردن ظرفیت باقی‌مانده با آگاهی از آنها انجام شود.
            ApplyHardRequiredAssignments(solution);

            // تولید انتساب‌های تصادفی اولیه
            var availableUsers = _constraints.UserConstraints.ToList();
            var dateRange = GetDateRange();

            foreach (var date in dateRange)
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var eligibleUsers = availableUsers
                            .Where(u => u.SpecialtyId == specialtyReq.SpecialtyId)
                            .Where(u => IsUserAvailableForShift(u, date, shiftReq.ShiftLabel))
                            .Where(u => u.IsActive) // فقط کاربران فعال
                            .ToList();

                        // انتساب نیروهای مورد نیاز
                        AssignRequiredPersonnel(solution, eligibleUsers, shiftReq, date, specialtyReq);
                    }
                }
            }

            // محاسبه امتیاز راه‌حل
            solution.Score = CalculateSolutionScore(solution);

            return solution;
        }

        /// <summary>
        /// تولید راه‌حل همسایه
        /// </summary>
        private ShiftSolution GenerateNeighbor(ShiftSolution currentSolution)
        {
            var neighbor = currentSolution.Clone();

            // حرکت‌های هدفمند بیمارستانی: جابجایی و انتساب مجدد پرتکرارتر از افزودن/حذف تصادفی
            var roll = _random.NextDouble();
            if (roll < 0.35)
            {
                PerformReassignMove(neighbor);
            }
            else if (roll < 0.65)
            {
                PerformSwapMove(neighbor);
            }
            else if (roll < 0.85)
            {
                PerformAddMove(neighbor);
            }
            else
            {
                PerformRemoveMove(neighbor);
            }

            neighbor.Score = CalculateSolutionScore(neighbor);
            return neighbor;
        }

        /// <summary>
        /// محاسبه امتیاز راه‌حل
        /// </summary>
        private double CalculateSolutionScore(ShiftSolution solution)
        {
            double score = 0;
            var violations = new List<string>();

            // امتیاز پایه
            score += CalculateBaseScore(solution);

            // جریمه برای نقض محدودیت‌ها
            score += CalculateConstraintViolations(solution, violations);

            // جریمه برای عدم تعادل جنسیتی
            score += CalculateGenderBalancePenalty(solution);

            // جریمه برای عدم تطابق تخصص
            score += CalculateSpecialtyMismatchPenalty(solution);

            // جریمه برای ترجیحات کاربران
            score += CalculateUserPreferencePenalty(solution);

            // جریمه‌های عدالت و چرخش
            score += CalculateFairShiftCountBalancePenalty(solution) * _constraints.SoftWeights.FairShiftCountBalanceWeight;
            score += CalculateExtraShiftRotationPenalty(solution) * _constraints.SoftWeights.ExtraShiftRotationWeight;
            score += CalculateShiftLabelBalancePenalty(solution) * _constraints.SoftWeights.ShiftLabelBalanceWeight;

            solution.Violations = violations;

            return score;
        }

        /// <summary>
        /// جریمهٔ جای خالی بودن شیفت (کم‌کاری از امتیاز بدتر است)
        /// </summary>
        private double CalculateBaseScore(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var regularCount = CountSpecialtyAssignments(
                            solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall: false);
                        var onCallCount = CountSpecialtyAssignments(
                            solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall: true);

                        if (specialtyReq.RequiredTotalCount > 0 && regularCount < specialtyReq.RequiredTotalCount)
                        {
                            penalty += (specialtyReq.RequiredTotalCount - regularCount) * 120;
                        }

                        if (specialtyReq.OnCallTotalCount > 0 && onCallCount < specialtyReq.OnCallTotalCount)
                        {
                            penalty += (specialtyReq.OnCallTotalCount - onCallCount) * 100;
                        }
                    }
                }
            }

            return penalty;
        }

        private double CalculateFairShiftCountBalancePenalty(ShiftSolution solution)
        {
            // اختلاف تعداد شیفت‌های این ماه نسبت به میانگین دپارتمان
            var counts = _constraints.UserConstraints.Select(u => (UserId: u.UserId, Count: solution.GetUserAllAssignments(u.UserId).Count)).ToList();
            if (counts.Count == 0) return 0;
            double avg = counts.Average(c => c.Count);
            double sumAbs = counts.Sum(c => Math.Abs(c.Count - avg));
            return sumAbs; // وزن بیرونی اعمال می‌شود
        }

        private double CalculateExtraShiftRotationPenalty(ShiftSolution solution)
        {
            // اگر کاربری در سابقه اخیر شیفت اضافه بیشتری داشته، دادن شیفت اضافه به او جریمه شود
            // تعریف ساده: "شیفت اضافه" = بالاتر از میانگین همین ماه
            var counts = _constraints.UserConstraints.Select(u => (User: u, Count: solution.GetUserAllAssignments(u.UserId).Count)).ToList();
            if (counts.Count == 0) return 0;
            double avg = counts.Average(c => c.Count);

            double penalty = 0;
            foreach (var (user, count) in counts)
            {
                bool isExtraThisMonth = count > avg + 0.5; // آستانه‌ی ساده
                if (!isExtraThisMonth) continue;

                // اگر در گذشته هم زیاد گرفته است، جریمه بیشتر
                int recent = user.RecentTotalShifts;
                penalty += Math.Max(0, recent - (int)avg);
            }
            return penalty;
        }

        private double CalculateShiftLabelBalancePenalty(ShiftSolution solution)
        {
            // برای کاربران گردشی، اختلاف توزیع Morning/Evening/Night از توزیع عادلانه جریمه شود
            double penalty = 0;
            foreach (var u in _constraints.UserConstraints)
            {
                if (u.ShiftType != ShiftTypes.RotatingShift) continue;
                var ua = solution.GetUserAllAssignments(u.UserId);
                if (ua.Count == 0) continue;
                double fair = ua.Count / 3.0;
                int m = ua.Count(x => x.ShiftLabel == ShiftLabel.Morning);
                int e = ua.Count(x => x.ShiftLabel == ShiftLabel.Evening);
                int n = ua.Count(x => x.ShiftLabel == ShiftLabel.Night);
                penalty += Math.Abs(m - fair) + Math.Abs(e - fair) + Math.Abs(n - fair);
            }
            return penalty;
        }

        /// <summary>
        /// محاسبه جریمه نقض محدودیت‌ها
        /// </summary>
		private double CalculateConstraintViolations(ShiftSolution solution, List<string> violations)
        {
            double penalty = 0;

            foreach (var userConstraint in _constraints.UserConstraints)
            {
                var userAssignments = solution.GetUserAllAssignments(userConstraint.UserId);

                // قوانین سخت در IsFeasible بررسی می‌شوند؛ اینجا فقط جریمهٔ نرم برای قوانین غیرفعال‌شده
                if (!_constraints.HardRules.EnforceMaxConsecutiveShifts)
                {
                    penalty += CheckConsecutiveShifts(userAssignments, userConstraint.MaxConsecutiveShifts, violations);
                }

                if (!_constraints.HardRules.EnforceMinRestDays)
                {
                    penalty += CheckRestDays(userAssignments, userConstraint.MinRestDaysBetweenShifts, violations);
                }

                if (!_constraints.HardRules.EnforceWeeklyMaxShifts)
                {
                    penalty += CheckWeeklyShifts(userAssignments, userConstraint.MaxShiftsPerWeek, violations) *
                              _constraints.SoftWeights.WeeklyMaxWeight;
                }

                if (!_constraints.HardRules.EnforceNightShiftMonthlyCap)
                {
                    penalty += CheckMonthlyNightShifts(userAssignments, userConstraint.MaxNightShiftsPerMonth, violations) *
                              _constraints.SoftWeights.MonthlyNightCapWeight;
                }

                if (!_constraints.HardRules.EnforceProductivityHours && userConstraint.ProductivityRequiredHours.HasValue)
                {
                    penalty += CheckMonthlyWorkingHours(userConstraint, userAssignments, violations) *
                              _constraints.SoftWeights.ProductivityOvertimeWeight;
                }
            }

            return penalty * _parameters.PenaltyWeight;
        }

        /// <summary>
        /// محاسبه جریمه عدم تعادل جنسیتی
        /// </summary>
        private double CalculateGenderBalancePenalty(ShiftSolution solution)
        {
            if (!_constraints.GlobalConstraints.RequireGenderBalance)
            {
                return 0;
            }

            // تعادل جنسیتی فقط وقتی برای شیفت تعداد مرد/زن به‌صورت صریح تعیین شده باشد
            // در سطح تخصص اعمال می‌شود (CalculateSpecialtyMismatchPenalty / IsFeasible).
            // اگر فقط مجموع نفرات تعیین شده باشد، هر ترکیبی مجاز است و جریمه سراسری اعمال نمی‌شود.
            return 0;
        }


        /// <summary>
        /// محاسبه جریمه عدم تطابق تخصص
        /// </summary>
		private double CalculateSpecialtyMismatchPenalty(ShiftSolution solution)
        {
            if (!_constraints.GlobalConstraints.PreferSpecialtyMatch)
                return 0;

            double penalty = 0;
            var dateRange = GetDateRange();

            foreach (var date in dateRange)
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    var assignments = solution.GetShiftAssignments(shiftReq.ShiftId, date);

                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var regularForSpec = assignments
                            .Where(a => GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId && !a.IsOnCall)
                            .ToList();
                        var onCallForSpec = assignments
                            .Where(a => GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId && a.IsOnCall)
                            .ToList();

                        var hasExplicitRegularGender =
                            specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0;
                        var hasExplicitOnCallGender =
                            specialtyReq.OnCallMaleCount > 0 || specialtyReq.OnCallFemaleCount > 0;

                        if (hasExplicitRegularGender)
                        {
                            if (specialtyReq.RequiredMaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(regularForSpec, UserGender.Male) - specialtyReq.RequiredMaleCount) * 80;
                            }

                            if (specialtyReq.RequiredFemaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(regularForSpec, UserGender.Female) - specialtyReq.RequiredFemaleCount) * 80;
                            }
                        }
                        else if (specialtyReq.RequiredTotalCount > 0 && regularForSpec.Count < specialtyReq.RequiredTotalCount)
                        {
                            penalty += (specialtyReq.RequiredTotalCount - regularForSpec.Count) * 50;
                        }

                        if (hasExplicitOnCallGender)
                        {
                            if (specialtyReq.OnCallMaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(onCallForSpec, UserGender.Male) - specialtyReq.OnCallMaleCount) * 80;
                            }

                            if (specialtyReq.OnCallFemaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(onCallForSpec, UserGender.Female) - specialtyReq.OnCallFemaleCount) * 80;
                            }
                        }
                        else if (specialtyReq.OnCallTotalCount > 0 && onCallForSpec.Count < specialtyReq.OnCallTotalCount)
                        {
                            penalty += (specialtyReq.OnCallTotalCount - onCallForSpec.Count) * 60;
                        }
                    }
                }
            }

            return penalty * _constraints.SoftWeights.SpecialtyPreferenceWeight;
        }


        /// <summary>
        /// محاسبه جریمه ترجیحات کاربران
        /// </summary>
		private double CalculateUserPreferencePenalty(ShiftSolution solution)
        {
            double penalty = 0;

            foreach (var assignment in solution.Assignments.Values)
            {
                var userConstraint = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                if (userConstraint == null) continue;

                // جریمه برای شیفت‌های ناخواسته
                if (userConstraint.UnwantedShifts.Contains(assignment.ShiftLabel))
                {
                    penalty += 20 * _constraints.SoftWeights.UserUnwantedShiftWeight;
                }

                // امتیاز منفی برای شیفت‌های ترجیحی
                if (userConstraint.PreferredShifts.Contains(assignment.ShiftLabel))
                {
                    penalty -= 5 * _constraints.SoftWeights.UserPreferredShiftWeight;
                }
            }

            return penalty;
        }


        /// <summary>
        /// بررسی رعایت قوانین قطعی (Hard) برای یک راه‌حل
        /// </summary>
        private bool IsFeasible(ShiftSolution solution)
        {
            // بررسی تاریخ‌های غیرقابل دسترس و شیفت‌های غیرمجاز
            foreach (var assignment in solution.Assignments.Values)
            {
                var userConstraint = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                if (userConstraint == null)
                {
                    continue;
                }

                if (userConstraint.UnavailableDates.Contains(assignment.Date.Date))
                {
                    return false;
                }

                if (userConstraint.UnavailableShiftSlots.Any(s =>
                        s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
                {
                    return false;
                }
            }

            // حضور قطعی در شیفت‌های درخواست‌شده
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                foreach (var required in userConstraint.RequiredShiftSlots)
                {
                    var shiftReq = GetShiftRequirement(required.ShiftLabel);
                    if (shiftReq == null)
                    {
                        continue;
                    }

                    var assignment = solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                        .FirstOrDefault(a => a.UserId == userConstraint.UserId && !a.IsOnCall);

                    if (assignment == null)
                    {
                        return false;
                    }
                }

                foreach (var presenceDate in userConstraint.RequiredPresenceDates)
                {
                    if (!solution.GetUserAssignments(userConstraint.UserId, presenceDate).Any())
                    {
                        return false;
                    }
                }
            }

            // الزام حضور مدیر شیفت در شیفت‌های عصر/شب (فقط برای نیروی حاضر، نه آنکال)
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    if (!RequiresShiftManager(shiftReq.ShiftLabel))
                    {
                        continue;
                    }

                    var regularAssignments = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Where(a => !a.IsOnCall)
                        .ToList();

                    if (regularAssignments.Count == 0)
                    {
                        continue;
                    }

                    var hasManager = regularAssignments.Any(a =>
                        _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.CanBeShiftManager == true);

                    if (!hasManager)
                    {
                        return false;
                    }
                }
            }

            // یک شیفت در روز و حداکثر شیفت روزانه
            if (_constraints.HardRules.ForbidDuplicateDailyAssignments || _constraints.HardRules.EnforceMaxShiftsPerDay)
            {
                var byUserDate = solution.Assignments.Values
                    .GroupBy(a => new { a.UserId, Date = a.Date.Date });
                foreach (var grp in byUserDate)
                {
                    if (_constraints.HardRules.ForbidDuplicateDailyAssignments && grp.Count() > 1)
                        return false;
                    if (_constraints.HardRules.EnforceMaxShiftsPerDay && grp.Count() > _constraints.GlobalConstraints.MaxShiftsPerDay)
                        return false;
                }
            }

            // حداقل استراحت و حداکثر متوالی
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                var userAssignments = solution.GetUserAllAssignments(userConstraint.UserId);
                if (_constraints.HardRules.EnforceMinRestDays)
                {
                    for (int i = 1; i < userAssignments.Count; i++)
                    {
                        var daysBetween = (userAssignments[i].Date - userAssignments[i - 1].Date).Days;
                        if (daysBetween < userConstraint.MinRestDaysBetweenShifts + 1)
                            return false;
                    }
                }
                if (_constraints.HardRules.EnforceMaxConsecutiveShifts)
                {
                    int consecutive = 1;
                    for (int i = 1; i < userAssignments.Count; i++)
                    {
                        if ((userAssignments[i].Date - userAssignments[i - 1].Date).Days == 1)
                        {
                            consecutive++;
                            if (consecutive > userConstraint.MaxConsecutiveShifts)
                                return false;
                        }
                        else
                        {
                            consecutive = 1;
                        }
                    }
                }

                if (_constraints.HardRules.EnforceProductivityHours && userConstraint.ProductivityRequiredHours.HasValue)
                {
                    var workedHours = CalculateUserWorkedHours(userAssignments);
                    if (workedHours > (double)userConstraint.ProductivityRequiredHours.Value + 0.25)
                    {
                        return false;
                    }
                }

                if (_constraints.HardRules.EnforceWeeklyMaxShifts)
                {
                    foreach (var week in userAssignments.GroupBy(a => GetWeekNumber(a.Date)))
                    {
                        if (week.Count() > userConstraint.MaxShiftsPerWeek)
                        {
                            return false;
                        }
                    }
                }

                if (_constraints.HardRules.EnforceNightShiftMonthlyCap)
                {
                    foreach (var month in userAssignments
                                 .Where(a => a.ShiftLabel == ShiftLabel.Night)
                                 .GroupBy(a => new { a.Date.Year, a.Date.Month }))
                    {
                        if (month.Count() > userConstraint.MaxNightShiftsPerMonth)
                        {
                            return false;
                        }
                    }
                }
            }

            // ظرفیت تخصص/شیفت/روز نباید بیش از نیاز باشد
            if (_constraints.HardRules.EnforceSpecialtyCapacity)
            {
                var dateRange = GetDateRange();
                foreach (var date in dateRange)
                {
                    foreach (var shiftReq in _constraints.ShiftRequirements)
                    {
                        var assignments = solution.GetShiftAssignments(shiftReq.ShiftId, date);
                        int totalRequired = shiftReq.SpecialtyRequirements.Sum(r => r.RequiredTotalCount + r.OnCallTotalCount);
                        if (assignments.Count > totalRequired)
                        {
                            return false;
                        }
                        foreach (var specReq in shiftReq.SpecialtyRequirements)
                        {
                            var specAssignments = assignments
                                .Where(a => GetUserSpecialty(a.UserId) == specReq.SpecialtyId)
                                .ToList();
                            var regular = specAssignments.Where(a => !a.IsOnCall).ToList();
                            var onCall = specAssignments.Where(a => a.IsOnCall).ToList();

                            if (specAssignments.Count > specReq.RequiredTotalCount + specReq.OnCallTotalCount)
                            {
                                return false;
                            }

                            if (regular.Count > specReq.RequiredTotalCount)
                            {
                                return false;
                            }

                            if (onCall.Count > specReq.OnCallTotalCount)
                            {
                                return false;
                            }

                            var hasExplicitRegularGender =
                                specReq.RequiredMaleCount > 0 || specReq.RequiredFemaleCount > 0;
                            var hasExplicitOnCallGender =
                                specReq.OnCallMaleCount > 0 || specReq.OnCallFemaleCount > 0;

                            if (hasExplicitRegularGender)
                            {
                                if (specReq.RequiredMaleCount > 0 &&
                                    CountGenderAssignments(regular, UserGender.Male) != specReq.RequiredMaleCount)
                                {
                                    return false;
                                }

                                if (specReq.RequiredFemaleCount > 0 &&
                                    CountGenderAssignments(regular, UserGender.Female) != specReq.RequiredFemaleCount)
                                {
                                    return false;
                                }
                            }

                            if (hasExplicitOnCallGender)
                            {
                                if (specReq.OnCallMaleCount > 0 &&
                                    CountGenderAssignments(onCall, UserGender.Male) != specReq.OnCallMaleCount)
                                {
                                    return false;
                                }

                                if (specReq.OnCallFemaleCount > 0 &&
                                    CountGenderAssignments(onCall, UserGender.Female) != specReq.OnCallFemaleCount)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }


        private ShiftSolution? RepairOrRegenerate(ShiftSolution solution)
        {
            // استراتژی ساده: اگر نامعتبر است، هیچ تعمیر پیچیده انجام نده و به فراخواننده اجازهٔ بازتولید بده
            return null;
        }

        #region Helper Methods

        private List<DateTime> GetDateRange()
        {
            var dates = new List<DateTime>();
            for (var date = _constraints.StartDate.Date; date <= _constraints.EndDate.Date; date = date.AddDays(1))
            {
                dates.Add(date);
            }
            return dates;
        }

        private void ApplyHardRequiredAssignments(ShiftSolution solution)
        {
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                foreach (var required in userConstraint.RequiredShiftSlots)
                {
                    var shiftReq = GetShiftRequirement(required.ShiftLabel);
                    if (shiftReq == null || !IsUserAvailableForShift(userConstraint, required.Date, required.ShiftLabel))
                    {
                        continue;
                    }

                    RemoveConflictingDailyAssignments(solution, userConstraint.UserId, required.Date, shiftReq.ShiftId);

                    if (!solution.HasAssignment(userConstraint.UserId, shiftReq.ShiftId, required.Date))
                    {
                        solution.AddAssignment(
                            userConstraint.UserId,
                            shiftReq.ShiftId,
                            required.Date,
                            required.ShiftLabel,
                            isOnCall: false);
                    }
                }

                foreach (var presenceDate in userConstraint.RequiredPresenceDates)
                {
                    if (solution.GetUserAssignments(userConstraint.UserId, presenceDate).Any())
                    {
                        continue;
                    }

                    foreach (var shiftReq in _constraints.ShiftRequirements)
                    {
                        if (!IsUserAvailableForShift(userConstraint, presenceDate, shiftReq.ShiftLabel))
                        {
                            continue;
                        }

                        var specialtyReq = shiftReq.SpecialtyRequirements
                            .FirstOrDefault(s => s.SpecialtyId == userConstraint.SpecialtyId);
                        if (specialtyReq == null || specialtyReq.RequiredTotalCount <= 0)
                        {
                            continue;
                        }

                        RemoveConflictingDailyAssignments(solution, userConstraint.UserId, presenceDate, shiftReq.ShiftId);
                        solution.AddAssignment(
                            userConstraint.UserId,
                            shiftReq.ShiftId,
                            presenceDate,
                            shiftReq.ShiftLabel,
                            isOnCall: false);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// ترمیم نهایی راه‌حل: تضمین رعایت درخواست‌های تأییدشده (حضور/عدم‌حضور)
        /// و الزام حضور مدیر شیفت، حتی اگر حلقه SA راه‌حل کاملاً معتبر پیدا نکرده باشد.
        /// </summary>
        private void EnforceHardRequestConstraints(ShiftSolution solution)
        {
            // 1) حذف انتساب‌های کاربران در تاریخ/شیفت‌های غیرمجاز (درخواست‌های عدم حضور تأییدشده)
            foreach (var assignment in solution.Assignments.Values.ToList())
            {
                var user = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                if (user == null)
                {
                    continue;
                }

                if (!IsUserAvailableForShift(user, assignment.Date, assignment.ShiftLabel))
                {
                    solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                }
            }

            // 2) تضمین حضور در شیفت‌های درخواست‌شده (درخواست حضور - شیفت مشخص)
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                foreach (var required in userConstraint.RequiredShiftSlots)
                {
                    var shiftReq = GetShiftRequirement(required.ShiftLabel);
                    if (shiftReq == null ||
                        required.Date.Date < _constraints.StartDate.Date ||
                        required.Date.Date > _constraints.EndDate.Date ||
                        !IsUserAvailableForShift(userConstraint, required.Date, required.ShiftLabel))
                    {
                        continue;
                    }

                    var alreadyAssigned = solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                        .Any(a => a.UserId == userConstraint.UserId && !a.IsOnCall);
                    if (alreadyAssigned)
                    {
                        continue;
                    }

                    // انتساب‌های دیگر همان روز کاربر حذف می‌شوند تا تداخل روزانه پیش نیاید
                    RemoveConflictingDailyAssignments(solution, userConstraint.UserId, required.Date, shiftReq.ShiftId);

                    // اگر ظرفیت تخصص کاربر در این شیفت پر است، یک نفر غیرمحافظت‌شده حذف می‌شود
                    MakeRoomInShift(solution, shiftReq, required.Date, userConstraint);

                    solution.AddAssignment(
                        userConstraint.UserId,
                        shiftReq.ShiftId,
                        required.Date,
                        required.ShiftLabel,
                        isOnCall: false);
                }

                // 3) تضمین حضور در روزهای درخواست‌شده (درخواست حضور - کل روز)
                foreach (var presenceDate in userConstraint.RequiredPresenceDates)
                {
                    if (presenceDate.Date < _constraints.StartDate.Date ||
                        presenceDate.Date > _constraints.EndDate.Date ||
                        solution.GetUserAssignments(userConstraint.UserId, presenceDate).Any())
                    {
                        continue;
                    }

                    // اولویت با شیفتی که ظرفیت خالی دارد؛ در غیر این صورت جایگزینی
                    var candidateShifts = _constraints.ShiftRequirements
                        .Where(s => IsUserAvailableForShift(userConstraint, presenceDate, s.ShiftLabel))
                        .Where(s => s.SpecialtyRequirements.Any(r => r.SpecialtyId == userConstraint.SpecialtyId && r.RequiredTotalCount > 0))
                        .OrderByDescending(s =>
                        {
                            var req = s.SpecialtyRequirements.First(r => r.SpecialtyId == userConstraint.SpecialtyId);
                            var current = CountSpecialtyAssignments(solution, s.ShiftId, presenceDate, userConstraint.SpecialtyId, isOnCall: false);
                            return req.RequiredTotalCount - current; // بیشترین جای خالی اول
                        })
                        .ToList();

                    var targetShift = candidateShifts.FirstOrDefault();
                    if (targetShift == null)
                    {
                        continue;
                    }

                    MakeRoomInShift(solution, targetShift, presenceDate, userConstraint);
                    solution.AddAssignment(
                        userConstraint.UserId,
                        targetShift.ShiftId,
                        presenceDate,
                        targetShift.ShiftLabel,
                        isOnCall: false);
                }
            }

            // 4) تضمین حضور مدیر شیفت در شیفت‌های عصر/شب دارای الزام
            var managerWarnings = EnsureShiftManagers(solution);

            solution.Score = CalculateSolutionScore(solution);
            solution.Violations.AddRange(managerWarnings);
        }

        /// <summary>
        /// اگر ظرفیت تخصص کاربر در شیفت/روز پر باشد، یک انتساب غیرمحافظت‌شده حذف می‌کند تا جا باز شود.
        /// </summary>
        private void MakeRoomInShift(ShiftSolution solution, ShiftRequirement shiftReq, DateTime date, UserConstraint incomingUser)
        {
            var specialtyReq = shiftReq.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == incomingUser.SpecialtyId);
            if (specialtyReq == null || specialtyReq.RequiredTotalCount <= 0)
            {
                return;
            }

            var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall && GetUserSpecialty(a.UserId) == incomingUser.SpecialtyId)
                .ToList();

            if (regulars.Count < specialtyReq.RequiredTotalCount)
            {
                return; // هنوز جا هست
            }

            var hasExplicitGender = specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0;

            // ترجیحاً کسی حذف شود که هم‌جنسیت کاربر ورودی است (برای حفظ ترکیب جنسیتی)
            var removable = regulars
                .Where(a => !IsProtectedAssignment(a))
                .OrderByDescending(a => !hasExplicitGender || GetUserGender(a.UserId) == incomingUser.Gender)
                .FirstOrDefault();

            if (removable != null)
            {
                solution.RemoveAssignment(removable.UserId, removable.ShiftId, removable.Date);
            }
        }

        /// <summary>
        /// برای هر شیفت عصر/شب دارای الزام مدیر، در صورت نبود مدیر، یک نیروی واجد شرایط جایگزین می‌کند.
        /// </summary>
        private List<string> EnsureShiftManagers(ShiftSolution solution)
        {
            var warnings = new List<string>();
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    if (!RequiresShiftManager(shiftReq.ShiftLabel))
                    {
                        continue;
                    }

                    var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                        .Where(a => !a.IsOnCall)
                        .ToList();

                    if (regulars.Count == 0)
                    {
                        continue;
                    }

                    var hasManager = regulars.Any(a =>
                        _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)?.CanBeShiftManager == true);
                    if (hasManager)
                    {
                        continue;
                    }

                    // جایگزینی یکی از نیروهای غیرمحافظت‌شده با یک کاربر واجد صلاحیت مدیریت
                    var replaced = false;
                    foreach (var occupant in regulars.Where(a => !IsProtectedAssignment(a)))
                    {
                        var occupantSpecialty = GetUserSpecialty(occupant.UserId);
                        var occupantGender = GetUserGender(occupant.UserId);
                        var specialtyReq = shiftReq.SpecialtyRequirements
                            .FirstOrDefault(r => r.SpecialtyId == occupantSpecialty);
                        var genderLocked = specialtyReq != null &&
                            (specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0);

                        var candidate = _constraints.UserConstraints
                            .Where(u => u.CanBeShiftManager && u.IsActive)
                            .Where(u => u.SpecialtyId == occupantSpecialty)
                            .Where(u => !genderLocked || u.Gender == occupantGender)
                            .Where(u => IsUserAvailableForShift(u, date, shiftReq.ShiftLabel))
                            .Where(u => !solution.GetUserAssignments(u.UserId, date).Any())
                            .OrderBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                            .FirstOrDefault();

                        if (candidate != null)
                        {
                            solution.RemoveAssignment(occupant.UserId, occupant.ShiftId, occupant.Date);
                            solution.AddAssignment(candidate.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false);
                            replaced = true;
                            break;
                        }
                    }

                    if (!replaced)
                    {
                        warnings.Add(
                            $"No eligible shift manager available for {shiftReq.ShiftLabel} shift on {date:yyyy-MM-dd}.");
                    }
                }
            }

            return warnings;
        }

        private void RemoveConflictingDailyAssignments(ShiftSolution solution, int userId, DateTime date, int keepShiftId)
        {
            foreach (var assignment in solution.GetUserAssignments(userId, date).ToList())
            {
                if (assignment.ShiftId != keepShiftId)
                {
                    solution.RemoveAssignment(userId, assignment.ShiftId, date);
                }
            }
        }

        private bool RequiresShiftManager(ShiftLabel shiftLabel)
        {
            return shiftLabel switch
            {
                ShiftLabel.Evening => _constraints.GlobalConstraints.RequireManagerForEveningShift,
                ShiftLabel.Night => _constraints.GlobalConstraints.RequireManagerForNightShift,
                _ => false
            };
        }

        private ShiftRequirement? GetShiftRequirement(ShiftLabel shiftLabel)
        {
            return _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == shiftLabel);
        }

        private bool IsUserAvailableForShift(UserConstraint user, DateTime date, ShiftLabel shiftLabel)
        {
            if (user.UnavailableDates.Contains(date.Date))
            {
                return false;
            }

            return !user.UnavailableShiftSlots.Any(s =>
                s.Date.Date == date.Date && s.ShiftLabel == shiftLabel);
        }

        private bool IsUserAvailableForShiftOnShiftId(UserConstraint user, DateTime date, int shiftId)
        {
            var shiftReq = _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId);
            if (shiftReq == null)
            {
                return !user.UnavailableDates.Contains(date.Date);
            }

            return IsUserAvailableForShift(user, date, shiftReq.ShiftLabel);
        }

        private bool IsRequiredShiftSlotForOtherUser(int userId, DateTime date, ShiftLabel shiftLabel)
        {
            return _constraints.UserConstraints.Any(u =>
                u.UserId != userId &&
                u.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftLabel));
        }

        private bool IsProtectedAssignment(SaShiftAssignment assignment)
        {
            var user = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user == null)
            {
                return false;
            }

            if (user.RequiredShiftSlots.Any(s =>
                    s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
            {
                return true;
            }

            return user.RequiredPresenceDates.Contains(assignment.Date.Date);
        }

        private IEnumerable<UserConstraint> OrderUsersForShiftAssignment(
            IEnumerable<UserConstraint> users,
            ShiftLabel shiftLabel,
            bool isOnCall)
        {
            if (!isOnCall && RequiresShiftManager(shiftLabel))
            {
                return users
                    .OrderByDescending(u => u.CanBeShiftManager)
                    .ThenBy(_ => _random.Next());
            }

            return users.OrderBy(_ => _random.Next());
        }


        private void AssignRequiredPersonnel(ShiftSolution solution, List<UserConstraint> eligibleUsers,
            ShiftRequirement shiftReq, DateTime date, SpecialtyRequirement specialtyReq)
        {
            var hasExplicitOnCallGender =
                specialtyReq.OnCallMaleCount > 0 || specialtyReq.OnCallFemaleCount > 0;
            var hasExplicitRegularGender =
                specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0;

            // آنکال
            if (hasExplicitOnCallGender)
            {
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, specialtyReq.OnCallMaleCount, UserGender.Male, isOnCall: true);
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, specialtyReq.OnCallFemaleCount, UserGender.Female, isOnCall: true);
                if (specialtyReq.OnCallTotalCount > specialtyReq.OnCallMaleCount + specialtyReq.OnCallFemaleCount)
                {
                    AssignRemainingBySpecialty(
                        solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                        specialtyReq.OnCallTotalCount, isOnCall: true);
                }
            }
            else if (specialtyReq.OnCallTotalCount > 0)
            {
                AssignRemainingBySpecialty(
                    solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                    specialtyReq.OnCallTotalCount, isOnCall: true);
            }

            // نیروی حاضر در محل
            if (hasExplicitRegularGender)
            {
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, specialtyReq.RequiredMaleCount, UserGender.Male, isOnCall: false);
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, specialtyReq.RequiredFemaleCount, UserGender.Female, isOnCall: false);
                if (specialtyReq.RequiredTotalCount > specialtyReq.RequiredMaleCount + specialtyReq.RequiredFemaleCount)
                {
                    AssignRemainingBySpecialty(
                        solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                        specialtyReq.RequiredTotalCount, isOnCall: false);
                }
            }
            else if (specialtyReq.RequiredTotalCount > 0)
            {
                AssignRemainingBySpecialty(
                    solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                    specialtyReq.RequiredTotalCount, isOnCall: false);
            }
        }

        private void AssignByGenderCount(
            ShiftSolution solution,
            List<UserConstraint> eligibleUsers,
            ShiftRequirement shiftReq,
            DateTime date,
            int specialtyId,
            int requiredCount,
            UserGender gender,
            bool isOnCall)
        {
            if (requiredCount <= 0)
            {
                return;
            }

            // انتساب‌های ازپیش‌اعمال‌شده (مثلاً درخواست‌های تأییدشده) شمرده می‌شوند
            var assigned = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Count(a => a.IsOnCall == isOnCall &&
                            GetUserSpecialty(a.UserId) == specialtyId &&
                            GetUserGender(a.UserId) == gender);

            var orderedUsers = OrderUsersForShiftAssignment(eligibleUsers, shiftReq.ShiftLabel, isOnCall);
            foreach (var user in orderedUsers.Where(u => u.Gender == gender))
            {
                if (assigned >= requiredCount)
                {
                    break;
                }

                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, date) &&
                    !HasDailyConflict(solution, user.UserId, date))
                {
                    solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall);
                    assigned++;
                }
            }
        }

        private void AssignRemainingBySpecialty(
            ShiftSolution solution,
            List<UserConstraint> eligibleUsers,
            ShiftRequirement shiftReq,
            DateTime date,
            int specialtyId,
            int targetCount,
            bool isOnCall)
        {
            var current = CountSpecialtyAssignments(solution, shiftReq.ShiftId, date, specialtyId, isOnCall);
            foreach (var user in OrderUsersForShiftAssignment(eligibleUsers, shiftReq.ShiftLabel, isOnCall))
            {
                if (current >= targetCount)
                {
                    break;
                }

                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, date) &&
                    !HasDailyConflict(solution, user.UserId, date))
                {
                    solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall);
                    current++;
                }
            }
        }

        /// <summary>
        /// آیا انتساب جدید در این روز باعث نقض قانون «یک شیفت در روز» می‌شود؟
        /// </summary>
        private bool HasDailyConflict(ShiftSolution solution, int userId, DateTime date)
        {
            var dailyCount = solution.GetUserAssignments(userId, date).Count;
            if (dailyCount == 0)
            {
                return false;
            }

            if (_constraints.HardRules.ForbidDuplicateDailyAssignments)
            {
                return true;
            }

            return _constraints.HardRules.EnforceMaxShiftsPerDay &&
                   dailyCount >= _constraints.GlobalConstraints.MaxShiftsPerDay;
        }

        private int CountSpecialtyAssignments(ShiftSolution solution, int shiftId, DateTime date, int specialtyId, bool isOnCall)
        {
            return solution.GetShiftAssignments(shiftId, date)
                .Count(a => a.IsOnCall == isOnCall && GetUserSpecialty(a.UserId) == specialtyId);
        }

        private int CountGenderAssignments(IEnumerable<SaShiftAssignment> assignments, UserGender gender)
        {
            return assignments.Count(a => GetUserGender(a.UserId) == gender);
        }

        private double CheckMonthlyWorkingHours(UserConstraint userConstraint, List<SaShiftAssignment> assignments, List<string> violations)
        {
            if (!userConstraint.ProductivityRequiredHours.HasValue)
            {
                return 0;
            }

            var workedHours = CalculateUserWorkedHours(assignments);
            var maxHours = (double)userConstraint.ProductivityRequiredHours.Value;
            if (workedHours <= maxHours + 0.25)
            {
                return 0;
            }

            violations.Add($"User {userConstraint.UserId} exceeds productivity hours ({workedHours:F1}/{maxHours:F1}).");
            return workedHours - maxHours;
        }

        private double CalculateUserWorkedHours(List<SaShiftAssignment> assignments)
        {
            double total = 0;
            foreach (var assignment in assignments)
            {
                total += GetShiftDuration(assignment.ShiftId);
            }
            return total;
        }

        private double GetShiftDuration(int shiftId)
        {
            if (_shiftDurationLookup.TryGetValue(shiftId, out var duration))
            {
                return duration;
            }

            return 8;
        }

        private void PerformSwapMove(ShiftSolution solution)
        {
            var slotGroups = solution.Assignments.Values
                .GroupBy(a => (a.ShiftId, a.Date.Date, a.IsOnCall, SpecialtyId: GetUserSpecialty(a.UserId)))
                .Where(g => g.Count() >= 2)
                .ToList();

            if (slotGroups.Count == 0)
            {
                return;
            }

            var group = slotGroups[_random.Next(slotGroups.Count)].ToList();
            var assignment1 = group[_random.Next(group.Count)];
            var assignment2 = group[_random.Next(group.Count)];

            if (assignment1.UserId == assignment2.UserId)
            {
                return;
            }

            if (IsProtectedAssignment(assignment1) || IsProtectedAssignment(assignment2))
            {
                return;
            }

            solution.RemoveAssignment(assignment1.UserId, assignment1.ShiftId, assignment1.Date);
            solution.RemoveAssignment(assignment2.UserId, assignment2.ShiftId, assignment2.Date);

            solution.AddAssignment(assignment2.UserId, assignment1.ShiftId, assignment1.Date, assignment1.ShiftLabel, assignment1.IsOnCall);
            solution.AddAssignment(assignment1.UserId, assignment2.ShiftId, assignment2.Date, assignment2.ShiftLabel, assignment2.IsOnCall);
        }

        private void PerformReassignMove(ShiftSolution solution)
        {
            var assignments = solution.Assignments.Values.ToList();
            if (assignments.Count == 0) return;

            var assignment = assignments[_random.Next(assignments.Count)];
            if (IsProtectedAssignment(assignment))
            {
                return;
            }

            var assignmentGender = GetUserGender(assignment.UserId);
            var specialtyId = GetUserSpecialty(assignment.UserId);
            var shiftReq = _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == assignment.ShiftId);
            var specialtyReq = shiftReq?.SpecialtyRequirements.FirstOrDefault(s => s.SpecialtyId == specialtyId);
            var lockGender = specialtyReq != null && (
                (assignment.IsOnCall && (specialtyReq.OnCallMaleCount > 0 || specialtyReq.OnCallFemaleCount > 0)) ||
                (!assignment.IsOnCall && (specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0)));

            var eligibleUsers = _constraints.UserConstraints
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => u.IsActive)
                .Where(u => !lockGender || u.Gender == assignmentGender)
                .Where(u => IsUserAvailableForShift(u, assignment.Date, assignment.ShiftLabel))
                .Where(u => !IsRequiredShiftSlotForOtherUser(u.UserId, assignment.Date, assignment.ShiftLabel))
                .Where(u => !solution.HasAssignment(u.UserId, assignment.ShiftId, assignment.Date))
                .OrderBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(_ => _random.Next())
                .ToList();

            if (eligibleUsers.Count > 0)
            {
                var newUser = eligibleUsers[0];
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                solution.AddAssignment(newUser.UserId, assignment.ShiftId, assignment.Date, assignment.ShiftLabel, assignment.IsOnCall);
            }
        }

        private void PerformAddMove(ShiftSolution solution)
        {
            var understaffedSlots = FindUnderstaffedSlots(solution);
            if (understaffedSlots.Count == 0)
            {
                return;
            }

            var slot = understaffedSlots[_random.Next(understaffedSlots.Count)];
            var shiftReq = _constraints.ShiftRequirements.First(s => s.ShiftId == slot.ShiftId);

            var eligibleUsers = _constraints.UserConstraints
                .Where(u => u.SpecialtyId == slot.SpecialtyId)
                .Where(u => u.IsActive)
                .Where(u => IsUserAvailableForShiftOnShiftId(u, slot.Date, slot.ShiftId))
                .Where(u => !IsRequiredShiftSlotForOtherUser(u.UserId, slot.Date, shiftReq.ShiftLabel))
                .Where(u => !solution.HasAssignment(u.UserId, slot.ShiftId, slot.Date))
                .Where(u => !slot.RequireMale || u.Gender == UserGender.Male)
                .Where(u => !slot.RequireFemale || u.Gender == UserGender.Female)
                .OrderBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(_ => _random.Next())
                .ToList();

            if (eligibleUsers.Count > 0)
            {
                var user = eligibleUsers[0];
                solution.AddAssignment(user.UserId, slot.ShiftId, slot.Date, shiftReq.ShiftLabel, slot.IsOnCall);
            }
        }

        private void PerformRemoveMove(ShiftSolution solution)
        {
            var overstaffed = FindOverstaffedAssignments(solution)
                .Where(a => !IsProtectedAssignment(a))
                .ToList();
            if (overstaffed.Count == 0)
            {
                return;
            }

            var assignment = overstaffed[_random.Next(overstaffed.Count)];
            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
        }

        private List<StaffingSlotGap> FindUnderstaffedSlots(ShiftSolution solution)
        {
            var gaps = new List<StaffingSlotGap>();
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        AddGenderGaps(gaps, solution, shiftReq, date, specialtyReq, isOnCall: true);
                        AddGenderGaps(gaps, solution, shiftReq, date, specialtyReq, isOnCall: false);
                    }
                }
            }

            return gaps;
        }

        private void AddGenderGaps(
            List<StaffingSlotGap> gaps,
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            SpecialtyRequirement specialtyReq,
            bool isOnCall)
        {
            var current = CountSpecialtyAssignments(solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall);
            var hasExplicitGender = isOnCall
                ? specialtyReq.OnCallMaleCount > 0 || specialtyReq.OnCallFemaleCount > 0
                : specialtyReq.RequiredMaleCount > 0 || specialtyReq.RequiredFemaleCount > 0;

            if (hasExplicitGender)
            {
                int maleTarget = isOnCall ? specialtyReq.OnCallMaleCount : specialtyReq.RequiredMaleCount;
                int femaleTarget = isOnCall ? specialtyReq.OnCallFemaleCount : specialtyReq.RequiredFemaleCount;
                var assignments = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                    .Where(a => a.IsOnCall == isOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId);

                int maleShort = maleTarget - CountGenderAssignments(assignments, UserGender.Male);
                int femaleShort = femaleTarget - CountGenderAssignments(assignments, UserGender.Female);

                for (int i = 0; i < maleShort; i++)
                {
                    gaps.Add(new StaffingSlotGap(shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall, requireMale: true));
                }

                for (int i = 0; i < femaleShort; i++)
                {
                    gaps.Add(new StaffingSlotGap(shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall, requireFemale: true));
                }
            }
            else
            {
                int target = isOnCall ? specialtyReq.OnCallTotalCount : specialtyReq.RequiredTotalCount;
                int shortfall = target - current;
                for (int i = 0; i < shortfall; i++)
                {
                    gaps.Add(new StaffingSlotGap(shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall));
                }
            }
        }

        private List<SaShiftAssignment> FindOverstaffedAssignments(ShiftSolution solution)
        {
            var removable = new List<SaShiftAssignment>();
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var regular = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                            .Where(a => !a.IsOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId)
                            .ToList();
                        var onCall = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                            .Where(a => a.IsOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId)
                            .ToList();

                        if (regular.Count > specialtyReq.RequiredTotalCount)
                        {
                            removable.AddRange(regular.OrderByDescending(_ => _random.Next()).Take(regular.Count - specialtyReq.RequiredTotalCount));
                        }

                        if (onCall.Count > specialtyReq.OnCallTotalCount)
                        {
                            removable.AddRange(onCall.OrderByDescending(_ => _random.Next()).Take(onCall.Count - specialtyReq.OnCallTotalCount));
                        }
                    }
                }
            }

            return removable;
        }

        private readonly struct StaffingSlotGap
        {
            public StaffingSlotGap(int shiftId, DateTime date, int specialtyId, bool isOnCall, bool requireMale = false, bool requireFemale = false)
            {
                ShiftId = shiftId;
                Date = date;
                SpecialtyId = specialtyId;
                IsOnCall = isOnCall;
                RequireMale = requireMale;
                RequireFemale = requireFemale;
            }

            public int ShiftId { get; }
            public DateTime Date { get; }
            public int SpecialtyId { get; }
            public bool IsOnCall { get; }
            public bool RequireMale { get; }
            public bool RequireFemale { get; }
        }

        private UserGender GetUserGender(int userId)
        {
            return _constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.Gender ?? UserGender.Male;
        }

        private int GetUserSpecialty(int userId)
        {
            return _constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId)?.SpecialtyId ?? 0;
        }

        private double CheckConsecutiveShifts(List<SaShiftAssignment> assignments, int maxConsecutive, List<string> violations)
        {
            double penalty = 0;
            int consecutiveCount = 1;

            for (int i = 1; i < assignments.Count; i++)
            {
                if ((assignments[i].Date - assignments[i - 1].Date).Days == 1)
                {
                    consecutiveCount++;
                    if (consecutiveCount > maxConsecutive)
                    {
                        penalty += 50;
                        violations.Add($"User {assignments[i].UserId} has {consecutiveCount} consecutive shifts (max: {maxConsecutive})");
                    }
                }
                else
                {
                    consecutiveCount = 1;
                }
            }

            return penalty;
        }

        private double CheckRestDays(List<SaShiftAssignment> assignments, int minRestDays, List<string> violations)
        {
            double penalty = 0;

            for (int i = 1; i < assignments.Count; i++)
            {
                var daysBetween = (assignments[i].Date - assignments[i - 1].Date).Days;
                if (daysBetween < minRestDays + 1)
                {
                    penalty += 30;
                    violations.Add($"User {assignments[i].UserId} has insufficient rest between shifts ({daysBetween} days, min: {minRestDays + 1})");
                }
            }

            return penalty;
        }

        private double CheckWeeklyShifts(List<SaShiftAssignment> assignments, int maxWeekly, List<string> violations)
        {
            double penalty = 0;
            var weeklyGroups = assignments.GroupBy(a => GetWeekNumber(a.Date));

            foreach (var week in weeklyGroups)
            {
                if (week.Count() > maxWeekly)
                {
                    penalty += 40;
                    violations.Add($"User {week.First().UserId} has {week.Count()} shifts in week {week.Key} (max: {maxWeekly})");
                }
            }

            return penalty;
        }

        private double CheckMonthlyNightShifts(List<SaShiftAssignment> assignments, int maxMonthly, List<string> violations)
        {
            double penalty = 0;
            var monthlyGroups = assignments
                .Where(a => a.ShiftLabel == ShiftLabel.Night)
                .GroupBy(a => new { a.Date.Year, a.Date.Month });

            foreach (var month in monthlyGroups)
            {
                if (month.Count() > maxMonthly)
                {
                    penalty += 60;
                    violations.Add($"User {month.First().UserId} has {month.Count()} night shifts in {month.Key.Year}/{month.Key.Month} (max: {maxMonthly})");
                }
            }

            return penalty;
        }

        private int GetWeekNumber(DateTime date)
        {
            var calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Saturday);
        }

        #endregion

        public AlgorithmStatistics GetStatistics()
        {
            return _statistics;
        }
    }

    public enum MoveType
    {
        Swap,      // تعویض دو انتساب
        Reassign,  // تغییر انتساب یک شیفت
        Add,       // اضافه کردن انتساب جدید
        Remove     // حذف انتساب
    }
}
