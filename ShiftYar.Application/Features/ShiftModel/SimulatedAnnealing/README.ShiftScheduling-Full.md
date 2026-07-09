# راهنمای جامع شیفت‌بندی از صفر تا صد (با الگوریتم Simulated Annealing)

این سند، روند کامل «شیفت‌بندی» در سیستم ShiftYar را از دید مفهومی، معماری، اجزای پروژه، ترتیب اجرای متدها و سناریوهای عملیاتی توضیح می‌دهد. مخاطب این سند توسعه‌دهندگان و اپراتورهای فنی هستند.

## 1) مسئله شیفت‌بندی چیست؟
- **هدف**: تخصیص بهینه پرسنل به شیفت‌های کاری در بازه زمانی دلخواه با رعایت محدودیت‌ها و ترجیحات.
- **محدودیت‌ها**: تعداد نیرو بر حسب تخصص، تعادل جنسیتی، حداکثر نوبت‌های متوالی، حداقل استراحت، حداکثر شیفت هفتگی/شبانه و ...
- **ترجیحات**: تاریخ‌های عدم حضور، شیفت‌های ترجیحی/ناخواسته.

برای حل مسئله، از الگوریتم Simulated Annealing استفاده می‌کنیم که با حرکت‌های همسایگی (Swap/Reassign/Add/Remove) و تابع امتیازدهی (Cost Function) به‌صورت تکرارشونده به راه‌حل‌های بهتر نزدیک می‌شود.

## توابعی مثل Swap، Reassign، Add و Remove از اجزای کلیدی الگوریتم Simulated Annealing (SA) هستن، چون SA ذاتاً بر اساس ایجاد تغییرات تصادفی کوچک در یک جواب فعلی (Neighbor Generation) کار می‌کنه تا فضای جواب‌ها رو جست‌وجو کنه.
## در الگوریتم Simulated Annealing، ما یک جواب اولیه داریم و در هر گام:

    - یک جواب همسایه (Neighbor Solution) تولید می‌کنیم (با استفاده از یکی از این توابع).

    - تفاوت هزینه یا کیفیت بین جواب فعلی و جواب جدید را حساب می‌کنیم.

    - با احتمال خاصی (وابسته به دما و تفاوت هزینه)، جواب جدید را می‌پذیریم یا رد می‌کنیم.

بنابراین، این توابع در واقع اپراتورهای ایجاد همسایه‌ها (Neighborhood Operators) هستن.

# تابع Swap

    - کاربرد: جابه‌جایی دو عنصر (وظیفه، شیفت، نقطه، آیتم و ...) در جواب فعلی. مثلا
    دو نفر از پرسنل را بین دو شیفت جابه‌جا کن یا در مسأله مسیر (TSP): جای دو شهر را در مسیر عوض کن.

# تابع Reassign

    -  کاربرد: تخصیص مجدد یک عنصر به موقعیت یا گروه دیگر. مثلا
    یک پرسنل را از شیفت صبح به شیفت شب منتقل کن یا در تخصیص وظایف: یک کار را به ماشین دیگری بده

# تابع Add

    - کاربرد: افزودن یک عنصر جدید به جواب فعلی. مثلا
    اضافه کردن یک پرسنل جدید به شیفت یا افزودن یک نقطه جدید به مسیر بازدید.

# تابع Remove

    - کاربرد: حذف یک عنصر از جواب فعلی. مثلا
    حذف یک پرسنل از شیفتی که بیش از حد نیرو دارد یا حذف یک نقطه غیرضروری از مسیر.
.

## 2) اجزای مرتبط در پروژه
- **Domain**:
  - `Shift` (تعریف شیفت، زمان‌ها، لیبل)
  - `ShiftRequiredSpecialty` (نیازمندی تخصص/تعداد برای هر شیفت)
  - `ShiftAssignment` (ثبت انتساب نهایی در پایگاه‌داده)
  - `User` (ویژگی‌های پرسنل: جنسیت، تخصص، نوع/زیرنوع شیفت و ...)
  - `ShiftDate` (تقویم روزها)
