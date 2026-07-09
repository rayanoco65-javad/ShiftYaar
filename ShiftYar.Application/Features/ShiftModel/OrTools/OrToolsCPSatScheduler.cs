using Google.OrTools.Sat;
using ShiftYar.Application.Features.ShiftModel.OrTools.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;
using static ShiftYar.Domain.Enums.UserModel.UserEnums;

namespace ShiftYar.Application.Features.ShiftModel.OrTools
{
    /// <summary>
    /// پیاده‌سازی الگوریتم OR-Tools CP-SAT برای بهینه‌سازی شیفت‌بندی
    /// </summary>
    public class OrToolsCPSatScheduler // حل‌کننده CP-SAT برای زمان‌بندی شیفت‌ها با OR-Tools
    {
        private readonly OrToolsConstraints _constraints; // محدودیت‌ها و داده‌های مسأله
        private readonly OrToolsParameters _parameters; // پارامترهای تنظیم حل‌کننده
        private readonly OrToolsStatistics _statistics; // آمار اجرا و خروجی حل‌کننده

        public OrToolsCPSatScheduler(OrToolsConstraints constraints, OrToolsParameters parameters) // سازنده با ورودی محدودیت‌ها و پارامترها
        {
            _constraints = constraints;
            _parameters = parameters;
            _statistics = new OrToolsStatistics();
        }

        /// <summary>
        /// اجرای الگوریتم OR-Tools CP-SAT
        /// </summary>
        public OrToolsShiftSolution Optimize() // اجرای فرآیند حل و تولید راه‌حل
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // ایجاد مدل CP-SAT
                var model = new CpModel();

                // ایجاد متغیرها
                var variables = CreateVariables(model);

                // اضافه کردن محدودیت‌ها
                AddHardConstraints(model, variables);
                AddSoftConstraints(model, variables);

                // تعریف تابع هدف
                var objective = DefineObjective(model, variables);
                model.Maximize(objective);

                // حل مدل
                var solver = new CpSolver();
                ConfigureSolver(solver);

                var status = solver.Solve(model);

                stopwatch.Stop();

                // جمع‌آوری نتایج
                var solution = ExtractSolution(solver, status, variables, stopwatch.Elapsed);

                return solution;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _statistics.SolverLogs.Add($"Error: {ex.Message}");

