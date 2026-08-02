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
- Validation سمت کلاینت: شب تعطیل ≤ کل شب؛ مقادیر ≥ 0.
- **اعتبارسنجی سمت سرور قبل از ذخیره:** مجموع سهمیه‌های دپارتمان با تقویم `ShiftDates` چک می‌شود:
  - مجموع `exactNightShiftCount` همه کاربران ≤ تعداد روزهای همان ماه شمسی در `ShiftDates`
  - مجموع `exactHolidayWeekendNightShiftCount` ≤ تعداد شب‌های تعطیل/آخرهفته همان ماه (طبق `IsHoliday` در `ShiftDates` و قاعده شبِ قبل از تعطیل)
  - اگر تقویم ناقص باشد یا مجموع از ظرفیت بیشتر شود، API با `isSuccess: false` و پیام فارسی مناسب برمی‌گردد — همان `message` را به سوپروایزر نشان دهید.

### فیلدهای DTO کاربر (باقی‌مانده)

در `UserDtoAdd` / `UserDtoGet`:

- `HardshipPercent: decimal?`
- `OvertimeConsent: bool?`

## ۱.۵ حذف و ایجاد شیفت‌بندی ماهانه (`ShiftScheduling`)

### قواعد کسب‌وکار (پیش‌فرض)

1. **شیفت‌بندی کلی ماه** (`optimize-and-save` / `optimize-and-save-async`) فقط وقتی مجاز است که:
   - بازه `startDate`/`endDate` داخل **یک** ماه شمسی باشد
   - آن ماه شمسی **هنوز شروع نشده** باشد (`امروز < اولین روز ماه`)
   - برای آن دپارتمان در آن ماه **هیچ انتساب ذخیره‌شده‌ای** وجود نداشته باشد
2. اگر برنامه قبلی وجود داشته باشد → خطا با پیام: ابتدا با اکشن حذف، شیفت‌بندی قبلی را پاک کنید.
3. اگر ماه شروع شده باشد → خطا: امکان شیفت‌بندی کلی / حذف برای این ماه وجود ندارد.
4. **حذف ماهانه** فقط تا قبل از شروع همان ماه شمسی مجاز است.

### فلگ‌های تنظیمات دپارتمان (`DepartmentSchedulingSettings`)

این دو آیتم در تنظیمات زمان‌بندی دپارتمان قابل روشن/خاموش‌اند (پیش‌فرض: خاموش / `null` = غیرفعال):

| فیلد API | عنوان UI پیشنهادی | رفتار |
|----------|-------------------|--------|
| `allowCurrentMonthScheduling` | امکان شیفت‌بندی ماه جاری | اگر فعال باشد، برای **ماه شمسی جاری** حتی پس از شروع ماه، Optimize و `DeleteMonthlySchedule` مجاز است. **فقط برای توسعه و تست** استفاده شود. ماه‌های گذشته همچنان مسدود می‌مانند. |
| `allowMonthlyRescheduleWithAutoDelete` | امکان شیفت‌بندی مجدد ماهانه و حذف خودکار شیفت‌بندی قبلی | اگر فعال باشد و برنامهٔ قبلی برای همان ماه وجود داشته باشد، قبل از ذخیرهٔ برنامهٔ جدید، همه انتساب‌های آن ماه دپارتمان **خودکار حذف** و با برنامه جدید جایگزین می‌شوند (نیازی به فراخوانی دستی حذف نیست). |

ترکیب پیشنهادی برای تست ماه جاری: هر دو فلگ را روشن کنید تا بتوانید ماه جاری را دوباره Optimize کنید و برنامه قبلی جایگزین شود.

### درخواست حضور (یادآوری برای فرانت)

- `requestType = 0` (`FullDay`) فقط با `requestAction = 1` (عدم‌حضور/مرخصی) معنا دارد.
- درخواست **حضور** باید `requestType = 1` (`SpecificShift`) + `shiftLabel` (صبح/عصر/شب) باشد.
- حضور کل‌روز مجاز نیست (سقف ۱۲ ساعت متوالی). ترکیب‌های مجاز یک روز: صبح+عصر یا صبح+شب؛ عصر+شب و شب→صبح روز بعد ممنوع‌اند. بک‌اند درخواست حضور کل‌روز را رد می‌کند.

### اکشن حذف

| اکشن | روش | توضیح |
|------|------|--------|
| `DeleteMonthlySchedule` | `POST /DeleteMonthlySchedule` | حذف همه انتساب‌های شیفت دپارتمان در ماه شمسی |

بدنه:

```json
{
  "departmentId": 1,
  "persianYear": 1405,
  "persianMonth": 5
}
```