- **Application (لایه منطق کسب‌وکار)**:
  - DTOs: `ShiftSchedulingRequestDto`, `ShiftSchedulingResultDto`, ...
  - مدل‌های الگوریتم: `ShiftConstraints`, `SaShiftAssignment`, `ShiftSolution`, `SimulatedAnnealingParameters`, `AlgorithmStatistics`
  - سرویس: `IShiftSchedulingService` و `ShiftSchedulingService`
  - الگوریتم: `SimulatedAnnealingScheduler`
- **API**:
  - Controller: `ShiftSchedulingController` (endpoints اجرای/آمار/اعتبارسنجی/ذخیره)

## 3) جریان داده از درخواست تا ذخیره خروجی
1. درخواست کاربر (Supervisor/Admin) به API → `ShiftSchedulingController`
2. اعتبارسنجی مقدماتی ModelState
3. فراخوانی سرویس: `IShiftSchedulingService.OptimizeShiftScheduleAsync(request)`
4. در سرویس:
   - بارگذاری محدودیت‌ها از پایگاه‌داده: `LoadConstraintsAsync`
   - ساخت پارامترهای الگوریتم از `request.Parameters`
   - اجرای الگوریتم: `new SimulatedAnnealingScheduler(constraints, parameters).Optimize()`
   - تبدیل راه‌حل به DTO خروجی: `ConvertSolutionToResultAsync`
5. بازگرداندن نتیجه به کنترلر → بازگشت به کلاینت
6. (اختیاری) ذخیره خروجی: `SaveOptimizedScheduleAsync(result)` → ایجاد رکوردهای `ShiftAssignment`

## 4) ترتیب اجرای متدها (گام‌به‌گام)
- **Optimize API**
  1) `ShiftSchedulingController.OptimizeShiftSchedule`
  2) `ShiftSchedulingService.OptimizeShiftScheduleAsync`
     - `LoadConstraintsAsync`
     - ساخت `SimulatedAnnealingParameters`
     - `SimulatedAnnealingScheduler.Optimize`
       - GenerateInitialSolution
       - تکرار: GenerateNeighbor → Evaluate/Accept → Cooling → Stop Criteria
     - `ConvertSolutionToResultAsync`
  3) بازگشت `ShiftSchedulingResultDto`

- **Validate API**
  1) `ShiftSchedulingController.ValidateConstraints`
  2) `ShiftSchedulingService.ValidateConstraintsAsync`
     - چک تاریخ‌ها، دپارتمان، پارامترها، صحت کاربران/محدودیت‌ها

- **Statistics API**
  1) `ShiftSchedulingController.GetAlgorithmStatistics`
  2) `ShiftSchedulingService.GetAlgorithmStatisticsAsync`
     - اجرای مشابه Optimize در ابعاد کوچک‌تر + `GetStatistics()`

- **Save API**
  1) `ShiftSchedulingController.SaveOptimizedSchedule`
  2) `ShiftSchedulingService.SaveOptimizedScheduleAsync`
     - (TODO) حذف انتساب‌های قبلی بازه
     - افزودن `ShiftAssignment`های جدید

## 5) ساختار درخواست ورودی (نمونه)
```json
{
  "departmentId": 1,
  "startDate": "2025-10-01T00:00:00Z",
  "endDate": "2025-10-31T23:59:59Z",
  "constraints": [
    {
      "userId": 12,
      "unavailableDates": ["2025-10-05T00:00:00Z"],
      "preferredShifts": [0, 1],
      "unwantedShifts": [2],
      "maxConsecutiveShifts": 3,
      "minRestDaysBetweenShifts": 1,
      "maxShiftsPerWeek": 5,
      "maxNightShiftsPerMonth": 8
    }
  ],
  "parameters": {
    "initialTemperature": 1000,
    "finalTemperature": 0.1,
    "coolingRate": 0.95,
    "maxIterations": 10000,
    "maxIterationsWithoutImprovement": 1000
  }
}
```

## 6) منطق الگوریتم (Simulated Annealing)
- **راه‌حل اولیه**: تخصیص تصادفی معتبر از بین کاربران واجد شرایط هر تخصص/شیفت/روز.
- **حرکت‌های همسایگی**:
  - Swap: تعویض دو انتساب
  - Reassign: جابجایی یک انتساب به کاربر دیگر واجد شرایط
  - Add: افزودن یک انتساب جدید (در صورت ظرفیت)
  - Remove: حذف یک انتساب
