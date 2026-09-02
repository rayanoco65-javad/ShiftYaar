import sys
sys.stdout.reconfigure(encoding='utf-8')
path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\SimulatedAnnealingScheduler.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

start_str = 'private ShiftSolution GenerateFeasibleInitialSolution()'
end_str = '        private ShiftSolution GenerateInitialSolution()'
start = text.find(start_str)
end = text.find(end_str)

new_code = '''private ShiftSolution GenerateFeasibleInitialSolution()
        {
            const int maxAttempts = 8;
            ShiftSolution best = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                var candidate = GenerateInitialSolution();
                var tGen = sw2.ElapsedMilliseconds; sw2.Restart();
                
                bool feasible = IsFeasible(candidate);
                var tFeas = sw2.ElapsedMilliseconds;
                
                System.IO.File.AppendAllText(@"d:\\Hampadco\\RealProjects\\ShiftYar\\sa_perf2.txt", $"InitAttempt {attempt}: Gen={tGen}ms, Feas={tFeas}ms\\n");

                if (feasible)
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

'''

text = text[:start] + new_code + text[end:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)
print('Injected successfully')
