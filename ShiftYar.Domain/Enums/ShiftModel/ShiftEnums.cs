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
    }
}
