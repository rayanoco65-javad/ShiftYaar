import sys
sys.stdout.reconfigure(encoding='utf-8')
path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\ManagerMixFeasibilityChecker.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

start_str = 'public static void ValidateOrThrow(ShiftConstraints constraints)'
end_str = '    public static string? TryGetInfeasibilityReason(ShiftConstraints constraints)'
start = text.find(start_str)
end = text.find(end_str)

new_code = '''public static void ValidateOrThrow(ShiftConstraints constraints)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var reason = TryGetInfeasibilityReason(constraints);
        System.IO.File.AppendAllText(@"d:\\Hampadco\\RealProjects\\ShiftYar\\sa_perf3.txt", $"ManagerMixFeasibilityChecker took {sw.ElapsedMilliseconds}ms\\n");
        if (reason != null)
        {
            throw new InvalidOperationException(reason);
        }
    }

'''

text = text[:start] + new_code + text[end:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)
print('Injected successfully')