پاسخ موفق نمونه: `deletedCount` تعداد انتساب‌های حذف‌شده.

### اقدام لازم در فرانت

- در فرم `DepartmentSchedulingSettings` دو سوئیچ بالا را اضافه کنید (برچسب فارسی مطابق جدول).
- دکمه «حذف شیفت‌بندی ماه» با تأیید کاربر؛ اگر ماه شروع شده و `allowCurrentMonthScheduling` خاموش است، دکمه را غیرفعال کنید.
- قبل از `optimize-and-save` اگر برنامه قبلی هست و `allowMonthlyRescheduleWithAutoDelete` خاموش است، دکمه Optimize را قفل کنید و کاربر را به حذف هدایت کنید؛ اگر فلگ روشن است، نیازی به قفل به‌خاطر برنامه قبلی نیست.
- پیام `message` خطای API را عیناً به سوپروایزر نشان دهید.

### توزیع صبح/عصر در روزهای تعطیل

الگوریتم علاوه بر تعادل ماهانهٔ صبح/عصر، **تعداد شیفت صبح و عصر روی روزهای تعطیل** را هم بین پرسنل گردشی هم‌تخصص پخش می‌کند (`HolidayMorningEveningFairnessGuard` + وزن نرم `FairHolidayMorningEveningPeerWeight`). بعد از Optimize مجدد، انتظار این است که تعطیلات روی چند نفر خاص متمرکز نشوند.

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

## 5. فیلدهای خروجی آمار بهره‌وری در نتیجه Optimize

در `data.statistics` خروجی اکشن Optimize این دیکشنری‌ها به‌ازای `userId` برمی‌گردند (فقط برای کاربرانی که مشمول طرح بهره‌وری‌اند و ساعت موظفی برایشان محاسبه شده):

| فیلد JSON | نوع | معنی کوتاه |
|-----------|------|------------|
| `workedHoursByUser` | ساعت مؤثر کارشده | چقدر کار (با ضریب قانون بهره‌وری) انجام شده |
| `productivityRequiredHoursByUser` | ساعت موظفی ماهانه | هدف/کف موظفی محاسبه‌شده برای آن بازه |
| `productivityOvertimeByUser` | ساعت مازاد غیرمجاز | چقدر از **سقف مجاز** رد شده |
| `productivityShortfallByUser` | ساعت کمبود | چقدر از **موظفی** کمتر کار شده |

همچنین:

- `totalScheduledHours`: جمع `workedHoursByUser` همه کاربران
- `productivityComplianceRate`: نسبت کاربرانی که از سقف مجاز رد نشده‌اند (۰ تا ۱)
- `productivityTargetFulfillmentRate`: نسبت کاربرانی که به ساعت موظفی رسیده‌اند یا بالاترند (۰ تا ۱)

---

### ۵.۱ `workedHoursByUser` — ساعات مؤثر کارشده

جمع ساعات **مؤثر** انتساب‌های همان کاربر در خروجی شیفت‌بندی (آنکال حساب نمی‌شود).

**صرفاً جمع سادهٔ مدت شیفت نیست.** طبق قانون ارتقای بهره‌وری:

- شیفت **شب** یا روز **تعطیل** با ضریب `1.5` محاسبه می‌شود
- بین شیفت‌های پشت‌سرهم، `1` ساعت **تحویل** هم به ساعات کار اضافه می‌شود

مثال: یک شب ۸ ساعته ≈ `8 × 1.5 = 12` ساعت مؤثر (به‌علاوه تحویل در صورت مجاورت با شیفت دیگر).

---

### ۵.۲ `productivityRequiredHoursByUser` — ساعت موظفی (هدف ماهانه)

ساعت موظفی ماهانهٔ محاسبه‌شده برای همان بازه برنامه‌ریزی (بر اساس ۴۴ ساعت هفتگی، سابقه، `HardshipPercent`، گردشی بودن، و تعداد هفته‌های بازه).

این مقدار **هدف/کف موظفی** است، نه لزوماً سقف سخت اضافه‌کاری.

- اگر `worked ≈ required` → وضعیت ایده‌آل از نظر پر کردن موظفی
- اگر `worked < required` → کمبود (نگاه کنید به shortfall)
- اگر `worked > required` → یا اضافه‌کار مجاز است یا مازاد غیرمجاز (نگاه کنید به overtime)

---

### ۵.۳ `productivityShortfallByUser` — کمبود نسبت به موظفی

```
shortfall = max(0, required − worked)   (با تلورانس حدود ۰٫۲۵ ساعت)
```

یعنی کاربر **کمتر از موظفی** شیفت گرفته است.

