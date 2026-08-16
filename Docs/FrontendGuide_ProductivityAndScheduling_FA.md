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
- **ارتباط با درخواست شیفت شب** (پیام خطا را عیناً به سوپروایزر نشان دهید):
  - هر درخواست تأییدشدهٔ حضور در شیفت شب، یک واحد از **سهمیه کل شب** همان ماه کاربر مصرف می‌کند.
  - **ثبت و تأیید** درخواست شب بدون سهمیه ماهانه، یا بیش از **سهمیه کل شب** (`exactNightShiftCount`)، رد می‌شود.
  - `exactHolidayWeekendNightShiftCount` فقط **حداقل** شب تعطیل/آخرهفته برای شیفت‌بندی است، نه سقف تأیید درخواست. بنابراین چند درخواست شب تعطیل بیش از این حداقل، تا وقتی از سهمیه کل شب فراتر نروند، قابل تأییدند.
  - اگر حداقل تعطیل > 0 باشد، تعداد درخواست شب **غیرتعطیل** نباید از `ExactNight − ExactHoliday` بیشتر شود (مثال: شب=۵ و تعطیل=۱ ⇒ حداکثر ۴ شب غیرتعطیل) تا جا برای حداقل تعطیل در برنامه بماند.
  - هنگام **تعیین/ویرایش/حذف سهمیه کل شب**: نمی‌توان سهمیه کل را کمتر از تعداد کل درخواست‌های شب تأییدشده گذاشت یا در حضور آن‌ها حذف کرد؛ ترکیب سهمیه نباید با شب‌های غیرتعطیل تأییدشده تناقض داشته باشد. حداقل تعطیل می‌تواند کمتر از تعداد درخواست‌های شب تعطیل تأییدشده باشد.
  - در Optimize، رسیدن به سهمیه حداقل شب **اجباری** است؛ در غیر این صورت شیفت‌بندی با خطا متوقف می‌شود.

#### ترتیب پیشنهادی کار سوپروایزر

1. تکمیل `ShiftDates` ماه موردنظر  
2. تنظیم سهمیه شب ماهانه (`UserMonthlyNightQuota`) طوری که مجموع ≤ تعداد شب‌های ماه باشد  
3. ثبت/تأیید درخواست‌ها (درخواست شب فقط داخل سهمیه؛ **تأیید حضور فقط تا ظرفیت شیفت/تخصص**)  
4. در صورت نیاز حذف برنامه قبلی (`DeleteMonthlySchedule`) یا فعال بودن `allowMonthlyRescheduleWithAutoDelete`  
5. `optimize-and-save` / `optimize-and-save-async`

## ۱.۶ قوانین ترکیب شیفت در یک روز

| ترکیب | وضعیت | دلیل |
|--------|--------|------|
| صبح + عصر | مجاز | حداکثر ترکیب متوالی مجاز |
| صبح + شب | مجاز | عصر بین آن‌ها فاصله زمانی است |
| عصر + شب | ممنوع | متوالی و بیش از ۱۲ ساعت |
| شب → صبحِ روز بعد | ممنوع | متوالی بدون فاصله |
| صبح + عصر + شب | ممنوع | شامل عصر+شب |

فرانت نباید ترکیب‌های ممنوع را به‌عنوان گزینهٔ همزمان پیشنهاد دهد؛ بک‌اند هم در الگوریتم و گاردها آن‌ها را رد می‌کند.

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

این آیتم‌ها در تنظیمات زمان‌بندی دپارتمان قابل روشن/خاموش‌اند:

