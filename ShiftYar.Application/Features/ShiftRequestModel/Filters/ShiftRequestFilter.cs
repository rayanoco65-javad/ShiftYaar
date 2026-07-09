using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Utilities;
using ShiftYar.Domain.Entities.ShiftDateModel;
using ShiftYar.Domain.Entities.ShiftRequestModel;
using ShiftYar.Domain.Enums.ShiftRequestModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftRequestModel.Filters
{
    public class ShiftRequestFilter : BaseFilter<ShiftRequest>
    {
        public int? Id { get; set; }
        public int? UserId { get; set; }
        public int? SupervisorId { get; set; }
        //public int? DepartmentId { get; set; }
        public string? FromPersianDate { get; set; }
        public string? ToPersianDate { get; set; }
        public RequestStatus? Status { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public ShiftRequestFilter()
        {
            
        }

        private Expression<Func<ShiftRequest, bool>>? _expression;
        public ShiftRequestFilter(Expression<Func<ShiftRequest, bool>> expression)
        {
            _expression = expression;
        }

        public override Expression<Func<ShiftRequest, bool>> GetExpression()
        {
            if (_expression != null)
            {
                return _expression;
            }

            Expression<Func<ShiftRequest, bool>> expression = shiftDate => true;

            if (Id.HasValue)
            {
                Expression<Func<ShiftRequest, bool>> idExpr = shiftRequest => shiftRequest.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (UserId.HasValue)
            {
                Expression<Func<ShiftRequest, bool>> userIdExpr = ShiftRequest => ShiftRequest.UserId == UserId;
                expression = CombineExpressions(expression, userIdExpr);
            }

            if (SupervisorId.HasValue)
            {
                Expression<Func<ShiftRequest, bool>> supervisorIdExpr = ShiftRequest => ShiftRequest.SupervisorId == SupervisorId;
                expression = CombineExpressions(expression, supervisorIdExpr);
            }

            if (!string.IsNullOrEmpty(FromPersianDate))
            {
                DateTime gregorianFromDate = DateConverter.ConvertToGregorianDate(FromPersianDate);

                Expression<Func<ShiftRequest, bool>> fromDateExpr = ShiftRequest => ShiftRequest.RequestDate >= gregorianFromDate;
                expression = CombineExpressions(expression, fromDateExpr);
            }

            if (!string.IsNullOrEmpty(ToPersianDate))
            {
                DateTime gregorianToDate = DateConverter.ConvertToGregorianDate(ToPersianDate);

                Expression<Func<ShiftRequest, bool>> toDateExpr = ShiftRequest => ShiftRequest.RequestDate <= gregorianToDate;
                expression = CombineExpressions(expression, toDateExpr);
            }

            if (Status.HasValue)
            {
                Expression<Func<ShiftRequest, bool>> statusExpr = ShiftRequest => ShiftRequest.Status == Status;
                expression = CombineExpressions(expression, statusExpr);
            }

            return expression;
        }
    }
}
