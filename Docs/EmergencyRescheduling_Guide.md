# راهنمای ماژول Emergency Rescheduling

این سند نحوه‌ی استفاده از ماژول «باززمان‌بندی اضطراری» شیفت‌یار را توضیح می‌دهد؛ قابلیتی که با رویکرد **Rolling Horizon** اجازه می‌دهد تنها بخش کوچکی از بازه‌ی زمانی یا کاربران تحت تأثیر دوباره زمان‌بندی شوند تا وقفه‌های عملیاتی (مثل غیبت ناگهانی، اشباع بخش یا رویدادهای بحرانی) سریعاً برطرف گردد.

## 1. معماری و اجزاء
- **Controller:** `EmergencyReschedulingController` در مسیر `/api/EmergencyRescheduling/rolling-horizon` قرار دارد و درخواست‌ها را دریافت می‌کند. (`ShiftYar.Api/Controllers/ShiftModel/EmergencyReschedulingController.cs`)
- **Service:** منطق اصلی در `EmergencyReschedulingService` پیاده‌سازی شده که برای هر پنجره زمانی، `IShiftSchedulingService.OptimizeShiftScheduleInternalAsync` را صدا می‌زند و خروجی‌ها را تجمیع می‌کند. (`ShiftYar.Application/Features/ShiftModel/Rescheduling/EmergencyReschedulingService.cs`)
- **Dependency Injection:** سرویس با `services.AddScoped<IEmergencyReschedulingService, EmergencyReschedulingService>();` در DI ثبت شده است و آماده تزریق به کنترلر است. (`ShiftYar.Application/ApplicationDependencyInjection.cs`)

## 2. جریان Rolling Horizon
1. بازه‌ی درخواستی به پنجره‌های کوچک‌تر تقسیم می‌شود. طول هر پنجره `WindowSizeDays` و همپوشانی آن `OverlapDays` است. قدم حرکت بین پنجره‌ها `WindowSizeDays - OverlapDays` خواهد بود.
2. برای هر پنجره، یک `ShiftSchedulingRequestInternalDto` ساخته شده و موتور انتخابی (SA / OR-Tools / Hybrid) اجرا می‌شود.
3. خروجی هر پنجره شامل تعداد انتساب، وضعیت الگوریتم، `ProductivityComplianceRate` و فهرست نقض‌ها نگهداری می‌شود.
4. انتساب‌های تولیدشده تجمیع می‌شوند. اگر `ImpactedUserIds` ارسال شده باشد فقط شیفت‌های کاربران ذکرشده در نتیجه نهایی قرار می‌گیرد؛ در غیر این صورت، همه انتساب‌ها حفظ می‌شوند. کلید تجمیع به صورت `UserId_ShiftId_yyyyMMdd` است تا آخرین نتیجه هر پنجره غالب شود.
5. در انتها، `RollingHorizonRescheduleResultDto` برگردانده می‌شود که شامل لیست انتساب‌های نهایی، نتایج هر پنجره، زمان حل تجمعی، پرچم نقض‌ها و یادداشت‌های راهنما است.

## 3. Endpoint و بدنه درخواست
- **Method:** `POST`
- **URL:** `/api/EmergencyRescheduling/rolling-horizon`
- **Authorization:** `[Authorize(Roles = "Admin,Supervisor")]`

### 3.1 مدل `EmergencyReschedulingRequestDto`
| فیلد | نوع | ضرورت | توضیح | محدودیت |
| --- | --- | --- | --- | --- |
| `DepartmentId` | `int` | ضروری | شناسه دپارتمان | `Range(1, ∞)` |
| `StartDate` | `string` | ضروری | تاریخ شروع (شمسی `yyyy/MM/dd`) | RegEx اعتبارسنجی + Required |
| `EndDate` | `string` | ضروری | تاریخ پایان (شمسی) | باید بعد از تاریخ شروع باشد |
| `WindowSizeDays` | `int` | اختیاری | طول هر پنجره (پیش‌فرض 7 روز) | `1…21` |
| `OverlapDays` | `int` | اختیاری | همپوشانی بین پنجره‌ها (پیش‌فرض 1 روز) | `0…14` و کوچکتر از طول پنجره |
| `ImpactedUserIds` | `List<int>` | اختیاری | شناسه کاربرانی که باید باززمان‌بندی شوند | لیست بدون تکرار |
| `Algorithm` | `SchedulingAlgorithm` | اختیاری | موتور موردنظر (SA، OR-Tools، Hybrid) | پیش‌فرض SA |

> نکته: اگر `ImpactedUserIds` خالی باشد، خروجی شامل تمامی انتساب‌های تولیدشده در پنجره‌ها خواهد بود.

### 3.2 نمونه درخواست
```http
POST /api/EmergencyRescheduling/rolling-horizon
Content-Type: application/json
Authorization: Bearer <token>

{
  "departmentId": 5,
  "startDate": "1404/02/01",
  "endDate": "1404/02/15",
  "windowSizeDays": 5,
  "overlapDays": 2,
  "impactedUserIds": [1012, 1055],
  "algorithm": 1
}
```

## 4. ساختار پاسخ
موفقیت منجر به `ApiResponse<RollingHorizonRescheduleResultDto>` می‌شود:

