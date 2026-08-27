using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShiftYar.Domain.Enums.ShiftModel.ShiftEnums;

namespace ShiftYar.Application.DTOs.ShiftModel
{
    public class ShiftDtoAdd
    {
        [Required(ErrorMessage = "شناسه بخش الزامی است")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "نوع شیفت الزامی است")]
        public ShiftLabel Label { get; set; }

        [Required(ErrorMessage = "زمان شروع الزامی است")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "زمان پایان الزامی است")]
        public TimeSpan EndTime { get; set; }

        /// <summary>ساعات عملکرد — روز عادی، خارج از طرح بهره‌وری</summary>
        [Range(0.25, 48, ErrorMessage = "ساعات شیفت باید بین ۰٫۲۵ تا ۴۸ باشد.")]
        public double? WeekdayNonProductivityHours { get; set; }

        /// <summary>ساعات عملکرد — روز تعطیل، خارج از طرح بهره‌وری</summary>
        [Range(0.25, 48, ErrorMessage = "ساعات شیفت باید بین ۰٫۲۵ تا ۴۸ باشد.")]
        public double? HolidayNonProductivityHours { get; set; }

        /// <summary>ساعات عملکرد — روز عادی، داخل طرح بهره‌وری</summary>
        [Range(0.25, 48, ErrorMessage = "ساعات شیفت باید بین ۰٫۲۵ تا ۴۸ باشد.")]
        public double? WeekdayProductivityPlanHours { get; set; }

        /// <summary>ساعات عملکرد — روز تعطیل، داخل طرح بهره‌وری</summary>
        [Range(0.25, 48, ErrorMessage = "ساعات شیفت باید بین ۰٫۲۵ تا ۴۸ باشد.")]
        public double? HolidayProductivityPlanHours { get; set; }

        /// <summary>حداقل تعداد مسئول شیفت در این نوبت (۰/null = بدون الزام)</summary>
        [Range(0, 20, ErrorMessage = "تعداد مسئول شیفت باید بین ۰ تا ۲۰ باشد.")]
        public int? ManagerRequiredCount { get; set; }

        /// <summary>حداقل تعداد مسئول سطح ۱ در این نوبت</summary>
        [Range(0, 20, ErrorMessage = "تعداد مسئول سطح ۱ باید بین ۰ تا ۲۰ باشد.")]
        public int? ManagerMinLevel1Count { get; set; }
    }
}
