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
        private readonly Func<int, bool> _isInProductivityPlan;

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
            _isInProductivityPlan = ProductivityWorkedHoursCalculator.BuildProductivityPlanLookup(constraints.UserConstraints);
        }

        /// <summary>
        /// اجرای الگوریتم Simulated Annealing
        /// </summary>
        public ShiftSolution Optimize()
        {
            var stopwatch = Stopwatch.StartNew();

            ManagerMixFeasibilityChecker.ValidateOrThrow(_constraints);

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
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            ExactComboShiftQuotaGuard.Enforce(solution, _constraints);

            // فاز ۱ (اسکلت مسئول): قبل از پر کردن ظرفیت با نیروی عادی، L1/مسئول روی Evening/Night قفل شود
            BuildReservedManagerSkeleton(solution);

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
                            .Where(u => NightQuotaEligibility.CanAssignInCoverageFill(
                                solution, _constraints, u, date))
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
                            .Where(u => u.IsActive)
                            .Where(u => DayShiftQuotaEligibility.CanAssignInCoverageFill(
                                solution, _constraints, u, shiftReq.ShiftLabel, date))
                            .ToList();

                        AssignRequiredPersonnel(solution, eligibleUsers, shiftReq, date, specialtyReq);
                    }
                }
            }

            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            ExactComboShiftQuotaGuard.Enforce(solution, _constraints);

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
            if (roll < 0.15)
            {
                PerformManagerMixRepairMove(neighbor);
            }
            else if (roll < 0.30)
            {
                PerformReassignMove(neighbor);
            }
            else if (roll < 0.48)
            {
                PerformHourBalanceMove(neighbor);
            }
            else if (roll < 0.63)
            {
                PerformMorningEveningBalanceMove(neighbor);
            }
            else if (roll < 0.76)
            {
                PerformSwapMove(neighbor);
            }
            else if (roll < 0.88)
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

        private void PerformManagerMixRepairMove(ShiftSolution solution)
        {
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements.Where(ShiftManagerRules.RequiresAnyManager))
                {
                    if (!SlotHasCoverageDemand(shiftReq, date))
                    {
                        continue;
                    }

                    var assignees = GetRegularAssignees(solution, shiftReq, date);
                    if (assignees.Count > 0 && !ShiftManagerRules.IsSatisfied(assignees, shiftReq))
                    {
                        EnsureShiftManagerMixForSlot(solution, shiftReq, date, strictPhase: false, markSkeleton: true);
                        if (IsSlotManagerMixSatisfied(solution, shiftReq, date))
                        {
                            SkeletonAssignmentGuard.LockSlotManagerAssignments(solution, _constraints, shiftReq, date);
                        }
                    }
                }
            }
        }

        private double CalculateManagerMixPenalty(ShiftSolution solution)
        {
            var violations = ShiftManagerMixGuard.GetViolations(solution, _constraints);
            return violations.Count * 100000.0;
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

            // جریمه سنگین برای نقض ترکیب مسئول شیفت (عصر/شب)
            score += CalculateManagerMixPenalty(solution);

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
            score += CalculateProductivityOvertimePenalty(solution) * _constraints.SoftWeights.ProductivityOvertimeWeight;
            score += CalculateFairNightShiftBalancePenalty(solution) * _constraints.SoftWeights.FairNightShiftBalanceWeight;
            score += CalculateMorningEveningBalancePenalty(solution) * _constraints.SoftWeights.MorningEveningBalanceWeight;
            score += CalculateFairMorningEveningPeerPenalty(solution) * _constraints.SoftWeights.FairMorningEveningPeerWeight;
            score += CalculateFairHolidayMorningEveningPeerPenalty(solution) * _constraints.SoftWeights.FairHolidayMorningEveningPeerWeight;
            score += CalculateWorkdaySpreadPenalty(solution) * _constraints.SoftWeights.WorkdaySpreadWeight;
            score += MaxConsecutiveWorkdayRules.CalculateOffSpreadPenalty(solution, _constraints)
                     * _constraints.SoftWeights.OffSpreadWeight;
            score += CalculateExactNightQuotaPenalty(solution) * _constraints.SoftWeights.ExactNightQuotaWeight;
            score += CalculateExtraShiftRotationPenalty(solution) * _constraints.SoftWeights.ExtraShiftRotationWeight;
            score += CalculateShiftLabelBalancePenalty(solution) * _constraints.SoftWeights.ShiftLabelBalanceWeight;
            score += CalculateShiftLabelSeniorityPenalty(solution, ShiftLabel.Morning)
                     * EffectiveSeniorityWeight(
                         _constraints.EnableMorningShiftDistributionBySeniority,
                         _constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight);
            score += CalculateShiftLabelSeniorityPenalty(solution, ShiftLabel.Evening)
                     * EffectiveSeniorityWeight(
                         _constraints.EnableEveningShiftDistributionBySeniority,
                         _constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight);
            score += CalculateShiftLabelSeniorityPenalty(solution, ShiftLabel.Night)
                     * EffectiveSeniorityWeight(
                         _constraints.EnableNightShiftDistributionBySeniority,
                         _constraints.SoftWeights.NightShiftDistributionBySeniorityWeight);

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
                        else if (regularCount > day.RequiredTotalCount)
                        {
                            penalty += (regularCount - day.RequiredTotalCount) * 200;
                        }

                        if (day.OnCallTotalCount > 0 && onCallCount < day.OnCallTotalCount)
                        {
                            penalty += (day.OnCallTotalCount - onCallCount) * 100;
                        }
                        else if (onCallCount > day.OnCallTotalCount)
                        {
                            penalty += (onCallCount - day.OnCallTotalCount) * 180;
                        }
                    }
                }
            }

            return penalty;
        }

        private double CalculateFairShiftCountBalancePenalty(ShiftSolution solution)
        {
            // سهم عادلانهٔ تعداد شیفت متناسب با موظفی هر نفر (نه میانگین ساده)
            var users = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .ToList();
            if (users.Count < 2)
            {
                return 0;
            }

            var counts = users.ToDictionary(
                u => u.UserId,
                u => solution.GetUserAllAssignments(u.UserId).Count(a => !a.IsOnCall));
            var totalShifts = counts.Values.Sum();
            if (totalShifts == 0)
            {
                return 0;
            }

            var weights = users.ToDictionary(
                u => u.UserId,
                u => u.IncludedInProductivityPlan && u.ProductivityRequiredHours.HasValue && u.ProductivityRequiredHours > 0
                    ? (double)u.ProductivityRequiredHours.Value
                    : 1.0);
            var totalWeight = weights.Values.Sum();
            if (totalWeight <= 0)
            {
                totalWeight = users.Count;
            }

            return users.Sum(u =>
            {
                var fairShare = totalShifts * weights[u.UserId] / totalWeight;
                return Math.Abs(counts[u.UserId] - fairShare);
            });
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
            // جریمه درجه دوم برای انحراف از میانگین نسبت تحقق موظفی
            return entries.Sum(r =>
            {
                var delta = Math.Abs(r - avgRatio);
                return delta * delta * 100 + delta * 20;
            });
        }

        private double CalculateProductivityShortfallPenalty(ShiftSolution solution)
        {
            const double peerTolerance = 2.0;
            const double nonProjectTolerance = ProjectPersonnelProductivityPriority.CrossTierToleranceHours;
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
                var tolerance = ProjectPersonnelProductivityPriority.IsProjectPersonnel(user)
                    ? peerTolerance
                    : nonProjectTolerance;
                if (shortfall <= tolerance)
                {
                    continue;
                }

                var tierWeight = ProjectPersonnelProductivityPriority.IsProjectPersonnel(user) ? 1.0 : 2.5;
                penalty += shortfall * shortfall * tierWeight;
                if (shortfall > 10)
                {
                    penalty += shortfall * 8 * tierWeight;
                }

                if (shortfall > 15)
                {
                    penalty += shortfall * 12 * tierWeight;
                }
            }

            return penalty;
        }

        private double CalculateProductivityOvertimePenalty(ShiftSolution solution)
        {
            const double peerTolerance = 2.0;
            const double crossTierTolerance = ProjectPersonnelProductivityPriority.CrossTierToleranceHours;

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
                var excess = worked - required;
                var tolerance = peerTolerance;

                if (ProjectPersonnelProductivityPriority.IsProjectPersonnel(user))
                {
                    if (excess <= crossTierTolerance)
                    {
                        continue;
                    }

                    penalty += excess * excess * 40;
                    if (excess > 5)
                    {
                        penalty += excess * 20;
                    }

                    continue;
                }

                if (excess <= tolerance)
                {
                    continue;
                }

                penalty += excess * excess;
                if (excess > 10)
                {
                    penalty += excess * 8;
                }

                if (excess > 15)
                {
                    penalty += excess * 12;
                }
            }

            return penalty;
        }

        private double CalculateFairNightShiftBalancePenalty(ShiftSolution solution)
        {
            var nightEligible = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => !u.HasExactNightQuota) // سهمیه دقیق از تعادل نرم خارج است
                .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, ShiftLabel.Night))
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
                    penalty += Math.Max(0, user.ExactNightShiftCount.Value - nights.Count) * 100;
                }

                if (user.ExactHolidayWeekendNightShiftCount.HasValue)
                {
                    var holidayNights = nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date));
                    penalty += Math.Max(0, user.ExactHolidayWeekendNightShiftCount.Value - holidayNights) * 150;
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
            var balanceableUsers = _constraints.UserConstraints
                .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => ShiftEligibilityResolver.SupportsMorningEveningCombo(u))
                .ToList();
            var limits = MorningEveningBalanceGuard.GetDepartmentSpreadLimits(solution, balanceableUsers);

            double penalty = 0;
            foreach (var u in balanceableUsers)
            {
                var morning = MorningEveningBalanceGuard.CountMorning(solution, u.UserId);
                var evening = MorningEveningBalanceGuard.CountEvening(solution, u.UserId);
                var excess = MorningEveningBalanceGuard.GetMorningEveningViolation(solution, u, limits);
                if (excess == 0)
                {
                    continue;
                }

                penalty += excess * excess * 4 + excess * 3;

                if (excess > 1)
                {
                    penalty += (excess - 1) * (excess - 1) * 12 + (excess - 1) * 8;
                }

                // جریمه شدید وقتی یک سمت کاملاً صفر است ولی شیفت کافی دارد
                var meTotal = morning + evening;
                if (meTotal >= 4 && (morning == 0 || evening == 0))
                {
                    penalty += 30 + meTotal * 8;
                }
            }

            return penalty;
        }

        /// <summary>
        /// تعادل تعداد صبح و عصر بین کاربران گردشی هم‌تخصص (نه فقط |M−E| درون یک نفر).
        /// </summary>
        private double CalculateFairMorningEveningPeerPenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
            {
                var eligible = _constraints.UserConstraints
                    .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                    .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
                    .ToList();
                if (eligible.Count < 2)
                {
                    continue;
                }

                var counts = eligible.ToDictionary(
                    u => u.UserId,
                    u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == label && !a.IsOnCall));
                var total = counts.Values.Sum();
                if (total == 0)
                {
                    continue;
                }

                var weights = eligible.ToDictionary(u => u.UserId, u => GetProductivityWeight(u));
                var totalWeight = weights.Values.Sum();
                if (totalWeight <= 0)
                {
                    totalWeight = eligible.Count;
                }

                foreach (var user in eligible)
                {
                    var fairShare = total * weights[user.UserId] / totalWeight;
                    var diff = counts[user.UserId] - fairShare;
                    penalty += Math.Abs(diff) * 3.0;

                    if (diff > 1.5)
                    {
                        var surplus = GetProductivityHourSurplus(user, solution);
                        if (surplus > 2)
                        {
                            penalty += diff * surplus * 0.35;
                        }
                    }
                    else if (diff < -1.5)
                    {
                        var deficit = GetProductivityHourDeficit(user, solution);
                        if (deficit > 2)
                        {
                            penalty += Math.Abs(diff) * deficit * 0.35;
                        }
                    }
                }
            }

            return penalty;
        }

        /// <summary>
        /// تعادل صبح/عصر فقط روی روزهای تعطیل بین کاربران گردشی هم‌تخصص.
        /// </summary>
        private double CalculateFairHolidayMorningEveningPeerPenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var specialtyGroup in _constraints.UserConstraints
                         .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                         .GroupBy(u => u.SpecialtyId))
            {
                foreach (var label in new[] { ShiftLabel.Morning, ShiftLabel.Evening })
                {
                    var eligible = specialtyGroup
                        .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
                        .ToList();
                    if (eligible.Count < 2)
                    {
                        continue;
                    }

                    var counts = eligible
                        .Select(u => HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                            solution, _constraints, u.UserId, label))
                        .ToList();
                    if (counts.All(c => c == 0))
                    {
                        continue;
                    }

                    var avg = counts.Average();
                    // وزن بالاتر از peer عادی تا تمرکز تعطیلات جریمه شود
                    penalty += counts.Sum(c => Math.Abs(c - avg)) * 6.0;
                    var spread = counts.Max() - counts.Min();
                    if (spread >= 2)
                    {
                        penalty += spread * spread * 4.0;
                    }
                }
            }

            return penalty;
        }

        /// <summary>
        /// جریمه تراکم روزهای کاری: رشته‌های متوالی بلند و هفته‌های تقریباً پر.
        /// </summary>
        private double CalculateWorkdaySpreadPenalty(ShiftSolution solution)
        {
            double penalty = 0;
            foreach (var user in _constraints.UserConstraints.Where(u => u.ShiftType != ShiftTypes.FixedShift))
            {
                var workDates = MaxConsecutiveWorkdayRules.GetCountableWorkDates(solution, user)
                    .OrderBy(d => d)
                    .ToList();
                if (workDates.Count == 0)
                {
                    continue;
                }

                var run = 1;
                for (var i = 1; i < workDates.Count; i++)
                {
                    if ((workDates[i] - workDates[i - 1]).Days == 1)
                    {
                        run++;
                        if (run > user.MaxConsecutiveShifts)
                        {
                            // سخت هم هست؛ اینجا نرم اضافه برای ترجیح فاصله
                            penalty += (run - user.MaxConsecutiveShifts) * (run - user.MaxConsecutiveShifts) * 8;
                        }
                        else if (run >= 3)
                        {
                            penalty += (run - 2) * 4;
                        }
                    }
                    else
                    {
                        run = 1;
                    }
                }

                foreach (var week in workDates.GroupBy(GetWeekNumber))
                {
                    var daysInWeek = week.Count();
                    if (daysInWeek >= 6)
                    {
                        penalty += (daysInWeek - 5) * 20;
                    }
                    else if (daysInWeek > user.MaxShiftsPerWeek)
                    {
                        penalty += (daysInWeek - user.MaxShiftsPerWeek) * 15;
                    }
                }
            }

            return penalty;
        }

        private static double EffectiveSeniorityWeight(bool enabled, double configuredWeight)
        {
            if (!enabled || configuredWeight <= 0)
            {
                return 0;
            }

            // وزن تنظیم‌شده در برابر جریمه‌های موظفی/عدالت (~۶–۸) ضعیف می‌ماند؛ تقویت حداقلی
            return Math.Max(configuredWeight * 4.0, 8.0);
        }

        private double CalculateShiftLabelSeniorityPenalty(ShiftSolution solution, ShiftLabel label)
        {
            var (enabled, distributionType, weight) = label switch
            {
                ShiftLabel.Morning => (
                    _constraints.EnableMorningShiftDistributionBySeniority,
                    _constraints.MorningShiftDistributionType,
                    _constraints.SoftWeights.MorningShiftDistributionBySeniorityWeight),
                ShiftLabel.Evening => (
                    _constraints.EnableEveningShiftDistributionBySeniority,
                    _constraints.EveningShiftDistributionType,
                    _constraints.SoftWeights.EveningShiftDistributionBySeniorityWeight),
                _ => (
                    _constraints.EnableNightShiftDistributionBySeniority,
                    _constraints.NightShiftDistributionType,
                    _constraints.SoftWeights.NightShiftDistributionBySeniorityWeight)
            };

            if (weight <= 0)
            {
                return 0;
            }

            var eligible = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
                .Where(u => label switch
                {
                    ShiftLabel.Morning => !u.HasExactMorningQuota,
                    ShiftLabel.Evening => !u.HasExactEveningQuota,
                    _ => !u.HasExactNightQuota
                })
                .ToList();
            if (eligible.Count < 2)
            {
                return 0;
            }

            var counts = eligible.ToDictionary(
                u => u.UserId,
                u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == label && !a.IsOnCall));
            var total = counts.Values.Sum();
            if (total == 0)
            {
                return 0;
            }

            if (!enabled)
            {
                var equalFair = total / (double)eligible.Count;
                return eligible.Sum(u => Math.Abs(counts[u.UserId] - equalFair));
            }

            var weights = eligible.ToDictionary(
                u => u.UserId,
                u => GetSeniorityDistributionWeight(u, distributionType));
            var totalWeight = weights.Values.Sum();
            if (totalWeight <= 0)
            {
                var equalFair = total / (double)eligible.Count;
                return eligible.Sum(u => Math.Abs(counts[u.UserId] - equalFair));
            }

            return eligible.Sum(u =>
            {
                var fair = total * weights[u.UserId] / totalWeight;
                return Math.Abs(counts[u.UserId] - fair);
            });
        }

        private double GetSeniorityDistributionWeight(UserConstraint user, int distributionType) =>
            ShiftSeniorityDistributionGuard.ResolveWeight(
                user.ExperienceYears,
                distributionType,
                _constraints.SeniorityDistributionSlope);

        private double CalculateExtraShiftRotationPenalty(ShiftSolution solution)
        {
            var users = _constraints.UserConstraints
                .Where(u => u.ShiftType != ShiftTypes.FixedShift)
                .Select(u => (User: u, Count: solution.GetUserAllAssignments(u.UserId).Count(a => !a.IsOnCall)))
                .ToList();
            if (users.Count < 2)
            {
                return 0;
            }

            var totalShifts = users.Sum(x => x.Count);
            if (totalShifts == 0)
            {
                return 0;
            }

            var weights = users.ToDictionary(
                x => x.User.UserId,
                x => GetProductivityWeight(x.User));
            var totalWeight = weights.Values.Sum();
            if (totalWeight <= 0)
            {
                totalWeight = users.Count;
            }

            double penalty = 0;
            foreach (var (user, count) in users)
            {
                var fairShare = totalShifts * weights[user.UserId] / totalWeight;
                if (count <= fairShare + 0.5)
                {
                    continue;
                }

                penalty += count - fairShare;
                var recent = user.RecentTotalShifts;
                var recentFair = recent / (double)Math.Max(1, users.Count);
                penalty += Math.Max(0, recent - recentFair) * 0.5;
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

                if (!ShiftEligibilityResolver.MayEverTakeLabel(userConstraint, assignment.ShiftLabel))
                {
                    return false;
                }
            }

            // ممنوعیت توالی عصر→شب و شب→صبح؛ عصر/شب بعد از شب با ON تأییدشده مستثنی می‌شود
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                if (AdjacentShiftRestGuard.HasReportableForbiddenPair(
                        userConstraint,
                        solution.GetUserAllAssignments(userConstraint.UserId),
                        _constraints.HardRules))
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

            // الزام ترکیب مسئول شیفت (سطح‌دار) برای صبح/عصر/شب
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    if (!ShiftManagerRules.RequiresAnyManager(shiftReq))
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

                    var assignees = regularAssignments
                        .Select(a => _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                        .Where(u => u != null)
                        .Cast<UserConstraint>();

                    if (!ShiftManagerRules.IsSatisfied(assignees, shiftReq))
                    {
                        return false;
                    }
                }
            }

            // ترکیب روزانه: صبح+عصر و صبح+شب مجاز؛ عصر+شب ممنوع؛ سقف MaxShiftsPerDay
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
                    var workDates = MaxConsecutiveWorkdayRules
                        .GetCountableWorkDatesFromAssignments(userAssignments, userConstraint)
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
                    var maxAllowed = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(userConstraint);
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
        /// ForceApply آخرین مرحله است تا گاردهای پوشش/عدالت/سهمیه شب حضور اجباری را نربایند.
        /// </summary>
        public void ApplyMandatoryConstraints(ShiftSolution solution)
        {
            solution.Violations.Clear();
            ApprovedRequestGuard.ForceApply(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
            ShiftEligibilityGuard.StripIneligibleAssignments(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            BuildReservedManagerSkeleton(solution);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            // پوشش ظرفیت اجباری اولویت مطلق دارد (عدالت نرم نباید جای خالی بسازد)
            ShiftCoverageGuard.Enforce(solution, _constraints);
            ProductivityHourFillGuard.Enforce(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            HolidayMorningEveningFairnessGuard.Enforce(solution, _constraints);
            MorningEveningBalanceGuard.Enforce(solution, _constraints);
            ShiftCoverageGuard.Enforce(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);

            // یک پاس نهایی برای نزدیک کردن ساعات مؤثر به موظفی پس از گاردهای پوشش/تعطیل
            ProductivityHourFillGuard.Enforce(solution, _constraints);
            MorningEveningBalanceGuard.Enforce(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            ShiftCoverageGuard.Enforce(solution, _constraints);

            // پس از Coverage/Fairness: سهمیه شب، سپس تعادل ظرفیت و ForceApply نهایی (ON مطلق).
            ShiftCoverageGuard.StripExcessCoverage(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            ShiftCoverageGuard.EnforceCapacityCeiling(solution, _constraints);
            // ۱) اول موظفی‌ها را پر کن / کسری را از مازاد جبران کن
            ProductivityHourFillGuard.EnforceFinalBalance(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            MorningEveningBalanceGuard.Enforce(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            // ۲) فقط شیفت‌های آزادِ بالای موظفی را طبق سابقه بازتوزیع کن (بدون ایجاد کسری)
            ShiftSeniorityDistributionGuard.Enforce(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            // ۳) اگر بازتوزیع سابقه جایی کسری ساخت، دوباره موظفی را ترمیم کن
            ProductivityHourFillGuard.EnforceFinalBalance(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            // ۴) مازاد باقی‌مانده را یک‌بار دیگر با سابقه تنظیم کن
            ShiftSeniorityDistributionGuard.Enforce(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            // پر کردن موظفی ممکن است صبح/عصر اضافه کند یا شب جابه‌جا کند — سهمیه شب را دوباره قفل کن
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            ExactComboShiftQuotaGuard.Enforce(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ApprovedRequestGuard.ForceApply(solution, _constraints);
            ShiftCoverageGuard.StripExcessCoverage(solution, _constraints);
            ShiftCoverageGuard.Enforce(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);

            // آخرین ترمیم موظفی: بعد از سهمیه/پوشش/Strip تا کسری دوباره از مازاد پر شود
            ProductivityHourFillGuard.EnforceFinalBalance(solution, _constraints);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            ExactDayShiftQuotaGuard.EnforceAll(solution, _constraints);
            RepairManagerMix(solution);
            RefreshManagerSkeletonFlags(solution);

            // آخرین حرف: ON تأییدشده بعد از همه گاردها دوباره اعمال شود
            ApprovedRequestGuard.ForceApply(solution, _constraints);

            // پوشش + مسئول شیفت + سهمیه + سقف روز متوالی — حلقهٔ نهایی محدود
            FinalizeMandatoryConstraints(solution);
            SealApprovedRequestsThenAdjacency(solution);
            FinalizeManagerMixMandatory(solution);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            ExactNightQuotaGuard.Enforce(solution, _constraints);
            RepairManagerMix(solution);
            RefreshManagerSkeletonFlags(solution);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);

            if (HasUnmetManagerMix(solution))
            {
                UnlockManagersOnUnmetMixSlots(solution);
                for (var repair = 0; repair < 5 && HasUnmetManagerMix(solution); repair++)
                {
                    RepairManagerMix(solution);
                    ApprovedRequestGuard.ForceApply(solution, _constraints);
                    AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                    ExactNightQuotaGuard.Enforce(solution, _constraints);
                    UnlockManagersOnUnmetMixSlots(solution);
                }

                if (HasUnmetManagerMix(solution))
                {
                    UnlockManagersOnUnmetMixSlots(solution);
                    BuildReservedManagerSkeleton(solution);
                    ApprovedRequestGuard.ForceApply(solution, _constraints);
                    AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                    FinalizeManagerMixMandatory(solution);
                }
            }

            solution.Score = CalculateSolutionScore(solution);
            ShiftManagerMixGuard.EnsureOrThrow(solution, _constraints);
            solution.Violations.AddRange(ShiftCoverageGuard.GetOverCapacityViolations(solution, _constraints));
            solution.Violations.AddRange(ApprovedRequestGuard.GetUnmetViolations(solution, _constraints));
            solution.Violations.AddRange(ShiftEligibilityGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(AdjacentShiftRestGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(DailyDuplicateAssignmentGuard.GetViolations(solution, _constraints));
            solution.Violations.AddRange(GetExactNightQuotaViolations(solution));
            solution.Violations.AddRange(GetExactDayShiftQuotaViolations(solution));
            solution.Violations.AddRange(ExactDayShiftQuotaGuard.GetFallbackPoolWarnings(solution, _constraints));
            solution.Violations.AddRange(MaxConsecutiveWorkdayRules.GetViolations(solution, _constraints));
        }

        public bool AreExactDayShiftQuotasSatisfied(ShiftSolution solution, out List<string> unmet)
        {
            unmet = GetExactDayShiftQuotaViolations(solution);
            return unmet.Count == 0;
        }

        private List<string> GetExactDayShiftQuotaViolations(ShiftSolution solution)
        {
            var violations = new List<string>();
            violations.AddRange(ExactDayShiftQuotaGuard.GetViolations(solution, _constraints, ShiftLabel.Morning));
            violations.AddRange(ExactDayShiftQuotaGuard.GetViolations(solution, _constraints, ShiftLabel.Evening));
            return violations;
        }

        public bool AreExactNightQuotasSatisfied(ShiftSolution solution, out List<string> unmet)
        {
            unmet = GetExactNightQuotaViolations(solution);
            return unmet.Count == 0;
        }

        private List<string> GetExactNightQuotaViolations(ShiftSolution solution)
        {
            var violations = new List<string>();
            foreach (var user in _constraints.UserConstraints)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                    .ToList();
                var holidayNights = user.ExactHolidayWeekendNightShiftCount.HasValue
                    ? nights.Count(a => _constraints.IsHolidayWeekendNight(a.Date))
                    : 0;
                var displayName = string.IsNullOrWhiteSpace(user.UserName)
                    ? $"کاربر {user.UserId}"
                    : $"«{user.UserName}» (شناسه {user.UserId})";

                if (user.ExactNightShiftCount.HasValue && nights.Count < user.ExactNightShiftCount.Value)
                {
                    var msg =
                        $"{displayName}: سهمیه حداقل شیفت شب رعایت نشد — {nights.Count} شب تخصیص داده شده، " +
                        $"حداقل موردنیاز {user.ExactNightShiftCount.Value} شب است.";
                    if (user.ExactHolidayWeekendNightShiftCount.HasValue &&
                        holidayNights < user.ExactHolidayWeekendNightShiftCount.Value)
                    {
                        msg +=
                            $" همچنین سهمیه شب تعطیل/آخرهفته: {holidayNights}/{user.ExactHolidayWeekendNightShiftCount.Value}.";
                    }

                    if (ComboShiftQuotaEligibility.HasComboQuotaConfigured(user)
                        && user.MorningNightShiftCount.HasValue
                        && user.MorningNightFallbackParticipation == false
                        && user.ExactNightShiftCount.Value > user.MorningNightShiftCount.Value)
                    {
                        msg +=
                            $" سهمیه ترکیبی صبح/شب ({user.MorningNightShiftCount.Value}) از سهمیه شب ({user.ExactNightShiftCount.Value}) کمتر است.";
                    }
                    else if (ComboShiftQuotaEligibility.HasComboQuotaConfigured(user)
                             && user.MorningNightShiftCount.HasValue)
                    {
                        var comboTotal = ComboShiftQuotaEligibility.CountMorningNightAssignments(solution, user.UserId);
                        if (comboTotal >= user.MorningNightShiftCount.Value
                            && user.MorningNightFallbackParticipation == false)
                        {
                            msg +=
                                $" سقف سهمیه ترکیبی صبح/شب ({comboTotal}/{user.MorningNightShiftCount.Value}) ممکن است مانع باشد.";
                        }
                    }

                    if (!_constraints.HardRules.AllowEveningAfterNightShift)
                    {
                        msg += " (تنظیم «اجازه عصر روز بعد از شب» غیرفعال است.)";
                    }

                    if (!_constraints.HardRules.AllowNightShiftAfterNightShift)
                    {
                        msg += " (تنظیم «اجازه شب روز بعد از شب» غیرفعال است.)";
                    }

                    if (_constraints.HardRules.EnforceMaxShiftsPerDay
                        && _constraints.GlobalConstraints.MaxShiftsPerDay <= 1
                        && _constraints.HardRules.AllowEveningAfterNightShift)
                    {
                        msg += " (حداکثر ۱ شیفت در روز فعال است؛ شیفت صبح/عصر همان روز ممکن است مانع شب شود.)";
                    }

                    violations.Add(msg);
                }
                else if (user.ExactHolidayWeekendNightShiftCount.HasValue &&
                         holidayNights < user.ExactHolidayWeekendNightShiftCount.Value)
                {
                    violations.Add(
                        $"{displayName}: سهمیه حداقل شب تعطیل/آخرهفته رعایت نشد — {holidayNights} شب، " +
                        $"حداقل موردنیاز {user.ExactHolidayWeekendNightShiftCount.Value} شب است " +
                        $"(کل شب‌های تخصیص‌یافته: {nights.Count}).");
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
                    if (!IsProtectedAssignment(solution, a)) return 0;
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
        /// تأمین ترکیب مسئول شیفت پس از همه گاردها. قابل فراخوانی مجدد از سرویس زمان‌بندی.
        /// </summary>
        public List<string> RepairShiftManagers(ShiftSolution solution)
        {
            RunShiftManagerRepairPasses(solution);
            return CollectManagerMixWarnings(solution);
        }

        /// <summary>
        /// ترمیم قطعی ترکیب مسئول — قابل فراخوانی از ShiftManagerMixGuard.
        /// </summary>
        public void EnforceShiftManagerMix(ShiftSolution solution)
        {
            PlaceManagerSkeleton(solution);
            RunShiftManagerRepairPasses(solution);
        }

        /// <summary>
        /// فاز ۱ — لایهٔ محافظت‌شدهٔ مسئول: Night سپس Evening، با قوانین سخت استراحت/متوالی.
        /// </summary>
        public void BuildReservedManagerSkeleton(ShiftSolution solution)
        {
            solution.ClearLockedSkeletonAssignments();
            PlaceManagerLayer(solution, strictPhase: true, lockAssignments: true);
        }

        /// <summary>
        /// ترمیم mix بدون پاک‌کردن پرچم اسکلت موجود؛ انتساب‌های جدید مسئول هم اسکلت می‌شوند.
        /// </summary>
        public void RepairManagerMix(ShiftSolution solution) =>
            PlaceManagerLayer(solution, strictPhase: false, lockAssignments: true);

        /// <summary>
        /// فاز ۱ زمان‌بندی سلسله‌مراتبی: برای هر اسلات Evening/Night اول مسئول سطح‌۱ و بقیهٔ مسئول‌ها
        /// گذاشته می‌شوند؛ ظرفیت باقی‌مانده بعداً با نیروی عادی پر می‌شود.
        /// </summary>
        public void PlaceManagerSkeleton(ShiftSolution solution) =>
            RepairManagerMix(solution);

        private void PlaceManagerLayer(ShiftSolution solution, bool strictPhase, bool lockAssignments)
        {
            var dates = GetDateRange().ToList();
            var shifts = _constraints.ShiftRequirements
                .Where(ShiftManagerRules.RequiresAnyManager)
                .OrderBy(s => s.ShiftLabel == ShiftLabel.Night ? 0
                    : s.ShiftLabel == ShiftLabel.Evening ? 1 : 2)
                .ThenBy(s => s.ShiftId)
                .ToList();

            foreach (var shiftReq in shifts)
            {
                foreach (var date in dates)
                {
                    if (!SlotHasCoverageDemand(shiftReq, date))
                    {
                        continue;
                    }

                    EnsureShiftManagerMixForSlot(
                        solution, shiftReq, date, strictPhase: strictPhase, markSkeleton: lockAssignments);
                    if (lockAssignments && IsSlotManagerMixSatisfied(solution, shiftReq, date))
                    {
                        SkeletonAssignmentGuard.LockSlotManagerAssignments(
                            solution, _constraints, shiftReq, date);
                    }
                }
            }

            RestoreDeficitNightQuotas(solution);
        }

        private bool SlotHasCoverageDemand(ShiftRequirement shiftReq, DateTime date)
        {
            var holiday = _constraints.IsHoliday(date);
            return shiftReq.SpecialtyRequirements.Any(s => s.ForDay(holiday).RequiredTotalCount > 0);
        }

        private bool SlotHasRoomForAnotherManager(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date)
        {
            var holiday = _constraints.IsHoliday(date);
            foreach (var specialtyReq in shiftReq.SpecialtyRequirements)
            {
                var needed = specialtyReq.ForDay(holiday).RequiredTotalCount;
                if (needed <= 0)
                {
                    continue;
                }

                var current = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                    .Count(a => !a.IsOnCall && GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId);
                if (current < needed)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryAddManagerToSlot(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            bool strictPhase,
            bool markSkeleton)
        {
            var specialtyId = shiftReq.SpecialtyRequirements
                .OrderByDescending(s => s.ForDay(_constraints.IsHoliday(date)).RequiredTotalCount)
                .Select(s => s.SpecialtyId)
                .FirstOrDefault();
            if (specialtyId == 0 && shiftReq.SpecialtyRequirements.Count > 0)
            {
                specialtyId = shiftReq.SpecialtyRequirements[0].SpecialtyId;
            }

            foreach (var candidate in RankManagerCandidatePool(
                         solution, shiftReq, date, needLevel1, specialtyId, UserGender.Female, genderLocked: false, strictPhase))
            {
                var backup = solution.Clone();
                if (!TryClearAdjacencyForManagerInstall(solution, candidate, date, shiftReq.ShiftLabel))
                {
                    RestoreSolutionFromQuotaBackup(solution, backup);
                    continue;
                }

                if (!IsUserAvailableForManagerInstall(
                        candidate, date, shiftReq.ShiftLabel, solution,
                        ignoreSameDayAssignments: true,
                        relaxNightSpacing: !strictPhase,
                        mandatoryInstall: !strictPhase))
                {
                    RestoreSolutionFromQuotaBackup(solution, backup);
                    continue;
                }

                if (!SlotHasRoomForAnotherManager(solution, shiftReq, date)
                    && !solution.HasAssignment(candidate.UserId, shiftReq.ShiftId, date))
                {
                    RestoreSolutionFromQuotaBackup(solution, backup);
                    continue;
                }

                AddShiftAssignmentForManagerInstall(solution, candidate, shiftReq, date, markSkeleton);
                return true;
            }

            return false;
        }

        /// <summary>
        /// تکمیل اجباری سهمیه شب پس از تعادل با مسئول شیفت — تا هر دو قید برقرار شوند.
        /// </summary>
        public void EnforceMandatoryNightQuotasUntilSatisfied(ShiftSolution solution)
        {
            for (var round = 0; round < 8; round++)
            {
                if (GetExactNightQuotaViolations(solution).Count == 0
                    && !HasUnmetManagerMix(solution))
                {
                    return;
                }

                ExactNightQuotaGuard.EnforceMandatoryMinimums(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                RestoreDeficitNightQuotas(solution);
            }
        }

        /// <summary>
        /// حلقه نهایی متقارن: سهمیه شب و ترکیب مسئول هر دو باید برقرار باشند؛
        /// هرگز یکی را به قیمت دیگری رها نمی‌کند.
        /// </summary>
        public void StabilizeManagerMixAndNightQuotas(ShiftSolution solution)
        {
            for (var round = 0; round < 12; round++)
            {
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RestoreDeficitNightQuotas(solution);

                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0)
                {
                    return;
                }
            }

            for (var round = 0; round < 8; round++)
            {
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                RestoreDeficitNightQuotas(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);

                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0)
                {
                    return;
                }
            }

            for (var round = 0; round < 6; round++)
            {
                if (GetExactNightQuotaViolations(solution).Count == 0 && !HasUnmetManagerMix(solution))
                {
                    return;
                }

                ExactNightQuotaGuard.Enforce(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                RestoreDeficitNightQuotas(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
            }
        }

        /// <summary>
        /// پس از جابجایی برای مسئول شیفت، سهمیه شب کاربرانی که زیر حداقل مانده‌اند را فوری جبران می‌کند.
        /// </summary>
        private void RestoreDeficitNightQuotas(ShiftSolution solution)
        {
            var progress = true;
            for (var pass = 0; pass < 4 && progress; pass++)
            {
                progress = false;
                foreach (var user in _constraints.UserConstraints.Where(u => u.ExactNightShiftCount.HasValue))
                {
                    var nights = solution.GetUserAllAssignments(user.UserId)
                        .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                    if (nights >= user.ExactNightShiftCount.Value)
                    {
                        continue;
                    }

                    var before = nights;
                    ExactNightQuotaGuard.EnforceExactNightQuotaForUser(solution, _constraints, user);
                    var after = solution.GetUserAllAssignments(user.UserId)
                        .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                    if (after > before)
                    {
                        progress = true;
                    }
                }
            }

            if (GetExactNightQuotaViolations(solution).Count > 0)
            {
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
            }
        }

        /// <summary>
        /// پس از جابجایی مسئول: سهمیه شب همهٔ کاربران (از جمله مسئول تازه‌نصب‌شده) باید برقرار بماند.
        /// </summary>
        private bool AllExactNightQuotasMet(ShiftSolution solution)
        {
            foreach (var user in _constraints.UserConstraints.Where(u => u.ExactNightShiftCount.HasValue))
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (nights < user.ExactNightShiftCount.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private bool RestoreAnyDroppedNightQuotas(ShiftSolution solution)
        {
            foreach (var user in _constraints.UserConstraints.Where(u => u.ExactNightShiftCount.HasValue))
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Count(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall);
                if (nights < user.ExactNightShiftCount.Value)
                {
                    ExactNightQuotaGuard.EnforceExactNightQuotaForUser(solution, _constraints, user);
                }
            }

            if (AllExactNightQuotasMet(solution))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// حلقهٔ نهایی محدود: پوشش → مسئول → سهمیه → مجاورت → سقف روز متوالی.
        /// پوشش و مسئول شیفت بعد از strip دوباره اعمال می‌شوند تا جای خالی و mix باقی نماند.
        /// </summary>
        private void FinalizeMandatoryConstraints(ShiftSolution solution)
        {
            for (var round = 0; round < 5; round++)
            {
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                ShiftCoverageGuard.FillRemainingAfterForceApply(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
                MaxConsecutiveWorkdayGuard.Enforce(solution, _constraints, RepairManagerMix);
                ApprovedRequestGuard.ForceApply(solution, _constraints);

                if (IsMandatoryStable(solution))
                {
                    FinishWithCoverageManagersAndQuotas(solution);
                    return;
                }
            }

            ApprovedRequestGuard.ForceApply(solution, _constraints);
            MaxConsecutiveWorkdayGuard.Enforce(solution, _constraints, RepairManagerMix);
            FinishWithCoverageManagersAndQuotas(solution);
        }

        /// <summary>
        /// پوشش و مسئول شیفت بعد از سهمیه/strip.
        /// ForceApply درخواست ON را برمی‌گرداند؛ سپس فقط جفت‌های غیرِON تنظیمات پاک می‌شوند
        /// و در صورت نیاز مسئول/سهمیه دوباره با رعایت توالی ترمیم می‌شوند.
        /// </summary>
        private void FinishWithCoverageManagersAndQuotas(ShiftSolution solution)
        {
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
            DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
            ShiftCoverageGuard.FillRemainingAfterForceApply(solution, _constraints);
            RunShiftManagerRepairPasses(solution);
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
            if (HasUnmetManagerMix(solution))
            {
                RunShiftManagerRepairPasses(solution);
            }

            if (!AreExactNightQuotasSatisfied(solution, out _))
            {
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
            }

            SealApprovedRequestsThenAdjacency(solution);
            if (HasUnmetManagerMix(solution))
            {
                RunShiftManagerRepairPasses(solution);
                ShiftCoverageGuard.FillRemainingAfterForceApply(solution, _constraints);
            }

            if (!AreExactNightQuotasSatisfied(solution, out _))
            {
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
            }

            SealApprovedRequestsThenAdjacency(solution);
            PlaceManagerSkeleton(solution);
            MaxConsecutiveWorkdayGuard.Enforce(solution, _constraints, RepairManagerMix);
            if (HasUnmetManagerMix(solution))
            {
                RepairManagerMix(solution);
            }

            SealApprovedRequestsThenAdjacency(solution);
        }

        private void SealApprovedRequestsThenAdjacency(ShiftSolution solution)
        {
            ApprovedRequestGuard.ForceApply(solution, _constraints);
            RepairManagerMix(solution);
            AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
        }

        /// <summary>
        /// آخرین تضمین mix مسئول: ترمیم تهاجمی + علامت‌گذاری اسکلت روی هر اسلات Evening/Night که mix برقرار است.
        /// </summary>
        private void FinalizeManagerMixMandatory(ShiftSolution solution)
        {
            for (var round = 0; round < 10; round++)
            {
                RefreshManagerSkeletonFlags(solution);

                if (!HasUnmetManagerMix(solution))
                {
                    return;
                }

                foreach (var date in OrderDatesForManagerRepair(solution))
                {
                    foreach (var shiftReq in _constraints.ShiftRequirements
                                 .Where(ShiftManagerRules.RequiresAnyManager)
                                 .OrderBy(s => s.ShiftLabel == ShiftLabel.Night ? 0 : 1))
                    {
                        if (!SlotHasCoverageDemand(shiftReq, date))
                        {
                            continue;
                        }

                        var assignees = GetRegularAssignees(solution, shiftReq, date);
                        if (assignees.Count == 0)
                        {
                            continue;
                        }

                        EnsureShiftManagerMixForSlot(
                            solution, shiftReq, date, strictPhase: false, markSkeleton: true);
                        if (IsSlotManagerMixSatisfied(solution, shiftReq, date))
                        {
                            SkeletonAssignmentGuard.LockSlotManagerAssignments(
                                solution, _constraints, shiftReq, date);
                        }
                    }
                }

                ApprovedRequestGuard.ForceApply(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                RepairManagerMix(solution);
                RefreshManagerSkeletonFlags(solution);

                if (round == 9 && HasUnmetManagerMix(solution))
                {
                    BuildReservedManagerSkeleton(solution);
                    ApprovedRequestGuard.ForceApply(solution, _constraints);
                    AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                    RefreshManagerSkeletonFlags(solution);
                }
            }
        }

        private void RefreshManagerSkeletonFlags(ShiftSolution solution) =>
            solution.SyncSkeletonFlagsFromLockSet();

        private bool IsMandatoryStable(ShiftSolution solution) =>
            AreExactNightQuotasSatisfied(solution, out _)
            && !HasUnmetManagerMix(solution)
            && AdjacentShiftRestGuard.GetViolations(solution, _constraints).Count == 0
            && DailyDuplicateAssignmentGuard.GetViolations(solution, _constraints).Count == 0;

        /// <summary>
        /// آخرین مرحله: ترمیم مسئول → تکمیل سهمیه؛ هیچ ترمیم مسئول بعد از پر شدن سهمیه اجرا نمی‌شود.
        /// </summary>
        private void FinalizeNightQuotasAndManagerMix(ShiftSolution solution)
        {
            for (var round = 0; round < 2; round++)
            {
                RunShiftManagerRepairPasses(solution);
                ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
                ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, _constraints);

                if (AreExactNightQuotasSatisfied(solution, out _) && !HasUnmetManagerMix(solution))
                {
                    return;
                }
            }

            // آخرین حرف مطلق: فقط سهمیه شب — بدون RunShiftManagerRepairPasses بعد از آن
            ExactNightQuotaGuard.ForceSatisfyAllDeficits(solution, _constraints);
            ExactNightQuotaGuard.GlobalRebalanceNightQuotas(solution, _constraints);
        }

        private static void RestoreSolutionFromQuotaBackup(ShiftSolution target, ShiftSolution backup)
        {
            target.Assignments.Clear();
            foreach (var assignment in backup.Assignments.Values)
            {
                target.AddAssignment(
                    assignment.UserId,
                    assignment.ShiftId,
                    assignment.Date,
                    assignment.ShiftLabel,
                    assignment.IsOnCall,
                    assignment.IsSkeleton);
            }

            foreach (var locked in backup.LockedSkeletonAssignments)
            {
                target.LockedSkeletonAssignments.Add(locked);
            }

            target.SyncSkeletonFlagsFromLockSet();

            target.Score = backup.Score;
        }

        /// <summary>
        /// پاس‌های متناوب: ترمیم مسئول شیفت → سهمیه شب → درخواست ON.
        /// </summary>
        private void ReconcileShiftManagersAndNightQuotas(ShiftSolution solution)
        {
            for (var round = 0; round < 5; round++)
            {
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                DailyDuplicateAssignmentGuard.StripDuplicates(solution, _constraints);
                ShiftCoverageGuard.Enforce(solution, _constraints);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RestoreDeficitNightQuotas(solution);

                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0)
                {
                    return;
                }
            }

            for (var finalize = 0; finalize < 3; finalize++)
            {
                ExactNightQuotaGuard.Enforce(solution, _constraints);
                AdjacentShiftRestGuard.StripForbiddenAdjacencies(solution, _constraints);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RunShiftManagerRepairPasses(solution);
                ApprovedRequestGuard.ForceApply(solution, _constraints);
                RestoreDeficitNightQuotas(solution);

                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0)
                {
                    break;
                }
            }
        }

        private bool IsSlotManagerMixSatisfied(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date)
        {
            var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (requiredTotal <= 0)
            {
                return false;
            }

            var assignees = GetRegularAssignees(solution, shiftReq, date);
            return assignees.Count > 0
                   && ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1);
        }

        private void UnlockManagersOnUnmetMixSlots(ShiftSolution solution)
        {
            foreach (var date in GetDateRange())
            {
                foreach (var shiftReq in _constraints.ShiftRequirements.Where(ShiftManagerRules.RequiresAnyManager))
                {
                    if (IsSlotManagerMixSatisfied(solution, shiftReq, date))
                    {
                        continue;
                    }

                    foreach (var assignment in solution.GetShiftAssignments(shiftReq.ShiftId, date)
                                 .Where(a => !a.IsOnCall))
                    {
                        var user = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                        if (user == null || !ShiftManagerRules.IsManager(user))
                        {
                            continue;
                        }

                        if (ShiftManagerRules.IsLevel1(user))
                        {
                            continue;
                        }

                        solution.UnlockSkeletonAssignment(
                            assignment.UserId, assignment.ShiftId, assignment.Date);
                    }
                }
            }

            solution.SyncSkeletonFlagsFromLockSet();
        }

        private bool HasUnmetManagerMix(ShiftSolution solution) =>
            ShiftManagerMixGuard.GetViolations(solution, _constraints).Count > 0;

        private List<string> CollectManagerMixWarnings(ShiftSolution solution) =>
            ShiftManagerMixGuard.GetViolations(solution, _constraints);

        private void RunShiftManagerRepairPasses(ShiftSolution solution)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                var progress = false;
                foreach (var date in OrderDatesForManagerRepair(solution))
                {
                    foreach (var shiftReq in _constraints.ShiftRequirements
                                 .OrderByDescending(s => ShiftManagerRules.GetRequirement(s).RequiredTotal))
                    {
                        if (EnsureShiftManagerMixForSlot(solution, shiftReq, date, strictPhase: false, markSkeleton: false))
                        {
                            progress = true;
                        }
                    }
                }

                if (!progress)
                {
                    break;
                }
            }

            RestoreDeficitNightQuotas(solution);
        }

        private IEnumerable<DateTime> OrderDatesForManagerRepair(ShiftSolution solution)
        {
            var dates = GetDateRange().ToList();
            return dates
                .OrderByDescending(date =>
                {
                    var broken = 0;
                    foreach (var shiftReq in _constraints.ShiftRequirements)
                    {
                        var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
                        if (requiredTotal <= 0)
                        {
                            continue;
                        }

                        var assignees = GetRegularAssignees(solution, shiftReq, date);
                        if (assignees.Count > 0 &&
                            !ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1))
                        {
                            broken++;
                        }
                    }

                    return broken;
                })
                .ThenBy(d => d);
        }

        private List<string> EnsureShiftManagers(ShiftSolution solution)
        {
            RunShiftManagerRepairPasses(solution);
            return CollectManagerMixWarnings(solution);
        }

        private List<UserConstraint> GetRegularAssignees(ShiftSolution solution, ShiftRequirement shiftReq, DateTime date) =>
            solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall)
                .Select(a => _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId))
                .Where(u => u != null)
                .Cast<UserConstraint>()
                .ToList();

        /// <returns>true if at least one replacement/swap succeeded this call.</returns>
        private bool EnsureShiftManagerMixForSlot(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool strictPhase = false,
            bool markSkeleton = false)
        {
            var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (requiredTotal <= 0)
            {
                return false;
            }

            var maxAttempts = Math.Max(8, requiredTotal * 3 + minLevel1 * 2);
            var progress = false;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var assignees = GetRegularAssignees(solution, shiftReq, date);
                if (ShiftManagerRules.IsSatisfied(assignees, requiredTotal, minLevel1))
                {
                    break;
                }

                var managerCount = assignees.Count(ShiftManagerRules.IsManager);
                var level1Count = assignees.Count(ShiftManagerRules.IsLevel1);
                var needLevel1 = level1Count < Math.Min(minLevel1, requiredTotal);
                var needAnyManager = managerCount < requiredTotal;
                var onlyNeedLevel1 = needLevel1 && !needAnyManager;

                if (!needLevel1 && !needAnyManager)
                {
                    break;
                }

                var added = assignees.Count == 0 || SlotHasRoomForAnotherManager(solution, shiftReq, date)
                    ? TryAddManagerToSlot(solution, shiftReq, date, needLevel1, strictPhase, markSkeleton)
                    : false;

                var replaced = added
                    || TryReplaceForManagerMix(
                    solution, shiftReq, date, needLevel1, needAnyManager, onlyNeedLevel1, allowQuotaBypass: false, markSkeleton: markSkeleton)
                    || TryReplaceForManagerMix(
                        solution, shiftReq, date, needLevel1, needAnyManager, onlyNeedLevel1, allowQuotaBypass: true, markSkeleton: markSkeleton);

                if (replaced)
                {
                    progress = true;
                }
                else
                {
                    break;
                }
            }

            var finalAssignees = GetRegularAssignees(solution, shiftReq, date);
            if (finalAssignees.Count > 0 && !ShiftManagerRules.IsSatisfied(finalAssignees, requiredTotal, minLevel1))
            {
                if (ForceInstallLevel1ManagerForSlot(solution, shiftReq, date, markSkeleton))
                {
                    progress = true;
                }
            }

            return progress;
        }

        private bool ForceInstallLevel1ManagerForSlot(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool markSkeleton = true)
        {
            var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
            var specialtyId = shiftReq.SpecialtyRequirements
                .OrderByDescending(s => s.ForDay(_constraints.IsHoliday(date)).RequiredTotalCount)
                .Select(s => s.SpecialtyId)
                .FirstOrDefault();

            var currentAssignees = GetRegularAssignees(solution, shiftReq, date);
            var currentL1 = currentAssignees.Count(ShiftManagerRules.IsLevel1);
            if (currentL1 >= Math.Min(minLevel1, requiredTotal) && currentAssignees.Count >= requiredTotal)
            {
                return false;
            }

            var candidates = _constraints.UserConstraints
                .Where(u => u.IsActive && ShiftManagerRules.IsLevel1(u))
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, shiftReq.ShiftLabel))
                .Where(u => !u.UnavailableDates.Any(d => d.Date == date.Date))
                .Where(u => !u.UnavailableShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel == shiftReq.ShiftLabel))
                .Where(u => !u.RequiredShiftSlots.Any(s => s.Date.Date == date.Date && s.ShiftLabel != shiftReq.ShiftLabel))
                .OrderBy(u => solution.GetUserAllAssignments(u.UserId).Count(a => a.ShiftLabel == shiftReq.ShiftLabel && !a.IsOnCall))
                .ToList();

            if (candidates.Count == 0)
            {
                var labelName = shiftReq.ShiftLabel == ShiftLabel.Night ? "شب" : shiftReq.ShiftLabel == ShiftLabel.Evening ? "عصر" : "صبح";
                throw new InvalidOperationException(
                    $"امکان تخصیص مسئول سطح-۱ برای شیفت {labelName} در تاریخ {date:yyyy-MM-dd} وجود ندارد. " +
                    "تمامی مسئولان سطح-۱ در این تاریخ دارای مرخصی، شیفت اجباری دیگر، یا عدم دسترسی ساختاری هستند.");
            }

            var selected = candidates.FirstOrDefault(c => !solution.HasAssignment(c.UserId, shiftReq.ShiftId, date)) ?? candidates.First();

            // پاک‌سازی تداخل شیفت همان روز اگر درخواست تأییدشده نباشد
            RemoveConflictingDailyAssignments(solution, selected.UserId, date, shiftReq.ShiftId, shiftReq.ShiftLabel);

            // پاک‌سازی تداخل مجاور غیر درخواستی
            TryClearAdjacencyForManagerInstall(solution, selected, date, shiftReq.ShiftLabel);

            // اگر شیفت پر است، یک فرد غیر مسئول سطح-۱ حذف شود تا جا باز شود
            MakeRoomInShift(solution, shiftReq, date, selected);

            // اضافه کردن مسئول سطح-۱
            solution.AddAssignment(selected.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall: false, isSkeleton: markSkeleton);
            if (markSkeleton)
            {
                SkeletonAssignmentGuard.LockSlotManagerAssignments(solution, _constraints, shiftReq, date);
            }

            return true;
        }

        private bool TryReplaceForManagerMix(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            bool needAnyManager,
            bool onlyNeedLevel1,
            bool allowQuotaBypass,
            bool markSkeleton = false)
        {
            var regulars = solution.GetShiftAssignments(shiftReq.ShiftId, date)
                .Where(a => !a.IsOnCall)
                .ToList();

            var occupants = regulars
                .Where(a => !solution.IsLockedSkeleton(a.UserId, a.ShiftId, a.Date))
                .Where(a => !a.IsSkeleton || (needLevel1 && !_constraints.UserConstraints.Any(u => u.UserId == a.UserId && ShiftManagerRules.IsLevel1(u))))
                .Where(a => !IsProtectedAssignment(solution, a, forManagerInstall: allowQuotaBypass) || (needLevel1 && !_constraints.UserConstraints.Any(u => u.UserId == a.UserId && ShiftManagerRules.IsLevel1(u))))
                .Select(a => new
                {
                    Assignment = a,
                    User = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId)
                })
                .Where(x => x.User != null)
                .OrderBy(x => CanDonateNightForManagerReplace(solution, x.User!, x.Assignment) ? 0 : 1)
                .ThenBy(x =>
                {
                    var level = ShiftManagerRules.EffectiveLevel(x.User!);
                    if (onlyNeedLevel1)
                    {
                        if (level == ShiftManagerRules.Level2)
                        {
                            return 0;
                        }

                        if (level == 0)
                        {
                            return 1;
                        }

                        return 2;
                    }

                    if (level == 0)
                    {
                        return 0;
                    }

                    if (needLevel1 && level == ShiftManagerRules.Level2)
                    {
                        return 1;
                    }

                    return 2;
                })
                .ToList();

            foreach (var occupant in occupants)
            {
                if (needLevel1 && ShiftManagerRules.IsLevel1(occupant.User!))
                {
                    continue;
                }

                if (!needLevel1 && needAnyManager && ShiftManagerRules.IsManager(occupant.User!))
                {
                    continue;
                }

                var backup = solution.Clone();
                if (TryInstallShiftManager(solution, shiftReq, date, needLevel1, occupant.Assignment, occupant.User!, markSkeleton))
                {
                    if (!RestoreAnyDroppedNightQuotas(solution))
                    {
                        RestoreSolutionFromQuotaBackup(solution, backup);
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// L1 را از شب غیرمجاور (فاصله ≥ ۲) به شب هدف منتقل می‌کند تا قوانین شب→شب نشکند.
        /// </summary>
        private bool TrySwapManagerFromNonAdjacentNight(
            ShiftSolution solution,
            ShiftRequirement targetShift,
            DateTime date,
            bool needLevel1,
            SaShiftAssignment occupantAssignment,
            UserConstraint occupantUser,
            bool markSkeleton = false)
        {
            if (targetShift.ShiftLabel != ShiftLabel.Night || !needLevel1)
            {
                return false;
            }

            if (IsProtectedAssignment(solution, occupantAssignment, forManagerInstall: true))
            {
                return false;
            }

            var specialtyId = GetUserSpecialty(occupantAssignment.UserId);
            var minGap = _constraints.UserConstraints
                .Where(u => ShiftManagerRules.IsLevel1(u))
                .Select(u => Math.Max(1, u.MinDaysBetweenNightShifts))
                .DefaultIfEmpty(1)
                .Max();

            foreach (var l1 in _constraints.UserConstraints
                         .Where(u => u.IsActive && ShiftManagerRules.IsLevel1(u) && u.SpecialtyId == specialtyId)
                         .OrderBy(u => solution.GetUserAllAssignments(u.UserId).Count))
            {
                foreach (var donorNight in solution.GetUserAllAssignments(l1.UserId)
                             .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                             .OrderByDescending(a => Math.Abs((a.Date.Date - date.Date).Days)))
                {
                    var gap = Math.Abs((donorNight.Date.Date - date.Date).Days);
                    if (gap < minGap + 1)
                    {
                        continue;
                    }

                    if (IsProtectedAssignment(solution, donorNight, forManagerInstall: true))
                    {
                        continue;
                    }

                    if (!IsUserAvailableForManagerInstall(
                            l1, date, ShiftLabel.Night, solution, donorNight.ShiftId,
                            ignoreSameDayAssignments: true, relaxNightSpacing: true))
                    {
                        continue;
                    }

                    if (!IsUserAvailableForManagerInstall(
                            occupantUser, donorNight.Date, ShiftLabel.Night, solution, occupantAssignment.ShiftId,
                            ignoreSameDayAssignments: true, relaxNightSpacing: true))
                    {
                        continue;
                    }

                    solution.RemoveAssignment(
                        occupantAssignment.UserId,
                        occupantAssignment.ShiftId,
                        occupantAssignment.Date,
                        force: true);
                    solution.RemoveAssignment(l1.UserId, donorNight.ShiftId, donorNight.Date, force: true);
                    AddShiftAssignmentForManagerInstall(solution, l1, targetShift, date, markSkeleton);
                    AddShiftAssignmentForManagerInstall(solution, occupantUser, targetShift, donorNight.Date);

                    if (occupantUser.HasExactNightQuota)
                    {
                        ExactNightQuotaGuard.EnforceExactNightQuotaForUser(solution, _constraints, occupantUser);
                    }

                    if (l1.HasExactNightQuota)
                    {
                        ExactNightQuotaGuard.EnforceExactNightQuotaForUser(solution, _constraints, l1);
                    }

                    return true;
                }
            }

            return false;
        }

        private bool CanDonateNightForManagerReplace(
            ShiftSolution solution,
            UserConstraint user,
            SaShiftAssignment assignment) =>
            assignment.ShiftLabel != ShiftLabel.Night
            || assignment.IsOnCall
            || ExactNightQuotaGuard.CanDonateNight(solution, _constraints, user, assignment);

        private bool TryInstallShiftManager(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            SaShiftAssignment occupantAssignment,
            UserConstraint occupantUser,
            bool markSkeleton = false)
        {
            if (TryDirectManagerInstall(solution, shiftReq, date, needLevel1, occupantAssignment, occupantUser, markSkeleton))
            {
                return true;
            }

            if (TrySwapManagerFromOtherShiftSameDay(
                    solution, shiftReq, date, needLevel1, occupantAssignment, occupantUser, markSkeleton))
            {
                return true;
            }

            return TrySwapManagerFromNonAdjacentNight(
                solution, shiftReq, date, needLevel1, occupantAssignment, occupantUser, markSkeleton);
        }

        private bool TryDirectManagerInstall(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            SaShiftAssignment occupantAssignment,
            UserConstraint occupantUser,
            bool markSkeleton = false)
        {
            var occupantSpecialty = GetUserSpecialty(occupantAssignment.UserId);
            var occupantGender = GetUserGender(occupantAssignment.UserId);
            var genderLocked = IsGenderLockedShift(shiftReq, date, occupantSpecialty);

            foreach (var candidate in RankManagerCandidatePool(
                         solution, shiftReq, date, needLevel1, occupantSpecialty, occupantGender, genderLocked)
                         .Where(u => u.UserId != occupantUser.UserId))
            {
                var backup = solution.Clone();
                if (!TryClearAdjacencyForManagerInstall(solution, candidate, date, shiftReq.ShiftLabel))
                {
                    RestoreSolutionFromQuotaBackup(solution, backup);
                    continue;
                }

                if (!IsUserAvailableForManagerInstall(
                        candidate, date, shiftReq.ShiftLabel, solution, occupantAssignment.ShiftId,
                        ignoreSameDayAssignments: true, relaxNightSpacing: true, mandatoryInstall: true))
                {
                    RestoreSolutionFromQuotaBackup(solution, backup);
                    continue;
                }

                solution.RemoveAssignment(
                    occupantAssignment.UserId,
                    occupantAssignment.ShiftId,
                    occupantAssignment.Date,
                    force: true);
                AddShiftAssignmentForManagerInstall(solution, candidate, shiftReq, date, markSkeleton);
                return true;
            }

            return false;
        }

        /// <summary>
        /// با MaxShiftsPerDay=1: مسئول L1 از شیفت دیگر همان روز (مثلاً صبح) به عصر/شب منتقل می‌شود و جای خالی با فرد آزاد پر می‌شود.
        /// </summary>
        private bool TrySwapManagerFromOtherShiftSameDay(
            ShiftSolution solution,
            ShiftRequirement targetShift,
            DateTime date,
            bool needLevel1,
            SaShiftAssignment occupantAssignment,
            UserConstraint occupantUser,
            bool markSkeleton = false)
        {
            var occupantSpecialty = GetUserSpecialty(occupantAssignment.UserId);
            var occupantGender = GetUserGender(occupantAssignment.UserId);

            var donorEntries = solution.Assignments.Values
                .Where(a => a.Date.Date == date.Date &&
                            a.ShiftId != targetShift.ShiftId &&
                            !a.IsOnCall)
                .Select(a => new
                {
                    Assignment = a,
                    User = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == a.UserId),
                    ShiftReq = _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftId == a.ShiftId)
                })
                .Where(x => x.User != null && x.ShiftReq != null)
                .Where(x => ShiftManagerRules.IsManager(x.User!))
                .Where(x => !needLevel1 || ShiftManagerRules.IsLevel1(x.User!))
                .Where(x => !solution.IsLockedSkeleton(x.Assignment.UserId, x.Assignment.ShiftId, x.Assignment.Date))
                .Where(x => !x.Assignment.IsSkeleton)
                .Where(x => !IsProtectedAssignment(solution, x.Assignment, forManagerInstall: true))
                .Where(x => IsUserAvailableForManagerInstall(
                    x.User!, date, targetShift.ShiftLabel, solution, x.Assignment.ShiftId, ignoreSameDayAssignments: true)
                    || IsUserAvailableForManagerInstall(
                        x.User!, date, targetShift.ShiftLabel, solution, x.Assignment.ShiftId,
                        ignoreSameDayAssignments: true, relaxNightSpacing: true)
                    || IsUserAvailableForManagerInstall(
                        x.User!, date, targetShift.ShiftLabel, solution, x.Assignment.ShiftId,
                        ignoreSameDayAssignments: true, mandatoryInstall: true))
                .OrderBy(x => ShiftManagerRules.GetRequirement(x.ShiftReq!).RequiredTotal)
                .ThenBy(x => WouldDonorRemovalBreakManagerMix(solution, x.ShiftReq!, date, x.User!) ? 1 : 0)
                .ThenByDescending(x => needLevel1 && ShiftManagerRules.IsLevel1(x.User!))
                .ThenByDescending(x => ShiftManagerRules.IsLevel1(x.User!))
                .ThenBy(x => solution.GetUserAllAssignments(x.User!.UserId).Count)
                .ToList();

            foreach (var donor in donorEntries)
            {
                var donorShiftReq = donor.ShiftReq!;
                var donorGenderLocked = IsGenderLockedShift(donorShiftReq, date, occupantSpecialty);
                var donorBreaksManagerMix = WouldDonorRemovalBreakManagerMix(
                    solution, donorShiftReq, date, donor.User!);

                UserConstraint? backfill = null;

                if (donorBreaksManagerMix)
                {
                    var remaining = GetRegularAssignees(solution, donorShiftReq, date)
                        .Where(u => u.UserId != donor.User!.UserId)
                        .ToList();
                    var (_, donorMinL1) = ShiftManagerRules.GetRequirement(donorShiftReq);
                    var needDonorL1 = remaining.Count(ShiftManagerRules.IsLevel1) < donorMinL1;

                    backfill = RankManagerCandidates(
                            solution,
                            donorShiftReq,
                            date,
                            needDonorL1,
                            occupantSpecialty,
                            donorGenderLocked ? GetUserGender(donor.Assignment.UserId) : occupantGender,
                            donorGenderLocked)
                        .FirstOrDefault()
                        ?? RankBackfillCandidates(
                            solution,
                            donorShiftReq,
                            date,
                            occupantSpecialty,
                            donorGenderLocked ? GetUserGender(donor.Assignment.UserId) : occupantGender,
                            donorGenderLocked)
                            .FirstOrDefault();
                }
                else if (IsUserAvailableForManagerInstall(
                        occupantUser, date, donorShiftReq.ShiftLabel, solution, occupantAssignment.ShiftId) &&
                    (!donorGenderLocked || occupantUser.Gender == GetUserGender(donor.Assignment.UserId)))
                {
                    backfill = occupantUser;
                }
                else
                {
                    backfill = RankBackfillCandidates(
                            solution,
                            donorShiftReq,
                            date,
                            occupantSpecialty,
                            donorGenderLocked ? GetUserGender(donor.Assignment.UserId) : occupantGender,
                            donorGenderLocked)
                        .FirstOrDefault();
                }

                if (backfill == null)
                {
                    continue;
                }

                solution.RemoveAssignment(
                    occupantAssignment.UserId,
                    occupantAssignment.ShiftId,
                    occupantAssignment.Date,
                    force: true);
                solution.RemoveAssignment(
                    donor.Assignment.UserId,
                    donor.Assignment.ShiftId,
                    donor.Assignment.Date,
                    force: true);

                AddShiftAssignmentForManagerInstall(solution, donor.User!, targetShift, date, markSkeleton);

                if (backfill.UserId != donor.User!.UserId)
                {
                    AddShiftAssignmentForManagerInstall(solution, backfill, donorShiftReq, date);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// کاندیداهای مسئول بدون چک توالی — تداخل غیرمحافظت‌شده قبل از نصب پاک می‌شود.
        /// </summary>
        private IEnumerable<UserConstraint> RankManagerCandidatePool(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            int specialtyId,
            UserGender occupantGender,
            bool genderLocked,
            bool strictPhase = false) =>
            _constraints.UserConstraints
                .Where(u => u.IsActive && ShiftManagerRules.IsManager(u))
                .Where(u => !needLevel1 || ShiftManagerRules.IsLevel1(u))
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => !genderLocked || u.Gender == occupantGender)
                .Where(u => !HasConflictingApprovedRequiredOnDate(u, date, shiftReq.ShiftLabel))
                .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, shiftReq.ShiftLabel))
                .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
                .OrderByDescending(u => ShiftManagerRules.IsLevel1(u))
                .ThenBy(u => CountUserLabelAssignments(solution, u.UserId, shiftReq.ShiftLabel))
                .ThenBy(u => HasRecentSameLabel(solution, u.UserId, date, shiftReq.ShiftLabel) ? 1 : 0)
                .ThenBy(u => AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(u.UserId), date, shiftReq.ShiftLabel, _constraints)
                    ? 1
                    : 0)
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(u => u.UserId);

        private static int CountUserLabelAssignments(ShiftSolution solution, int userId, ShiftLabel label) =>
            solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

        private static bool HasRecentSameLabel(
            ShiftSolution solution,
            int userId,
            DateTime date,
            ShiftLabel label)
        {
            if (label != ShiftLabel.Night)
            {
                return false;
            }

            return solution.GetUserAllAssignments(userId)
                .Any(a => !a.IsOnCall
                          && a.ShiftLabel == ShiftLabel.Night
                          && Math.Abs((a.Date.Date - date.Date).Days) == 1);
        }

        /// <summary>
        /// تداخل غیرِON و غیرِmix-critical را برای نصب مسئول شب/عصر پاک می‌کند.
        /// </summary>
        private bool TryClearAdjacencyForManagerInstall(
            ShiftSolution solution,
            UserConstraint user,
            DateTime date,
            ShiftLabel label)
        {
            if (label == ShiftLabel.Night)
            {
                if (!_constraints.HardRules.AllowNightShiftAfterNightShift)
                {
                    if (!TryClearUserAssignmentsOnDate(solution, user, date.Date.AddDays(-1), ShiftLabel.Night))
                    {
                        return false;
                    }
                }

                foreach (var assignment in solution.GetUserAssignments(user.UserId, date.Date.AddDays(1)).ToList())
                {
                    if (!_constraints.HardRules.IsForbiddenOnDayAfterNight(assignment.ShiftLabel))
                    {
                        continue;
                    }

                    if (!TryClearAssignment(solution, user, assignment))
                    {
                        return false;
                    }
                }

                foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                             .Where(a => a.ShiftLabel == ShiftLabel.Evening)
                             .ToList())
                {
                    if (!TryClearAssignment(solution, user, assignment))
                    {
                        return false;
                    }
                }
            }
            else if (label == ShiftLabel.Evening && !_constraints.HardRules.AllowEveningAfterNightShift)
            {
                if (!TryClearUserAssignmentsOnDate(solution, user, date.Date.AddDays(-1), ShiftLabel.Night))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryClearUserAssignmentsOnDate(
            ShiftSolution solution,
            UserConstraint user,
            DateTime date,
            ShiftLabel label)
        {
            foreach (var assignment in solution.GetUserAssignments(user.UserId, date)
                         .Where(a => a.ShiftLabel == label && !a.IsOnCall)
                         .ToList())
            {
                if (!TryClearAssignment(solution, user, assignment))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryClearAssignment(
            ShiftSolution solution,
            UserConstraint user,
            SaShiftAssignment assignment)
        {
            if (ApprovedRequestGuard.IsApprovedRequiredSlot(
                    user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
            {
                return false;
            }

            if (solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date)
                || assignment.IsSkeleton)
            {
                return false;
            }

            if (ShiftManagerRules.IsCriticalForManagerMix(_constraints, solution, assignment))
            {
                return false;
            }

            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date, force: true);
            return true;
        }

        private IEnumerable<UserConstraint> RankManagerCandidates(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            bool needLevel1,
            int specialtyId,
            UserGender occupantGender,
            bool genderLocked,
            int? ignoreShiftIdForAvailability = null,
            bool relaxNightSpacing = false,
            bool mandatoryInstall = false) =>
            _constraints.UserConstraints
                .Where(u => u.IsActive && ShiftManagerRules.IsManager(u))
                .Where(u => !needLevel1 || ShiftManagerRules.IsLevel1(u))
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => !genderLocked || u.Gender == occupantGender)
                .Where(u => !HasConflictingApprovedRequiredOnDate(u, date, shiftReq.ShiftLabel))
                .Where(u => IsUserAvailableForManagerInstall(
                    u, date, shiftReq.ShiftLabel, solution, ignoreShiftIdForAvailability,
                    ignoreSameDayAssignments: true,
                    relaxNightSpacing: relaxNightSpacing || mandatoryInstall,
                    mandatoryInstall: mandatoryInstall))
                .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
                .OrderByDescending(u => ShiftManagerRules.IsLevel1(u))
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(u => u.UserId);

        /// <summary>
        /// پر کردن جای خالی پس از انتقال مسئول — هر پرسنل واجد شرایط (نه فقط مسئول).
        /// </summary>
        private IEnumerable<UserConstraint> RankBackfillCandidates(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            int specialtyId,
            UserGender requiredGender,
            bool genderLocked,
            int? ignoreShiftIdForAvailability = null) =>
            _constraints.UserConstraints
                .Where(u => u.IsActive)
                .Where(u => u.SpecialtyId == specialtyId)
                .Where(u => !genderLocked || u.Gender == requiredGender)
                .Where(u => !HasConflictingApprovedRequiredOnDate(u, date, shiftReq.ShiftLabel))
                .Where(u => IsUserAvailableForManagerInstall(
                    u, date, shiftReq.ShiftLabel, solution, ignoreShiftIdForAvailability, ignoreSameDayAssignments: true))
                .Where(u => !solution.HasAssignment(u.UserId, shiftReq.ShiftId, date))
                .OrderBy(u => ShiftManagerRules.IsManager(u) ? 1 : 0)
                .ThenBy(u => solution.GetUserAllAssignments(u.UserId).Count)
                .ThenBy(u => u.UserId);

        private bool WouldDonorRemovalBreakManagerMix(
            ShiftSolution solution,
            ShiftRequirement shiftReq,
            DateTime date,
            UserConstraint donor)
        {
            var (requiredTotal, minLevel1) = ShiftManagerRules.GetRequirement(shiftReq);
            if (requiredTotal <= 0)
            {
                return false;
            }

            var remaining = GetRegularAssignees(solution, shiftReq, date)
                .Where(u => u.UserId != donor.UserId)
                .ToList();
            return !ShiftManagerRules.IsSatisfied(remaining, requiredTotal, minLevel1);
        }

        /// <summary>
        /// کاربر با درخواست ON تأییدشدهٔ همان روز را به شیفت ناسازگار (مثلاً عصر به‌جای صبح اجباری) منتقل نکن.
        /// </summary>
        private bool HasConflictingApprovedRequiredOnDate(
            UserConstraint user,
            DateTime date,
            ShiftLabel installLabel)
        {
            var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;
            var forbidDup = _constraints.HardRules.ForbidDuplicateDailyAssignments;

            return user.RequiredShiftSlots.Any(s =>
                s.Date.Date == date.Date &&
                s.ShiftLabel != installLabel &&
                !DailyAssignmentRules.IsValidDaySet(
                    new[] { s.ShiftLabel, installLabel }, maxPerDay, forbidDup));
        }

        private bool IsGenderLockedShift(ShiftRequirement shiftReq, DateTime date, int specialtyId)
        {
            var specialtyReq = shiftReq.SpecialtyRequirements.FirstOrDefault(r => r.SpecialtyId == specialtyId);
            var day = specialtyReq?.ForDay(_constraints.IsHoliday(date));
            return day != null &&
                   (day.Value.RequiredMaleCount > 0 || day.Value.RequiredFemaleCount > 0);
        }

        private void AddShiftAssignmentForManagerInstall(
            ShiftSolution solution,
            UserConstraint user,
            ShiftRequirement shiftReq,
            DateTime date,
            bool markSkeleton = false)
        {
            RemoveConflictingDailyAssignments(
                solution,
                user.UserId,
                date,
                shiftReq.ShiftId,
                shiftReq.ShiftLabel);
            solution.AddAssignment(
                user.UserId,
                shiftReq.ShiftId,
                date,
                shiftReq.ShiftLabel,
                isOnCall: false,
                isSkeleton: false);
        }

        /// <summary>
        /// برای نصب مسئول: مثل IsUserAvailableForShift ولی بدون سقف موظفی (الزام مسئول سخت‌تر است).
        /// ignoreShiftId: شیفت همان روز که کاربر از آن جابجا می‌شود (برای MaxShiftsPerDay=1).
        /// </summary>
        private bool IsUserAvailableForManagerInstall(
            UserConstraint user,
            DateTime date,
            ShiftLabel shiftLabel,
            ShiftSolution solution,
            int? ignoreShiftId = null,
            bool ignoreSameDayAssignments = false,
            bool relaxNightSpacing = false,
            bool mandatoryInstall = false)
        {
            if (user.UnavailableDates.Any(d => d.Date == date.Date))
            {
                return false;
            }

            if (user.UnavailableShiftSlots.Any(s =>
                    s.Date.Date == date.Date && s.ShiftLabel == shiftLabel))
            {
                return false;
            }

            var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                : 2;
            var sameDayAssignments = solution.GetUserAssignments(user.UserId, date);
            var existingLabels = ignoreSameDayAssignments
                ? Enumerable.Empty<ShiftLabel>()
                : sameDayAssignments
                    .Where(a => !ignoreShiftId.HasValue || a.ShiftId != ignoreShiftId.Value)
                    .Select(a => a.ShiftLabel);
            if (!ShiftEligibilityResolver.IsAssignmentAllowed(
                    user, existingLabels, shiftLabel, maxPerDay,
                    _constraints.HardRules.ForbidDuplicateDailyAssignments))
            {
                return false;
            }

            var ignoreSettingsAfterNight =
                ApprovedRequestGuard.IsApprovedRequiredSlot(user, date, shiftLabel);
            if (AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(user.UserId),
                    date, shiftLabel, _constraints, ignoreShiftId,
                    ignoreSettingsControlledAfterNight: ignoreSettingsAfterNight))
            {
                return false;
            }

            if (MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                    solution, _constraints, user, date))
            {
                return false;
            }

            var relaxNightGap = relaxNightSpacing || mandatoryInstall;
            if (shiftLabel == ShiftLabel.Night)
            {
                var nights = solution.GetUserAllAssignments(user.UserId)
                    .Where(a => a.ShiftLabel == ShiftLabel.Night && !a.IsOnCall)
                    .Where(a => !ignoreShiftId.HasValue ||
                                a.ShiftId != ignoreShiftId.Value ||
                                a.Date.Date != date.Date)
                    .ToList();

                if (!mandatoryInstall
                    && _constraints.HardRules.EnforceNightShiftMonthlyCap
                    && nights.Count >= user.MaxNightShiftsPerMonth)
                {
                    return false;
                }

                if (!relaxNightGap && user.MinDaysBetweenNightShifts > 0)
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

            var user = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == userId);
            foreach (var assignment in solution.GetUserAssignments(userId, date).ToList())
            {
                if (assignment.ShiftId == keepShiftId)
                {
                    continue;
                }

                if (user != null &&
                    ApprovedRequestGuard.IsApprovedRequiredSlot(
                        user, assignment.Date, assignment.ShiftLabel, assignment.ShiftId))
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
            var shiftReq = _constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == shiftLabel);
            return shiftReq != null && ShiftManagerRules.RequiresAnyManager(shiftReq);
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

            if (user.UnavailableShiftSlots.Any(s =>
                    s.Date.Date == date.Date && s.ShiftLabel == shiftLabel))
            {
                return false;
            }

            if (solution != null)
            {
                var maxPerDay = _constraints.HardRules.EnforceMaxShiftsPerDay
                    ? Math.Max(1, _constraints.GlobalConstraints.MaxShiftsPerDay)
                    : 2;
                var existingLabels = solution.GetUserAssignments(user.UserId, date)
                    .Select(a => a.ShiftLabel);
                if (!ShiftEligibilityResolver.IsAssignmentAllowed(
                        user, existingLabels, shiftLabel, maxPerDay,
                        _constraints.HardRules.ForbidDuplicateDailyAssignments))
                {
                    return false;
                }
            }
            else if (!ShiftEligibilityResolver.MayEverTakeLabel(user, shiftLabel))
            {
                return false;
            }

            if (solution != null &&
                AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(user.UserId), date, shiftLabel, _constraints))
            {
                return false;
            }

            if (solution != null &&
                MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                    solution, _constraints, user, date))
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

            if (solution != null &&
                _constraints.HardRules.EnforceProductivityHours &&
                user.IncludedInProductivityPlan &&
                user.ProductivityRequiredHours.HasValue)
            {
                var shiftReq = GetShiftRequirement(shiftLabel, user.SpecialtyId);
                if (shiftReq != null)
                {
                    var projected = solution.GetUserAllAssignments(user.UserId).ToList();
                    projected.Add(new SaShiftAssignment
                    {
                        UserId = user.UserId,
                        ShiftId = shiftReq.ShiftId,
                        Date = date,
                        ShiftLabel = shiftLabel,
                        IsOnCall = false
                    });
                    var worked = CalculateUserWorkedHours(projected);
                    if (ProjectPersonnelProductivityPriority.WouldExceedSchedulingCap(user, worked))
                    {
                        return false;
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

        private bool IsProtectedAssignment(
            ShiftSolution solution,
            SaShiftAssignment assignment,
            bool forManagerInstall = false)
        {
            var user = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
            if (user == null)
            {
                return false;
            }

            if (solution.IsLockedSkeleton(assignment.UserId, assignment.ShiftId, assignment.Date)
                || assignment.IsSkeleton)
            {
                return true;
            }

            if (user.RequiredShiftSlots.Any(s =>
                    s.Date.Date == assignment.Date.Date && s.ShiftLabel == assignment.ShiftLabel))
            {
                return true;
            }

            if (user.RequiredPresenceDates.Any(d => d.Date == assignment.Date.Date))
            {
                return true;
            }

            // الزام ترکیب مسئول از سهمیه شب مهم‌تر است — جایگزینی برای L1 مجاز
            if (forManagerInstall)
            {
                return false;
            }

            // شب‌هایی که حذف‌شان کاربر را زیر حداقل سهمیه می‌برد محافظت شوند
            if (assignment.ShiftLabel == ShiftLabel.Night && !assignment.IsOnCall)
            {
                return !ExactNightQuotaGuard.CanDonateNight(solution, _constraints, user, assignment);
            }

            return false;
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
                    .OrderByDescending(u => ShiftManagerRules.IsLevel1(u) ? 2 : ShiftManagerRules.IsManager(u) ? 1 : 0)
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
                .OrderBy(u => isHoliday
                    ? HolidayMorningEveningFairnessGuard.CountHolidayLabel(solution, _constraints, u.UserId, shiftLabel)
                    : CountUserLabelShifts(solution, u.UserId, shiftLabel))
                .ThenBy(u => CountUserLabelShifts(solution, u.UserId, shiftLabel))
                .ThenBy(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .ThenBy(u => ProjectPersonnelAtRequiredCapPriority(solution, u))
                .ThenBy(u => CountConsecutiveWorkdaysEndingAt(solution, u.UserId, date.Date.AddDays(-1)))
                .ThenBy(u => CountWorkdaysInWeek(solution, u.UserId, date))
                .ThenBy(u => CalculateUserWorkedHours(solution.GetUserAllAssignments(u.UserId)))
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

        private static int CountUserLabelShifts(ShiftSolution solution, int userId, ShiftLabel label) =>
            solution.GetUserAllAssignments(userId).Count(a => a.ShiftLabel == label && !a.IsOnCall);

        private int CountWorkdaysInWeek(ShiftSolution solution, int userId, DateTime date)
        {
            var week = GetWeekNumber(date);
            return solution.GetUserAllAssignments(userId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .Distinct()
                .Count(d => GetWeekNumber(d) == week);
        }

        /// <summary>
        /// تعداد روزهای کاری متوالی که به endDate ختم می‌شوند (اگر endDate کار نباشد ۰).
        /// </summary>
        private static int CountConsecutiveWorkdaysEndingAt(ShiftSolution solution, int userId, DateTime endDate)
        {
            var workDates = solution.GetUserAllAssignments(userId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .ToHashSet();
            if (!workDates.Contains(endDate.Date) && endDate.Date != DateTime.MinValue)
            {
                // برای اولویت‌بندی هنگام انتساب روز جدید، از روز قبل حساب می‌کنیم
            }

            var cursor = endDate.Date;
            var count = 0;
            while (workDates.Contains(cursor))
            {
                count++;
                cursor = cursor.AddDays(-1);
            }

            return count;
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
                        solution.GetUserAllAssignments(user.UserId), date, shiftReq.ShiftLabel, _constraints) &&
                    !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                        solution, _constraints, user, date))
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
                        solution.GetUserAllAssignments(user.UserId), date, shiftReq.ShiftLabel, _constraints) &&
                    !MaxConsecutiveWorkdayRules.WouldExceedMaxConsecutiveWorkdays(
                        solution, _constraints, user, date))
                {
                    solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel, isOnCall);
                    current++;
                }
            }
        }

        /// <summary>
        /// آیا انتساب جدید با قوانین ترکیب روزانه ناسازگار است؟
        /// صبح+عصر و صبح+شب مجاز؛ عصر+شب ممنوع؛ تکرار لیبل ممنوع.
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
            var maxAllowed = ProjectPersonnelProductivityPriority.GetMaxAllowedSchedulingHours(userConstraint);
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
                _constraints.IsHoliday,
                _isInProductivityPlan);
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

            if (IsProtectedAssignment(solution, assignment1) || IsProtectedAssignment(solution, assignment2))
            {
                return;
            }

            solution.RemoveAssignment(assignment1.UserId, assignment1.ShiftId, assignment1.Date);
            solution.RemoveAssignment(assignment2.UserId, assignment2.ShiftId, assignment2.Date);

            // همان شیفت/روز جابه‌جا می‌شود؛ توالی و ترکیب روزانه را چک کن
            if (AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(assignment2.UserId),
                    assignment1.Date, assignment1.ShiftLabel, _constraints) ||
                AdjacentShiftRestRules.WouldConflict(
                    solution.GetUserAllAssignments(assignment1.UserId),
                    assignment2.Date, assignment2.ShiftLabel, _constraints) ||
                HasDailyConflict(solution, assignment2.UserId, assignment1.Date, assignment1.ShiftLabel) ||
                HasDailyConflict(solution, assignment1.UserId, assignment2.Date, assignment2.ShiftLabel))
            {
                // برگرداندن
                solution.AddAssignment(assignment1.UserId, assignment1.ShiftId, assignment1.Date, assignment1.ShiftLabel, assignment1.IsOnCall, assignment1.IsSkeleton);
                solution.AddAssignment(assignment2.UserId, assignment2.ShiftId, assignment2.Date, assignment2.ShiftLabel, assignment2.IsOnCall, assignment2.IsSkeleton);
                return;
            }

            solution.AddAssignment(assignment2.UserId, assignment1.ShiftId, assignment1.Date, assignment1.ShiftLabel, assignment1.IsOnCall, assignment1.IsSkeleton);
            solution.AddAssignment(assignment1.UserId, assignment2.ShiftId, assignment2.Date, assignment2.ShiftLabel, assignment2.IsOnCall, assignment2.IsSkeleton);
        }

        private void PerformReassignMove(ShiftSolution solution)
        {
            var assignments = solution.Assignments.Values.ToList();
            if (assignments.Count == 0) return;

            var assignment = assignments[_random.Next(assignments.Count)];
            if (IsProtectedAssignment(solution, assignment))
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
                .Where(u => !ProjectPersonnelAtRequiredCap(u, solution))
                .OrderBy(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .ThenByDescending(u => GetProductivityHourDeficit(u, solution))
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
                .Where(a => !IsProtectedAssignment(solution, a))
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

            var crossTier = ProjectPersonnelProductivityPriority.CrossTierToleranceHours;
            var receiver = productivityUsers
                .Where(u => GetProductivityHourDeficit(u, solution) > crossTier)
                .OrderBy(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .ThenByDescending(u => GetProductivityHourDeficit(u, solution))
                .FirstOrDefault();
            if (receiver == null)
            {
                return;
            }

            var surplusTolerance = ProjectPersonnelProductivityPriority.IsProjectPersonnel(receiver)
                ? 2.0
                : crossTier;
            var donor = productivityUsers
                .Where(u => u.UserId != receiver.UserId)
                .Where(u => GetProductivityHourSurplus(u, solution, surplusTolerance) > crossTier)
                .OrderByDescending(u => GetProductivityHourSurplus(u, solution, surplusTolerance))
                .ThenByDescending(u => ProjectPersonnelProductivityPriority.FillTier(u))
                .FirstOrDefault();
            if (donor == null)
            {
                return;
            }

            var donorAssignments = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && !IsProtectedAssignment(solution, a))
                .Where(a => ExactNightQuotaGuard.CanDonateNight(solution, _constraints, donor, a))
                .OrderBy(a => a.ShiftLabel == ShiftLabel.Morning ? 0 : a.ShiftLabel == ShiftLabel.Evening ? 1 : 2)
                .ThenByDescending(a => GetShiftEffectiveHours(a))
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

        /// <summary>
        /// جابه‌جایی صبح/عصر بین دو کاربر برای کاهش نابرابری تعداد و تراکم روزهای کاری.
        /// شب‌ها دست نخورده می‌مانند.
        /// </summary>
        private void PerformMorningEveningBalanceMove(ShiftSolution solution)
        {
            if (MorningEveningBalanceGuard.TrySingleSwap(solution, _constraints))
            {
                return;
            }

            var label = _random.Next(2) == 0 ? ShiftLabel.Morning : ShiftLabel.Evening;
            var eligible = _constraints.UserConstraints
                .Where(u => u.IsActive && u.ShiftType != ShiftTypes.FixedShift)
                .Where(u => ShiftEligibilityResolver.MayEverTakeLabel(u, label))
                .ToList();
            if (eligible.Count < 2)
            {
                return;
            }

            var ranked = eligible
                .Select(u => (
                    User: u,
                    Count: CountUserLabelShifts(solution, u.UserId, label),
                    HolidayCount: HolidayMorningEveningFairnessGuard.CountHolidayLabel(
                        solution, _constraints, u.UserId, label),
                    Surplus: GetProductivityHourSurplus(u, solution),
                    Deficit: GetProductivityHourDeficit(u, solution)))
                .ToList();

            var donorPick = ranked
                .Where(x => x.Surplus > 2 || x.Count >= ranked.Average(r => r.Count) + 2)
                .OrderByDescending(x => x.Surplus)
                .ThenByDescending(x => x.HolidayCount)
                .ThenByDescending(x => x.Count)
                .FirstOrDefault();

            var receiverPick = ranked
                .Where(x => x.Deficit > 2)
                .OrderByDescending(x => x.Deficit)
                .ThenBy(x => x.Count)
                .FirstOrDefault();

            var donor = donorPick.User != null
                ? donorPick.User
                : ranked.OrderByDescending(x => x.HolidayCount).ThenByDescending(x => x.Count).First().User;
            var receiver = receiverPick.User != null && receiverPick.User.UserId != donor.UserId
                ? receiverPick.User
                : ranked.Where(x => x.User.UserId != donor.UserId).OrderByDescending(x => x.Deficit).ThenBy(x => x.Count).First().User;

            if (ranked.Max(x => x.HolidayCount) - ranked.Min(x => x.HolidayCount) < 2 &&
                ranked.Max(x => x.Count) - ranked.Min(x => x.Count) < 2 &&
                GetProductivityHourSurplus(donor, solution) <= 2 &&
                GetProductivityHourDeficit(receiver, solution) <= 2)
            {
                donor = eligible
                    .OrderByDescending(u => MaxConsecutiveRun(solution, u.UserId))
                    .ThenByDescending(u => CountUserLabelShifts(solution, u.UserId, label))
                    .First();
                receiver = eligible
                    .Where(u => u.UserId != donor.UserId)
                    .OrderBy(u => MaxConsecutiveRun(solution, u.UserId))
                    .ThenBy(u => CountUserLabelShifts(solution, u.UserId, label))
                    .FirstOrDefault() ?? receiver;
            }

            var preferHolidayMove = ranked.Max(x => x.HolidayCount) - ranked.Min(x => x.HolidayCount) >= 2;
            var donorMe = solution.GetUserAllAssignments(donor.UserId)
                .Where(a => !a.IsOnCall && (a.ShiftLabel == ShiftLabel.Morning || a.ShiftLabel == ShiftLabel.Evening))
                .Where(a => !IsProtectedAssignment(solution, a))
                .OrderByDescending(a => preferHolidayMove && _constraints.IsHoliday(a.Date) ? 2 : 0)
                .ThenByDescending(a => a.ShiftLabel == label ? 1 : 0)
                .ThenByDescending(a => CountConsecutiveWorkdaysEndingAt(solution, donor.UserId, a.Date.Date))
                .ToList();

            foreach (var assignment in donorMe)
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

        private static int MaxConsecutiveRun(ShiftSolution solution, int userId)
        {
            var workDates = solution.GetUserAllAssignments(userId)
                .Where(a => !a.IsOnCall)
                .Select(a => a.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            if (workDates.Count == 0)
            {
                return 0;
            }

            var maxRun = 1;
            var run = 1;
            for (var i = 1; i < workDates.Count; i++)
            {
                if ((workDates[i] - workDates[i - 1]).Days == 1)
                {
                    run++;
                    maxRun = Math.Max(maxRun, run);
                }
                else
                {
                    run = 1;
                }
            }

            return maxRun;
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

        private double GetProductivityHourSurplus(
            UserConstraint user,
            ShiftSolution solution,
            double tolerance = 2.0)
        {
            if (!user.IncludedInProductivityPlan || !user.ProductivityRequiredHours.HasValue)
            {
                return CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            }

            var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            return Math.Max(0, worked - (double)user.ProductivityRequiredHours.Value - tolerance);
        }

        private static double GetProductivityWeight(UserConstraint user) =>
            user.IncludedInProductivityPlan && user.ProductivityRequiredHours.HasValue && user.ProductivityRequiredHours > 0
                ? (double)user.ProductivityRequiredHours.Value
                : 1.0;

        private int ProjectPersonnelAtRequiredCapPriority(ShiftSolution solution, UserConstraint user)
        {
            if (!ProjectPersonnelProductivityPriority.IsProjectPersonnel(user))
            {
                return 0;
            }

            var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            return ProjectPersonnelProductivityPriority.IsAtOrAboveRequiredHours(user, worked) ? 1 : 0;
        }

        private bool ProjectPersonnelAtRequiredCap(UserConstraint user, ShiftSolution solution)
        {
            if (!ProjectPersonnelProductivityPriority.IsProjectPersonnel(user))
            {
                return false;
            }

            var worked = CalculateUserWorkedHours(solution.GetUserAllAssignments(user.UserId));
            return ProjectPersonnelProductivityPriority.IsAtOrAboveRequiredHours(user, worked);
        }

        private double GetShiftEffectiveHours(SaShiftAssignment assignment)
        {
            return ProductivityWorkedHoursCalculator.EstimateAssignmentHours(
                assignment,
                _shiftInfoLookup,
                _constraints.IsHoliday(assignment.Date),
                _isInProductivityPlan(assignment.UserId));
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
