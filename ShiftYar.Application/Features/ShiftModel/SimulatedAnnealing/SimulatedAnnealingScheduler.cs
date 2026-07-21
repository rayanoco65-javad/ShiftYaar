using ShiftYar.Application.Common.Utilities;
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
        private readonly Dictionary<int, ProductivityWorkedHoursCalculator.ShiftWorkInfo> _shiftInfoLookup;

        public SimulatedAnnealingScheduler(ShiftConstraints constraints, SimulatedAnnealingParameters parameters)
        {
            _constraints = constraints;
            _parameters = parameters;
            _random = new Random();
            _statistics = new AlgorithmStatistics();
            _shiftInfoLookup = ProductivityWorkedHoursCalculator.BuildShiftInfoLookup(constraints.ShiftRequirements);
            _shiftDurationLookup = _shiftInfoLookup.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.DurationHours > 0 ? kvp.Value.DurationHours : 8);
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

            // تضمین نهایی: درخواست‌های تأییدشده آخرین حرف را می‌زنند
            ApplyMandatoryConstraints(bestSolution);

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
            ApplyMandatoryConstraints(currentSolution);
            var bestSolution = currentSolution.Clone();

            _statistics.BestScore = currentSolution.Score;
            _statistics.CurrentScore = currentSolution.Score;

            RunAnnealingLoop(ref currentSolution, ref bestSolution);

            ApplyMandatoryConstraints(bestSolution);

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

            // سهمیه دقیق شب را قبل از پر کردن ظرفیت روزانه قفل کن
            ExactNightQuotaGuard.Enforce(solution, _constraints);

            // تولید انتساب‌های تصادفی اولیه
            var availableUsers = _constraints.UserConstraints.ToList();
            var dateRange = GetDateRange();

            // اول شیفت‌های شب را پر کن تا سهمیه‌دارها جایشان را از دست ندهند
            foreach (var date in dateRange)
            {
                foreach (var shiftReq in _constraints.ShiftRequirements.Where(s => s.ShiftLabel == ShiftLabel.Night))
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var eligibleUsers = availableUsers
                            .Where(u => u.SpecialtyId == specialtyReq.SpecialtyId)
                            .Where(u => IsUserAvailableForShift(u, date, shiftReq.ShiftLabel, solution))
                            .Where(u => u.IsActive)
                            .ToList();
                        AssignRequiredPersonnel(solution, eligibleUsers, shiftReq, date, specialtyReq);
                    }
                }
            }

            foreach (var date in dateRange)
            {
                foreach (var shiftReq in _constraints.ShiftRequirements.Where(s => s.ShiftLabel != ShiftLabel.Night))
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var eligibleUsers = availableUsers
                            .Where(u => u.SpecialtyId == specialtyReq.SpecialtyId)
                            .Where(u => IsUserAvailableForShift(u, date, shiftReq.ShiftLabel, solution))
                            .Where(u => u.IsActive) // فقط کاربران فعال
                            .ToList();

                        // انتساب نیروهای مورد نیاز
                        AssignRequiredPersonnel(solution, eligibleUsers, shiftReq, date, specialtyReq);
                    }
                }
            }

            ExactNightQuotaGuard.Enforce(solution, _constraints);

            ProductivityHourFillGuard.Enforce(solution, _constraints);

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
            if (roll < 0.25)
            {
                PerformReassignMove(neighbor);
            }
            else if (roll < 0.45)
            {
                PerformHourBalanceMove(neighbor);
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
            score += CalculateFairWorkedHoursBalancePenalty(solution) * _constraints.SoftWeights.FairWorkedHoursBalanceWeight;
            score += CalculateProductivityShortfallPenalty(solution) * _constraints.SoftWeights.ProductivityShortfallWeight;
            score += CalculateFairNightShiftBalancePenalty(solution) * _constraints.SoftWeights.FairNightShiftBalanceWeight;
            score += CalculateMorningEveningBalancePenalty(solution) * _constraints.SoftWeights.MorningEveningBalanceWeight;
            score += CalculateExactNightQuotaPenalty(solution) * _constraints.SoftWeights.ExactNightQuotaWeight;
            score += CalculateExtraShiftRotationPenalty(solution) * _constraints.SoftWeights.ExtraShiftRotationWeight;
            score += CalculateShiftLabelBalancePenalty(solution) * _constraints.SoftWeights.ShiftLabelBalanceWeight;
            score += CalculateNightShiftSeniorityPenalty(solution) * _constraints.SoftWeights.NightShiftDistributionBySeniorityWeight;

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
                        var day = specialtyReq.ForDay(_constraints.IsHoliday(date));
                        var regularCount = CountSpecialtyAssignments(
                            solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall: false);
                        var onCallCount = CountSpecialtyAssignments(
                            solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall: true);

                        if (day.RequiredTotalCount > 0 && regularCount < day.RequiredTotalCount)
                        {
                            penalty += (day.RequiredTotalCount - regularCount) * 120;
                        }

                        if (day.OnCallTotalCount > 0 && onCallCount < day.OnCallTotalCount)
                        {
                            penalty += (day.OnCallTotalCount - onCallCount) * 100;
                        }
                    }
                }
            }

            return penalty;
        }

        private double CalculateFairShiftCountBalancePenalty(ShiftSolution solution)
        {
            // اختلاف تعداد شیفت‌های این ماه نسبت به میانگین دپارتمان
            // پرسنل فیکس (حضور روزانهٔ اجباری) از محاسبه حذف می‌شوند تا میانگین گردشی‌ها را منحرف نکنند
            var counts = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Select(u => (UserId: u.UserId, Count: solution.GetUserAllAssignments(u.UserId).Count))
                .ToList();
            if (counts.Count == 0) return 0;
            double avg = counts.Average(c => c.Count);
            double sumAbs = counts.Sum(c => Math.Abs(c.Count - avg));
            return sumAbs; // وزن بیرونی اعمال می‌شود
        }

        private double CalculateFairWorkedHoursBalancePenalty(ShiftSolution solution)
        {
            var entries = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue)
                .Select(u =>
                {
                    var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId));
                    var required = (double)u.ProductivityRequiredHours!.Value;
                    return required > 0 ? worked / required : 0;
                })
                .ToList();

            if (entries.Count < 2)
            {
                return 0;
            }

            var avgRatio = entries.Average();
            // جریمه انحراف از میانگین نسبت تحقق موظفی (تعادل بین پرسنل با سقف‌های متفاوت)
            return entries.Sum(r => Math.Abs(r - avgRatio)) * 100;
        }

        private double CalculateProductivityShortfallPenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var user in _constraints.UserConstraints)
            {
                if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
                {
                    continue;
                }

                if (user.ShiftType == ShiftTypes.FixedShift)
                {
                    continue;
                }

                var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
                var required = (double)user.ProductivityRequiredHours.Value;
                var shortfall = required - worked;
                if (shortfall > 2)
                {
                    // جریمه درجه دوم برای کمبود بزرگ ساعت موظفی
                    penalty += shortfall * shortfall;
                }
            }

            return penalty;
        }

        private double CalculateFairNightShiftBalancePenalty(ShiftSolution solution)
        {
            var nightEligible = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => !u.HasExactNightQuota) // سهمیه دقیق از تعادل نرم خارج است
                .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Night))
                .ToList();
            if (nightEligible.Count < 2) return 0;

            var nightCounts = nightEligible
                .Select(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall))
                .ToList();
            var avg = nightCounts.Average();
            return nightCounts.Sum(c => Math.Abs(c - avg)) * 2.0;
        }

        private double CalculateExactNightQuotaPenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var user in _constraints.UserConstraints)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                    .ToList();

                if (user.ExactNightShiftCount.HasValue)
                {
                    penalty += Math.Max(0, user.ExactNightShiftCount.Value - nights.Count) * 20;
                }

                if (user.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));
                    penalty += Math.Max(0, user.ExactHolidayWeekendNightShiftCount.Value - holidayNights) * 25;
                }

                if (nights.Count >= 2 &&
                    (user.HasExactNightQuota || user.ExactHolidayWeekendNightShiftCount.HasValue))
                {
                    penalty += ExactNightQuotaGuard.CalculateSpreadPenalty(
                        nights.Select(a => a.Date).ToList(),
                        _constraints.StartDate,
                        _constraints.EndDate) * 0.5;
                }
            }

            return penalty;
        }

        private double CalculateMorningEveningBalancePenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var u in _constraints.UserConstraints)
            {
                if (u.ShiftType != ShiftTypes.RotatingShift) continue;
                if (!ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Morning) ||
                    !ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Evening))
                {
                    continue;
                }

                var ua = solution.GetUserAllAssignments(u.UserId);
                var morning = ua.Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);
                var evening = ua.Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);
                penalty += Math.Abs(morning - evening) * 2.0;
            }

            return penalty;
        }

        private double CalculateNightShiftSeniorityPenalty(ShiftSolution solution)
        {
            if (_constraints.SoftWeights.NightShiftDistributionBySeniorityWeight <= 0)
            {
                return 0;
            }

            var eligible = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => ShiftEligibilityResolver.IsLabelAllowed(u.AllowedShiftLabels, ShiftLabel.Night))
                .ToList();
            if (eligible.Count < 2) return 0;

            var totalNights = eligible.Sum(u =>
                solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall));
            if (totalNights == 0) return 0;

            double fair = totalNights / (double)eligible.Count;
            return eligible.Sum(u =>
            {
                var nights = solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                return Math.Abs(nights - fair);
            });
        }

        private double CalculateExtraShiftRotationPenalty(ShiftSolution solution)
        {
            // اگر کاربری در سابقه اخیر شیفت اضافه بیشتری داشته، دادن شیفت اضافه به او جریمه شود
            // تعریف ساده: "شیفت اضافه" = بالاتر از میانگین همین ماه
            var counts = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Select(u => (User: u, Count: solution.GetUserAllAssignments(u.UserId).Count))
                .ToList();
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
            // برای کاربران گردشی، اختلاف توزیع برچسب‌های مجاز از توزیع عادلانه جریمه شود
            double penalty = 0;
            foreach (var u in _constraints.UserConstraints)
            {
                if (u.ShiftType != ShiftTypes.RotatingShift) continue;
                var ua = solution.GetUserAllAssignments(u.UserId);
                if (ua.Count == 0) continue;

                var allowed = u.AllowedShiftLabels != null && u.AllowedShiftLabels.Count > 0
                    ? u.AllowedShiftLabels
                    : new List<ShiftLabel> { ShiftLabel.Morning, ShiftLabel.Evening, ShiftLabel.Night };

                if (allowed.Count == 0) continue;

                double fair = ua.Count / (double)allowed.Count;
                foreach (var label in allowed)
                {
                    int count = ua.Count(x => x.ShiftLabel == label);
                    penalty += Math.Abs(count - fair);
                }
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
                var isDailyFixedStaff = userConstraint.ShiftType == ShiftTypes.FixedShift;

                // قوانین سخت در IsFeasible بررسی می‌شوند؛ اینجا فقط جریمهٔ نرم برای قوانین غیرفعال‌شده
                if (!_constraints.HardRules.EnforceMaxConsecutiveShifts && !isDailyFixedStaff)
                {
                    penalty += CheckConsecutiveShifts(userAssignments, userConstraint.MaxConsecutiveShifts, violations);
                }

                if (!_constraints.HardRules.EnforceMinRestDays && !isDailyFixedStaff)
                {
                    penalty += CheckRestDays(userAssignments, userConstraint.MinRestDaysBetweenShifts, violations);
                }

                if (!_constraints.HardRules.EnforceWeeklyMaxShifts && !isDailyFixedStaff)
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
                        var day = specialtyReq.ForDay(_constraints.IsHoliday(date));
                        var regularForSpec = assignments
                            .Where(a => GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId && !a.IsOnCall)
                            .ToList();
                        var onCallForSpec = assignments
                            .Where(a => GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId && a.IsOnCall)
                            .ToList();

                        var hasExplicitRegularGender =
                            day.RequiredMaleCount > 0 || day.RequiredFemaleCount > 0;
                        var hasExplicitOnCallGender =
                            day.OnCallMaleCount > 0 || day.OnCallFemaleCount > 0;

                        if (hasExplicitRegularGender)
                        {
                            if (day.RequiredMaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(regularForSpec, UserGender.Male) - day.RequiredMaleCount) * 80;
                            }

                            if (day.RequiredFemaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(regularForSpec, UserGender.Female) - day.RequiredFemaleCount) * 80;
                            }
                        }
                        else if (day.RequiredTotalCount > 0 && regularForSpec.Count < day.RequiredTotalCount)
                        {
                            penalty += (day.RequiredTotalCount - regularForSpec.Count) * 50;
                        }

                        if (hasExplicitOnCallGender)
                        {
                            if (day.OnCallMaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(onCallForSpec, UserGender.Male) - day.OnCallMaleCount) * 80;
                            }

                            if (day.OnCallFemaleCount > 0)
                            {
                                penalty += Math.Abs(CountGenderAssignments(onCallForSpec, UserGender.Female) - day.OnCallFemaleCount) * 80;
                            }
                        }
                        else if (day.OnCallTotalCount > 0 && onCallForSpec.Count < day.OnCallTotalCount)
                        {
                            penalty += (day.OnCallTotalCount - onCallForSpec.Count) * 60;
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

                if (userConstraint.UnavailableDates.Any(d => d.Date == assignment.Date.Date))
                {
                    return false;
                }

                if (userConstraint.UnavailableShiftSlots.Any(s =>
                        s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
                {
                    return false;
                }

                if (!ShiftEligibilityResolver.IsLabelAllowed(userConstraint.AllowedShiftLabels, assignment.ShiftLabel))
                {
                    return false;
                }
            }

            // ممنوعیت توالی عصر→شب و شب→صبح بدون فاصله
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                if (AdjacentShiftRestRules.HasForbiddenAdjacentPair(
                        solution.GetUserAllAssignments(userConstraint.UserId)))
                {
                    return false;
                }
            }

            // حضور قطعی در شیفت‌های درخواست‌شده
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                foreach (var required in userConstraint.RequiredShiftSlots)
                {
                    var shiftReq = GetShiftRequirement(required.ShiftLabel, userConstraint.SpecialtyId);
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
                    if (!solution.GetUserAssignments(userConstraint.UserId, presenceDate).Any(a => !a.IsOnCall))
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

            // ترکیب روزانه: صبح+عصر مجاز؛ شب تنها؛ سقف MaxShiftsPerDay
            {
                var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                    ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                    : 2;
                var forbidDup = _constraints.HardRules.ForbidDuplicateDailyAssignments;
                foreach (var grp in solution.Assignments.Values.GroupBy(a => new { a.UserId, Date = a.Date.Date }))
                {
                    if (!DailyAssignmentRules.IsValidDaySet(grp.Select(a => a.ShiftLabel), maxPerDay, forbidDup))
                    {
                        return false;
                    }
                }
            }

            // حداقل استراحت و حداکثر متوالی
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                var userAssignments = solution.GetUserAllAssignments(userConstraint.UserId);
                // پرسنل فیکس هر روز غیرتعطیل شیفت‌اند؛ قواعد استراحت/توالی/سقف هفتگی/موظفی برایشان بی‌معناست
                var isDailyFixedStaff = userConstraint.ShiftType == ShiftTypes.FixedShift;

                if (_constraints.HardRules.EnforceMinRestDays && !isDailyFixedStaff)
                {
                    for (int i = 1; i < userAssignments.Count; i++)
                    {
                        var daysBetween = (userAssignments[i].Date - userAssignments[i - 1].Date).Days;
                        // صبح+عصر همان روز (daysBetween=0) مجاز است
                        if (daysBetween == 0)
                        {
                            continue;
                        }

                        if (daysBetween < userConstraint.MinRestDaysBetweenShifts + 1)
                            return false;
                    }
                }
                if (_constraints.HardRules.EnforceMaxConsecutiveShifts && !isDailyFixedStaff)
                {
                    var workDates = userAssignments
                        .Select(a => a.Date.Date)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                    int consecutive = 1;
                    for (int i = 1; i < workDates.Count; i++)
                    {
                        if ((workDates[i] - workDates[i - 1]).Days == 1)
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

                if (_constraints.HardRules.EnforceProductivityHours &&
                    userConstraint.ProductivityRequiredHours.HasValue &&
                    !isDailyFixedStaff)
                {
                    var workedHours = CalculateUserWorkedHours(userAssignments);
                    var maxAllowed = ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
                        userConstraint.ProductivityRequiredHours,
                        userConstraint.OvertimeConsent,
                        userConstraint.MaxMonthlyOvertimeHours);
                    if (workedHours > maxAllowed + 0.25)
                    {
                        return false;
                    }
                }

                if (_constraints.HardRules.EnforceMaxConsecutiveWorkHours && !isDailyFixedStaff)
                {
                    if (ProductivityWorkedHoursCalculator.ExceedsMaxConsecutiveWorkHours(
                            userAssignments,
                            _shiftInfoLookup,
                            userConstraint.MaxConsecutiveWorkHours))
                    {
                        return false;
                    }
                }

                if (_constraints.HardRules.EnforceWeeklyMaxShifts && !isDailyFixedStaff)
                {
                    foreach (var week in userAssignments.GroupBy(a => GetWeekNumber(a.Date)))
                    {
                        if (week.Count() > userConstraint.MaxShiftsPerWeek)
                        {
                            return false;
                        }
                    }
                }

                if (_constraints.HardRules.EnforceNightShiftMonthlyCap || userConstraint.HasExactNightQuota)
                {
                    var nights = userAssignments.Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall).ToList();
                    if (_constraints.HardRules.EnforceNightShiftMonthlyCap)
                    {
                        foreach (var month in nights.GroupBy(a => new { a.Date.Year, a.Date.Month }))
                        {
                            if (month.Count() > userConstraint.MaxNightShiftsPerMonth)
                            {
                                return false;
                            }
                        }
                    }

                    if (userConstraint.ExactHolidayWeekendNightShiftCount.HasValue)
                    {
                        var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));
                        if (holidayNights > userConstraint.ExactHolidayWeekendNightShiftCount.Value)
                        {
                            return false;
                        }
                    }

                    if (userConstraint.MinDaysBetweenNightShifts > 0 && nights.Count > 1)
                    {
                        var ordered = nights.OrderBy(a => a.Date).ToList();
                        for (var i = 1; i < ordered.Count; i++)
                        {
                            if (Math.Abs((ordered[i].Date.Date - ordered[i - 1].Date.Date).Days) <=
                                userConstraint.MinDaysBetweenNightShifts)
                            {
                                return false;
                            }
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
                        var isHoliday = _constraints.IsHoliday(date);
                        int totalRequired = shiftReq.SpecialtyRequirements.Sum(r =>
                        {
                            var d = r.ForDay(isHoliday);
                            return d.RequiredTotalCount + d.OnCallTotalCount;
                        });
                        if (assignments.Count > totalRequired)
                        {
                            return false;
                        }
                        foreach (var specReq in shiftReq.SpecialtyRequirements)
                        {
                            var day = specReq.ForDay(isHoliday);
                            var specAssignments = assignments
                                .Where(a => GetUserSpecialty(a.UserId) == specReq.SpecialtyId)
                                .ToList();
                            var regular = specAssignments.Where(a => !a.IsOnCall).ToList();
                            var onCall = specAssignments.Where(a => a.IsOnCall).ToList();

                            if (specAssignments.Count > day.RequiredTotalCount + day.OnCallTotalCount)
                            {
                                return false;
                            }

                            if (regular.Count > day.RequiredTotalCount)
                            {
                                return false;
                            }

                            if (onCall.Count > day.OnCallTotalCount)
                            {
                                return false;
                            }

                            var hasExplicitRegularGender =
                                day.RequiredMaleCount > 0 || day.RequiredFemaleCount > 0;
                            var hasExplicitOnCallGender =
                                day.OnCallMaleCount > 0 || day.OnCallFemaleCount > 0;

                            if (hasExplicitRegularGender)
                            {
                                if (day.RequiredMaleCount > 0 &&
                                    CountGenderAssignments(regular, UserGender.Male) != day.RequiredMaleCount)
                                {
                                    return false;
                                }

                                if (day.RequiredFemaleCount > 0 &&
                                    CountGenderAssignments(regular, UserGender.Female) != day.RequiredFemaleCount)
                                {
                                    return false;
                                }
                            }

                            if (hasExplicitOnCallGender)
                            {
                                if (day.OnCallMaleCount > 0 &&
                                    CountGenderAssignments(onCall, UserGender.Male) != day.OnCallMaleCount)
                                {
                                    return false;
                                }

                                if (day.OnCallFemaleCount > 0 &&
                                    CountGenderAssignments(onCall, UserGender.Female) != day.OnCallFemaleCount)
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
                    var shiftReq = GetShiftRequirement(required.ShiftLabel, userConstraint.SpecialtyId);
                    if (shiftReq == null || !IsUserAvailableForShift(userConstraint, required.Date, required.ShiftLabel, solution))
                    {
                        continue;
                    }

                    RemoveConflictingDailyAssignments(
                        solution, userConstraint.UserId, required.Date, shiftReq.ShiftId, required.ShiftLabel);

                    var existing = solution.GetShiftAssignments(shiftReq.ShiftId, required.Date)
                        .FirstOrDefault(a => a.UserId == userConstraint.UserId);
                    if (existing == null || existing.IsOnCall)
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
                    if (solution.GetUserAssignments(userConstraint.UserId, presenceDate).Any(a => !a.IsOnCall))
                    {
                        continue;
                    }

                    foreach (var shiftReq in _constraints.ShiftRequirements
                                 .Where(s => IsUserAvailableForShift(userConstraint, presenceDate, s.ShiftLabel, solution))
                                 .OrderByDescending(s =>
                                 {
                                     var req = s.SpecialtyRequirements
                                         .FirstOrDefault(r => r.SpecialtyId == userConstraint.SpecialtyId);
                                     return req?.RequiredTotalCount ?? 0;
                                 }))
                    {
                        RemoveConflictingDailyAssignments(
                            solution, userConstraint.UserId, presenceDate, shiftReq.ShiftId, shiftReq.ShiftLabel);
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
        /// اعمال قطعی قیود درخواست‌های تأییدشده و الزام مدیر شیفت روی راه‌حل نهایی.
        /// ترتیب: تعمیر مدیر → اجبار درخواست‌ها (آخرین حرف) → گزارش نقض.
        /// </summary>
        public void ApplyMandatoryConstraints(ShiftSolution solution)
        {
            var managerWarnings = EnsureShiftManagers(solution);
            ApprovedRequestGuard.ForceApply(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ProductivityHourFillGuard.Enforce(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
            solution.Score = CalculateSolutionScore(solution);
            solution.Violations.AddRange(managerWarnings);
            solution.Violations.AddRange(ApprovedRequestGuard.GetUnmetViolations(solution, _constraints));
            solution.Violations.AddRange(ShiftEligibilityGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(AdjacentShiftRestGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(DailyDuplicateAssignmentGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(GetExactNightQuotaViolations(solution));
        }

        private List<string> GetExactNightQuotaViolations(ShiftSolution solution)
        {
            var violations = new List<string>();
            foreach (var user in _constraints.UserConstraints)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                    .ToList();
                if (user.ExactNightShiftCount.HasValue && nights.Count < user.ExactNightShiftCount.Value)
                {
                    violations.Add(
                        $"User {user.UserId} minimum night quota not met ({nights.Count}/{user.ExactNightShiftCount.Value}).");
                }

                if (user.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));
                    if (holidayNights < user.ExactHolidayWeekendNightShiftCount.Value)
                    {
                        violations.Add(
                            $"User {user.UserId} minimum holiday/weekend night quota not met ({holidayNights}/{user.ExactHolidayWeekendNightShiftCount.Value}).");
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// آیا همه درخواست‌های تأییدشدهٔ بارگذاری‌شده روی راه‌حل رعایت شده‌اند؟
        /// </summary>
        public bool AreApprovedRequestsSatisfied(ShiftSolution solution, out List<string> unmet)
        {
            unmet = ApprovedRequestGuard.GetUnmetViolations(solution, _constraints);
            return unmet.Count == 0;
        }

        private void EnforceHardRequestConstraints(ShiftSolution solution)
        {
            ApplyMandatoryConstraints(solution);
        }

        /// <summary>
        /// اگر ظرفیت تخصص کاربر در شیفت/روز پر باشد، یک انتساب غیرمحافظت‌شده حذف می‌کند تا جا باز شود.
        /// در صورت نیاز، حتی انتساب «حضور کل‌روز» را جابه‌جا می‌کند تا درخواست شیفت‌مشخص اولویت بگیرد.
        /// </summary>
        private void MakeRoomInShift(ShiftSolution solution, ShiftRequirement shiftReq, DateTime date, UserConstraint incomingUser)
        {
            var specialtyReq = shiftReq.SpecialtyRequirements
                .FirstOrDefault(r => r.SpecialtyId == incomingUser.SpecialtyId);
            var day = specialtyReq?.ForDay(_constraints.IsHoliday(date));
            if (specialtyReq == null || day == null || day.Value.RequiredTotalCount <= 0)
            {
                return;
            }

            var dayCounts = day.Value;
            var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall &&
                            a.UserId != incomingUser.UserId &&
                            GetUserSpecialty(a.UserId) == incomingUser.SpecialtyId)
                .ToList();

            if (regulars.Count < dayCounts.RequiredTotalCount)
            {
                return; // هنوز جا هست
            }

            var hasExplicitGender = dayCounts.RequiredMaleCount > 0 || dayCounts.RequiredFemaleCount > 0;

            // ۱) غیرمحافظت‌شده
            // ۲) فقط محافظت حضور کل‌روز (نه شیفت مشخص)
            // ۳) در نهایت هر کسی به‌جز دارندهٔ همین RequiredShiftSlot
            var removable = regulars
                .OrderBy(a =>
                {
                    if (!IsProtectedAssignment(a)) return 0;
                    var u = _constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                    if (u != null &&
                        u.RequiredShiftSlots.Any(s => s.Date.Date == a.Date.Date && s.ShiftLabel == a.ShiftLabel))
                    {
                        return 2; // محافظت شیفت مشخص — ترجیحاً دست نخور
                    }

                    return 1; // فقط حضور کل‌روز
                })
                .ThenByDescending(a => !hasExplicitGender || GetUserGender(a.UserId) == incomingUser.Gender)
                .FirstOrDefault(a =>
                {
                    var u = _constraints.UserConstraints.FirstOrDefault(x => x.UserId == a.UserId);
                    return u == null ||
                           !u.RequiredShiftSlots.Any(s => s.Date.Date == a.Date.Date && s.ShiftLabel == a.ShiftLabel);
                });

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
                        var day = specialtyReq?.ForDay(_constraints.IsHoliday(date));
                        var genderLocked = day != null &&
                            (day.Value.RequiredMaleCount > 0 || day.Value.RequiredFemaleCount > 0);

                        var candidate = _constraints.UserConstraints
                            .Where(u => u.CanBeShiftManager && u.IsActive)
                            .Where(u => u.SpecialtyId == occupantSpecialty)
                            .Where(u => !genderLocked || u.Gender == occupantGender)
                            .Where(u => IsUserAvailableForShift(u, date, shiftReq.ShiftLabel, solution))
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

        private void RemoveConflictingDailyAssignments(
            ShiftSolution solution,
            int userId,
            DateTime date,
            int keepShiftId,
            ShiftLabel keepLabel)
        {
            var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;
            var forbidDup = _constraints.HardRules.ForbidDuplicateDailyAssignments;

            foreach (var assignment in solution.GetUserAssignments(userId, date).ToList())
            {
                if (assignment.ShiftId == keepShiftId)
                {
                    continue;
                }

                // صبح+عصر قابل نگه‌داشتن با هم هستند؛ فقط ناسازگارها حذف شوند
                var trial = new[] { assignment.ShiftLabel, keepLabel };
                if (!DailyAssignmentRules.IsValidDaySet(trial, maxPerDay, forbidDup))
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

        private ShiftRequirement? GetShiftRequirement(ShiftLabel shiftLabel, int? specialtyId = null)
        {
            var matches = _constraints.ShiftRequirements
                .Where(s => s.ShiftLabel == shiftLabel)
                .ToList();

            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count == 1 || !specialtyId.HasValue)
            {
                return matches[0];
            }

            // اگر چند شیفت با یک Label وجود داشته باشد، شیفتی که ظرفیت تخصص کاربر را دارد اولویت دارد
            return matches
                .OrderByDescending(s =>
                    s.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId.Value)?.RequiredTotalCount ?? 0)
                .ThenBy(s => s.ShiftId)
                .First();
        }

        private bool IsUserAvailableForShift(
            UserConstraint user,
            DateTime date,
            ShiftLabel shiftLabel,
            ShiftSolution? solution = null)
        {
            if (user.UnavailableDates.Any(d => d.Date == date.Date))
            {
                return false;
            }

            if (!ShiftEligibilityResolver.IsLabelAllowed(user.AllowedShiftLabels, shiftLabel))
            {
                return false;
            }

            if (user.UnavailableShiftSlots.Any(s =>
                    s.Date.Date == date.Date && s.ShiftLabel == shiftLabel))
            {
                return false;
            }

            if (solution != null &&
                AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(user.UserId), date, shiftLabel))
            {
                return false;
            }

            if (solution != null &&
                HasDailyConflict(solution, user.UserId, date, shiftLabel))
            {
                return false;
            }

            if (shiftLabel == ShiftLabel.Night && solution != null)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                    .ToList();

                if (_constraints.HardRules.EnforceNightShiftMonthlyCap && nights.Count >= user.MaxNightShiftsPerMonth)
                {
                    return false;
                }

                // رزرو شب‌های باقی‌مانده برای تکمیل سهمیه تعطیل/آخر هفته
                if (user.ExactNightShiftCount.HasValue &&
                    user.ExactHolidayWeekendNightShiftCount.HasValue &&
                    !_constraints.IsHolidayWeekendNight(date))
                {
                    var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));
                    var remainingTotal = user.ExactNightShiftCount.Value - nights.Count;
                    var remainingHoliday = user.ExactHolidayWeekendNightShiftCount.Value - holidayNights;
                    if (remainingHoliday > 0 && remainingTotal <= remainingHoliday)
                    {
                        return false;
                    }
                }

                if (user.MinDaysBetweenNightShifts > 0)
                {
                    foreach (var n in nights)
                    {
                        if (Math.Abs((date.Date - n.Date.Date).Days) <= user.MinDaysBetweenNightShifts)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool IsUserAvailableForShiftOnShiftId(UserConstraint user, DateTime date, int shiftId, ShiftSolution? solution = null)
        {
            var shiftReq = _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == shiftId);
            if (shiftReq == null)
            {
                return !user.UnavailableDates.Contains(date.Date);
            }

            return IsUserAvailableForShift(user, date, shiftReq.ShiftLabel, solution);
        }

        private bool IsRequiredShiftSlotForOtherUser(int userId, DateTime date, ShiftLabel shiftLabel)
        {
            // فقط وقتی ظرفیت شیفت تک‌نفره (یا کمتر) است، جای seat اجباری کاربر دیگر را قفل کن
            var shiftReq = GetShiftRequirement(shiftLabel);
            if (shiftReq == null)
            {
                return false;
            }

            foreach (var other in _constraints.UserConstraints.Where(u => u.UserId != userId))
            {
                if (!other.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftLabel))
                {
                    continue;
                }

                var specialtyReq = shiftReq.SpecialtyRequirements
                    .FirstOrDefault(r => r.SpecialtyId == other.SpecialtyId);
                var capacity = specialtyReq?.ForDay(_constraints.IsHoliday(date)).RequiredTotalCount ?? 0;
                if (capacity <= 1)
                {
                    return true;
                }
            }

            return false;
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

            return user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date);
        }

        private IEnumerable<UserConstraint> OrderUsersForShiftAssignment(
            ShiftSolution solution,
            IEnumerable<UserConstraint> users,
            ShiftLabel shiftLabel,
            bool isOnCall,
            DateTime date)
        {
            var isHoliday = _constraints.IsHoliday(date);

            // اولویت: ساعات مؤثر کمتر، سپس تعداد شب کمتر (برای شیفت شب)، سپس تعداد شیفت کمتر
            if (!isOnCall && RequiresShiftManager(shiftLabel))
            {
                return users
                    .OrderByDescending(u => u.CanBeShiftManager)
                    .ThenBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
                    .ThenBy(u => CountUserNightShifts(solution, u.UserId))
                    .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                    .ThenBy(u => u.RecentTotalShifts)
                    .ThenBy(_ => _random.Next());
            }

            if (shiftLabel == ShiftLabel.Night)
            {
                var isHolidayWeekendNight = _constraints.IsHolidayWeekendNight(date);
                return users
                    .OrderBy(u => NightQuotaPriority(solution, u, isHolidayWeekendNight))
                    .ThenBy(u => CountUserNightShifts(solution, u.UserId))
                    .ThenBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
                    .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                    .ThenBy(u => u.RecentTotalShifts)
                    .ThenBy(_ => _random.Next());
            }

            return users
                .OrderBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
                .ThenBy(u => MorningEveningImbalance(solution, u, shiftLabel))
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(u => u.RecentTotalShifts)
                .ThenBy(_ => _random.Next());
        }

        private int NightQuotaPriority(ShiftSolution solution, UserConstraint user, bool dateIsHolidayWeekendNight)
        {
            if (!user.HasExactNightQuota && !user.ExactHolidayWeekendNightShiftCount.HasValue)
            {
                return CountUserNightShifts(solution, user.UserId);
            }

            var nights = solution.GetUserAllAssignments(user.UserId)
                .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                .ToList();
            var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));

            if (dateIsHolidayWeekendNight && user.ExactHolidayWeekendNightShiftCount.HasValue)
            {
                var holidayDeficit = user.ExactHolidayWeekendNightShiftCount.Value - holidayNights;
                if (holidayDeficit > 0)
                {
                    return -2000 - holidayDeficit;
                }
            }

            var deficit = (user.ExactNightShiftCount ?? 0) - nights.Count;
            if (deficit > 0)
            {
                return -1000 - deficit;
            }

            return 1000 + nights.Count;
        }

        private static int MorningEveningImbalance(ShiftSolution solution, UserConstraint user, ShiftLabel assigningLabel)
        {
            var ua = solution.GetUserAllAssignments(user.UserId);
            var m = ua.Count(a => a.ShiftLabel == ShiftLabel.Morning && !a.IsOnCall);
            var e = ua.Count(a => a.ShiftLabel == ShiftLabel.Evening && !a.IsOnCall);
            // کسی که صبح بیشتر دارد برای عصر اولویت بگیرد و برعکس
            if (assigningLabel == ShiftLabel.Morning)
            {
                return m - e;
            }

            if (assigningLabel == ShiftLabel.Evening)
            {
                return e - m;
            }

            return Math.Abs(m - e);
        }

        private static int CountUserNightShifts(ShiftSolution solution, int userId)
        {
            return solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
        }

        private void AssignRequiredPersonnel(ShiftSolution solution, List<UserConstraint> eligibleUsers,
            ShiftRequirement shiftReq, DateTime date, SpecialtyRequirement specialtyReq)
        {
            var day = specialtyReq.ForDay(_constraints.IsHoliday(date));
            var hasExplicitOnCallGender =
                day.OnCallMaleCount > 0 || day.OnCallFemaleCount > 0;
            var hasExplicitRegularGender =
                day.RequiredMaleCount > 0 || day.RequiredFemaleCount > 0;

            // آنکال
            if (hasExplicitOnCallGender)
            {
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, day.OnCallMaleCount, UserGender.Male, isOnCall: true);
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, day.OnCallFemaleCount, UserGender.Female, isOnCall: true);
                if (day.OnCallTotalCount > day.OnCallMaleCount + day.OnCallFemaleCount)
                {
                    AssignRemainingBySpecialty(
                        solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                        day.OnCallTotalCount, isOnCall: true);
                }
            }
            else if (day.OnCallTotalCount > 0)
            {
                AssignRemainingBySpecialty(
                    solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                    day.OnCallTotalCount, isOnCall: true);
            }

            // نیروی حاضر در محل
            if (hasExplicitRegularGender)
            {
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, day.RequiredMaleCount, UserGender.Male, isOnCall: false);
                AssignByGenderCount(solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId, day.RequiredFemaleCount, UserGender.Female, isOnCall: false);
                if (day.RequiredTotalCount > day.RequiredMaleCount + day.RequiredFemaleCount)
                {
                    AssignRemainingBySpecialty(
                        solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                        day.RequiredTotalCount, isOnCall: false);
                }
            }
            else if (day.RequiredTotalCount > 0)
            {
                AssignRemainingBySpecialty(
                    solution, eligibleUsers, shiftReq, date, specialtyReq.SpecialtyId,
                    day.RequiredTotalCount, isOnCall: false);
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

            var orderedUsers = OrderUsersForShiftAssignment(solution, eligibleUsers, shiftReq.ShiftLabel, isOnCall, date);
            foreach (var user in orderedUsers.Where(u => u.Gender == gender))
            {
                if (assigned >= requiredCount)
                {
                    break;
                }

                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, date) &&
                    !HasDailyConflict(solution, user.UserId, date, shiftReq.ShiftLabel) &&
                    !AdjacentShiftRestRules.WouldConflict(
                        solution.GetUserAllAssignments(user.UserId), date, shiftReq.ShiftLabel))
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
            foreach (var user in OrderUsersForShiftAssignment(solution, eligibleUsers, shiftReq.ShiftLabel, isOnCall, date))
            {
                if (current >= targetCount)
                {
                    break;
                }

                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, date) &&
                    !HasDailyConflict(solution, user.UserId, date, shiftReq.ShiftLabel) &&
                    !AdjacentShiftRestRules.WouldConflict(
                        solution.GetUserAllAssignments(user.UserId), date, shiftReq.ShiftLabel))
                {
                    solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall);
                    current++;
                }
            }
        }

        /// <summary>
        /// آیا انتساب جدید با قوانین ترکیب روزانه ناسازگار است؟
        /// صبح+عصر مجاز؛ شب تنها؛ تکرار لیبل ممنوع.
        /// </summary>
        private bool HasDailyConflict(ShiftSolution solution, int userId, DateTime date, ShiftLabel newLabel)
        {
            var existing = solution.GetUserAssignments(userId, date).Select(a => a.ShiftLabel);
            var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;
            return !DailyAssignmentRules.CanAddShift(
                existing,
                newLabel,
                maxPerDay,
                _constraints.HardRules.ForbidDuplicateDailyAssignments);
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
            var maxAllowed = ProductivityWorkedHoursCalculator.GetMaxAllowedHours(
                userConstraint.ProductivityRequiredHours,
                userConstraint.OvertimeConsent,
                userConstraint.MaxMonthlyOvertimeHours);
            if (workedHours <= maxAllowed + 0.25)
            {
                return 0;
            }

            violations.Add($"User {userConstraint.UserId} exceeds productivity hours ({workedHours:F1}/{maxAllowed:F1}).");
            return workedHours - maxAllowed;
        }

        private double CalculateUserWorkedHours(List<SaShiftAssignment> assignments)
        {
            return ProductivityWorkedHoursCalculator.CalculateEffectiveWorkedHours(
                assignments,
                _shiftInfoLookup,
                _constraints.IsHoliday);
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

            // همان شیفت/روز جابه‌جا می‌شود؛ توالی و ترکیب روزانه را چک کن
            if (AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(assignment2.UserId),
                    assignment1.Date, assignment1.ShiftLabel) ||
                AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(assignment1.UserId),
                    assignment2.Date, assignment2.ShiftLabel) ||
                HasDailyConflict(solution, assignment2.UserId, assignment1.Date, assignment1.ShiftLabel) ||
                HasDailyConflict(solution, assignment1.UserId, assignment2.Date, assignment2.ShiftLabel))
            {
                // برگرداندن
                solution.AddAssignment(assignment1.UserId, assignment1.ShiftId, assignment1.Date, assignment1.ShiftLabel, assignment1.IsOnCall);
                solution.AddAssignment(assignment2.UserId, assignment2.ShiftId, assignment2.Date, assignment2.ShiftLabel, assignment2.IsOnCall);
                return;
            }

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
            var day = specialtyReq?.ForDay(_constraints.IsHoliday(assignment.Date));
            var lockGender = day != null && (
                (assignment.IsOnCall && (day.Value.OnCallMaleCount > 0 || day.Value.OnCallFemaleCount > 0)) ||
                (!assignment.IsOnCall && (day.Value.RequiredMaleCount > 0 || day.Value.RequiredFemaleCount > 0)));

            var eligibleUsers = _constraints.UserConstraints
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => u.IsActive)
                .Where(u => !lockGender || u.Gender == assignmentGender)
                .Where(u => IsUserAvailableForShift(u, assignment.Date, assignment.ShiftLabel, solution))
                .Where(u => !IsRequiredShiftSlotForOtherUser(u.UserId, assignment.Date, assignment.ShiftLabel))
                .Where(u => !solution.HasAssignment(u.UserId, assignment.ShiftId, assignment.Date))
                .OrderBy(u => GetProductivityHourDeficit(u, solution))
                .ThenBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
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
                .Where(u => IsUserAvailableForShiftOnShiftId(u, slot.Date, slot.ShiftId, solution))
                .Where(u => !IsRequiredShiftSlotForOtherUser(u.UserId, slot.Date, shiftReq.ShiftLabel))
                .Where(u => !solution.HasAssignment(u.UserId, slot.ShiftId, slot.Date))
                .Where(u => !slot.RequireMale || u.Gender == UserGender.Male)
                .Where(u => !slot.RequireFemale || u.Gender == UserGender.Female)
                .OrderBy(u => GetProductivityHourDeficit(u, solution))
                .ThenBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
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
                .OrderByDescending(a => GetProductivityHourSurplus(GetUserConstraint(a.UserId), solution))
                .ThenByDescending(a => CalculateUserWorkedHours(solution.GetUserAllAssignments(a.UserId)))
                .ToList();
            if (overstaffed.Count == 0)
            {
                return;
            }

            var assignment = overstaffed[_random.Next(Math.Min(3, overstaffed.Count))];
            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
        }

        private void PerformHourBalanceMove(ShiftSolution solution)
        {
            var productivityUsers = _constraints.UserConstraints
                .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue)
                .ToList();
            if (productivityUsers.Count < 2)
            {
                PerformReassignMove(solution);
                return;
            }

            var receiver = productivityUsers
                .OrderByDescending(u => GetProductivityHourDeficit(u, solution))
                .FirstOrDefault(u => GetProductivityHourDeficit(u, solution) > 2);
            if (receiver == null)
            {
                return;
            }

            var donor = productivityUsers
                .Where(u => u.UserId != receiver.UserId)
                .OrderByDescending(u => GetProductivityHourSurplus(u, solution))
                .FirstOrDefault(u => GetProductivityHourSurplus(u, solution) > 2);
            if (donor == null)
            {
                return;
            }

            var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && !IsProtectedAssignment(a))
                .OrderByDescending(a => a.ShiftLabel == ShiftLabel.Night ? 1 : 0)
                .ToList();
            foreach (var assignment in donorAssignments)
            {
                if (solution.HasAssignment(receiver.UserId, assignment.ShiftId, assignment.Date))
                {
                    continue;
                }

                if (!IsUserAvailableForShift(receiver, assignment.Date, assignment.ShiftLabel, solution))
                {
                    continue;
                }

                solution.RemoveAssignment(donor.UserId, assignment.ShiftId, assignment.Date);
                solution.AddAssignment(
                    receiver.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall);
                return;
            }
        }

        private UserConstraint? GetUserConstraint(int userId) =>
            _constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId);

        private double GetProductivityHourDeficit(UserConstraint user, ShiftSolution solution)
        {
            if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
            {
                return 0;
            }

            var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            return Math.Max(0, (double)user.ProductivityRequiredHours.Value - worked);
        }

        private double GetProductivityHourSurplus(UserConstraint user, ShiftSolution solution)
        {
            if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
            {
                return CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            }

            var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            return Math.Max(0, worked - (double)user.ProductivityRequiredHours.Value);
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
            var day = specialtyReq.ForDay(_constraints.IsHoliday(date));
            var current = CountSpecialtyAssignments(solution, shiftReq.ShiftId, date, specialtyReq.SpecialtyId, isOnCall);
            var hasExplicitGender = isOnCall
                ? day.OnCallMaleCount > 0 || day.OnCallFemaleCount > 0
                : day.RequiredMaleCount > 0 || day.RequiredFemaleCount > 0;

            if (hasExplicitGender)
            {
                int maleTarget = isOnCall ? day.OnCallMaleCount : day.RequiredMaleCount;
                int femaleTarget = isOnCall ? day.OnCallFemaleCount : day.RequiredFemaleCount;
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
                int target = isOnCall ? day.OnCallTotalCount : day.RequiredTotalCount;
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
                var isHoliday = _constraints.IsHoliday(date);
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
                    {
                        var day = specialtyReq.ForDay(isHoliday);
                        var regular = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                            .Where(a => !a.IsOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId)
                            .ToList();
                        var onCall = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                            .Where(a => a.IsOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId)
                            .ToList();

                        if (regular.Count > day.RequiredTotalCount)
                        {
                            removable.AddRange(regular.OrderByDescending(_ => _random.Next()).Take(regular.Count - day.RequiredTotalCount));
                        }

                        if (onCall.Count > day.OnCallTotalCount)
                        {
                            removable.AddRange(onCall.OrderByDescending(_ => _random.Next()).Take(onCall.Count - day.OnCallTotalCount));
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
            var workDates = assignments
                .Select(a => a.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            int consecutiveCount = 1;
            int? userId = assignments.FirstOrDefault()?.UserId;

            for (int i = 1; i < workDates.Count; i++)
            {
                if ((workDates[i] - workDates[i - 1]).Days == 1)
                {
                    consecutiveCount++;
                    if (consecutiveCount > maxConsecutive)
                    {
                        penalty += 50;
                        violations.Add($"User {userId} has {consecutiveCount} consecutive shifts (max: {maxConsecutive})");
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
                // صبح+عصر همان روز مجاز است
                if (daysBetween == 0)
                {
                    continue;
                }

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