                return new OrToolsShiftSolution
                {
                    Status = OrToolsSolverStatus.Abnormal,
                    SolveTime = stopwatch.Elapsed,
                    Violations = new List<string> { $"Solver error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// ایجاد متغیرهای مدل
        /// </summary>
        private Dictionary<string, IntVar> CreateVariables(CpModel model) // تعریف متغیرهای تصمیم‌گیری (انتساب‌ها و کمکی)
        {
            var variables = new Dictionary<string, IntVar>();
            int variableIndex = 0;

            // ایجاد متغیرهای انتساب
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                {
                    for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                    {
                        var assignmentKey = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                        var onCallKey = OrToolsVariableKeys.GetOnCallKey(userIndex, shiftIndex, dateIndex);

                        // متغیر انتساب (0 یا 1)
                        variables[assignmentKey] = model.NewBoolVar(assignmentKey);

                        // متغیر آماده‌باش (0 یا 1)
                        variables[onCallKey] = model.NewBoolVar(onCallKey);

                        // ذخیره ایندکس متغیرها
                        var user = _constraints.UserConstraints[userIndex];
                        var shift = _constraints.ShiftRequirements[shiftIndex];
                        var date = _constraints.StartDate.AddDays(dateIndex);

                        user.AssignmentVariables[assignmentKey] = variableIndex++;
                        user.OnCallVariables[onCallKey] = variableIndex++;
                        shift.AssignmentVariables[assignmentKey] = variableIndex++;
                        shift.OnCallVariables[onCallKey] = variableIndex++;
                    }
                }
            }

            // ایجاد متغیرهای کمکی برای محدودیت‌های نرم
            CreateAuxiliaryVariables(model, variables);

            _statistics.NumVariables = variables.Count;
            return variables;
        }

        /// <summary>
        /// ایجاد متغیرهای کمکی
        /// </summary>
        private void CreateAuxiliaryVariables(CpModel model, Dictionary<string, IntVar> variables) // تعریف متغیرهای کمکی (تعادل جنسیت/تخصص)
        {
            // متغیرهای تعادل جنسیتی
            for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
            {
                for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                {
                    for (int genderIndex = 0; genderIndex < 2; genderIndex++)
                    {
                        var key = OrToolsVariableKeys.GetGenderBalanceKey(shiftIndex, dateIndex, genderIndex);
                        variables[key] = model.NewIntVar(0, _constraints.NumUsers, key);
                    }
                }
            }

            // متغیرهای تعادل تخصص
            for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
            {
                for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                {
                    for (int specialtyIndex = 0; specialtyIndex < _constraints.NumSpecialties; specialtyIndex++)
                    {
                        var key = OrToolsVariableKeys.GetSpecialtyBalanceKey(shiftIndex, dateIndex, specialtyIndex);
                        variables[key] = model.NewIntVar(0, _constraints.NumUsers, key);
                    }
                }
            }
        }

        /// <summary>
        /// اضافه کردن محدودیت‌های سخت
        /// </summary>
        private void AddHardConstraints(CpModel model, Dictionary<string, IntVar> variables) // افزودن قیود قطعی
        {
            // محدودیت: هر کاربر حداکثر یک شیفت در روز
            if (_constraints.HardRules.ForbidDuplicateDailyAssignments)
            {
                AddDailyAssignmentConstraints(model, variables);
            }


            // محدودیت: رعایت ظرفیت موردنیاز هر تخصص
            if (_constraints.HardRules.EnforceSpecialtyCapacity)
            {
                AddSpecialtyCapacityConstraints(model, variables);
            }

            // محدودیت: حداقل استراحت بین شیفت‌ها
            if (_constraints.HardRules.EnforceMinRestDays)
            {
                AddMinRestDaysConstraints(model, variables);
            }

            // محدودیت: حداکثر شیفت‌های متوالی
            if (_constraints.HardRules.EnforceMaxConsecutiveShifts)
            {
                AddMaxConsecutiveShiftsConstraints(model, variables);
            }

            if (_constraints.HardRules.EnforceProductivityHours)
            {
                AddProductivityHourConstraints(model, variables);
            }
        }

        /// <summary>
        /// اضافه کردن محدودیت‌های نرم
        /// </summary>
        private void AddSoftConstraints(CpModel model, Dictionary<string, IntVar> variables) // افزودن قیود نرم (با جریمه)
        {
            // محدودیت نرم: تعادل جنسیتی
            if (_constraints.GlobalConstraints.RequireGenderBalance)
            {
                AddGenderBalanceConstraints(model, variables);
            }

            // محدودیت نرم: حداکثر شیفت هفتگی
            if (!_constraints.HardRules.EnforceWeeklyMaxShifts)
            {
                AddWeeklyMaxShiftsConstraints(model, variables);
            }

            // محدودیت نرم: حداکثر شیفت شب ماهانه
            if (!_constraints.HardRules.EnforceNightShiftMonthlyCap)
            {
                AddMonthlyNightShiftsConstraints(model, variables);
            }
        }

        /// <summary>
        /// محدودیت: هر کاربر حداکثر یک شیفت در روز
        /// </summary>
        private void AddDailyAssignmentConstraints(CpModel model, Dictionary<string, IntVar> variables) // محدودیت یک‌شیفت در روز برای هر کاربر
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                {
                    var dailyAssignments = new List<IntVar>();

                    for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                    {
                        var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                        if (variables.ContainsKey(key))
                        {
                            dailyAssignments.Add(variables[key]);
                        }
                    }

                    if (dailyAssignments.Count > 0)
                    {
                        model.Add(LinearExpr.Sum(dailyAssignments) <= _constraints.GlobalConstraints.MaxShiftsPerDay);
                    }
                }
            }
        }