| فیلد API | عنوان UI پیشنهادی | رفتار |
|----------|-------------------|--------|
| `allowCurrentMonthScheduling` | امکان شیفت‌بندی ماه جاری | اگر فعال باشد، برای **ماه شمسی جاری** حتی پس از شروع ماه، Optimize و `DeleteMonthlySchedule` مجاز است. **فقط برای توسعه و تست** استفاده شود. ماه‌های گذشته همچنان مسدود می‌مانند. |
| `allowMonthlyRescheduleWithAutoDelete` | امکان شیفت‌بندی مجدد ماهانه و حذف خودکار شیفت‌بندی قبلی | اگر فعال باشد و برنامهٔ قبلی برای همان ماه وجود داشته باشد، قبل از ذخیرهٔ برنامهٔ جدید، همه انتساب‌های آن ماه دپارتمان **خودکار حذف** و با برنامه جدید جایگزین می‌شوند (نیازی به فراخوانی دستی حذف نیست). |
| `allowEveningAfterNightShift` | اجازه شیفت عصر در روز بعد از شب | **فعال (پیش‌فرض اگر null):** بعد از شیفت شب، صبح روز بعد همچنان ممنوع است؛ ولی الگوریتم در صورت نیاز می‌تواند **عصر** روز بعد را بدهد (الزام نیست). **غیرفعال:** روز بعد از شب باید کاملاً **off** باشد (صبح/عصر/شب هیچ‌کدام). |

ترکیب پیشنهادی برای تست ماه جاری: دو فلگ اول را روشن کنید تا بتوانید ماه جاری را دوباره Optimize کنید و برنامه قبلی جایگزین شود.

### درخواست حضور (یادآوری برای فرانت)

- `requestType = 0` (`FullDay`) فقط با `requestAction = 1` (عدم‌حضور/مرخصی) معنا دارد.
- درخواست **حضور** باید `requestType = 1` (`SpecificShift`) + `shiftLabel` (صبح/عصر/شب) باشد.
- حضور کل‌روز مجاز نیست (سقف ۱۲ ساعت متوالی). ترکیب‌های مجاز یک روز: صبح+عصر یا صبح+شب؛ عصر+شب و شب→صبح روز بعد ممنوع‌اند. بک‌اند درخواست حضور کل‌روز را رد می‌کند.

## ۱.۶ بررسی درخواست شیفت توسط سوپروایزر (`ShiftRequest`)

فقط درخواست‌های با `status = 1` (`Approved`) در Optimize لحاظ می‌شوند.

### وضعیت‌ها

| مقدار | نام | معنی |
|------:|-----|------|
| `0` | `Pending` | در انتظار بررسی |
| `1` | `Approved` | تأیید شده |
| `2` | `Rejected` | رد شده |

### اکشن‌ها

| اکشن | روش | توضیح |
|------|------|--------|
| `UpdateShiftRequestBySupervisor` | `PUT /api/ShiftRequest/UpdateShiftRequestBySupervisor?id={id}` | تأیید / رد / **لغو تأیید** |
| `DeleteShiftRequest` | `DELETE /api/ShiftRequest/DeleteShiftRequest?id={id}` | حذف درخواست |

بدنهٔ بررسی سوپروایزر:

```json
{
  "status": 2,
  "supervisorComment": "تأیید قبلی اشتباه بود؛ رد شد."
}
```

فیلد DTO: `supervisorComment` (نه `supervisorNote`).

### قواعد وضعیت برای UI

| وضعیت فعلی | عملیات مجاز سوپروایزر | دکمه پیشنهادی |
|------------|----------------------|----------------|
| `Pending` | تأیید (`status=1`) یا رد (`status=2`) | «تأیید» / «رد» |
| `Approved` | فقط رد (`status=2`) — **لغو تأیید اشتباه** | «رد / لغو تأیید» |
| `Rejected` | تغییر وضعیت ندارد؛ قابل **حذف** است | «حذف» |

| وضعیت فعلی | حذف (`DeleteShiftRequest`) |
|------------|----------------------------|
| `Pending` | مجاز (معمولاً توسط خود کاربر قبل از بررسی) |
| `Rejected` | **مجاز** (پاک‌سازی لیست ردشده‌ها) |
| `Approved` | **غیرمجاز** — ابتدا رد کنید، سپس در صورت نیاز حذف کنید |

### نکات مهم