- **تابع امتیاز**:
  - امتیاز پایه: منفی متناسب با تعداد انتساب‌ها (کمتر → بهتر)
  - جریمه‌ها: نقض حد استراحت/توالی/هفتگی/شبانه، عدم تعادل جنسیتی، عدم تکمیل نیازمندی تخصص، بی‌توجهی به ترجیحات
- **پذیرش راه‌حل**:
  - اگر بهتر: پذیرش
  - اگر بدتر: با احتمال `exp(-Δ/Temp)`
- **سردسازی**: `Temp *= CoolingRate`
- **توقف**: رسیدن به دمای نهایی یا N بار عدم بهبود یا تمام شدن `MaxIterations`

## 7) سرویس‌ها و کلاس‌های کلیدی
- `ShiftSchedulingService`
  - `OptimizeShiftScheduleAsync(request)`
  - `ValidateConstraintsAsync(request)`
  - `GetAlgorithmStatisticsAsync(request)`
  - `SaveOptimizedScheduleAsync(result)`
- `SimulatedAnnealingScheduler`
  - `Optimize()` → خروجی: `ShiftSolution`
  - `GetStatistics()`
- مدل‌های الگوریتم
  - `ShiftConstraints`, `UserConstraint`, `ShiftRequirement`, `SpecialtyRequirement`
  - `ShiftSolution`, `SaShiftAssignment`
  - `SimulatedAnnealingParameters`, `AlgorithmStatistics`
- DTOs
  - `ShiftSchedulingRequestDto`, `ShiftSchedulingResultDto`, `ShiftAssignmentDto`, ...

## 8) امنیت و دسترسی
- کنترلر: `[Authorize(Roles = "Admin,Supervisor")]`
- برای تغییر نقش‌ها، نام رول‌ها باید با مقدار `Role.Name` در دیتابیس هم‌خوان باشد.

## 9) ثبت در DI
در `ApplicationDependencyInjection`:
- `services.AddScoped<IShiftSchedulingService, ShiftSchedulingService>();`

## 10) تست API (اختصار)
- فایل `ShiftYar.Api/ShiftScheduling.http` شامل درخواست‌های نمونه برای:
  - Validate
  - Statistics
  - Optimize
  - Optimize-And-Save
  - Save

## 11) نکات عملکردی و تنظیمات
- بازه‌های زمانی کوچک‌تر (1 تا 3 ماه) زمان بهینه‌تری دارند.
- با افزایش محدودیت‌ها/کمبود نیرو زمان اجرا و جریمه‌ها افزایش می‌یابد.
- با تغییر `InitialTemperature`, `CoolingRate`, `MaxIterations` کیفیت/زمان تغییر می‌کند.

## 12) عیب‌یابی رایج
- نقش‌ها کار نمی‌کنند → بررسی `[Authorize(Roles=...)]` و Claimهای Role در JWT
- کمبود نیرو در یک تخصص → مقدار `ShiftRequiredSpecialty.RequiredTotalCount` را تعدیل کنید یا نیرو اضافه کنید
- عدم تعادل جنسیتی زیاد → `MinGenderBalanceRatio` و نیازمندی‌های جنسیتی را تنظیم کنید
- زمان اجرای بالا → کاهش بازه یا کاهش `MaxIterations` یا افزایش `CoolingRate`

## 13) کارهای آتی (پیشنهادی)
- حذف انتساب‌های قبلی بازه در `SaveOptimizedScheduleAsync`
- افزودن محدودیت‌های پیچیده‌تر (چرخش الگوهای TwoShifts/ThreeShifts)
- گزارش‌های مدیریتی از آمار نهایی تخصیص
- واحدتست برای تابع امتیاز و حرکت‌ها

---
این راهنما نمای کلی و عملیاتی کامل شیفت‌بندی از دریافت ورودی تا ذخیره خروجی را ارائه می‌دهد. برای جزییات API و مثال‌های کامل، به `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/README.md` و فایل `ShiftYar.Api/ShiftScheduling.http` مراجعه کنید.
