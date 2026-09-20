using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Domain.Enums.ShiftModel
{
    public class ShiftEnums
    {
        //انواع شیفت
        public enum ShiftTypes
        {
            FixedShift = 0,        //شیفت ثابت
            RotatingShift = 1      //شیفت درگردش
        }

        //انواع زیر شیفت
        public enum ShiftSubTypes
        {
            FixedMorning = 0,  //فیکس صبح
            FixedEvening = 1,  //فیکس عصر
            TwoShifts = 2,     //گردشی دونوبت کاری
            ThreeShifts = 3    //گردشی سه نوبت کاری
        }

        //حالات شیفت گردشی دونوبت کاری
        public enum TwoShiftRotationPattern
        {
            MorningEvening = 0,  // صبح/عصر
            MorningNight = 1,    // صبح/شب
            EveningNight = 2,    // عصر/شب
        }

        public enum ShiftLabel
        {
            Morning = 0,  //شیفت صبح
            Evening = 1,  //شیفت عصر
            Night = 2     //شیفت شب
        }

        /// <summary>
        /// انواع مجاز شیفت برای هر کاربر (تکی + ترکیب همان روز).
        /// ترکیب‌ها فقط وقتی معنا دارند که MaxShiftsPerDay ≥ 2 باشد.
        /// </summary>
        [Flags]
        public enum UserShiftPermission
        {
            None = 0,
            Morning = 1,
            Evening = 2,
            Night = 4,
            MorningEveningSameDay = 8,
            MorningNightSameDay = 16
        }

        public enum ShiftStatus
        {
            Planned = 0,     // برنامه‌ریزی‌شده
            Approved = 0,    // تأیید شده
            Cancelled = 0,   // لغو شده
            Completed = 0    // انجام شده
        }

        // انواع تنظیمات شب‌دوست/شب‌گریز
        public enum NightShiftPreferenceType
        {
            NightFriendly = 0,  // شب‌دوست - باقیمانده شیفت‌های شب به پرسنل با سابقه بیشتر
            NightAvoiding = 1,   // شب‌گریز - باقیمانده شیفت‌های شب به پرسنل با سابقه کمتر
            Neutral = 2          // خنثی - بدون ترجیح خاص
        }

        // انواع تنظیمات تمایل به اضافه کار و توزیع بر اساس سابقه
        public enum OvertimePreferenceType
        {
            OvertimeFriendly = 0, // علاقه‌مند به اضافه کار - سابقه بیشتر = اضافه کار بیشتر
            OvertimeAvoiding = 1, // گریزان از اضافه کار - سابقه کمتر = اضافه کار بیشتر (محافظت از با‌سابقه‌ها)
            Neutral = 2           // خنثی - توزیع مساوی اضافه کار
        }
    }
}