- رد کردن درخواست تأییدشده، آن را از قیود Optimize بعدی خارج می‌کند؛ اگر برنامهٔ ماه قبلاً ذخیره شده، برای اعمال تغییر باید دوباره Optimize/ذخیره شود (یا برنامه ماه حذف و از نو ساخته شود).
- درخواست ردشده را نمی‌توان دوباره تأیید کرد؛ در صورت نیاز حذف و ثبت درخواست جدید.
- پیام `message` خطای API را عیناً نشان دهید (مثلاً تلاش برای تأیید مجدد درخواست تأییدشده، یا حذف مستقیم Approved).

### محدودیت ظرفیت هنگام تأیید حضور (جدید)

سوپروایزر **نمی‌تواند** درخواست **حضور در شیفت مشخص** (`requestAction = 0` یعنی `RequestToBeOnShift` + `requestType = 1` یعنی `SpecificShift`) را بیش از **ظرفیت همان شیفت برای تخصص کاربر** در همان روز تأیید کند.

#### منبع ظرفیت

- از `ShiftRequiredSpecialty` همان دپارتمان خوانده می‌شود:
  - روز عادی: `requiredTottalCount`
  - روز تعطیل: `holidayRequiredTottalCount` (اگر خالی باشد همان `requiredTottalCount`)
- ظرفیت **به ازای تخصص** است، نه کل دپارتمان. مثلاً اگر شیفت صبح برای تخصص «هوشبری» ظرفیت ۲ نفر دارد، حداکثر ۲ درخواست حضور تأییدشده برای همان **تاریخ + شیفت + تخصص** مجاز است.

#### زمان اعمال

- فقط هنگام **`UpdateShiftRequestBySupervisor`** با `status: 1` (`Approved`) و وقتی درخواست فعلاً `Pending` است.
- درخواست‌های **عدم‌حضور (OFF)**، **کل‌روز**، یا **رد/لغو تأیید** تحت این قاعده نیستند.
- ثبت درخواست توسط کاربر (`CreateShiftRequest`) هنوز مسدود نمی‌شود؛ محدودیت در **لحظه تأیید** اعمال می‌شود.

#### پاسخ API در صورت پر بودن ظرفیت

- `isSuccess: false`
- `message` نمونه:

```text
ظرفیت شیفت صبح در تاریخ 1405/05/24 تکمیل شده است. در حال حاضر 2 درخواست حضور تأییدشده (علی رضایی، مینا جاهدی) وجود دارد و ظرفیت این شیفت 2 نفر است. امکان تأیید درخواست حضور بیش از ظرفیت وجود ندارد.
```

#### اقدام لازم در فرانت (ظرفیت)

| مورد | پیشنهاد UI |
|------|------------|
| خطای تأیید | پس از `PUT UpdateShiftRequestBySupervisor` اگر `isSuccess === false` بود، **`message` را در toast/alert/modal عیناً** نشان دهید (نه پیام عمومی «خطا در تأیید»). |
| دکمه تأیید | برای ردیف‌های `Pending` با `requestAction = حضور` و `requestType = SpecificShift`، **اختیاری:** قبل از کلیک، با شمارش درخواست‌های `Approved` همان روز/شیفت/تخصص در لیست فعلی، اگر ظرفیت پر است دکمه «تأیید» را **غیرفعال** کنید و tooltip بگذارید: «ظرفیت این شیفت تکمیل شده است». |
| شمارش سمت کلاینت | API جداگانه برای «ظرفیت باقی‌مانده» وجود ندارد؛ می‌توانید از همان `GetShiftRequests` با فیلتر `status=Approved` + همان `requestDate` + `shiftLabel` + تخصص کاربر درخواست‌دهنده، تعداد را با `requiredTottalCount` (یا مقدار تعطیل از `ShiftRequiredSpecialty`) مقایسه کنید. **منبع حقیقت نهایی بک‌اند است** — حتی با پیش‌نمایش UI، پاسخ خطای API را همیشه پوشش دهید. |
| نمایش لیست | در جزئیات درخواست یا کنار دکمه تأیید، **اختیاری:** «تأییدشده برای این شیفت: X از Y» را نشان دهید تا سوپروایزر قبل از کلیک بداند. |
| لغو تأیید | با **رد** (`status: 2`) یک درخواست `Approved`، یک جایگاه ظرفیت آزاد می‌شود؛ دکمه تأیید درخواست‌های Pending همان slot دوباره فعال می‌شود. |
| Optimize | اگر قبلاً بیش از ظرفیت تأیید شده بود، Optimize همچنان خطا می‌دهد؛ این قاعده جدید از **تأیید نفر سوم به بعد** جلوگیری می‌کند. |