        /// <summary>
        /// محدودیت: رعایت ظرفیت موردنیاز هر تخصص
        /// </summary>
        private void AddSpecialtyCapacityConstraints(CpModel model, Dictionary<string, IntVar> variables) // ظرفیت نیروی موردنیاز به‌ازای تخصص
        {
            for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
            {
                var shift = _constraints.ShiftRequirements[shiftIndex];

                foreach (var specialtyReq in shift.SpecialtyRequirements)
                {
                    for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                    {
                        var maleAssignments = new List<IntVar>();
                        var femaleAssignments = new List<IntVar>();
                        var totalAssignments = new List<IntVar>();

                        for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
                        {
                            var user = _constraints.UserConstraints[userIndex];

                            if (user.SpecialtyId == specialtyReq.SpecialtyId)
                            {
                                var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                                if (variables.ContainsKey(key))
                                {
                                    totalAssignments.Add(variables[key]);

                                    if (user.Gender == UserGender.Male)
                                    {
                                        maleAssignments.Add(variables[key]);
                                    }
                                    else
                                    {
                                        femaleAssignments.Add(variables[key]);
                                    }
                                }
                            }
                        }

                        // محدودیت تعداد کل
                        if (totalAssignments.Count > 0)
                        {
                            model.Add(LinearExpr.Sum(totalAssignments) >= specialtyReq.RequiredTotalCount);
                            model.Add(LinearExpr.Sum(totalAssignments) <= specialtyReq.RequiredTotalCount + 2); // انعطاف‌پذیری
                        }

                        // محدودیت جنسیت
                        if (maleAssignments.Count > 0)
                        {
                            model.Add(LinearExpr.Sum(maleAssignments) >= specialtyReq.RequiredMaleCount);
                        }

                        if (femaleAssignments.Count > 0)
                        {
                            model.Add(LinearExpr.Sum(femaleAssignments) >= specialtyReq.RequiredFemaleCount);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// محدودیت: حداقل استراحت بین شیفت‌ها
        /// </summary>
        private void AddMinRestDaysConstraints(CpModel model, Dictionary<string, IntVar> variables) // حداقل روز استراحت بین شیفت‌ها
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];

                for (int dateIndex = 0; dateIndex < _constraints.NumDays - user.MinRestDaysBetweenShifts; dateIndex++)
                {
                    for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                    {
                        var currentKey = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);

                        for (int nextDateIndex = dateIndex + 1; nextDateIndex <= dateIndex + user.MinRestDaysBetweenShifts; nextDateIndex++)
                        {
                            if (nextDateIndex < _constraints.NumDays)
                            {
                                for (int nextShiftIndex = 0; nextShiftIndex < _constraints.NumShifts; nextShiftIndex++)
                                {
                                    var nextKey = OrToolsVariableKeys.GetAssignmentKey(userIndex, nextShiftIndex, nextDateIndex);

                                    if (variables.ContainsKey(currentKey) && variables.ContainsKey(nextKey))
                                    {
                                        model.Add(variables[currentKey] + variables[nextKey] <= 1);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// محدودیت: حداکثر شیفت‌های متوالی
        /// </summary>
        private void AddMaxConsecutiveShiftsConstraints(CpModel model, Dictionary<string, IntVar> variables) // سقف شیفت‌های متوالی برای هر کاربر
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];

                for (int startDateIndex = 0; startDateIndex <= _constraints.NumDays - user.MaxConsecutiveShifts; startDateIndex++)
                {
                    var consecutiveAssignments = new List<IntVar>();

                    for (int dayOffset = 0; dayOffset < user.MaxConsecutiveShifts; dayOffset++)
                    {
                        var dateIndex = startDateIndex + dayOffset;

                        for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                        {
                            var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                            if (variables.ContainsKey(key))
                            {
                                consecutiveAssignments.Add(variables[key]);
                            }
                        }
                    }

                    if (consecutiveAssignments.Count > 0)
                    {
                        model.Add(LinearExpr.Sum(consecutiveAssignments) <= user.MaxConsecutiveShifts);
                    }
                }
            }
        }

        private void AddProductivityHourConstraints(CpModel model, Dictionary<string, IntVar> variables)
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];
                if (!user.ProductivityRequiredHours.HasValue)
                {
                    continue;
                }

                var capMinutes = (long)Math.Round(user.ProductivityRequiredHours.Value * 60);
                var weightedAssignments = new List<LinearExpr>();

                for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                {
                    var shift = _constraints.ShiftRequirements[shiftIndex];
                    var duration = shift.DurationMinutes > 0 ? shift.DurationMinutes : 480;

                    for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                    {
                        var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                        if (variables.ContainsKey(key))
                        {
                            weightedAssignments.Add(LinearExpr.Term(variables[key], duration));
                        }
                    }
                }

                if (weightedAssignments.Count > 0)
                {
                    model.Add(LinearExpr.Sum(weightedAssignments) <= capMinutes);
                }
            }
        }

        /// <summary>
        /// محدودیت نرم: تعادل جنسیتی
        /// </summary>
        private void AddGenderBalanceConstraints(CpModel model, Dictionary<string, IntVar> variables) // تعادل جنسیتی در هر شیفت/روز
        {
            for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
            {
                for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                {
                    var maleKey = OrToolsVariableKeys.GetGenderBalanceKey(shiftIndex, dateIndex, 0);
                    var femaleKey = OrToolsVariableKeys.GetGenderBalanceKey(shiftIndex, dateIndex, 1);

                    var maleAssignments = new List<IntVar>();
                    var femaleAssignments = new List<IntVar>();

                    for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
                    {
                        var user = _constraints.UserConstraints[userIndex];
                        var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);

                        if (variables.ContainsKey(key))
                        {
                            if (user.Gender == UserGender.Male)
                            {
                                maleAssignments.Add(variables[key]);
                            }
                            else
                            {
                                femaleAssignments.Add(variables[key]);
                            }
                        }
                    }

                    if (maleAssignments.Count > 0)
                    {
                        model.Add(variables[maleKey] == LinearExpr.Sum(maleAssignments));
                    }

                    if (femaleAssignments.Count > 0)
                    {
                        model.Add(variables[femaleKey] == LinearExpr.Sum(femaleAssignments));
                    }
                }
            }
        }

        /// <summary>
        /// محدودیت نرم: حداکثر شیفت هفتگی
        /// </summary>
        private void AddWeeklyMaxShiftsConstraints(CpModel model, Dictionary<string, IntVar> variables) // سقف نرم شیفت‌های هفتگی
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];
                var numWeeks = (_constraints.NumDays + 6) / 7; // تعداد هفته‌ها

                for (int weekIndex = 0; weekIndex < numWeeks; weekIndex++)
                {
                    var weeklyAssignments = new List<IntVar>();
                    var startDay = weekIndex * 7;
                    var endDay = Math.Min(startDay + 7, _constraints.NumDays);

                    for (int dateIndex = startDay; dateIndex < endDay; dateIndex++)
                    {
                        for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                        {
                            var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                            if (variables.ContainsKey(key))
                            {
                                weeklyAssignments.Add(variables[key]);
                            }
                        }
                    }

                    if (weeklyAssignments.Count > 0)
                    {
                        // محدودیت نرم: جریمه برای تجاوز از حد مجاز
                        var excessVar = model.NewIntVar(0, _constraints.NumDays, $"weekly_excess_{userIndex}_{weekIndex}");
                        model.Add(excessVar >= LinearExpr.Sum(weeklyAssignments) - user.MaxShiftsPerWeek);
                    }
                }
            }
        }

        /// <summary>
        /// محدودیت نرم: حداکثر شیفت شب ماهانه
        /// </summary>
        private void AddMonthlyNightShiftsConstraints(CpModel model, Dictionary<string, IntVar> variables) // سقف نرم شیفت‌های شب ماهانه
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];
                var nightShifts = new List<IntVar>();

                for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                {
                    for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                    {
                        var shift = _constraints.ShiftRequirements[shiftIndex];

                        if (shift.ShiftLabel == ShiftLabel.Night)
                        {
                            var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);
                            if (variables.ContainsKey(key))
                            {
                                nightShifts.Add(variables[key]);
                            }
                        }
                    }
                }

                if (nightShifts.Count > 0)
                {
                    // محدودیت نرم: جریمه برای تجاوز از حد مجاز
                    var excessVar = model.NewIntVar(0, _constraints.NumDays, $"monthly_night_excess_{userIndex}");
                    model.Add(excessVar >= LinearExpr.Sum(nightShifts) - user.MaxNightShiftsPerMonth);
                }
            }
        }

        /// <summary>
        /// تعریف تابع هدف
        /// </summary>
        private LinearExpr DefineObjective(CpModel model, Dictionary<string, IntVar> variables) // تعریف تابع هدف برای بیشینه‌سازی پوشش و کمینه‌سازی جریمه‌ها
        {
            var objectiveTerms = new List<LinearExpr>();

            // هدف: حداکثر کردن انتساب‌ها (کمترین جریمه)
            foreach (var variable in variables.Values)
            {
                if (variable.Name().StartsWith("assign_"))
                {
                    objectiveTerms.Add(variable);
                }
            }

            // جریمه برای محدودیت‌های نرم
            AddSoftConstraintPenalties(model, variables, objectiveTerms);

            return LinearExpr.Sum(objectiveTerms);
        }

        /// <summary>
        /// اضافه کردن جریمه‌های محدودیت‌های نرم
        /// </summary>
        private void AddSoftConstraintPenalties(CpModel model, Dictionary<string, IntVar> variables, List<LinearExpr> objectiveTerms) // افزودن جریمه‌های قیود نرم به هدف
        {
            // جریمه برای عدم تعادل جنسیتی
            if (_constraints.GlobalConstraints.RequireGenderBalance)
            {
                for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                {
                    for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                    {
                        var maleKey = OrToolsVariableKeys.GetGenderBalanceKey(shiftIndex, dateIndex, 0);
                        var femaleKey = OrToolsVariableKeys.GetGenderBalanceKey(shiftIndex, dateIndex, 1);

                        if (variables.ContainsKey(maleKey) && variables.ContainsKey(femaleKey))
                        {
                            var balancePenalty = model.NewIntVar(0, _constraints.NumUsers, $"gender_penalty_{shiftIndex}_{dateIndex}");
                            model.Add(balancePenalty >= variables[maleKey] - variables[femaleKey]);
                            model.Add(balancePenalty >= variables[femaleKey] - variables[maleKey]);

                            objectiveTerms.Add(LinearExpr.Term(balancePenalty, (int)(-_parameters.GenderBalanceWeight)));
                        }
                    }
                }
            }

            // جریمه برای ترجیحات کاربران
            AddUserPreferencePenalties(model, variables, objectiveTerms);
        }

        /// <summary>
        /// اضافه کردن جریمه‌های ترجیحات کاربران
        /// </summary>
        private void AddUserPreferencePenalties(CpModel model, Dictionary<string, IntVar> variables, List<LinearExpr> objectiveTerms) // پاداش/جریمه ترجیحات کاربر
        {
            for (int userIndex = 0; userIndex < _constraints.NumUsers; userIndex++)
            {
                var user = _constraints.UserConstraints[userIndex];

                for (int shiftIndex = 0; shiftIndex < _constraints.NumShifts; shiftIndex++)
                {
                    var shift = _constraints.ShiftRequirements[shiftIndex];

                    for (int dateIndex = 0; dateIndex < _constraints.NumDays; dateIndex++)
                    {
                        var key = OrToolsVariableKeys.GetAssignmentKey(userIndex, shiftIndex, dateIndex);

                        if (variables.ContainsKey(key))
                        {
                            // پاداش برای شیفت‌های ترجیحی
                            if (user.PreferredShifts.Contains(shift.ShiftLabel))
                            {
                                objectiveTerms.Add(LinearExpr.Term(variables[key], (int)_parameters.UserPreferenceWeight));
                            }

                            // جریمه برای شیفت‌های ناخواسته
                            if (user.UnwantedShifts.Contains(shift.ShiftLabel))
                            {
                                objectiveTerms.Add(LinearExpr.Term(variables[key], (int)(-_parameters.UserPreferenceWeight * 2)));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// پیکربندی حل‌کننده
        /// </summary>
        private void ConfigureSolver(CpSolver solver) // پیکربندی پارامترهای حل‌کننده CP-SAT
        {
            solver.StringParameters = $"max_time_in_seconds:{_parameters.MaxTimeInSeconds}," +
                                    $"num_search_workers:{_parameters.NumSearchWorkers}," +
                                    $"log_search_progress:{_parameters.LogSearchProgress.ToString().ToLower()}," +
                                    $"max_solutions:{_parameters.MaxSolutions}," +
                                    $"relative_gap_limit:{_parameters.RelativeGapLimit}";

            if (_parameters.LogSearchProgress)
            {
                solver.StringParameters += ",log_to_stdout:true";
            }
        }

        /// <summary>
        /// استخراج راه‌حل از حل‌کننده
        /// </summary>
        private OrToolsShiftSolution ExtractSolution(CpSolver solver, CpSolverStatus status, Dictionary<string, IntVar> variables, TimeSpan solveTime) // استخراج راه‌حل از مدل حل‌شده
        {
            var solution = new OrToolsShiftSolution
            {
                Status = ConvertSolverStatus(status),
                SolveTime = solveTime,
                ObjectiveValue = solver.ObjectiveValue,
                NumVariables = variables.Count,
                NumConstraints = _statistics.NumConstraints
            };

            if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
            {
                // استخراج انتساب‌ها
                foreach (var kvp in variables)
                {
                    if (kvp.Key.StartsWith("assign_") && solver.Value(kvp.Value) == 1)
                    {
                        var parts = kvp.Key.Split('_');
                        if (parts.Length >= 4)
                        {
                            var userIndex = int.Parse(parts[1]);
                            var shiftIndex = int.Parse(parts[2]);
                            var dateIndex = int.Parse(parts[3]);

                            var user = _constraints.UserConstraints[userIndex];
                            var shift = _constraints.ShiftRequirements[shiftIndex];
                            var date = _constraints.StartDate.AddDays(dateIndex);

                            solution.AddAssignment(user.UserId, shift.ShiftId, date, shift.ShiftLabel);
                        }
                    }
                }

                // استخراج آمارهای حل‌کننده
                _statistics.Status = solution.Status;
                _statistics.SolveTime = solveTime;
                _statistics.ObjectiveValue = solver.ObjectiveValue;
                _statistics.NumBranches = (int)solver.NumBranches();
                _statistics.NumConflicts = (int)solver.NumConflicts();
                //_statistics.NumRestarts = (int)solver.NumRestarts();
                // نسخه فعلی OR-Tools متد NumRestarts را اکسپوز نمی‌کند
            }
            else
            {
                solution.Violations.Add($"Solver status: {status}");
            }

            return solution;
        }

        /// <summary>
        /// تبدیل وضعیت حل‌کننده
        /// </summary>
        private OrToolsSolverStatus ConvertSolverStatus(CpSolverStatus status) // نگاشت وضعیت OR-Tools به enum داخلی
        {
            switch (status)
            {
                case CpSolverStatus.Optimal: return OrToolsSolverStatus.Optimal;
                case CpSolverStatus.Feasible: return OrToolsSolverStatus.Feasible;
                case CpSolverStatus.Infeasible: return OrToolsSolverStatus.Infeasible;
                default: return OrToolsSolverStatus.Unknown; // سایر حالات در نسخه فعلی پشتیبانی نمی‌شوند
            }
        }

        /// <summary>
        /// دریافت آمارهای الگوریتم
        /// </summary>
        public OrToolsStatistics GetStatistics() // دریافت آمار اجرای حل‌کننده
        {
            return _statistics;
        }
    }
}
