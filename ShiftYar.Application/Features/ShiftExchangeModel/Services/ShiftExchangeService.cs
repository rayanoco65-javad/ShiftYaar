using AutoMapper;
using ShiftYar.Application.DTOs.ShiftExchangeModel;
using ShiftYar.Application.Interfaces;
using ShiftYar.Application.Interfaces.Persistence;
using ShiftYar.Domain.Entities.ShiftExchangeModel;
using ShiftYar.Domain.Entities.ShiftModel;
using ShiftYar.Domain.Entities.UserModel;
using ShiftYar.Domain.Enums.ShiftExchangeModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Features.ShiftExchangeModel.Services
{
    public class ShiftExchangeService : IShiftExchangeService
    {
        private readonly IEfRepository<ShiftExchange> _repository;
        private readonly IEfRepository<ShiftAssignment> _shiftAssignmentRepository;
        private readonly IEfRepository<User> _userRepository;
        private readonly IMapper _mapper;

        public ShiftExchangeService(
            IEfRepository<ShiftExchange> repository,
            IEfRepository<ShiftAssignment> shiftAssignmentRepository,
            IEfRepository<User> userRepository,
            IMapper mapper)
        {
            _repository = repository;
            _shiftAssignmentRepository = shiftAssignmentRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<ShiftExchangeDtoGet?> GetByIdAsync(int id)
        {
            var exchange = await _repository.GetByIdAsync(id);
            return _mapper.Map<ShiftExchangeDtoGet>(exchange);
        }

        public async Task<List<ShiftExchangeDtoGet>> GetAllAsync()
        {
            var result = await _repository.GetByFilterAsync();
            return _mapper.Map<List<ShiftExchangeDtoGet>>(result.Items);
        }

        public async Task<List<ShiftExchangeDtoGet>> GetByUserIdAsync(int userId)
        {
            var result = await _repository.GetByFilterAsync();
            var userExchanges = result.Items.Where(x => x.RequestingUserId == userId || x.OfferingUserId == userId).ToList();
            return _mapper.Map<List<ShiftExchangeDtoGet>>(userExchanges);
        }

        public async Task<List<ShiftExchangeDtoGet>> GetPendingApprovalsAsync(int supervisorId)
        {
            var result = await _repository.GetByFilterAsync();
            var pendingExchanges = result.Items.Where(x => x.SupervisorId == supervisorId && x.Status == ExchangeStatus.Pending).ToList();
            return _mapper.Map<List<ShiftExchangeDtoGet>>(pendingExchanges);
        }

        public async Task<ShiftExchangeDtoGet?> CreateAsync(ShiftExchangeDtoAdd dto)
        {
            // بررسی اینکه آیا درخواست قبلی وجود دارد یا نه
            var existingResult = await _repository.GetByFilterAsync();
            var existingExchange = existingResult.Items.FirstOrDefault(x => 
                (x.RequestingUserId == dto.RequestingUserId && x.OfferingUserId == dto.OfferingUserId &&
                 x.RequestingShiftAssignmentId == dto.RequestingShiftAssignmentId && 
                 x.OfferingShiftAssignmentId == dto.OfferingShiftAssignmentId) ||
                (x.RequestingUserId == dto.OfferingUserId && x.OfferingUserId == dto.RequestingUserId &&
                 x.RequestingShiftAssignmentId == dto.OfferingShiftAssignmentId && 
                 x.OfferingShiftAssignmentId == dto.RequestingShiftAssignmentId));

            if (existingExchange != null)
            {
                throw new InvalidOperationException("درخواست جابجایی قبلاً ثبت شده است");
            }

            // بررسی اینکه آیا شیفت‌ها متعلق به کاربران هستند
            var requestingAssignment = await _shiftAssignmentRepository.GetByIdAsync(dto.RequestingShiftAssignmentId);
            var offeringAssignment = await _shiftAssignmentRepository.GetByIdAsync(dto.OfferingShiftAssignmentId);

            if (requestingAssignment == null || offeringAssignment == null)
            {
                throw new InvalidOperationException("شیفت‌های انتخاب شده یافت نشدند");
            }

            if (requestingAssignment.UserId != dto.RequestingUserId || offeringAssignment.UserId != dto.OfferingUserId)
            {
                throw new InvalidOperationException("شیفت‌های انتخاب شده متعلق به کاربران نیستند");
            }

            // پیدا کردن سوپروایزر دپارتمان
            var requestingUser = await _userRepository.GetByIdAsync(dto.RequestingUserId);
            if (requestingUser?.Department?.SupervisorId == null)
            {
                throw new InvalidOperationException("سوپروایزر دپارتمان تعریف نشده است");
            }

            var exchange = new ShiftExchange
            {
                RequestingUserId = dto.RequestingUserId,
                OfferingUserId = dto.OfferingUserId,
                RequestingShiftAssignmentId = dto.RequestingShiftAssignmentId,
                OfferingShiftAssignmentId = dto.OfferingShiftAssignmentId,
                Status = ExchangeStatus.Pending,
                RequestDate = DateTime.Now,
                Reason = dto.Reason,
                SupervisorId = requestingUser.Department.SupervisorId
            };

            await _repository.AddAsync(exchange);
            await _repository.SaveAsync();

            return _mapper.Map<ShiftExchangeDtoGet>(exchange);
        }

        public async Task<ShiftExchangeDtoGet?> UpdateAsync(ShiftExchangeDtoUpdate dto)
        {
            var exchange = await _repository.GetByIdAsync(dto.Id);
            if (exchange == null)
            {
                return null;
            }

            if (exchange.Status != ExchangeStatus.Pending)
            {
                throw new InvalidOperationException("فقط درخواست‌های در انتظار تأیید قابل ویرایش هستند");
            }

            exchange.Reason = dto.Reason;
            exchange.UpdateDate = DateTime.Now;

            _repository.Update(exchange);
            await _repository.SaveAsync();

            return _mapper.Map<ShiftExchangeDtoGet>(exchange);
        }

        public async Task<bool> ApproveAsync(ShiftExchangeApprovalDto dto)
        {
            var exchange = await _repository.GetByIdAsync(dto.Id);
            if (exchange == null)
            {
                return false;
            }

            if (exchange.Status != ExchangeStatus.Pending)
            {
                throw new InvalidOperationException("فقط درخواست‌های در انتظار تأیید قابل تأیید یا رد هستند");
            }

            exchange.Status = dto.IsApproved ? ExchangeStatus.Approved : ExchangeStatus.Rejected;
            exchange.SupervisorComment = dto.SupervisorComment;
            exchange.ApprovalDate = DateTime.Now;
            exchange.UpdateDate = DateTime.Now;

            _repository.Update(exchange);
            await _repository.SaveAsync();

            return true;
        }

        public async Task<bool> ExecuteExchangeAsync(int exchangeId)
        {
            var exchange = await _repository.GetByIdAsync(exchangeId);
            if (exchange == null)
            {
                return false;
            }

            if (exchange.Status != ExchangeStatus.Approved)
            {
                throw new InvalidOperationException("فقط درخواست‌های تأیید شده قابل اجرا هستند");
            }

            // جابجایی شیفت‌ها
            var requestingAssignment = await _shiftAssignmentRepository.GetByIdAsync(exchange.RequestingShiftAssignmentId.Value);
            var offeringAssignment = await _shiftAssignmentRepository.GetByIdAsync(exchange.OfferingShiftAssignmentId.Value);

            if (requestingAssignment == null || offeringAssignment == null)
            {
                throw new InvalidOperationException("شیفت‌های مورد نظر یافت نشدند");
            }

            var requestingUserId = requestingAssignment.UserId;
            var offeringUserId = offeringAssignment.UserId;

            requestingAssignment.UserId = offeringUserId;
            offeringAssignment.UserId = requestingUserId;

            _shiftAssignmentRepository.Update(requestingAssignment);
            _shiftAssignmentRepository.Update(offeringAssignment);

            exchange.Status = ExchangeStatus.Executed;
            exchange.ExecutionDate = DateTime.Now;
            exchange.UpdateDate = DateTime.Now;

            _repository.Update(exchange);
            await _repository.SaveAsync();

            return true;
        }

        public async Task<bool> CancelAsync(int exchangeId)
        {
            var exchange = await _repository.GetByIdAsync(exchangeId);
            if (exchange == null)
            {
                return false;
            }

            if (exchange.Status == ExchangeStatus.Executed)
            {
                throw new InvalidOperationException("درخواست‌های اجرا شده قابل لغو نیستند");
            }

            exchange.Status = ExchangeStatus.Cancelled;
            exchange.UpdateDate = DateTime.Now;

            _repository.Update(exchange);
            await _repository.SaveAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var exchange = await _repository.GetByIdAsync(id);
            if (exchange == null)
            {
                return false;
            }

            if (exchange.Status == ExchangeStatus.Executed)
            {
                throw new InvalidOperationException("درخواست‌های اجرا شده قابل حذف نیستند");
            }

            _repository.Delete(exchange);
            await _repository.SaveAsync();

            return true;
        }
    }
}