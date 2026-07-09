using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftYar.Application.Common.Models.ResponseModel;
using ShiftYar.Application.DTOs.ShiftModel.ShiftSchedulingModel;
using ShiftYar.Application.Interfaces.ShiftModel;
using ShiftYar.Infrastructure.Persistence.AppDbContext;
using System.Threading.Tasks;

namespace ShiftYar.Api.Controllers.ShiftModel
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EmergencyReschedulingController : BaseController
    {
        private readonly IEmergencyReschedulingService _reschedulingService;

        public EmergencyReschedulingController(
            IEmergencyReschedulingService reschedulingService,
            ShiftYarDbContext context) : base(context)
        {
            _reschedulingService = reschedulingService;
        }

        [HttpPost("rolling-horizon")]
        public async Task<IActionResult> RescheduleAsync([FromBody] EmergencyReschedulingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<RollingHorizonRescheduleResultDto>.Fail("Invalid request data."));
            }

            var result = await _reschedulingService.RescheduleAsync(request);

            if (result.IsSuccess)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
    }
}

