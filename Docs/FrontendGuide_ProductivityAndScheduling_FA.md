# راهنمای تغییرات بک‌اند برای تیم فرانت

این فایل خلاصه تغییراتی است که در بک‌اند پروژه `ShiftYar` برای قوانین جدید شیفت‌بندی و قانون ارتقای بهره‌وری انجام شده تا تیم فرانت بداند چه بخش‌هایی را باید در رابط کاربری، فرم‌ها و نمایش نتایج به‌روزرسانی کند.

## ۱. تغییرات مربوط به کاربر

### فیلدهای ثابت روی کاربر

- `HardshipPercent`
- `OvertimeConsent`

### سهمیه شب — مدل ماهانه (جدید)

سهمیه حداقل شیفت شب و شب تعطیل/آخرهفته از تنظیمات کاربر **خارج** شد و در موجودیت جداگانهٔ ماهانه ثبت می‌شود، چون تعداد تعطیلات هر ماه فرق دارد.

**قبل از Optimize هر ماه**، سوپروایزر باید سهمیه را برای همان ماه شمسی تنظیم کند.

#### APIها (`UserMonthlyNightQuotaController`)

| اکشن | روش | توضیح |
|------|------|--------|
| `GetDepartmentMonthlyNightQuotas` | GET | لیست سهمیه دپارتمان برای `departmentId` + `persianYear` + `persianMonth` |
| `GetUserMonthlyNightQuotaByUserMonth` | GET | سهمیه یک کاربر در یک ماه |
| `GetUserMonthlyNightQuotas` | GET | فیلتر/صفحه‌بندی |
| `UpsertUserMonthlyNightQuota` | POST | ایجاد/به‌روزرسانی یک کاربر |
| `UpsertDepartmentMonthlyNightQuotas` | POST | تنظیم یک‌جای همه کاربران دپارتمان |
| `DeleteUserMonthlyNightQuota` | DELETE | حذف با `id` |

#### بدنهٔ نمونه — یک کاربر

```json
{
  "userId": 10,
  "persianYear": 1405,
  "persianMonth": 4,
  "exactNightShiftCount": 2,
  "exactHolidayWeekendNightShiftCount": 1
}
```

#### بدنهٔ نمونه — یک‌جا برای دپارتمان

```json
{
  "departmentId": 1,
  "persianYear": 1405,
  "persianMonth": 4,
  "items": [
    { "userId": 3, "exactNightShiftCount": 5, "exactHolidayWeekendNightShiftCount": 2 },
    { "userId": 10, "exactNightShiftCount": 2, "exactHolidayWeekendNightShiftCount": 1 }
  ]
}
```

#### معنی فیلدها

- `exactNightShiftCount`: حداقل تعداد شیفت شب در آن ماه شمسی. `null` = بدون حداقل اجباری.
- `exactHolidayWeekendNightShiftCount`: حداقل شبِ تعطیل/آخر هفته. باید ≤ تعداد کل شب باشد.
- تعریف شب تعطیل/آخر هفته: شب همان روز تعطیل، یا شب روز قبل از تعطیل (مثلاً پنجشنبه قبل از جمعه).

#### اقدام لازم در فرانت

- فیلدهای `ExactNightShiftCount` / `ExactHolidayWeekendNightShiftCount` را از فرم کاربر حذف کنید.
- صفحه/مدال جدا برای «سهمیه شب ماهانه» بسازید: انتخاب سال و ماه شمسی + جدول کاربران دپارتمان.
- قبل از دکمه Optimize، اگر برای ماه شروع بازه سهمیه ثبت نشده، هشدار دهید.
- Validation: شب تعطیل ≤ کل شب؛ مقادیر ≥ 0.

### فیلدهای DTO کاربر (باقی‌مانده)

در `UserDtoAdd` / `UserDtoGet`:

- `HardshipPercent: decimal?`
- `OvertimeConsent: bool?`

## 2. تغییرات مربوط به نیازمندی تخصص شیفت

برای اینکه تعداد نیروی مورد نیاز در روزهای تعطیل با روزهای عادی متفاوت باشد، فیلدهای جدیدی به `ShiftRequiredSpecialty` اضافه شده‌اند.

### فیلدهای جدید

- `HolidayRequiredMaleCount`
- `HolidayRequiredFemaleCount`
- `HolidayRequiredTottalCount`
- `HolidayOnCallMaleCount`
- `HolidayOnCallFemaleCount`
- `HolidayOnCallTottalCount`

