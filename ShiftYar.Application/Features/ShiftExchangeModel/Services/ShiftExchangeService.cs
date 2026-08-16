using AutoMapper;
using ShiftYar.Application.Common.Filters;
using ShiftYar.Application.Common.Utilities;
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
            var exchangeType = ShiftExchangeValidator.ResolveExchangeType(dto.ExchangeType);
            var userError = ShiftExchangeValidator.ValidateUsersAreDifferent(dto.RequestingUserId, dto.OfferingUserId);
            if (userError != null)
            {
                throw new InvalidOperationException(userError);
            }

            var existingResult = await _repository.GetByFilterAsync();
            if (HasDuplicateExchange(existingResult.Items, dto, exchangeType))
            {
                throw new InvalidOperationException("درخواست جابجایی قبلاً ثبت شده است");
            }

            var requestingAssignment = await _shiftAssignmentRepository.GetByIdAsync(dto.RequestingShiftAssignmentId);
            if (requestingAssignment == null)
            {
                throw new InvalidOperationException("شیفت انتخاب‌شده یافت نشد");
            }

            if (requestingAssignment.UserId != dto.RequestingUserId)
            {
                throw new InvalidOperationException("شیفت انتخاب‌شده متعلق به کاربر درخواست‌کننده نیست");
            }

            if (!requestingAssignment.ShiftDateId.HasValue || !requestingAssignment.ShiftId.HasValue)
            {
                throw new InvalidOperationException("اطلاعات تاریخ/شیفت انتساب ناقص است");
            }

            var requestingUser = await _userRepository.GetByIdAsync(dto.RequestingUserId, "Department");
            if (requestingUser?.Department?.SupervisorId == null)
            {
                throw new InvalidOperationException("سوپروایزر دپارتمان تعریف نشده است");
            }

            var offeringUser = await _userRepository.GetByIdAsync(dto.OfferingUserId, "Department");
            var departmentError = ShiftExchangeValidator.ValidateSameDepartment(
                requestingUser.DepartmentId,
                offeringUser?.DepartmentId);
            if (departmentError != null)
            {
                throw new InvalidOperationException(departmentError);
            }

            ShiftAssignment? offeringAssignment = null;
            if (exchangeType == ExchangeType.Swap)
            {
                var offeringAssignmentError = ShiftExchangeValidator.ValidateSwapOfferingAssignment(dto.OfferingShiftAssignmentId);
                if (offeringAssignmentError != null)
                {
                    throw new InvalidOperationException(offeringAssignmentError);
                }

                offeringAssignment = await _shiftAssignmentRepository.GetByIdAsync(dto.OfferingShiftAssignmentId!.Value);
                if (offeringAssignment == null)
                {
                    throw new InvalidOperationException("شیفت کاربر مقابل یافت نشد");
                }

                if (offeringAssignment.UserId != dto.OfferingUserId)
                {
                    throw new InvalidOperationException("شیفت کاربر مقابل متعلق به او نیست");
                }
            }
            else
            {
                var transferAssignmentError = ShiftExchangeValidator.ValidateTransferOfferingAssignment(dto.OfferingShiftAssignmentId);
                if (transferAssignmentError != null)
                {
                    throw new InvalidOperationException(transferAssignmentError);
                }

                if (await HasAssignmentOnSameSlotAsync(
                        dto.OfferingUserId,
                        requestingAssignment.ShiftDateId.Value,
                        requestingAssignment.ShiftId.Value))
                {
                    throw new InvalidOperationException(
                        "کاربر دریافت‌کننده در زمان این شیفت انتساب فعال دارد. برای واگذاری باید در همان زمان آزاد (بدون شیفت) باشد.");
                }
            }

            var exchange = new ShiftExchange
            {
                RequestingUserId = dto.RequestingUserId,
                OfferingUserId = dto.OfferingUserId,
                RequestingShiftAssignmentId = dto.RequestingShiftAssignmentId,
                OfferingShiftAssignmentId = exchangeType == ExchangeType.Swap
                    ? dto.OfferingShiftAssignmentId
                    : null,
                ExchangeType = exchangeType,
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

            var requestingAssignment = await _shiftAssignmentRepository.GetByIdAsync(exchange.RequestingShiftAssignmentId!.Value);
            if (requestingAssignment == null)
            {
                throw new InvalidOperationException("شیفت مورد نظر یافت نشد");
            }

            if (requestingAssignment.UserId != exchange.RequestingUserId)
            {
                throw new InvalidOperationException("شیفت دیگر متعلق به کاربر درخواست‌کننده نیست؛ احتمالاً برنامه تغییر کرده است");
            }

            var exchangeType = ShiftExchangeValidator.ResolveExchangeType(exchange.ExchangeType);
            if (exchangeType == ExchangeType.Transfer)
            {
                if (!requestingAssignment.ShiftDateId.HasValue || !requestingAssignment.ShiftId.HasValue)
                {
                    throw new InvalidOperationException("اطلاعات تاریخ/شیفت انتساب ناقص است");
                }

                if (await HasAssignmentOnSameSlotAsync(
                        exchange.OfferingUserId!.Value,
                        requestingAssignment.ShiftDateId.Value,
                        requestingAssignment.ShiftId.Value))
                {
                    throw new InvalidOperationException(
                        "کاربر دریافت‌کننده اکنون در زمان این شیفت انتساب دارد؛ اجرای واگذاری ممکن نیست.");
                }

                requestingAssignment.UserId = exchange.OfferingUserId;
                _shiftAssignmentRepository.Update(requestingAssignment);
            }
            else
            {
                var offeringAssignment = await _shiftAssignmentRepository.GetByIdAsync(exchange.OfferingShiftAssignmentId!.Value);
                if (offeringAssignment == null)
                {
                    throw new InvalidOperationException("شیفت کاربر مقابل یافت نشد");
                }

                if (offeringAssignment.UserId != exchange.OfferingUserId)
                {
                    throw new InvalidOperationException("شیفت کاربر مقابل دیگر متعلق به او نیست؛ احتمالاً برنامه تغییر کرده است");
                }

                var requestingUserId = requestingAssignment.UserId;
                var offeringUserId = offeringAssignment.UserId;

                requestingAssignment.UserId = offeringUserId;
                offeringAssignment.UserId = requestingUserId;

                _shiftAssignmentRepository.Update(requestingAssignment);
                _shiftAssignmentRepository.Update(offeringAssignment);
            }

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

        private static bool HasDuplicateExchange(
            IEnumerable<ShiftExchange> existingExchanges,
            ShiftExchangeDtoAdd dto,
            ExchangeType exchangeType)
        {
            foreach (var existing in existingExchanges)
            {
                if (exchangeType == ExchangeType.Transfer &&
                    ShiftExchangeValidator.IsDuplicateTransfer(
                        existing.ExchangeType,
                        existing.Status,
                        existing.RequestingUserId,
                        existing.OfferingUserId,
                        existing.RequestingShiftAssignmentId,
                        dto.RequestingUserId,
                        dto.OfferingUserId,
                        dto.RequestingShiftAssignmentId))
                {
                    return true;
                }

                if (exchangeType == ExchangeType.Swap &&
                    dto.OfferingShiftAssignmentId.HasValue &&
                    ShiftExchangeValidator.IsDuplicateSwap(
                        existing.ExchangeType,
                        existing.Status,
                        existing.RequestingUserId,
                        existing.OfferingUserId,
                        existing.RequestingShiftAssignmentId,
                        existing.OfferingShiftAssignmentId,
                        dto.RequestingUserId,
                        dto.OfferingUserId,
                        dto.RequestingShiftAssignmentId,
                        dto.OfferingShiftAssignmentId.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> HasAssignmentOnSameSlotAsync(int userId, int shiftDateId, int shiftId)
        {
            var (assignments, _) = await _shiftAssignmentRepository.GetByFilterAsync(
                new SimpleFilter<ShiftAssignment>(a =>
                    a.UserId == userId &&
                    a.ShiftDateId == shiftDateId &&
                    a.ShiftId == shiftId));

            return assignments.Count > 0;
        }
    }
}
