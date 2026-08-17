# راهنمای فارسی محاسبه ساعت موظفی مبتنی بر آیین‌نامه بهره‌وری

این مستند توضیح می‌دهد که چه قابلیت‌هایی به سامانه شیفت‌یاری اضافه شده، فرمول‌های محاسبه ساعت موظفی پرسنل بالینی بر اساس «آیین‌نامه ارتقای بهره‌وری کارکنان بالینی» چگونه پیاده‌سازی شده و چطور می‌توانید از سرویس جدید استفاده کنید.

---

## ۱. چه چیزی به پروژه اضافه شد؟

- **مدل‌های دامنه‌ای بهره‌وری** در `ShiftYar.Domain/Entities/ProductivityModel/` شامل تنظیمات آیین‌نامه (`ProductivityRuleConfig`) و اطلاعات استخدامی پرسنل (`StaffEmploymentInfo`).
- **DTO‌ های ورود داده** در `ShiftYar.Application/DTOs/ProductivityModel/` مانند `StaffEmploymentInfoDto` و `WorkingHoursCalculationRequestDto` که ورودی/خروجی API‌ها و سرویس‌ها را از لایه دامنه جدا می‌کند.
- **DTOهای محاسبه** در `ShiftYar.Application/DTOs/ProductivityModel/` برای ورود داده، خروجی و جزئیات محاسبه.
- **واسط جدید** `IWorkingHoursCalculator` در `ShiftYar.Application/Interfaces/ProductivityModel/` که لایه Application را از پیاده‌سازی جدا می‌کند.
- **سرویس محاسبات** `WorkingHoursCalculator` در `ShiftYar.Application/Features/ProductivityModel/Services/` که منطق اصلی را پیاده‌سازی می‌کند و در DI ثبت شده است.
- **مستند انگلیسی کوتاه** در مسیر `ShiftYar.Application/Features/ProductivityModel/README.md` و این مستند فارسی (`README_FA.md`) برای توضیحات تکمیلی.

---

## ۲. منطق محاسبه ساعت موظفی

طبق آیین‌نامه، ساعت موظفی هفتگی پایه برای نیروهای بالینی **۴۴ ساعت** است. این مقدار می‌تواند حداکثر تا **۸ ساعت** کاهش یابد و کاهش‌ها از سه منبع تشکیل می‌شوند:

1. **سابقه خدمت (حداکثر ۵ ساعت)**  
   مقادیر دقیق در جدول آیین‌نامه پیاده‌سازی شده (بخش بعدی).
2. **سختی کار (حداکثر ۲ ساعت)**  
   اگر پرسنل در بخش‌های دارای ضریب سختی کار باشد، ۲ ساعت از موظفی هفتگی کسر می‌شود.
3. **شیفت‌های غیرمتعارف/چرخشی (حداکثر ۱ ساعت)**  
   اگر فرد در الگوهای چرخشی غیرمتعارف (شب و تعطیل) باشد، ۱ ساعت از موظفی کم می‌شود.

همچنین ساعات واقعی کار در **شب/تعطیل** با ضریب **۱٫۵** محاسبه می‌شود؛ یعنی هر ساعت شب معادل ۱٫۵ ساعت موظفی محسوب می‌شود و مازاد ۰٫۵ ساعت به عنوان «اعتبار» از موظفی ماهانه کسر می‌گردد.

---

## ۳. فرمول‌های محاسبه

- پایه هفتگی: `BaseWeekly = 44`
- مجموع کاهش هفتگی:  
  `WeeklyReduction = min(8, Seniority + Hardship + Rotating)`
- موظفی هفتگی نهایی:  
  `WeeklyRequired = max(0, BaseWeekly - WeeklyReduction)`
- پایه ماهانه (قبل از کاهش):  
  `MonthlyBase = 44 × NumberOfWeeks`
- کاهش ماهانه ناشی از کسورات هفتگی:  
  `WeeklyDeductionsMonthly = WeeklyReduction × NumberOfWeeks`
- اعتبار ساعات شب/تعطیل:  
  `NightHolidayCredit = (NightHours × 1.5) - NightHours`
- موظفی ماهانه نهایی:  
  `FinalMonthly = max(0, MonthlyBase - WeeklyDeductionsMonthly - NightHolidayCredit)`

### ۳.۱ override دستی ساعت موظفی (`MaxProductivityRequiredHours`)

در فرم کاربر (`UserDtoAdd` / `UserDtoGet`) فیلد **`MaxProductivityRequiredHours`** اضافه شده:

