# الگوریتم Simulated Annealing برای بهینه‌سازی شیفت‌بندی

## مقدمه

این پیاده‌سازی از الگوریتم **Simulated Annealing** برای بهینه‌سازی خودکار شیفت‌بندی در سیستم ShiftYar استفاده می‌کند. این الگوریتم قادر است با در نظر گیری محدودیت‌های مختلف، بهترین تخصیص شیفت‌ها را برای پرسنل پیدا کند.

## ویژگی‌های کلیدی

### 1. محدودیت‌های کاربر
- **تاریخ‌های غیرقابل دسترس**: روزهایی که کاربر نمی‌تواند شیفت داشته باشد
- **شیفت‌های ترجیحی**: شیفت‌هایی که کاربر ترجیح می‌دهد
- **شیفت‌های ناخواسته**: شیفت‌هایی که کاربر نمی‌خواهد
- **حداکثر شیفت‌های متوالی**: تعداد حداکثر شیفت‌های پیاپی
- **حداقل روزهای استراحت**: حداقل فاصله بین شیفت‌ها
- **حداکثر شیفت‌های هفتگی**: تعداد حداکثر شیفت در هفته
- **حداکثر شیفت‌های شبانه ماهانه**: تعداد حداکثر شیفت شب در ماه

### 2. محدودیت‌های سراسری
- **تعادل جنسیتی**: حفظ نسبت مناسب بین نیروهای مرد و زن
- **تطابق تخصص**: تخصیص کاربران بر اساس تخصص مورد نیاز
- **مدیر شیفت**: تعیین مسئول شیفت در صورت نیاز

### 3. پارامترهای الگوریتم
- **دمای اولیه**: مقدار اولیه دما (پیش‌فرض: 1000)
- **دمای نهایی**: مقدار نهایی دما (پیش‌فرض: 0.1)
- **نرخ خنک‌سازی**: سرعت کاهش دما (پیش‌فرض: 0.95)
- **حداکثر تکرار**: تعداد حداکثر تکرار الگوریتم (پیش‌فرض: 10000)
- **حداکثر تکرار بدون بهبود**: توقف در صورت عدم بهبود (پیش‌فرض: 1000)

## نحوه استفاده

### 1. درخواست بهینه‌سازی

```csharp
var request = new ShiftSchedulingRequestDto
{
    DepartmentId = 1,
    StartDate = DateTime.Parse("2024-01-01"),
    EndDate = DateTime.Parse("2024-01-31"),
    Constraints = new List<ShiftSchedulingConstraintDto>
    {
        new ShiftSchedulingConstraintDto
        {
            UserId = 1,
            UnavailableDates = new List<DateTime> 
            { 
                DateTime.Parse("2024-01-15"),
                DateTime.Parse("2024-01-20")
            },
            PreferredShifts = new List<ShiftLabel> { ShiftLabel.Morning, ShiftLabel.Evening },
            UnwantedShifts = new List<ShiftLabel> { ShiftLabel.Night },
            MaxConsecutiveShifts = 3,
            MinRestDaysBetweenShifts = 1,
            MaxShiftsPerWeek = 5,
            MaxNightShiftsPerMonth = 8
        }
    },
    Parameters = new ShiftSchedulingParametersDto
    {
        InitialTemperature = 1000.0,
        FinalTemperature = 0.1,
        CoolingRate = 0.95,
        MaxIterations = 10000,
        MaxIterationsWithoutImprovement = 1000
    }
};
```

### 2. اجرای الگوریتم

```csharp
var result = await _shiftSchedulingService.OptimizeShiftScheduleAsync(request);
```

### 3. بررسی نتیجه

```csharp
if (result.IsSuccess)
{
    var optimizedSchedule = result.Data;
    Console.WriteLine($"Final Score: {optimizedSchedule.FinalScore}");
    Console.WriteLine($"Total Iterations: {optimizedSchedule.TotalIterations}");
    Console.WriteLine($"Execution Time: {optimizedSchedule.ExecutionTime}");
    
    foreach (var assignment in optimizedSchedule.Assignments)
    {
        Console.WriteLine($"User: {assignment.UserName}, " +
                         $"Shift: {assignment.ShiftLabel}, " +
                         $"Date: {assignment.Date:yyyy-MM-dd}");
    }
}
```

## انواع حرکت در الگوریتم