#### فیلدهای مرتبط برای مقایسه در UI

| فیلد درخواست | کاربرد |
|--------------|--------|
| `requestDate` / `requestPersianDate` | روز مورد نظر |
| `shiftLabel` | `0=Morning`, `1=Evening`, `2=Night` |
| `requestAction` | فقط `0` (حضور) مشمول سقف |
| `requestType` | فقط `1` (SpecificShift) مشمول سقف |
| `status` | فقط `Approved` در شمارش ظرفیت مصرف‌شده |
| `user.specialtyId` | تخصص برای تطبیق با `ShiftRequiredSpecialty` |

### اقدام لازم در فرانت

- در لیست درخواست‌ها ستون/برچسب **حضور / عدم‌حضور** (`requestAction`) را نمایش دهید تا با «تأیید شده» اشتباه گرفته نشود.
- برای ردیف‌های `Approved` دکمه «رد / لغو تأیید» را فعال کنید (همان `UpdateShiftRequestBySupervisor` با `status: 2`).
- برای ردیف‌های `Rejected` دکمه «حذف» را نشان دهید؛ برای `Approved` حذف را مخفی/غیرفعال کنید.
- قبل از رد تأییدشده و قبل از حذف ردشده، یک تأیید دو مرحله‌ای (confirm) بگذارید.
- برای درخواست **حضور** (`Pending`): خطای ظرفیت پر (`message` شامل «ظرفیت شیفت») را عیناً نشان دهید؛ در صورت امکان دکمه تأیید را وقتی ظرفیت پر است غیرفعال کنید.

### اکشن حذف شیفت‌بندی ماه

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

- در فرم `DepartmentSchedulingSettings` سوئیچ‌های `allowCurrentMonthScheduling`، `allowMonthlyRescheduleWithAutoDelete` و **`allowEveningAfterNightShift`** را اضافه کنید (برچسب فارسی مطابق جدول).
- برای `allowEveningAfterNightShift` توضیح کوتاه UI: «اگر خاموش باشد، روز بعد از شیفت شب حتماً مرخص/بدون شیفت است؛ اگر روشن باشد فقط عصر روز بعد در صورت نیاز مجاز است (صبح بعد از شب همیشه ممنوع).»
- دکمه «حذف شیفت‌بندی ماه» با تأیید کاربر؛ اگر ماه شروع شده و `allowCurrentMonthScheduling` خاموش است، دکمه را غیرفعال کنید.
- قبل از `optimize-and-save` اگر برنامه قبلی هست و `allowMonthlyRescheduleWithAutoDelete` خاموش است، دکمه Optimize را قفل کنید و کاربر را به حذف هدایت کنید؛ اگر فلگ روشن است، نیازی به قفل به‌خاطر برنامه قبلی نیست.
- پیام `message` خطای API را عیناً به سوپروایزر نشان دهید.

### توزیع صبح/عصر در روزهای تعطیل

الگوریتم علاوه بر تعادل ماهانهٔ صبح/عصر، **تعداد شیفت صبح و عصر روی روزهای تعطیل** را هم بین پرسنل گردشی هم‌تخصص پخش می‌کند (`HolidayMorningEveningFairnessGuard` + وزن نرم `FairHolidayMorningEveningPeerWeight`). بعد از Optimize مجدد، انتظار این است که تعطیلات روی چند نفر خاص متمرکز نشوند.

## ۱.۷ جابجایی / واگذاری شیفت (`ShiftExchange`)

