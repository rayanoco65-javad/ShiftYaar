# راهنمای الگوریتم‌های شیفت‌بندی

این سند راهنمای کاملی از الگوریتم‌های مختلف شیفت‌بندی پیاده‌سازی شده در سیستم ShiftYar ارائه می‌دهد.

## الگوریتم‌های موجود

### 1. Simulated Annealing (الگوریتم اصلی)
- **مسیر**: `ShiftYar.Application/Features/ShiftModel/SimulatedAnnealing/`
- **کلاس اصلی**: `SimulatedAnnealingScheduler`
- **مزایا**:
  - انعطاف‌پذیری بالا در مواجهه با محدودیت‌های پیچیده
  - قابلیت بهبود تدریجی راه‌حل
  - مناسب برای مسائل بزرگ و پیچیده
- **معایب**:
  - زمان اجرای طولانی‌تر
  - عدم تضمین بهینه‌بودن راه‌حل

### 2. OR-Tools CP-SAT (جدید)
- **مسیر**: `ShiftYar.Application/Features/ShiftModel/OrTools/`
- **کلاس اصلی**: `OrToolsCPSatScheduler`
- **مزایا**:
  - تضمین بهینه‌بودن راه‌حل (در صورت وجود)
  - سرعت بالا برای مسائل کوچک تا متوسط
  - پشتیبانی از محدودیت‌های پیچیده
  - استفاده از تکنیک‌های پیشرفته constraint programming
- **معایب**:
  - ممکن است برای مسائل بسیار بزرگ کند باشد
  - نیاز به حافظه بیشتر

### 3. Hybrid Algorithm (ترکیبی)
- **مسیر**: `ShiftYar.Application/Features/ShiftModel/Hybrid/`
- **کلاس اصلی**: `HybridScheduler`
- **استراتژی‌های موجود**:
  - `OrToolsFirst`: ابتدا OR-Tools، سپس بهبود با SA
  - `SimulatedAnnealingFirst`: ابتدا SA، سپس بهبود با OR-Tools
  - `Parallel`: اجرای موازی هر دو الگوریتم
  - `Iterative`: بهبود تکراری
  - `Adaptive`: انتخاب الگوریتم بر اساس پیچیدگی مسئله

## نحوه استفاده

### 1. استفاده از الگوریتم خاص

```csharp
var request = new ShiftSchedulingRequestDto
{
    DepartmentId = 1,
    StartDate = DateTime.Today,
    EndDate = DateTime.Today.AddDays(30),
    Algorithm = SchedulingAlgorithm.OrToolsCPSat, // یا SimulatedAnnealing یا Hybrid
    Parameters = new AlgorithmParametersDto
    {
        // پارامترهای مخصوص هر الگوریتم
    }
};

var result = await shiftSchedulingService.OptimizeShiftScheduleAsync(request);
```

### 2. مقایسه عملکرد الگوریتم‌ها

```csharp
var comparator = new AlgorithmPerformanceComparator(logger);
var comparisonResult = await comparator.CompareAllAlgorithmsAsync(request);

// نتایج مقایسه
Console.WriteLine($"بهترین الگوریتم: {comparisonResult.BestAlgorithm}");
Console.WriteLine($"سریع‌ترین الگوریتم: {comparisonResult.FastestAlgorithm}");
```

## پارامترهای الگوریتم‌ها

### Simulated Annealing
- `InitialTemperature`: دمای اولیه (پیش‌فرض: 1000)
- `FinalTemperature`: دمای نهایی (پیش‌فرض: 0.1)
- `CoolingRate`: نرخ کاهش دما (پیش‌فرض: 0.95)
- `MaxIterations`: حداکثر تکرار (پیش‌فرض: 10000)
- `MaxIterationsWithoutImprovement`: حداکثر تکرار بدون بهبود (پیش‌فرض: 1000)

### OR-Tools CP-SAT
- `MaxTimeInSeconds`: حداکثر زمان حل (پیش‌فرض: 300)
- `NumSearchWorkers`: تعداد کارگران جستجو (پیش‌فرض: 4)
- `LogSearchProgress`: ثبت پیشرفت جستجو (پیش‌فرض: true)
- `MaxSolutions`: حداکثر تعداد راه‌حل (پیش‌فرض: 1)
- `RelativeGapLimit`: حد فاصله نسبی (پیش‌فرض: 0.01)

### Hybrid
- `HybridStrategy`: استراتژی ترکیبی
- `MaxHybridIterations`: حداکثر تکرار ترکیبی (پیش‌فرض: 5)
- `ComplexityThreshold`: آستانه پیچیدگی (پیش‌فرض: 100.0)

## توصیه‌های انتخاب الگوریتم

### برای مسائل کوچک (کمتر از 50 کاربر، کمتر از 30 روز)
- **OR-Tools CP-SAT**: بهترین انتخاب برای تضمین بهینه‌بودن
- **Hybrid (OrToolsFirst)**: اگر نیاز به بهبود بیشتر دارید

### برای مسائل متوسط (50-200 کاربر، 30-90 روز)
- **Hybrid (Adaptive)**: انتخاب خودکار بر اساس پیچیدگی
- **Hybrid (Parallel)**: اگر زمان کافی دارید

### برای مسائل بزرگ (بیش از 200 کاربر، بیش از 90 روز)
- **Simulated Annealing**: انعطاف‌پذیری بالا
- **Hybrid (SimulatedAnnealingFirst)**: ترکیب با OR-Tools برای بهبود

## محدودیت‌های پشتیبانی شده