| حالت | رفتار |
|------|--------|
| `null` یا `0` | محاسبه خودکار طبق آیین‌نامه (سابقه، `HardshipPercent`، شیفت گردشی و …) |
| مقدار **مثبت** | همان عدد به‌عنوان **ساعت موظفی ماهانه** در شیفت‌بندی اعمال می‌شود |

Resolver مربوط: `ProductivityRequiredHoursResolver` — در `ShiftSchedulingService.LoadConstraints` فراخوانی می‌شود.

جدول کاهش سابقه:

| سابقه (سال) | کاهش هفتگی |
|-------------|------------|
| 1 تا 5      | 1 ساعت     |
| 6 تا 10     | 2 ساعت     |
| 11 تا 15    | 3 ساعت     |
| 16 تا 20    | 4 ساعت     |
| بیش از 20   | 5 ساعت     |

---

## ۴. فایل‌ها، سرویس‌ها و مدل‌های جدید

| مسیر | توضیح |
|------|-------|
| `ShiftYar.Domain/Entities/ProductivityModel/ProductivityRuleConfig.cs` | تنظیمات ثابت آیین‌نامه (۴۴ ساعت، سقف ۸ ساعت، ضرایب و جدول سابقه). |
| `ShiftYar.Domain/Entities/ProductivityModel/StaffEmploymentInfo.cs` | اطلاعات استخدامی/پرونده هر پرسنل برای محاسبه (تاریخ استخدام، سختی کار و ...). |
| `ShiftYar.Application/DTOs/ProductivityModel/WorkingHoursCalculationRequestDto.cs` | ورودی سرویس شامل ماه هدف، تعداد هفته، ساعات شب و … |
| `ShiftYar.Application/DTOs/ProductivityModel/WorkingHoursCalculationResultDto.cs` | خروجی تجمیعی شامل پایه ماهانه، کسورات و موظفی نهایی. |
| `ShiftYar.Application/DTOs/ProductivityModel/WorkingHoursCalculationBreakdownDto.cs` | جزئیات کامل محاسبه (کاهش‌ها، ضرایب، یادداشت‌ها). |
| `ShiftYar.Application/Interfaces/ProductivityModel/IWorkingHoursCalculator.cs` | واسط عمومی سرویس محاسبه. |
| `ShiftYar.Application/Features/ProductivityModel/Services/WorkingHoursCalculator.cs` | پیاده‌سازی منطق محاسبه. |
| `ShiftYar.Application/ApplicationDependencyInjection.cs` | ثبت `IWorkingHoursCalculator` در DI Container. |
| `ShiftYar.Application/Features/ProductivityModel/README.md` | مستند انگلیسی کوتاه. |
| `ShiftYar.Application/Common/Utilities/ProductivityRequiredHoursResolver.cs` | تعیین موظفی ماهانه (خودکار یا `MaxProductivityRequiredHours` دستی). |
| `ShiftYar.Application/Common/Utilities/ApprovedOffNightBeforeRules.cs` | OFF تأییدشده صبح/کل‌روز → مسدود کردن شب روز قبل. |
| `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/ProjectPersonnelProductivityPriority.cs` | اولویت غیرطرحی/طرحی در پر کردن موظفی. |
| `README_FA.md` | این مستند فارسی. |

---

## ۵. نحوه استفاده و نمونه فراخوانی

1. **تزریق وابستگی**  
   در کنترلر یا سرویس خود، `IWorkingHoursCalculator` را از طریق سازنده دریافت کنید. (در DI ثبت شده است.)

2. **ساخت درخواست بدون تکرار داده**  
   با استفاده از `StaffEmploymentInfoDto` یا متد `StaffEmploymentInfo.FromUser` اطلاعات را آماده کنید، سپس سرویس را صدا بزنید:

```csharp
using ShiftYar.Application.DTOs.ProductivityModel;
using ShiftYar.Domain.Entities.ProductivityModel;
using ShiftYar.Domain.Entities.UserModel;

public class ProductivityFacade
{
    private readonly IWorkingHoursCalculator _calculator;

    public ProductivityFacade(IWorkingHoursCalculator calculator)
    {
        _calculator = calculator;
    }

    public WorkingHoursCalculationResultDto GetMonthlyHours(User user, DateTime month, decimal nightHours)
    {
        var staffInfo = new StaffEmploymentInfoDto
        {
            StaffId = user.Id ?? 0,
            StaffFullName = user.FullName,
            DateOfEmployment = user.DateOfEmployment,
            HasHardshipDuty = true,
            HasUncommonRotatingShifts = false
        };

        var request = new WorkingHoursCalculationRequestDto
        {
            TargetMonth = month,
            NumberOfWeeksInMonth = 4, // یا محاسبه پویا بر اساس تقویم ماه
            NightHolidayHours = nightHours,
            Staff = staffInfo,
            RuleOverrides = new ProductivityRuleOverrideDto
            {
                // در صورت نیاز، مقادیر زیر را override کنید
                // BaseWeeklyHours = 42,
                // MaxWeeklyReduction = 7
            }
        };

        return _calculator.CalculateMonthlyHours(request);
    }
}
```

