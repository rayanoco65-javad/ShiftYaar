using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Enums.DepartmentModel
{
    public class DepartmentEnums
    {
        /// <summary>
        /// نحوه سازمان‌دهی چیدمان و مهارت‌های نیروها در بخش
        /// </summary>
        public enum DepartmentStaffingMode
        {
            /// <summary>ساده / عمومی (فقط تخصص، بدون سطح و بدون مسئولیت خاص)</summary>
            Simple = 0,

            /// <summary>سطح‌بندی سلسله‌مراتبی (اورژانس، بخش‌های بستری: سطح ۱، سطح ۲، عادی)</summary>
            LevelBased = 1,

            /// <summary>مسئولیت‌محور / چندمهارته (اتاق عمل: اسکراب ۱، اسکراب ۲، سیرکولر، اد، وینیست)</summary>
            ResponsibilityBased = 2,

            /// <summary>ترکیبی (هم سطح‌بندی مسئول شیفت و هم مسئولیت‌های چندگانه)</summary>
            Hybrid = 3
        }
    }
}
