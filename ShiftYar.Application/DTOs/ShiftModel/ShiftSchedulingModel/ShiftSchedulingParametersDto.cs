using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel
{
    /// <summary>
    /// پارامترهای الگوریتم Simulated Annealing
    /// </summary>
    public class ShiftSchedulingParametersDto
    {
        public double InitialTemperature { get; set; } = 1000.0;
        public double FinalTemperature { get; set; } = 0.1;
        public double CoolingRate { get; set; } = 0.95;
        public int MaxIterations { get; set; } = 10000;
        public int MaxIterationsWithoutImprovement { get; set; } = 1000;
    }
}
