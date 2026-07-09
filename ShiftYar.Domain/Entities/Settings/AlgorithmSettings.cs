using ShiftYar.Domain.Entities.BaseModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftYar.Domain.Entities.Settings
{
    public class AlgorithmSettings : BaseEntity
    {
        [Key]
        public int? Id { get; set; }

        [ForeignKey("Department")]
        public int? DepartmentId { get; set; } // اگر null باشد یعنی تنظیمات سراسری
        public DepartmentModel.Department? Department { get; set; }

        // نوع الگوریتم هدف
        [Required]
        public int AlgorithmType { get; set; } // 1=SA, 2=OR-Tools, 3=Hybrid مطابق SchedulingAlgorithm

        // پارامترهای Simulated Annealing
        public double? SA_InitialTemperature { get; set; }
        public double? SA_FinalTemperature { get; set; }
        public double? SA_CoolingRate { get; set; }
        public int? SA_MaxIterations { get; set; }
        public int? SA_MaxIterationsWithoutImprovement { get; set; }

        // پارامترهای OR-Tools
        public int? ORT_MaxTimeInSeconds { get; set; }
        public int? ORT_NumSearchWorkers { get; set; }
        public bool? ORT_LogSearchProgress { get; set; }
        public int? ORT_MaxSolutions { get; set; }
        public double? ORT_RelativeGapLimit { get; set; }

        // پارامترهای Hybrid
        public int? HYB_Strategy { get; set; } // مطابق HybridStrategy enum
        public int? HYB_MaxIterations { get; set; }
        public double? HYB_ComplexityThreshold { get; set; }
    }
}


