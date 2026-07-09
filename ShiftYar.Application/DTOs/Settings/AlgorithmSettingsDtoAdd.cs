using System.ComponentModel.DataAnnotations;

namespace ShiftYar.Application.DTOs.Settings
{
    public class AlgorithmSettingsDtoAdd
    {
        public int? DepartmentId { get; set; } // null = سراسری، مقدار = مخصوص دپارتمان

        [Required]
        [Range(1, 3, ErrorMessage = "نوع الگوریتم باید بین 1 تا 3 باشد (1=SA, 2=OR-Tools, 3=Hybrid)")]
        public int AlgorithmType { get; set; }

        // پارامترهای Simulated Annealing
        [Range(0.1, double.MaxValue, ErrorMessage = "دمای اولیه باید مثبت باشد")]
        public double? SA_InitialTemperature { get; set; }

        [Range(0.001, double.MaxValue, ErrorMessage = "دمای نهایی باید مثبت باشد")]
        public double? SA_FinalTemperature { get; set; }

        [Range(0.1, 0.99, ErrorMessage = "نرخ کاهش دما باید بین 0.1 تا 0.99 باشد")]
        public double? SA_CoolingRate { get; set; }

        [Range(100, int.MaxValue, ErrorMessage = "حداکثر تکرار باید حداقل 100 باشد")]
        public int? SA_MaxIterations { get; set; }

        [Range(10, int.MaxValue, ErrorMessage = "حداکثر تکرار بدون بهبود باید حداقل 10 باشد")]
        public int? SA_MaxIterationsWithoutImprovement { get; set; }

        // پارامترهای OR-Tools
        [Range(1, 3600, ErrorMessage = "حداکثر زمان باید بین 1 تا 3600 ثانیه باشد")]
        public int? ORT_MaxTimeInSeconds { get; set; }

        [Range(1, 16, ErrorMessage = "تعداد تردهای جست‌وجو باید بین 1 تا 16 باشد")]
        public int? ORT_NumSearchWorkers { get; set; }

        public bool? ORT_LogSearchProgress { get; set; }

        [Range(1, 100, ErrorMessage = "حداکثر راه‌حل باید بین 1 تا 100 باشد")]
        public int? ORT_MaxSolutions { get; set; }

        [Range(0.001, 1.0, ErrorMessage = "حد گپ نسبی باید بین 0.001 تا 1.0 باشد")]
        public double? ORT_RelativeGapLimit { get; set; }

        // پارامترهای Hybrid
        [Range(1, 5, ErrorMessage = "استراتژی Hybrid باید بین 1 تا 5 باشد")]
        public int? HYB_Strategy { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "حداکثر تکرار Hybrid باید مثبت باشد")]
        public int? HYB_MaxIterations { get; set; }

        [Range(1.0, double.MaxValue, ErrorMessage = "آستانه پیچیدگی باید مثبت باشد")]
        public double? HYB_ComplexityThreshold { get; set; }
    }

    public class AlgorithmSettingsDtoGet : AlgorithmSettingsDtoAdd
    {
        public int Id { get; set; }
        public string? DepartmentName { get; set; } // نام دپارتمان (در صورت وجود)
        public string AlgorithmTypeName { get; set; } = string.Empty; // نام الگوریتم
        public DateTime? CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }
    }
}