علاوه بر **جابجایی دوطرفه** (هر دو نفر شیفت دارند)، امکان **واگذاری شیفت به همکار آزاد** اضافه شده است: کاربر A شیفت خود را به B می‌دهد؛ B در همان تاریخ و همان شیفت انتساب ندارد؛ A آزاد می‌شود.

### APIها

| اکشن | روش | توضیح |
|------|------|--------|
| `GetAll` / `GetById` | `GET /api/ShiftExchange` ، `GET /api/ShiftExchange/{id}` | لیست / جزئیات |
| `GetByUserId` | `GET /api/ShiftExchange/user/{userId}` | درخواست‌های مرتبط با کاربر |
| `GetPendingApprovals` | `GET /api/ShiftExchange/pending-approvals/{supervisorId}` | در انتظار تأیید سوپروایزر |
| `Create` | `POST /api/ShiftExchange` | ثبت درخواست |
| `Update` | `PUT /api/ShiftExchange` | ویرایش دلیل (فقط `Pending`) |
| `Approve` | `POST /api/ShiftExchange/approve` | تأیید / رد |
| `ExecuteExchange` | `POST /api/ShiftExchange/execute/{exchangeId}` | اعمال روی برنامه (پس از تأیید) |
| `Cancel` | `POST /api/ShiftExchange/cancel/{exchangeId}` | لغو |
| `Delete` | `DELETE /api/ShiftExchange/{id}` | حذف (غیر از `Executed`) |

### نوع عملیات (`exchangeType`)

| مقدار | نام | معنی UI |
|------:|-----|---------|
| `0` | `Swap` | **جابجایی دوطرفه** — هر دو کاربر یک `ShiftAssignment` انتخاب می‌کنند |
| `1` | `Transfer` | **واگذاری** — فقط شیفت کاربر A؛ B در همان slot **آزاد** (Off) است |

فیلد جدید در پاسخ: `exchangeType` در `ShiftExchangeDtoGet`.

### وضعیت‌ها (`status` / `ExchangeStatus`)

| مقدار | نام | معنی |
|------:|-----|------|
| `0` | `Pending` | در انتظار تأیید |
| `1` | `Approved` | تأیید شده — آمادهٔ `execute` |
| `2` | `Rejected` | رد شده |
| `3` | `Executed` | روی برنامه اعمال شده |
| `4` | `Cancelled` | لغو شده |

### بدنهٔ ثبت — جابجایی دوطرفه (`Swap`)

```json
{
  "requestingUserId": 1,
  "offeringUserId": 2,
  "requestingShiftAssignmentId": 10,
  "offeringShiftAssignmentId": 20,
  "exchangeType": 0,
  "reason": "جابجایی دوطرفه"
}
```

### بدنهٔ ثبت — واگذاری به کاربر آزاد (`Transfer`)

```json
{
  "requestingUserId": 1,
  "offeringUserId": 3,
  "requestingShiftAssignmentId": 10,
  "offeringShiftAssignmentId": null,
  "exchangeType": 1,
  "reason": "واگذاری شیفت به همکار آزاد"
}
```

**نکته:** برای `Transfer` حتماً `offeringShiftAssignmentId` را **`null` بفرستید** (نه `0`). اگر شیفت طرف مقابل انتخاب شود، API خطا می‌دهد.

### بدنهٔ تأیید سوپروایزر

```json
{
  "id": 1,
  "isApproved": true,
  "supervisorComment": "تأیید شد"
}
```

### فرآیند UI (پیشنهادی)

```
کاربر A → انتخاب نوع (Swap | Transfer)
       → انتخاب شیفت خودش (requestingShiftAssignmentId)
       → انتخاب کاربر B
       → [فقط Swap] انتخاب شیفت B
       → POST Create → Pending
سوپروایزر → GET pending-approvals → Approve/Reject
         → [پس از Approved] POST execute/{id}
```

اجرای `execute` جدا از تأیید است؛ بعد از تأیید، دکمه «اعمال روی برنامه» را نشان دهید.

