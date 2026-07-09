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

            // ایجاد راه‌حل اولیه
            var currentSolution = GenerateInitialSolution();
            // اگر راه‌حل اولیه نامعتبر است، تلاش برای بازتولید
            if (!IsFeasible(currentSolution))
            {
                currentSolution = RepairOrRegenerate(currentSolution) ?? GenerateInitialSolution();
            }
            var bestSolution = currentSolution.Clone();

            _statistics.BestScore = currentSolution.Score;
            _statistics.CurrentScore = currentSolution.Score;

            double temperature = _parameters.InitialTemperature;
            int iterationsWithoutImprovement = 0;

            for (int iteration = 0; iteration < _parameters.MaxIterations; iteration++)
            {
                _statistics.TotalIterations = iteration + 1;
                _statistics.CurrentTemperature = temperature;

                // تولید راه‌حل همسایه
                var neighborSolution = GenerateNeighbor(currentSolution);
                if (!IsFeasible(neighborSolution))
                {
                    _statistics.RejectedMoves++;
                    iterationsWithoutImprovement++;
                    // کاهش دما و ادامه
                    temperature *= _parameters.CoolingRate;
                    _statistics.CurrentScore = currentSolution.Score;
                    _statistics.ScoreHistory.Add(currentSolution.Score);
                    _statistics.TemperatureHistory.Add(temperature);
                    if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement || temperature <= _parameters.FinalTemperature)
                    {
                        break;
                    }
                    continue;
                }

                // محاسبه تفاوت امتیاز
                double deltaScore = neighborSolution.Score - currentSolution.Score;

                // تصمیم‌گیری برای پذیرش یا رد راه‌حل جدید
                bool acceptMove = false;

                if (deltaScore < 0) // راه‌حل بهتر
                {
                    acceptMove = true;
                }
                else // راه‌حل بدتر - احتمال پذیرش بر اساس دما
                {
                    double acceptanceProbability = Math.Exp(-deltaScore / temperature);
                    acceptMove = _random.NextDouble() < acceptanceProbability;
                }

                if (acceptMove)
                {
                    currentSolution = neighborSolution;
                    _statistics.AcceptedMoves++;

                    // بررسی بهترین راه‌حل
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

                // کاهش دما
                temperature *= _parameters.CoolingRate;

                // توقف زودهنگام در صورت عدم بهبود
                if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement)
                {
                    break;
                }

                // توقف در صورت رسیدن به دمای نهایی
                if (temperature <= _parameters.FinalTemperature)
                {
                    break;
                }
            }

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
            var bestSolution = currentSolution.Clone();

            _statistics.BestScore = currentSolution.Score;
            _statistics.CurrentScore = currentSolution.Score;

            double temperature = _parameters.InitialTemperature;
            int iterationsWithoutImprovement = 0;

            for (int iteration = 0; iteration < _parameters.MaxIterations; iteration++)
            {
                _statistics.TotalIterations = iteration + 1;
                _statistics.CurrentTemperature = temperature;

                // تولید راه‌حل همسایه
                var neighborSolution = GenerateNeighbor(currentSolution);
                if (!IsFeasible(neighborSolution))
                {
                    _statistics.RejectedMoves++;
                    iterationsWithoutImprovement++;
                    temperature *= _parameters.CoolingRate;
                    _statistics.CurrentScore = currentSolution.Score;
                    _statistics.ScoreHistory.Add(currentSolution.Score);
                    _statistics.TemperatureHistory.Add(temperature);
                    if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement || temperature <= _parameters.FinalTemperature)
                    {
                        break;
                    }
                    continue;
                }

                // محاسبه تفاوت امتیاز
                double deltaScore = neighborSolution.Score - currentSolution.Score;

                // تصمیم‌گیری برای پذیرش یا رد راه‌حل جدید
                bool acceptMove = false;

                if (deltaScore < 0) // راه‌حل بهتر
                {
                    acceptMove = true;
                }
                else // راه‌حل بدتر - احتمال پذیرش بر اساس دما
                {
                    double acceptanceProbability = Math.Exp(-deltaScore / temperature);
                    acceptMove = _random.NextDouble() < acceptanceProbability;
                }

                if (acceptMove)
                {
                    currentSolution = neighborSolution;
                    _statistics.AcceptedMoves++;

                    // بررسی بهترین راه‌حل
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

                // کاهش دما
                temperature *= _parameters.CoolingRate;

                // توقف زودهنگام در صورت عدم بهبود
                if (iterationsWithoutImprovement >= _parameters.MaxIterationsWithoutImprovement)
                {
                    break;
                }

                // توقف در صورت رسیدن به دمای نهایی
                if (temperature <= _parameters.FinalTemperature)
                {
                    break;
                }
            }

            stopwatch.Stop();
            _statistics.ExecutionTime = stopwatch.Elapsed;

            return bestSolution;
        }


        /// <summary>
        /// تولید راه‌حل اولیه
        /// </summary>
        private ShiftSolution GenerateInitialSolution()
        {
            var solution = new ShiftSolution();

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
                            .Where(u => !u.UnavailableDates.Contains(date.Date))
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

            // انتخاب تصادفی نوع تغییر
            var moveType = (MoveType)_random.Next(Enum.GetValues(typeof(MoveType)).Length);

            switch (moveType)
            {
                case MoveType.Swap:
                    PerformSwapMove(neighbor);
                    break;
                case MoveType.Reassign:
                    PerformReassignMove(neighbor);
                    break;
                case MoveType.Add:
                    PerformAddMove(neighbor);
                    break;
                case MoveType.Remove:
                    PerformRemoveMove(neighbor);
                    break;
            }

            // محاسبه امتیاز جدید
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
        /// محاسبه امتیاز پایه
        /// </summary>
        private double CalculateBaseScore(ShiftSolution solution)
        {
            // امتیاز منفی برای هر انتساب (هر چه کمتر، بهتر)
            return -solution.Assignments.Count * 10;
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

            // بررسی محدودیت‌های هر کاربر
            foreach (var userConstraint in _constraints.UserConstraints)
            {
                var userAssignments = solution.GetUserAllAssignments(userConstraint.UserId);

                // بررسی حداکثر شیفت‌های متوالی
                if (!_constraints.HardRules.EnforceMaxConsecutiveShifts)
                {
                    penalty += CheckConsecutiveShifts(userAssignments, userConstraint.MaxConsecutiveShifts, violations);
                }

                // بررسی حداقل روزهای استراحت
                if (!_constraints.HardRules.EnforceMinRestDays)
                {
                    penalty += CheckRestDays(userAssignments, userConstraint.MinRestDaysBetweenShifts, violations);
                }

                // بررسی حداکثر شیفت‌های هفتگی
                if (!_constraints.HardRules.EnforceWeeklyMaxShifts)
                {
                    penalty += CheckWeeklyShifts(userAssignments, userConstraint.MaxShiftsPerWeek, violations) * _constraints.SoftWeights.WeeklyMaxWeight;
                }

                // بررسی حداکثر شیفت‌های شبانه ماهانه
                if (!_constraints.HardRules.EnforceNightShiftMonthlyCap)
                {
                    penalty += CheckMonthlyNightShifts(userAssignments, userConstraint.MaxNightShiftsPerMonth, violations) * _constraints.SoftWeights.MonthlyNightCapWeight;
                }

                if (!_constraints.HardRules.EnforceProductivityHours && userConstraint.ProductivityRequiredHours.HasValue)
                {
                    penalty += CheckMonthlyWorkingHours(userConstraint, userAssignments, violations) * _constraints.SoftWeights.ProductivityOvertimeWeight;
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
                return 0;

            double penalty = 0;
            var dateRange = GetDateRange();

            foreach (var date in dateRange)
            {
                foreach (var shiftReq in _constraints.ShiftRequirements)
                {
                    var assignments = solution.GetShiftAssignments(shiftReq.ShiftId, date);
                    var maleCount = assignments.Count(a => GetUserGender(a.UserId) == UserGender.Male);
                    var femaleCount = assignments.Count(a => GetUserGender(a.UserId) == UserGender.Female);
                    var totalCount = assignments.Count;

                    if (totalCount > 0)
                    {
                        double maleRatio = (double)maleCount / totalCount;
                        double femaleRatio = (double)femaleCount / totalCount;

                        if (maleRatio < _constraints.GlobalConstraints.MinGenderBalanceRatio ||
                            femaleRatio < _constraints.GlobalConstraints.MinGenderBalanceRatio)
                        {
                            penalty += 100;
                        }
                    }
                }
            }

            return penalty * _constraints.SoftWeights.GenderBalanceWeight;
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
                        var assignedCount = assignments.Count(a => GetUserSpecialty(a.UserId) == specialtyReq.SpecialtyId);
                        var requiredCount = specialtyReq.RequiredTotalCount;

                        if (assignedCount < requiredCount)
                        {
                            penalty += (requiredCount - assignedCount) * 50;
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
            // بررسی تاریخ‌های غیرقابل دسترس کاربران
            foreach (var assignment in solution.Assignments.Values)
            {
                var userConstraint = _constraints.UserConstraints.FirstOrDefault(u => u.UserId == assignment.UserId);
                if (userConstraint != null && userConstraint.UnavailableDates.Contains(assignment.Date.Date))
                {
                    return false; // کاربر در تاریخ غیرقابل دسترس انتساب شده
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
                        // کل ظرفیت مجموع تخصص‌ها
                        int totalRequired = shiftReq.SpecialtyRequirements.Sum(r => r.RequiredTotalCount);
                        if (assignments.Count > totalRequired)
                            return false;
                        foreach (var specReq in shiftReq.SpecialtyRequirements)
                        {
                            int assignedSpec = assignments.Count(a => GetUserSpecialty(a.UserId) == specReq.SpecialtyId);
                            if (assignedSpec > specReq.RequiredTotalCount)
                                return false;
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


        private void AssignRequiredPersonnel(ShiftSolution solution, List<UserConstraint> eligibleUsers,
            ShiftRequirement shiftReq, DateTime date, SpecialtyRequirement specialtyReq)
        {
            var shuffledUsers = eligibleUsers.OrderBy(x => _random.Next()).ToList();
            int assignedCount = 0;

            foreach (var user in shuffledUsers)
            {
                if (assignedCount >= specialtyReq.RequiredTotalCount)
                    break;

                if (!solution.HasAssignment(user.UserId, shiftReq.ShiftId, date))
                {
                    solution.AddAssignment(user.UserId, shiftReq.ShiftId, date, shiftReq.ShiftLabel);
                    assignedCount++;
                }
            }
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
            var assignments = solution.Assignments.Values.ToList();
            if (assignments.Count < 2) return;

            var assignment1 = assignments[_random.Next(assignments.Count)];
            var assignment2 = assignments[_random.Next(assignments.Count)];

            if (assignment1.UserId != assignment2.UserId)
            {
                solution.RemoveAssignment(assignment1.UserId, assignment1.ShiftId, assignment1.Date);
                solution.RemoveAssignment(assignment2.UserId, assignment2.ShiftId, assignment2.Date);

                solution.AddAssignment(assignment2.UserId, assignment1.ShiftId, assignment1.Date, assignment1.ShiftLabel);
                solution.AddAssignment(assignment1.UserId, assignment2.ShiftId, assignment2.Date, assignment2.ShiftLabel);
            }
        }

        private void PerformReassignMove(ShiftSolution solution)
        {
            var assignments = solution.Assignments.Values.ToList();
            if (assignments.Count == 0) return;

            var assignment = assignments[_random.Next(assignments.Count)];
            var eligibleUsers = _constraints.UserConstraints
                .Where(u => u.SpecialtyId == GetUserSpecialty(assignment.UserId))
                .Where(u => !u.UnavailableDates.Contains(assignment.Date.Date))
                .ToList();

            if (eligibleUsers.Count > 1)
            {
                var newUser = eligibleUsers[_random.Next(eligibleUsers.Count)];
                solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
                solution.AddAssignment(newUser.UserId, assignment.ShiftId, assignment.Date, assignment.ShiftLabel);
            }
        }

        private void PerformAddMove(ShiftSolution solution)
        {
            var dateRange = GetDateRange();
            var randomDate = dateRange[_random.Next(dateRange.Count)];
            var randomShift = _constraints.ShiftRequirements[_random.Next(_constraints.ShiftRequirements.Count)];
            var randomSpecialty = randomShift.SpecialtyRequirements[_random.Next(randomShift.SpecialtyRequirements.Count)];

            var eligibleUsers = _constraints.UserConstraints
                .Where(u => u.SpecialtyId == randomSpecialty.SpecialtyId)
                .Where(u => !u.UnavailableDates.Contains(randomDate.Date))
                .Where(u => !solution.HasAssignment(u.UserId, randomShift.ShiftId, randomDate))
                .ToList();

            if (eligibleUsers.Count > 0)
            {
                var user = eligibleUsers[_random.Next(eligibleUsers.Count)];
                solution.AddAssignment(user.UserId, randomShift.ShiftId, randomDate, randomShift.ShiftLabel);
            }
        }

        private void PerformRemoveMove(ShiftSolution solution)
        {
            var assignments = solution.Assignments.Values.ToList();
            if (assignments.Count == 0) return;

            var assignment = assignments[_random.Next(assignments.Count)];
            solution.RemoveAssignment(assignment.UserId, assignment.ShiftId, assignment.Date);
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