3. **دسترسی به خروجی**  
   - `BaseMonthlyHours`: پایه ماهانه قبل از کسورات  
   - `TotalDeductions`: مجموع کسورات (کاهش هفتگی × هفته‌ها + اعتبار شیفت شب/تعطیل)  
   - `FinalMonthlyRequiredHours`: موظفی نهایی  
   - `Breakdown`: جزئیات کامل برای گزارش‌دهی

---

## ۶. نکات پیکربندی و توسعه

- **تنظیمات آیین‌نامه**: اگر در آینده اعداد آیین‌نامه تغییر کرد، کافی‌ است مقادیر `ProductivityRuleConfig` را به‌روزرسانی یا نمونه جدیدی از آن را به `WorkingHoursCalculationRequestDto` تزریق کنید.
- **تعداد هفته‌های ماه**: ورودی `NumberOfWeeksInMonth` را می‌توانید با توجه به تقویم شمسی/میلادی محاسبه و ارسال کنید. مقدار پیش‌فرضی وجود ندارد و باید در هر فراخوان تعیین شود.
- **اطلاعات پرسنل**: با `StaffEmploymentInfo.FromUser(user, ...)` می‌توانید داده را بدون تکرار از موجودیت `User` استخراج کنید؛ اگر سابقه خدمت را قبلاً محاسبه کرده‌اید، مقدار `YearsOfServiceOverride` را ست کنید تا نیاز به تاریخ استخدام نباشد.
- **اطلاعات پرسنل**: اگر به داده‌های خام نیاز دارید از `StaffEmploymentInfoDto` استفاده کنید و در صورت داشتن آبجکت دامنه، همچنان متد `StaffEmploymentInfo.FromUser` قابل‌استفاده است؛ هر دو مسیر نهایتاً به مدل دامنه تبدیل می‌شوند.
- **Override تنظیمات**: اگر بیمارستان سیاست متفاوتی دارد، به‌جای ساخت مستقیم `ProductivityRuleConfig`، از `RuleOverrides` استفاده کنید تا مقادیر دلخواه (۴۴ ساعت، سقف ۸ ساعت، ضرایب و جدول سابقه) را فقط در همان فراخوانی تغییر دهید.
- **گسترشات آینده**: در صورت نیاز می‌توانید نوت‌های اختصاصی یا قوانین افزوده را در `WorkingHoursCalculationBreakdownDto.Notes` ثبت کنید تا در خروجی گزارش شود.
- **پوشش در الگوریتم‌های شیفت‌بندی**: کد فعلی شیفت‌سازی در سه مسیر (`SimulatedAnnealing`, `Hybrid`, `OrTools` در مسیر `ShiftYar.Application/Features/ShiftModel/`) هنوز مستقیماً از خروجی این سرویس استفاده نمی‌کند. برای همسان‌سازی باید در مرحله پیش‌پردازش داده‌ی ورودی به هر الگوریتم، نتیجه `WorkingHoursCalculationResultDto` به صورت Constraint یا Penalty به کلاس‌های `SimulatedAnnealingScheduler`, `HybridScheduler` و `OrToolsCPSatScheduler` تزریق شود.
- **دیپلوی**: تغییری در دیتابیس نیاز نیست؛ فقط کدهای Application و Domain به‌روزرسانی شده‌اند. بعد از Pull/Deploy، پروژه آماده استفاده از سرویس جدید است.
- **تست/Build**: اجرای `dotnet build ShiftYar.sln` باید بدون خطا باشد (هشدارهای قبلی پروژه همچنان ممکن است دیده شوند).

---

برای هرگونه سؤال یا توسعه بیشتر (مثلاً اتصال به گزارش‌ساز، داشبورد یا محاسبات ترکیبی با شیفت‌سازی)، از `WorkingHoursCalculator` به عنوان نقطه ورودی استفاده کنید و منطق تکمیلی را در لایه Application اضافه کنید. موفق باشید! 🎯

