namespace ShiftYar.Domain.Entities.ProductivityModel
{
    /// <summary>
    /// Represents the shift rotation pattern of an employee for productivity hour deduction calculations.
    /// </summary>
    public enum ShiftPatternType
    {
        /// <summary>ثابت روزکار (بدون کسر ساعت نوبت‌کاری / ۰.۰ ساعت در هفته)</summary>
        FixedDay = 0,

        /// <summary>در گردش دو نوبته (صبح/عصر، صبح/شب یا عصر/شب - ۱.۰ ساعت در هفته)</summary>
        TwoShiftRotating = 1,

        /// <summary>در گردش سه نوبته کامل (صبح، عصر و شب - ۱.۰ ساعت در هفته)</summary>
        ThreeShiftRotating = 2,

        /// <summary>ثابت شب (۱.۰ ساعت در هفته)</summary>
        FixedNight = 3
    }
}
