import sys, re
sys.stdout.reconfigure(encoding='utf-8')
path = r'd:\Hampadco\RealProjects\ShiftYar\ShiftYar.Application\Features\ShiftModel\SimulatedAnnealing\SimulatedAnnealingScheduler.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

new_code = '''
        private ShiftSolution GenerateNeighbor(ShiftSolution currentSolution)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var neighbor = currentSolution.Clone();
            var tClone = sw.ElapsedMilliseconds; sw.Restart();
            var moveType = "";

            var roll = _random.NextDouble();
            if (roll < 0.20)
            {
                PerformManagerMixRepairMove(neighbor);
                moveType = "PerformManagerMixRepairMove";
            }
            else if (roll < 0.35)
            {
                PerformReassignMove(neighbor);
                moveType = "PerformReassignMove";
            }
            else if (roll < 0.50)
            {
                PerformHourBalanceMove(neighbor);
                moveType = "PerformHourBalanceMove";
            }
            else if (roll < 0.65)
            {
                PerformMorningEveningBalanceMove(neighbor);
                moveType = "PerformMorningEveningBalanceMove";
            }
            else if (roll < 0.78)
            {
                PerformSwapMove(neighbor);
                moveType = "PerformSwapMove";
            }
            else if (roll < 0.89)
            {
                PerformAddMove(neighbor);
                moveType = "PerformAddMove";
            }
            else
            {
                PerformRemoveMove(neighbor);
                moveType = "PerformRemoveMove";
            }
            
            var tMove = sw.ElapsedMilliseconds; sw.Restart();
            CalculateSolutionScore(neighbor);
            var tScore = sw.ElapsedMilliseconds;

            if (tClone > 1 || tMove > 1 || tScore > 1) 
            {
                System.IO.File.AppendAllText(@"d:\\Hampadco\\RealProjects\\ShiftYar\\sa_perf.txt", $"Clone: {tClone}ms, Move ({moveType}): {tMove}ms, Score: {tScore}ms\n");
            }

            return neighbor;
        }
'''

text = re.sub(r'private ShiftSolution GenerateNeighbor\(ShiftSolution currentSolution\)\s*\{[^{}]*var neighbor.*?return neighbor;\s*\}', new_code, text, flags=re.DOTALL)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)
print('Injected telemetry')