| مثال | required | worked | shortfall |
|------|----------|--------|-----------|
| کمبود | 152 | 145 | ≈ 7 |
| پر شده | 152 | 157 | 0 |

مقدار `0` یعنی حداقل موظفی برآورده شده (یا بیشتر کار شده).

---

### ۵.۴ `productivityOvertimeByUser` — مازاد نسبت به سقف مجاز

```
maxAllowed = OvertimeConsent ? (required + 80) : required
overtime   = max(0, worked − maxAllowed)   (با تلورانس حدود ۰٫۲۵ ساعت)
```

یعنی چقدر از **سقف مجاز** رد شده، نه لزوماً چقدر از موظفی بیشتر کار شده.

| وضعیت | معنی |
|--------|------|
| `OvertimeConsent = false` و `worked > required` | همان اختلاف ≈ overtime (اضافه‌کار غیرمجاز) |
| `OvertimeConsent = true` | تا `required + 80` مجاز است؛ overtime فقط بالای آن پر می‌شود |
| `worked` بین `required` و `maxAllowed` | overtime = 0 (مازاد داخل سقف رضایت) |

**تفاوت مهم با shortfall:** یک کاربر نمی‌تواند هم‌زمان shortfall و overtime معنادار داشته باشد؛ یا زیر موظفی است یا بالای سقف مجاز (یا وسط و هر دو صفر).

---

### ۵.۵ مقایسه یک‌خطی

```
shortfall  →  «کم کار کرده» نسبت به موظفی (required)
overtime   →  «بیش از حد کار کرده» نسبت به سقف مجاز (maxAllowed)
worked     →  «چقدر کار کرده» (مؤثر)
required   →  «چقدر باید کار می‌کرد» (موظفی)
```

رابطهٔ تقریبی:

```
worked ≈ required − shortfall + (مازاد داخل سقف رضایت) + overtime
```

---

### ۵.۶ پیشنهاد نمایش در UI

برای هر کاربر در گزارش Optimize:

| برچسب پیشنهادی | فیلد |
|----------------|------|
| ساعات مؤثر | `workedHoursByUser[id]` |
| موظفی ماه | `productivityRequiredHoursByUser[id]` |
| کمبود | `productivityShortfallByUser[id]` (اگر > 0 قرمز/هشدار) |
| مازاد غیرمجاز | `productivityOvertimeByUser[id]` (اگر > 0 قرمز/هشدار) |

وضعیت خلاصه:

- `shortfall > 0` → «کمبود موظفی»
- `overtime > 0` → «تجاوز از سقف مجاز»
- هر دو `0` و `worked ≥ required` → «در محدوده مجاز»

### اقدام لازم در فرانت

- این چهار دیکشنری را در داشبورد/گزارش شیفت نمایش دهید.
- `ProductivityRequiredHoursByUser` را «سقف مطلق» برچسب نزنید؛ برچسب درست: **ساعت موظفی / هدف ماهانه**.
- برای تفسیر overtime، `OvertimeConsent` کاربر را هم در نظر بگیرید (در صورت نمایش جزئیات).

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
- دکمه حذف شیفت‌بندی ماه (`DeleteMonthlySchedule`) + قفل Optimize وقتی برنامه قبلی هست یا ماه شروع شده (با درنظرگرفتن فلگ‌های `allowCurrentMonthScheduling` و `allowMonthlyRescheduleWithAutoDelete`)
- دو سوئیچ در فرم تنظیمات دپارتمان: `allowCurrentMonthScheduling` و `allowMonthlyRescheduleWithAutoDelete`
- در فرم درخواست شیفت: برای حضور فقط `SpecificShift`؛ گزینهٔ حضور کل‌روز را نشان ندهید / غیرفعال کنید
- توزیع عادلانه صبح/عصر **روزهای تعطیل** (نه فقط تعادل ماهانه M/E)
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
- `ShiftYar.Api/Controllers/ShiftModel/ShiftSchedulingController.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftSchedulingModel/DeleteMonthlyScheduleRequestDto.cs`
- `ShiftYar.Domain/Entities/DepartmentModel/DepartmentSchedulingSettings.cs`
- `ShiftYar.Application/DTOs/DepartmentModel/DepartmentSchedulingSettingsDtoAdd.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoAdd.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoGet.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftSchedulingModel/ShiftSchedulingResultDto.cs`
- `ShiftYar.Domain/Entities/UserModel/User.cs`
- `ShiftYar.Application/Common/Utilities/ProductivityWorkedHoursCalculator.cs`
- `ShiftYar.Application/Features/ProductivityModel/Services/WorkingHoursCalculator.cs`