### محدودیت‌های سخت (Hard Constraints)
- عدم انتساب در تاریخ‌های غیرقابل دسترس
- حداکثر یک شیفت در روز برای هر کاربر
- رعایت ظرفیت موردنیاز هر تخصص
- حداقل استراحت بین شیفت‌ها
- حداکثر شیفت‌های متوالی

### محدودیت‌های نرم (Soft Constraints)
- تعادل جنسیتی
- ترجیحات کاربران (شیفت‌های ترجیحی/ناخواسته)
- حداکثر شیفت هفتگی
- حداکثر شیفت شب ماهانه

## آمار و گزارش‌گیری

هر الگوریتم آمارهای مفصلی ارائه می‌دهد:

### Simulated Annealing
- تعداد تکرارها
- تعداد حرکت‌های پذیرفته شده/رد شده
- تاریخچه امتیاز و دما
- زمان اجرا

### OR-Tools CP-SAT
- وضعیت حل‌کننده (Optimal, Feasible, Infeasible)
- تعداد متغیرها و محدودیت‌ها
- تعداد شاخه‌ها و تضادها
- لاگ‌های حل‌کننده

### Hybrid
- زمان اجرای هر فاز
- استراتژی استفاده شده
- تعداد بهبودها
- پیچیدگی مسئله

## مثال کامل

```csharp
// تنظیم درخواست
var request = new ShiftSchedulingRequestDto
{
    DepartmentId = 1,
    StartDate = new DateTime(2024, 1, 1),
    EndDate = new DateTime(2024, 1, 31),
    Algorithm = SchedulingAlgorithm.Hybrid,
    Parameters = new AlgorithmParametersDto
    {
        HybridStrategy = HybridStrategy.Adaptive,
        MaxTimeInSeconds = 600,
        MaxIterations = 5000
    },
    Constraints = new List<UserConstraintDto>
    {
        new UserConstraintDto
        {
            UserId = 1,
            UnavailableDates = new List<DateTime> { new DateTime(2024, 1, 15) },
            PreferredShifts = new List<ShiftLabel> { ShiftLabel.Morning },
            UnwantedShifts = new List<ShiftLabel> { ShiftLabel.Night }
        }
    }
};

// اجرای بهینه‌سازی
var result = await shiftSchedulingService.OptimizeShiftScheduleAsync(request);

// بررسی نتایج
if (result.IsSuccess)
{
    Console.WriteLine($"الگوریتم استفاده شده: {result.Data.AlgorithmUsed}");
    Console.WriteLine($"امتیاز نهایی: {result.Data.FinalScore}");
    Console.WriteLine($"زمان اجرا: {result.Data.ExecutionTime}");
    Console.WriteLine($"تعداد انتساب‌ها: {result.Data.Assignments.Count}");
    
    if (result.Data.HybridResult != null)
    {
        Console.WriteLine($"استراتژی ترکیبی: {result.Data.HybridResult.StrategyUsed}");
        Console.WriteLine($"زمان کل: {result.Data.HybridResult.TotalExecutionTime}");
    }
}
```

## نکات مهم

1. **حافظه**: OR-Tools ممکن است حافظه بیشتری مصرف کند
2. **زمان**: برای مسائل بزرگ، Simulated Annealing ممکن است زمان بیشتری نیاز داشته باشد
3. **کیفیت**: OR-Tools تضمین بهینه‌بودن ارائه می‌دهد، در حالی که SA ممکن است راه‌حل‌های تقریبی ارائه دهد
4. **انعطاف‌پذیری**: SA انعطاف‌پذیری بیشتری در مواجهه با محدودیت‌های پیچیده دارد

## پشتیبانی و توسعه

برای سوالات یا پیشنهادات، لطفاً با تیم توسعه تماس بگیرید.

###############################################################################################################
- بله. با اضافه‌شدن OR-Tools و Hybrid:
  - در مسائل کوچک/متوسط، OR-Tools معمولاً سریع‌تر و با کیفیت بالاتر (بهینه/نزدیک به بهینه) جواب می‌دهد.
  - در مسائل بزرگ/پیچیده، SA یا Hybrid معمولاً پایدارتر و با کیفیت بهتر عمل می‌کند.
  - Hybrid بهترینِ هر دو دنیا را می‌گیرد (OR-Tools برای ساخت راه‌حل خوب + SA برای بهبود محلی).

- انتخاب الگوریتم داینامیک است:
  - شما در `ShiftSchedulingRequestDto.Algorithm` مقدار را مشخص می‌کنید: `SimulatedAnnealing | OrToolsCPSat | Hybrid`.
  - اگر `Hybrid` را انتخاب کنید، با `Parameters.HybridStrategy = Adaptive` موتور به‌صورت خودکار بر اساس پیچیدگی مسئله بین OR-Tools/SA/Hybrid تصمیم می‌گیرد.

- اگر بخواهید بدون فکر کردن انتخاب بهینه را بسپارید:
  - برای بازه کوچک/تعداد نیرو کم: `Algorithm = OrToolsCPSat`
  - برای سناریوهای بزرگ/قیدهای زیاد: `Algorithm = Hybrid` + `HybridStrategy = Adaptive`

- برای ارزیابی مستند کیفیت/سرعت روی داده‌های خودتان:
  - از `AlgorithmPerformanceComparator` استفاده کنید تا SA، OR-Tools و Hybrid را موازی مقایسه کند و توصیه بدهد.

خلاصه:
- کیفیت و سرعت بهتر: بله، مخصوصاً با OR-Tools و Hybrid.
- انتخاب داینامیک: بله، از طریق `SchedulingAlgorithm = Hybrid` با استراتژی `Adaptive` (یا تعیین مستقیم الگوریتم).