### رفتار بک‌اند

- فیلدهای بدون پیشوند `Holiday` مربوط به روزهای غیرتعطیل هستند.
- اگر فیلدهای `Holiday*` خالی باشند، بک‌اند به صورت خودکار همان مقادیر روز غیرتعطیل را برای روز تعطیل استفاده می‌کند.

### اقدام لازم در فرانت

- در فرم ثبت/ویرایش `ShiftRequiredSpecialty`، یک بخش جدا برای «روزهای تعطیل» اضافه شود.
- بهتر است UI به شکل دو بخش باشد:
  - مقادیر روزهای غیرتعطیل
  - مقادیر روزهای تعطیل
- کنار فیلدهای تعطیل توضیح داده شود که «در صورت خالی بودن، مقدار روز عادی استفاده می‌شود».

## 3. تغییرات در منطق شیفت‌بندی

بک‌اند الان این قواعد را در زمان شیفت‌بندی سخت‌گیرانه‌تر اعمال می‌کند:

- نوع شیفت کاربر باید رعایت شود.
  - مثلا کاربر `FixedMorning` فقط شیفت صبح می‌گیرد.
- پرسنل شیفت ثابت در تمام روزهای غیرتعطیل باید شیفت داشته باشند.
- پرسنل شیفت ثابت در روزهای تعطیل نباید شیفت بگیرند، مگر اینکه از سمت قواعد موجود مجاز شده باشند.
- توالی‌های ممنوع رعایت می‌شوند:
  - `عصر -> شب` در همان روز ممنوع
  - `شب -> صبح` در روز بعد ممنوع
- ظرفیت روزهای تعطیل و غیرتعطیل برای تخصص‌ها جداگانه در نظر گرفته می‌شود.

### اثر این تغییرات روی فرانت

- اگر در UI قبلا امکان ثبت نوع شیفت کاربر وجود دارد، همان داده الان اثر قطعی در زمان‌بندی دارد؛ بنابراین باید حتما دقیق و اجباری ثبت شود.
- در صفحه نتایج شیفت‌بندی، بهتر است اگر لازم است توضیح کوتاهی نمایش داده شود که زمان‌بندی با رعایت نوع شیفت، تعطیلی و محدودیت‌های استراحت انجام شده است.

## 4. تغییرات مربوط به قانون ارتقای بهره‌وری

منطق محاسبه ساعات موظفی و ساعات مؤثر کار طبق قانون جدید در بک‌اند پیاده‌سازی شده است.

### قواعد اصلی که در بک‌اند اعمال می‌شوند

- ساعت پایه هفتگی: `44`
- کاهش بر اساس سابقه
- کاهش بر اساس `HardshipPercent`
- کاهش 1 ساعت برای شیفت گردشی
- سقف مجموع کاهش هفتگی: `8` ساعت
- ضریب `1.5` برای شیفت شب یا روز تعطیل
- `1` ساعت تحویل شیفت به عنوان ساعت کار
- حداکثر `12` ساعت کار متوالی
- حداکثر `80` ساعت اضافه‌کاری در ماه فقط در صورت `OvertimeConsent = true`

### نتیجه برای فرانت

اگر در UI بخشی برای نمایش تحلیل یا آمار شیفت‌بندی دارید، لازم است توجه کنید که:

- «ساعات کار شده» دیگر صرفا جمع ساده مدت شیفت‌ها نیست و ممکن است با ضریب و تحویل شیفت محاسبه شده باشد.
- مازاد ساعت باید با در نظر گرفتن `OvertimeConsent` تفسیر شود.

## 5. فیلدهای خروجی جدید/مهم در نتیجه شیفت‌بندی

در خروجی `ShiftSchedulingResultDto.Statistics` این فیلدها برای فرانت مهم هستند:

- `WorkedHoursByUser`
- `ProductivityRequiredHoursByUser`
- `ProductivityOvertimeByUser`
- `ProductivityComplianceRate`
- `TotalScheduledHours`

### معنی فیلدها

- `WorkedHoursByUser`: ساعات مؤثر کار هر کاربر
- `ProductivityRequiredHoursByUser`: سقف ساعات موظفی هر کاربر
- `ProductivityOvertimeByUser`: مقدار اضافه‌کار/تجاوز از سقف برای هر کاربر
- `ProductivityComplianceRate`: درصد رعایت قانون بهره‌وری در کل خروجی
- `TotalScheduledHours`: مجموع ساعات زمان‌بندی‌شده

