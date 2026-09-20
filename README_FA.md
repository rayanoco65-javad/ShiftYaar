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

ساعت موظفی ماهانه پرسنل بر اساس روزهای کاری ماه و قوانین ارتقای بهره‌وری محاسبه می‌شود:
- **ساعت موظف روزانه غیرتعطیل:** $\frac{22}{3} \approx 7.333333$ ساعت (۷ ساعت و ۲۰ دقیقه).
- **روزهای کاری ماه:** $\text{WorkingDays} = \text{TotalDays} - (\text{FridaysCount} + \text{OfficialHolidaysCount})$
  (پنج‌شنبه‌ها برای مراکز درمانی ۲۴ ساعته روز کاری محسوب می‌شوند).
- **ساعت پایه ناخالص ماهانه:** $\text{BaseMonthlyHours} = \text{WorkingDays} \times \frac{22}{3}$

برای پرسنل بالینی مشمول آیین‌نامه، تخفیف‌های هفتگی تا سقف **۸ ساعت** اعمال شده و به نسبت روزهای ماه به کسر ماهانه تبدیل می‌شود:
1. **سابقه خدمت بالینی (حداکثر ۳ ساعت):** کمتر از ۵ سال (۰ ساعت)، ۵ تا ۱۲ سال (۱ ساعت)، ۱۳ تا ۱۷ سال (۲ ساعت)، ۱۸ سال و بالاتر (۳ ساعت).
2. **سختی کار (حداکثر ۲ ساعت):** بخش‌های ویژه (سوختگی، روانپزشکی، ICU، CCU و ...) ۲ ساعت، بخش‌های جنرال و عادی ۰ ساعت (مگر با تعریف بخش ویژه یا آورراید کارگزینی).
3. **الگوی نوبت‌کاری/شیفت‌های گردشی (حداکثر ۱ ساعت):** سه نوبته گردشی یا ثابت شب برای پرسنل دارای حداقل ۱۰ سال سابقه خدمت (۱ ساعت)، دو نوبته و ثابت روز (۰ ساعت).

> **نکته کلیدی:** ضریب **۱٫۵ برابر** شیفت‌های شب و روزهای تعطیل به **کارکرد پرسنل** در زمان‌بندی تعلق می‌گیرد (`ProductivityWorkedHoursCalculator`) و از سقف موظفی ماهانه کسر نمی‌شود تا عدالت کاری رعایت شود.

---

## ۳. فرمول‌های محاسبه

- روزهای کاری غیرتعطیل:  
  `WorkingDays = TotalDays - (FridaysCount + OfficialHolidaysCount)`
- پایه ماهانه ناخالص:  
  `MonthlyBase = WorkingDays × (22 / 3)`
- سقف ماه استاندارد کارگزینی (در صورت فعال بودن `CapBaseHoursToStandardMonth = true`):  
  `WorkingDays = min(24, WorkingDays)`  
  `MonthlyBase = min(176, MonthlyBase)`
- مجموع کاهش هفتگی:  
  `WeeklyReduction = min(8, Seniority + Hardship + Rotating)`
- کاهش ماهانه ناشی از کسورات هفتگی:  
  `MonthlyDeductions = (TotalDays / 7) × WeeklyReduction`
- موظفی ماهانه نهایی پرسنل:  
  `FinalMonthly = max(0, MonthlyBase - MonthlyDeductions)`
- موظفی گردشده:  
  `FinalMonthlyRounded = Math.Round(FinalMonthly)`

### ۳.۱ override دستی ساعت موظفی (`MaxProductivityRequiredHours`)

در فرم کاربر (`UserDtoAdd` / `UserDtoGet`) فیلد **`MaxProductivityRequiredHours`** اضافه شده:

| حالت | رفتار |
|------|--------|
| `null` یا `0` | محاسبه خودکار طبق آیین‌نامه (تقویم ماه، سابقه، `HardshipPercent`، شیفت گردشی و …) |
| مقدار **مثبت** | همان عدد به‌عنوان **ساعت موظفی ماهانه** در شیفت‌بندی اعمال می‌شود |

Resolver مربوط: `ProductivityRequiredHoursResolver` — در `ShiftSchedulingService.LoadConstraints` فراخوانی می‌شود.

جدول کاهش سابقه خدمت بالینی:

| سابقه بالینی (سال) | کاهش هفتگی |
|:------------------:|:----------:|
| کمتر از ۴ سال (۰ تا ۳ سال) | ۰ ساعت |
| ۴ تا ۷ سال | ۰.۵ ساعت |
| ۸ تا ۱۱ سال | ۱.۰ ساعت |
| ۱۲ تا ۱۵ سال | ۱.۵ ساعت |
| ۱۶ سال و بالاتر | ۲.۰ ساعت (سقف) |

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
| `ShiftYar.Application/Common/Utilities/ApprovedLeaveCapacityValidator.cs` | اعتبارسنجی سقف مجاز مرخصی روزانه بر اساس تراز پرسنل و نیاز شیفت‌ها. |
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

