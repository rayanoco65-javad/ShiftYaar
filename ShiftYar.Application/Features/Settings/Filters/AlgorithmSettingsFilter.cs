using ShiftYar.Application.Common.Filters;

namespace ShiftYar.Application.Features.Settings.Filters
{
    public class AlgorithmSettingsFilter : BaseFilter<ShiftYar.Domain.Entities.Settings.AlgorithmSettings>
    {
        public int? DepartmentId { get; set; }
        public int? AlgorithmType { get; set; }
        public bool? IsGlobal { get; set; } // true = سراسری، false = مخصوص دپارتمان

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public override System.Linq.Expressions.Expression<System.Func<ShiftYar.Domain.Entities.Settings.AlgorithmSettings, bool>> GetExpression()
        {
            return x =>
                (DepartmentId == null || x.DepartmentId == DepartmentId) &&
                (AlgorithmType == null || x.AlgorithmType == AlgorithmType) &&
                (IsGlobal == null || (IsGlobal.Value ? x.DepartmentId == null : x.DepartmentId != null));
        }
    }
}