### اقدام لازم در فرانت

- اگر صفحه گزارش یا داشبورد شیفت‌بندی دارید، این مقادیر را نمایش دهید.
- بهتر است برای هر کاربر یک خلاصه مثل زیر نشان داده شود:
  - ساعات مؤثر
  - سقف موظفی
  - مازاد ساعت
  - وضعیت مجاز/غیرمجاز

## 6. پیشنهاد UI برای فرم کاربر

در فرم ایجاد/ویرایش کاربر این فیلدها بهتر است کنار هم نمایش داده شوند:

- `IncludedProductivityPlan`
- `HardshipPercent`
- `OvertimeConsent`
- `ShiftType`
- `ShiftSubType`
- `TwoShiftRotationPattern`

### پیشنهاد UX

- اگر `IncludedProductivityPlan = false` بود، می‌توانید فیلدهای مربوط به بهره‌وری را غیر فعال یا کم‌رنگ کنید.
- اگر `ShiftType` از نوع گردشی بود، توضیح دهید که کاهش 1 ساعت هفتگی در محاسبه اعمال می‌شود.
- اگر `OvertimeConsent = false` بود، در UI توضیح کوتاه بدهید که اضافه‌کاری در زمان‌بندی مجاز نخواهد بود.

## 7. پیشنهاد UI برای فرم نیازمندی تخصص شیفت

برای جلوگیری از خطای کاربری، بهتر است فرم `ShiftRequiredSpecialty` به این صورت طراحی شود:

- بخش اول: نیازمندی روزهای غیرتعطیل
  - تعداد حاضر مرد
  - تعداد حاضر زن
  - تعداد حاضر کل
  - تعداد آنکال مرد
  - تعداد آنکال زن
  - تعداد آنکال کل
- بخش دوم: نیازمندی روزهای تعطیل
  - همان 6 فیلد بالا
  - با برچسب «اختیاری»

## 8. نکات مهم برای هماهنگی با بک‌اند

- در فرانت برای شیفت‌ها حتما `ShiftLabel` و `ShiftId` را با هم اشتباه نگیرید.
- در فرم‌های جدید، فیلدهای عددی خالی را در صورت نیاز به شکل `null` ارسال کنید، نه لزوما `0`.
- اگر برای روز تعطیل مقدار خاصی ندارید، فیلدهای `Holiday*` را خالی بگذارید تا fallback بک‌اند درست کار کند.

## 9. چک‌لیست نهایی برای تیم فرانت

- اضافه کردن `HardshipPercent` به فرم و مدل کاربر
- اضافه کردن `OvertimeConsent` به فرم و مدل کاربر
- **حذف** سهمیه شب از فرم کاربر؛ ساخت UI ماهانه با `UserMonthlyNightQuota` APIها
- قبل از Optimize، تنظیم سهمیه شب برای ماه شمسی موردنظر
- اضافه کردن فیلدهای `Holiday*` به فرم `ShiftRequiredSpecialty`
- تفکیک UI روز عادی و روز تعطیل در نیازمندی تخصص
- نمایش آمار بهره‌وری در خروجی شیفت‌بندی
- بررسی صحیح بودن استفاده از `ShiftLabel` در فرانت

## 10. فایل‌های بک‌اند مرتبط

اگر تیم فرانت خواست دقیق‌تر بررسی کند، این فایل‌ها مهم‌ترین نقاط تغییر هستند:

- `ShiftYar.Application/DTOs/UserModel/UserDtoAdd.cs`
- `ShiftYar.Application/DTOs/UserModel/UserDtoGet.cs`
- `ShiftYar.Application/DTOs/UserModel/UserMonthlyNightQuotaDtoAdd.cs`
- `ShiftYar.Application/DTOs/UserModel/UserMonthlyNightQuotaBulkUpsertDto.cs`
- `ShiftYar.Api/Controllers/UserModel/UserMonthlyNightQuotaController.cs`
- `ShiftYar.Domain/Entities/UserModel/UserMonthlyNightQuota.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoAdd.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoGet.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftSchedulingModel/ShiftSchedulingResultDto.cs`
- `ShiftYar.Domain/Entities/UserModel/User.cs`
- `ShiftYar.Application/Common/Utilities/ProductivityWorkedHoursCalculator.cs`
- `ShiftYar.Application/Features/ProductivityModel/Services/WorkingHoursCalculator.cs`