### 1. Swap (تعویض)
دو انتساب شیفت بین کاربران مختلف تعویض می‌شود.

### 2. Reassign (تخصیص مجدد)
یک شیفت از یک کاربر به کاربر دیگری تخصیص داده می‌شود.

### 3. Add (اضافه کردن)
یک انتساب شیفت جدید اضافه می‌شود.

### 4. Remove (حذف)
یک انتساب شیفت حذف می‌شود.

## محاسبه امتیاز

امتیاز راه‌حل بر اساس موارد زیر محاسبه می‌شود:

### 1. امتیاز پایه
- امتیاز منفی برای هر انتساب (هر چه کمتر، بهتر)

### 2. جریمه نقض محدودیت‌ها
- نقض حداکثر شیفت‌های متوالی: 50 امتیاز
- نقض حداقل روزهای استراحت: 30 امتیاز
- نقض حداکثر شیفت‌های هفتگی: 40 امتیاز
- نقض حداکثر شیفت‌های شبانه ماهانه: 60 امتیاز

### 3. جریمه عدم تعادل جنسیتی
- عدم رعایت نسبت جنسیتی: 100 امتیاز

### 4. جریمه عدم تطابق تخصص
- کمبود نیرو در تخصص مورد نیاز: 50 امتیاز

### 5. جریمه ترجیحات کاربران
- شیفت ناخواسته: 20 امتیاز
- شیفت ترجیحی: -5 امتیاز (امتیاز مثبت)

## API Endpoints

### 1. بهینه‌سازی شیفت‌بندی
```
POST /api/ShiftScheduling/optimize
```

### 2. دریافت آمارهای الگوریتم
```
POST /api/ShiftScheduling/statistics
```

### 3. اعتبارسنجی محدودیت‌ها
```
POST /api/ShiftScheduling/validate
```

### 4. ذخیره نتیجه بهینه‌سازی
```
POST /api/ShiftScheduling/save
```

### 5. بهینه‌سازی و ذخیره کامل
```
POST /api/ShiftScheduling/optimize-and-save
```

## نکات مهم

1. **کارایی**: الگوریتم برای بازه‌های زمانی کوتاه‌مدت (1-3 ماه) بهینه است.

2. **محدودیت‌ها**: هر چه محدودیت‌ها بیشتر باشد، زمان اجرا افزایش می‌یابد.

3. **پارامترها**: تنظیم صحیح پارامترهای الگوریتم بر کیفیت نتیجه تأثیر دارد.

4. **اعتبارسنجی**: همیشه قبل از اجرا، محدودیت‌ها را اعتبارسنجی کنید.

5. **ذخیره**: پس از بهینه‌سازی، حتماً نتیجه را در دیتابیس ذخیره کنید.

## مثال کامل

```csharp
// 1. ایجاد درخواست
var request = new ShiftSchedulingRequestDto
{
    DepartmentId = 1,
    StartDate = DateTime.Today.AddDays(1),
    EndDate = DateTime.Today.AddDays(30),
    Constraints = GetUserConstraints(),
    Parameters = new ShiftSchedulingParametersDto
    {
        InitialTemperature = 1000.0,
        FinalTemperature = 0.1,
        CoolingRate = 0.95,
        MaxIterations = 10000,
        MaxIterationsWithoutImprovement = 1000
    }
};

// 2. اعتبارسنجی
var validation = await _shiftSchedulingService.ValidateConstraintsAsync(request);
if (!validation.IsSuccess || validation.Data.Count > 0)
{
    // مدیریت خطاهای اعتبارسنجی
    return;
}

// 3. بهینه‌سازی
var optimization = await _shiftSchedulingService.OptimizeShiftScheduleAsync(request);
if (!optimization.IsSuccess)
{
    // مدیریت خطاهای بهینه‌سازی
    return;
}

// 4. ذخیره
var save = await _shiftSchedulingService.SaveOptimizedScheduleAsync(optimization.Data);
if (!save.IsSuccess)
{
    // مدیریت خطاهای ذخیره
    return;
}

// 5. نمایش نتیجه
Console.WriteLine("شیفت‌بندی با موفقیت بهینه‌سازی و ذخیره شد!");
```

## پشتیبانی

برای سوالات و مشکلات مربوط به الگوریتم، با تیم توسعه تماس بگیرید.