```json
{
  "isSuccess": true,
  "message": "",
  "data": {
    "aggregatedAssignments": [
      {
        "userId": 1012,
        "shiftId": 34,
        "date": "2025-04-20T00:00:00",
        "shiftLabel": 2,
        "departmentId": 5
      }
    ],
    "windows": [
      {
        "windowIndex": 1,
        "startDate": "2025-04-19T00:00:00",
        "endDate": "2025-04-23T00:00:00",
        "algorithmStatus": "Completed",
        "assignmentCount": 48,
        "productivityComplianceRate": 0.97,
        "violations": []
      }
    ],
    "totalSolveTime": "00:01:42",
    "hasConflicts": false,
    "notes": [
      "Windows executed: 3",
      "WindowSizeDays=5, OverlapDays=2",
      "All windows satisfied constraints.",
      "Impacted users: 1012,1055"
    ]
  }
}
```

در صورت بروز خطا (مثلاً شکست الگوریتم در یکی از پنجره‌ها یا خطای اعتبارسنجی) پاسخ به شکل `isSuccess = false` و پیام متنی مناسب خواهد بود.

## 5. سناریوهای استفاده متداول
1. **غیبت ناگهانی چند نفر:** با تعیین `ImpactedUserIds` فقط شیفت‌های آن افراد در بازه‌ی کوتاه باززمان‌بندی می‌شود.
2. **تقسیم بازه‌های طولانی:** هنگام نیاز به بازتنظیم ۳۰ تا ۶۰ روز، می‌توان پنجره‌های ۷ روزه با همپوشانی ۱-۲ روزه تعریف کرد تا هزینه محاسباتی کنترل شود.
3. **پیاده‌سازی قانون «آخرین نتیجه غالب است»:** در صورت همپوشانی، انتساب‌های پنجره‌های پایانی جایگزین نتایج پنجره‌های قبلی می‌شوند؛ بنابراین بهتر است کاربران تغییر یافته در پنجره‌های انتهایی بررسی شوند.

## 6. ملاحظات عملیاتی
- **انتخاب اندازه پنجره:** هرچه `WindowSizeDays` کوچک‌تر باشد، زمان حل هر پنجره کاهش می‌یابد اما تعداد پنجره‌ها بیشتر می‌شود. برای بخش‌های بحرانی معمولاً 5 تا 7 روز مناسب است.
- **همپوشانی:** مقدار `OverlapDays` جلوی «لبه‌های ناتمام» را می‌گیرد. اگر همپوشانی صفر باشد احتمال دارد برخی تعارض‌ها در مرز پنجره‌ها دیده نشود.
- **رعایت بهره‌وری:** همان تنظیمات `EnforceProductivityHours` و وزن‌های نرم دپارتمان در اجرای Rolling Horizon نیز لحاظ می‌شوند؛ بنابراین قبل از فراخوانی، از به‌روزرسانی تنظیمات دپارتمان مطمئن شوید.
- **لاگ و مانیتورینگ:** لاگ خطاهای سرویس در `EmergencyReschedulingService` ثبت می‌شود. در صورت شکست پنجره‌ی خاص، پیام «Window {index} failed» در خروجی برگردانده می‌شود.
- **زمان پاسخ:** `TotalSolveTime` مجموع طول اجرای همه پنجره‌ها است و برای مانیتور کردن عملکرد موتور به کار می‌رود.

## 7. رفع اشکال رایج
| خطا | علت محتمل | راهکار |
| --- | --- | --- |
| `End date must be after start date.` | بازه زمانی اشتباه ارسال شده است. | تاریخ‌ها را اصلاح کنید. |
| `No scheduling windows could be generated.` | طول بازه از پنجره کوچک‌تر بوده یا پیکربندی باعث صفر شدن حلقه شده است. | `WindowSizeDays` و `OverlapDays` را بررسی کنید. |
| `Window X failed: ...` | الگوریتم انتخابی در آن بازه شکست خورده یا داده ورودی ناقص بوده است. | لاگ‌های `ShiftSchedulingService` را بررسی کنید، ظرفیت تخصص‌ها و درخواست‌های Pending را رفع کنید. |
| نتیجه فقط برخی کاربران را شامل می‌شود | `ImpactedUserIds` پر شده و افراد دیگر فیلتر شده‌اند. | در صورت نیاز فهرست را خالی ارسال کنید تا همه‌ی انتساب‌ها بازگردند. |

## 8. چک‌لیست قبل از اجرای باززمان‌بندی اضطراری
- [ ] تقویم و شیفت‌های پایه برای بازه‌ی مدنظر قبلاً تولید شده‌اند.
- [ ] درخواست‌های شیفت در وضعیت Pending تصفیه شده‌اند تا داده‌ی ناهمخوان وارد الگوریتم نشود.
- [ ] `DepartmentSchedulingSettings` شامل قوانین سخت/نرم موردنظر به‌روز است.
- [ ] اگر فقط برخی کاربران اهمیت دارند، شناسه‌های آن‌ها آماده و بدون تکرار است.
- [ ] توکن دسترسی با نقش `Admin` یا `Supervisor` در اختیار دارید.

با پیروی از این راهنما می‌توانید ماژول Emergency Rescheduling را به‌صورت قابل‌اعتماد در سناریوهای بحرانی به کار بگیرید و حداقل تداخل را در برنامه‌ی اصلی شیفت‌ها ایجاد کنید.