### قواعد اعتبارسنجی (خطاهای رایج — `message` را عیناً نشان دهید)

| شرایط | پیام / رفتار |
|--------|----------------|
| `Transfer` + `offeringShiftAssignmentId` پر | «در واگذاری شیفت به کاربر آزاد، نباید شیفت برای کاربر مقابل انتخاب شود.» |
| `Swap` بدون `offeringShiftAssignmentId` | «برای جابجایی دوطرفه، شناسه شیفت کاربر مقابل الزامی است.» |
| B در همان تاریخ+شیفت انتساب دارد | «کاربر دریافت‌کننده در زمان این شیفت انتساب فعال دارد…» |
| A و B دپارتمان متفاوت | «کاربر دریافت‌کننده باید از همان دپارتمان…» |
| A = B | «امکان جابجایی یا واگذاری شیفت با خودتان وجود ندارد.» |
| اجرا و B دیگر آزاد نیست | «کاربر دریافت‌کننده اکنون در زمان این شیفت انتساب دارد…» |

### اقدام لازم در فرانت

| بخش | کار |
|-----|-----|
| **فرم ثبت** | سوئیچ یا radio: «جابجایی با همکار» (`Swap`) / «واگذاری به همکار آزاد» (`Transfer`) |
| **انتخاب کاربر B** | در `Transfer`: لیست همکاران **بدون انتساب** روی همان `shiftDateId` + `shiftId` (یا همان روز/برچسب شیفت از برنامهٔ ماه) |
| **انتخاب شیفت B** | فقط در `Swap` نمایش داده شود؛ در `Transfer` مخفی باشد |
| **لیست درخواست‌ها** | ستون «نوع»: جابجایی / واگذاری (`exchangeType`) |
| **سوپروایزر** | در `Transfer` فقط یک شیفت (A) و نام دریافت‌کننده را نشان دهید، نه دو شیفت |
| **پس از Approved** | دکمه `POST execute/{id}`؛ پس از موفقیت برنامهٔ ماه را refresh کنید |
| **خطا** | `BadRequest` با متن فارسی — در toast/modal **عین `message`** |

### تشخیص «کاربر آزاد» در UI

- Off = **بدون** `ShiftAssignment` برای همان `shiftDateId` + `shiftId`.
- داشتن شیفت **دیگر** در همان روز (مثلاً عصر وقتی صبح واگذاری می‌شود) مانع `Transfer` نیست؛ فقط **همان slot** نباید انتساب داشته باشد.
- منبع داده: همان API/جدول برنامهٔ ماه که برای جابجایی قبلی استفاده می‌کردید.

### سازگاری با نسخه قبل

- درخواست‌های قدیمی بدون `exchangeType` در DB مقدار `0` (`Swap`) دارند.
- اگر `exchangeType` ارسال نشود، پیش‌فرض `Swap` است؛ رفتار قبلی حفظ می‌شود.

### Migration

ستون `ExchangeType` روی `ShiftExchanges` — migration: `20260816120000_AddExchangeTypeToShiftExchange` (بعد از `Update-Database`).

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
  - `شب -> صبح` در روز بعد همیشه ممنوع
  - اگر `allowEveningAfterNightShift = false` باشد، روز بعد از شب کاملاً بدون شیفت است (عصر/شب روز بعد هم ممنوع)
  - اگر `allowEveningAfterNightShift = true` باشد، عصر روز بعد از شب در صورت نیاز مجاز است (الزام نیست)
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

### تاریخ استخدام (`DateOfEmployment`)

