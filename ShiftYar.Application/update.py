import re

sa_path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\SimulatedAnnealingScheduler.cs'
with open(sa_path, 'r', encoding='utf-8') as f:
    sa = f.read()

sa = re.sub(r'(public void StabilizeManagerMixAndNightQuotas.*?for \(var round = 0; round < 12; round\+\+\)\s*\{)(.*?)(if \(!HasUnmetManagerMix\(solution\) && GetExactNightQuotaViolations\(solution\)\.Count == 0\)\s*\{\s*return;\s*\})',
            r'\1\n                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0) return;\n\2', sa, flags=re.DOTALL)

sa = re.sub(r'(for \(var round = 0; round < 8; round\+\+\)\s*\{)(.*?)(if \(!HasUnmetManagerMix\(solution\) && GetExactNightQuotaViolations\(solution\)\.Count == 0\)\s*\{\s*return;\s*\})',
            r'\1\n                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0) return;\n\2', sa, flags=re.DOTALL)

sa = re.sub(r'(for \(var round = 0; round < 6; round\+\+\)\s*\{)(.*?)(if \(GetExactNightQuotaViolations\(solution\)\.Count == 0 && !HasUnmetManagerMix\(solution\)\)\s*\{\s*return;\s*\})',
            r'\1\n                if (!HasUnmetManagerMix(solution) && GetExactNightQuotaViolations(solution).Count == 0) return;\n\2', sa, flags=re.DOTALL)

sa = re.sub(r'(private void RestoreDeficitNightQuotas\(ShiftSolution solution\)\s*\{\s*)ExactNightQuotaGuard\.Enforce\(solution, _constraints\);\s*ExactNightQuotaGuard\.ForceSatisfyAllDeficits',
            r'\1ExactNightQuotaGuard.ForceSatisfyAllDeficits', sa)

sa = re.sub(r'(ShiftManagerMixGuard\.EnsureOrThrow\(bestSolution, _constraints\);\s*)(stopwatch\.Stop\(\);)',
            r'\1ExactNightQuotaGuard.OptimizeSpread(bestSolution, _constraints);\n            \2', sa)

with open(sa_path, 'w', encoding='utf-8') as f:
    f.write(sa)

nq_path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\ExactNightQuotaGuard.cs'
with open(nq_path, 'r', encoding='utf-8') as f:
    nq = f.read()

# Instead of regex matching persian strings, match the ImproveNightSpread method usage in Enforce
nq = re.sub(r'// [^\n]*\n\s*foreach \(var user in constraints\.UserConstraints\.Where\(u => u\.HasExactNightQuota \|\| u\.ExactHolidayWeekendNightShiftCount\.HasValue\)\)\s*\{\s*ImproveNightSpread\(solution, constraints, user, nightShift\);\s*\}', '', nq)

optimize_func = '''    public static void OptimizeSpread(ShiftSolution solution, ShiftConstraints constraints)
    {
        var nightShift = constraints.ShiftRequirements.FirstOrDefault(s => s.ShiftLabel == ShiftYar.Domain.Enums.ShiftModel.ShiftEnums.ShiftLabel.Night);
        if (nightShift == null) return;
        foreach (var user in constraints.UserConstraints.Where(u => u.HasExactNightQuota || u.ExactHolidayWeekendNightShiftCount.HasValue))
        {
            ImproveNightSpread(solution, constraints, user, nightShift);
        }
    }

    /// <summary>
    /// آخرین تلاش'''
nq = re.sub(r'    /// <summary>\n    /// آخرین تلاش', optimize_func, nq)

nq = re.sub(r'(for \(var pass = 0; pass < 6; pass\+\+\)\s*\{\s*)foreach \(var user in OrderUsersByDeficit\(solution, constraints\)\)',
            r'\1var defs = OrderUsersByDeficit(solution, constraints).ToList(); if (defs.Count == 0) break; foreach (var user in defs)', nq)

nq = re.sub(r'(for \(var round = 0; round < 4; round\+\+\)\s*\{\s*)foreach \(var user in OrderUsersByDeficit\(solution, constraints\)\)',
            r'\1var defs = OrderUsersByDeficit(solution, constraints).ToList(); if (defs.Count == 0) break; foreach (var user in defs)', nq)

nq = re.sub(r'(for \(var pass = 0; pass < 10 && stalePasses < 2; pass\+\+\)\s*\{\s*)var deficits = OrderUsersByDeficit\(solution, constraints\)\.ToList\(\);',
            r'\1var deficits = OrderUsersByDeficit(solution, constraints).ToList(); if (deficits.Count == 0) break;', nq)

with open(nq_path, 'w', encoding='utf-8') as f:
    f.write(nq)

dq_path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\ExactDayShiftQuotaGuard.cs'
with open(dq_path, 'r', encoding='utf-8') as f:
    dq = f.read()

dq = re.sub(r'(for \(var pass = 0; pass < 5; pass\+\+\)\s*\{\s*)foreach \(var user in OrderUsersByDeficit\(solution, constraints, label\)\)',
            r'\1var defs = OrderUsersByDeficit(solution, constraints, label).ToList(); if (defs.Count == 0) break; foreach (var user in defs)', dq)

with open(dq_path, 'w', encoding='utf-8') as f:
    f.write(dq)

print("Done")
