using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Api.Filters;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System.Globalization;

namespace ShiftYar.Api.Controllers
{
    [ServiceFilter(typeof(RequestLoggingFilter))]
    [Route("[action]")]
    [ApiController]
    public class BaseController : ControllerBase
    {
        private readonly ShiftYarDbContext _context;

        public BaseController(ShiftYarDbContext context)
        {
            _context = context;
        }
    }
}
