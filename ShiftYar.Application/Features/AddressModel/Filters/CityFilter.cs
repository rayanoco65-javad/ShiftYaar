using ShiftYar.Application.Common.Filters;
using ShiftYar.Domain.Entities.AddressModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.AddressModel.Filters
{
    public class CityFilter : BaseFilter<City>
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public int? ProvinceId { get; set; }

        // Pagination parameters
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10000;

        public CityFilter()
        {
            SortBy = "Name";
            SortAscending = true;
        }

        public override Expression<Func<City, bool>> GetExpression()
        {
            Expression<Func<City, bool>> expression = city => true;

            if (Id.HasValue)
            {
                Expression<Func<City, bool>> idExpr = city => city.Id == Id;
                expression = CombineExpressions(expression, idExpr);
            }

            if (!string.IsNullOrEmpty(Name))
            {
                Expression<Func<City, bool>> nameExpr = city => city.Name == Name;
                expression = CombineExpressions(expression, nameExpr);
            }

            if (ProvinceId.HasValue)
            {
                Expression<Func<City, bool>> provinceIdExpr = city => city.ProvinceId == ProvinceId;
                expression = CombineExpressions(expression, provinceIdExpr);
            }

            return expression;
        }
    }
}