- در API ایجاد/ویرایش کاربر، `dateOfEmployment` را **شمسی** بفرستید (مثلاً `1380/01/04`).
- بک‌اند آن را به میلادی واقعی ذخیره می‌کند.
- در خروجی لیست/جزئیات کاربر، مقدار میلادی **نرمال‌شده** برمی‌گردد (اگر دادهٔ قدیمی اجزای شمسی را در فیلد میلادی داشته باشد، هنگام خواندن و با migration داده اصلاح می‌شود).
- برای محاسبهٔ سنوات بهره‌وری همیشه از همین تاریخ میلادی واقعی استفاده می‌شود.

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
- سوئیچ `allowEveningAfterNightShift` (اجازه عصر روز بعد از شب / یا off کامل روز بعد)
- در فرم درخواست شیفت: برای حضور فقط `SpecificShift`؛ گزینهٔ حضور کل‌روز را نشان ندهید / غیرفعال کنید
- نمایش ستون `requestAction` (حضور / عدم‌حضور) در لیست درخواست‌ها
- دکمه «رد / لغو تأیید» برای درخواست‌های `Approved` (`UpdateShiftRequestBySupervisor` با `status: 2`)
- **اعتبارسنجی ظرفیت هنگام تأیید حضور:** نمایش عین `message` API؛ غیرفعال کردن اختیاری دکمه تأیید وقتی X از Y جایگاه پر است
- **جابجایی شیفت (`ShiftExchange`):** radio Swap/Transfer؛ در Transfer فقط شیفت A + کاربر آزاد؛ `offeringShiftAssignmentId: null`؛ ستون `exchangeType` در لیست
- **پس از تأیید جابجایی:** دکمه `execute/{id}` و refresh برنامهٔ ماه
- دکمه حذف برای درخواست‌های `Rejected` و `Pending`؛ مخفی بودن حذف برای `Approved`
- قبل از ثبت درخواست شب: نمایش باقی‌ماندهٔ سهمیه شب ماهانه کاربر؛ در صورت پر بودن سهمیه، دکمه را قفل کنید
- نمایش خطای Optimize وقتی سهمیه شب برآورده نشود (`message` را عیناً نشان دهید)
- توزیع عادلانه صبح/عصر **روزهای تعطیل** (نه فقط تعادل ماهانه M/E)
- اضافه کردن فیلدهای `Holiday*` به فرم `ShiftRequiredSpecialty`
- تفکیک UI روز عادی و روز تعطیل در نیازمندی تخصص
- نمایش آمار بهره‌وری در خروجی شیفت‌بندی
- بررسی صحیح بودن استفاده از `ShiftLabel` در فرانت

## 10. فایل‌های بک‌اند مرتبط

اگر تیم فرانت خواست دقیق‌تر بررسی کند، این فایل‌ها مهم‌ترین نقاط تغییر هستند:

- `ShiftYar.Api/Controllers/ShiftRequestModel/ShiftRequestController.cs`
- `ShiftYar.Application/Features/ShiftRequestModel/Services/ShiftRequestService.cs`
- `ShiftYar.Application/Common/Utilities/ApprovedOnShiftCapacityValidator.cs`
- `ShiftYar.Api/Controllers/ShiftExchangeModel/ShiftExchangeController.cs`
- `ShiftYar.Application/Features/ShiftExchangeModel/Services/ShiftExchangeService.cs`
- `ShiftYar.Application/Common/Utilities/ShiftExchangeValidator.cs`
- `ShiftYar.Application/DTOs/ShiftExchangeModel/ShiftExchangeDtoAdd.cs`
- `ShiftExchange_Feature_Documentation.md` (جزئیات کامل API جابجایی)
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequestModel/ShiftRequestDtoUpdateBySupervisor.cs`
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
- `ShiftYar.Application/Common/Utilities/NightQuotaRequestLinker.cs`
- `ShiftYar.Application/Common/Utilities/DailyAssignmentRules.cs`
- `ShiftYar.Application/Common/Utilities/AdjacentShiftRestRules.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoAdd.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftRequiredSpecialtyModel/ShiftRequiredSpecialtyDtoGet.cs`
- `ShiftYar.Application/DTOs/ShiftModel/ShiftSchedulingModel/ShiftSchedulingResultDto.cs`
- `ShiftYar.Domain/Entities/UserModel/User.cs`
- `ShiftYar.Application/Common/Utilities/ProductivityWorkedHoursCalculator.cs`
- `ShiftYar.Application/Features/ProductivityModel/Services/WorkingHoursCalculator.cs`

