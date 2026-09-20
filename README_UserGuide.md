# راهنمای جامع راه‌اندازی و اجرای شیفت‌بندی (ShiftYar)

این راهنما برای کاربر مبتدی نوشته شده است تا بدون شناخت قبلی از پروژه بتواند داده‌های اولیه را وارد کند و عملیات شیفت‌بندی را به‌صورت صحیح و بهینه انجام دهد.

## 1) مفاهیم کلیدی
- تاریخ‌ها: ورودی‌ها در مرز API به‌صورت شمسی `yyyy/MM/dd` هستند؛ داخل سیستم همه‌چیز با تاریخ میلادی `DateTime` انجام می‌شود.
- قواعد سخت (Hard): الزاماتی که نقض‌پذیر نیستند (مثل یک شیفت در روز، حداقل استراحت، ظرفیت تخصص، سقف روزهای کار متوالی و ...). نقض قواعد سخت صرفاً یک هشدار نیست، بلکه منجر به خطا و توقف کامل عملیات شیفت‌بندی می‌شود.
- قواعد نرم (Soft/Weights): اهداف ترجیحی که با وزن قابل تنظیم‌اند (تعادل جنسیت، ترجیح تخصص، عدالت و چرخش شیفت اضافه...).
- درخواست شیفت (ShiftRequest): کاربر تاریخ/شیفت ترجیحی یا عدم‌حضورش را ثبت می‌کند؛ سوپروایزر پس از بررسی تأیید/رد می‌کند. فقط تأیید‌شده‌ها در برنامه لحاظ می‌شوند. درخواست‌های تأییدشده بر قوانین پیش‌فرض تعطیلی پرسنل فیکس و همچنین مجوزهای پیش‌فرض شیفت پرسنل گردشی اولویت مطلق دارند. سوپروایزر می‌تواند تأیید اشتباه را با رد کردن لغو کند؛ درخواست‌های ردشده قابل حذف‌اند.

## 2) پیش‌نیازها
- SQL Server در حال اجرا
- اجرای مهاجرت‌های پایگاه‌داده (EF Migrations)
- داشتن کاربر با نقش سوپروایزر یا ادمین برای انجام عملیات مدیریتی

## 3) آماده‌سازی داده‌های پایه
### 3.1) تقویم (ShiftDate)
ابتدا باید تقویم شمسی سال هدف را بسازید تا نگاشت تاریخ‌ها و تعطیلات آماده شود.
- Endpoint: `POST /api/CalendarSeeder/SeedYear?year=1404`
- خروجی: ایجاد رکوردهای `ShiftDates` برای تمام روزهای سال با نگاشت شمسی/میلادی و تعطیلات.
- بررسی: `GET /api/CalendarSeeder` با فیلترهای `PersianDateStart` و `PersianDateEnd`.

### 3.2) ساختار سازمانی و تخصص‌ها
- ایجاد بیمارستان/بخش‌ها (در صورت وجود کنترلرهای مربوط)
- ایجاد دپارتمان و تعیین سوپروایزر
- تعریف تخصص‌ها و ارتباط‌شان با شیفت‌ها

### 3.3) تعریف شیفت‌ها (Shift)
برای هر دپارتمان شیفت‌ها را تعریف کنید (صبح/عصر/شب)، به‌همراه بازه زمانی و ظرفیت‌های تخصصی.
- Endpoint نمونه: `POST /api/Shift`
- Payload نمونه:
```
{
  "departmentId": 1,
  "label": 0, // Morning=0, Evening=1, Night=2
  "startTime": "07:00:00",
  "endTime": "15:00:00",
  "requiredSpecialties": [
    { "specialtyId": 3, "requiredMaleCount": 1, "requiredFemaleCount": 1, "requiredTottalCount": 2 }
  ]
}
```

### 3.4) قوانین شیفت‌بندیِ دپارتمان (DepartmentSchedulingSettings)
سوپروایزر یا ادمین می‌تواند قواعد سخت، مقادیر عددی مرتبط، و وزن‌های نرم را CRUD کند.
- Endpoint‌ها:
  - Create: `POST /api/DepartmentSchedulingSettings`
  - Get (paged): `GET /api/DepartmentSchedulingSettings?DepartmentId=1`
  - Get by id: `GET /api/DepartmentSchedulingSettings/{id}`
  - Update: `PUT /api/DepartmentSchedulingSettings/{id}`
  - فلگ `allowEveningAfterNightShift`: اگر true، عصر روز بعد از شب در صورت نیاز مجاز است؛ اگر false، روز بعد از شب کاملاً بدون شیفت است (صبح بعد از شب همیشه ممنوع).

### 3.5) تنظیمات الگوریتم‌های بهینه‌سازی (AlgorithmSettings) - فقط ادمین
ادمین می‌تواند پارامترهای الگوریتم‌های SA، OR-Tools و Hybrid را به‌صورت سراسری یا مخصوص هر دپارتمان تنظیم کند.
- Endpoint‌ها:
  - Create: `POST /api/AlgorithmSettings`
  - Get (paged): `GET /api/AlgorithmSettings?DepartmentId=1&AlgorithmType=1`
  - Get by id: `GET /api/AlgorithmSettings/{id}`
  - Get by department and type: `GET /api/AlgorithmSettings/by-department-and-type?departmentId=1&algorithmType=1`
  - Update: `PUT /api/AlgorithmSettings/{id}`
  - Delete: `DELETE /api/AlgorithmSettings/{id}`
- Payload نمونه برای SA:
```
{
  "departmentId": 1, // null = سراسری
  "algorithmType": 1, // 1=SA, 2=OR-Tools, 3=Hybrid
  "SA_InitialTemperature": 1000.0,
  "SA_FinalTemperature": 0.1,
  "SA_CoolingRate": 0.95,
  "SA_MaxIterations": 10000,
  "SA_MaxIterationsWithoutImprovement": 1000
}
```
- نکته: برای هر دپارتمان فقط یک رکورد تنظیمات مجاز است (ایندکس یونیک روی DepartmentId).
- فیلدهای مهم (نمونه Payload برای Create/Update):
```
{
  "departmentId": 1,
  // Hard
  "forbidUnavailableDates": true,
  "forbidDuplicateDailyAssignments": true,
  "enforceMaxShiftsPerDay": true,
  "enforceMinRestDays": true,
  "enforceMaxConsecutiveShifts": true,
  "enforceWeeklyMaxShifts": false,
  "enforceNightShiftMonthlyCap": false,
  "enforceSpecialtyCapacity": true,
  // Numeric values (optional; used when corresponding Enforce is true)
  "minRestDaysBetweenShifts": 1,
  "maxConsecutiveShifts": 3,
  "maxShiftsPerWeek": 5,
  "maxNightShiftsPerMonth": 8,
  "maxShiftsPerDay": 1,
  "maxConsecutiveNightShifts": 2,
  // Soft/Weights
  "genderBalanceWeight": 1.0,
  "specialtyPreferenceWeight": 1.0,
  "userUnwantedShiftWeight": 1.0,
  "userPreferredShiftWeight": 1.0,
  "weeklyMaxWeight": 1.0,
  "monthlyNightCapWeight": 1.0,
  // Fairness & Rotation
  "fairShiftCountBalanceWeight": 0.5,
  "extraShiftRotationWeight": 0.8,
  "shiftLabelBalanceWeight": 0.6,
  "fairnessLookbackMonths": 2
}
```
- منطق اولویت اعمال مقادیر عددی هنگام شیفت‌بندی: Per-user → Department → Default
  - اگر در بدنه درخواست `optimize` برای کاربر مقدار داده شود، همان اعمال می‌شود.
  - در غیر این صورت اگر در تنظیمات دپارتمان مقدار ثبت شده و `Enforce*` مربوط روشن باشد، مقدار دپارتمان اعمال می‌شود.
  - در غیر این صورت مقادیر پیش‌فرض سیستم استفاده می‌شود (مثلاً `MaxConsecutiveShifts=3`, `MinRestDaysBetweenShifts=1`, `MaxShiftsPerWeek=5`, `MaxNightShiftsPerMonth=8`, `MaxShiftsPerDay=1`, `MaxConsecutiveNightShifts=2`).
- راهنمای وزن‌ها:
  - اگر بخواهید عدالت و چرخش قوی‌تر عمل کند، وزن‌ها را افزایش دهید.
  - اگر پروژه کوچک است، وزن‌های 0.3 تا 1.0 پیشنهاد می‌شود.

### 3.5) کاربران (User)
- ایجاد کاربر و انتساب دپارتمان/تخصص
- تاریخ استخدام در ورودی شمسی است و در سیستم به میلادی تبدیل می‌شود.
- تعیین `ShiftType` کاربر (ثابت/گردشی) و قابلیت مدیر شیفت بودن در صورت نیاز.

## 4) سهمیه شب ماهانه و درخواست‌های شیفت

### 4.1) سهمیه شب ماهانه (`UserMonthlyNightQuota`)
قبل از Optimize هر ماه، سوپروایزر سهمیه حداقل شب را برای همان ماه شمسی تنظیم می‌کند.
- مجموع سهمیه‌های دپارتمان ≤ تعداد شب‌های ماه (`ShiftDates`)
- هر درخواست شب تأییدشده یک واحد از سهمیه کاربر مصرف می‌کند
- رسیدن به سهمیه در Optimize **اجباری** است

نمونه: `POST /UpsertDepartmentMonthlyNightQuotas`

### 4.2) ثبت درخواست‌های شیفت توسط کارکنان
کارکنان تا قبل از شروع ماه جدید، درخواست‌های خود را ثبت می‌کنند؛ سپس سوپروایزر بررسی و تأیید/رد می‌کند.
- Create Request: `POST /api/ShiftRequest` با بدنه (مثال حضور در شیفت مشخص):
```
{
  "userId": 10,
  "requestPersianDate": "1405/05/13",
  "requestType": 1,
  "shiftLabel": 2,
  "requestAction": 0,
  "reason": "درخواست شیفت شب"
}
```
- `requestType`:
  - برای پرسنل گردشی: `0` = کل‌روز (فقط مرخصی/عدم‌حضور)، `1` = شیفت مشخص (برای حضور الزامی). حضور کل‌روز برای پرسنل گردشی مجاز نیست.
  - برای پرسنل با شیفت فیکس (`FixedShift`): امکان ثبت درخواست حضور (`RequestToBeOnShift`) برای روزهای تعطیل رسمی حتی به صورت کل‌روز (`0`) یا بدون انتخاب برچسب شیفت وجود دارد و شیفت فیکس کاربر به‌طور پیش‌فرض لحاظ می‌شود. همچنین امکان درخواست حضور در شیفتی متفاوت (مثلاً شیفت عصر برای پرسنل فیکس صبح) وجود دارد.
- **اولویت مطلق درخواست‌های شیفت تأییدشده بر سایر قوانین:**
  - **اولویت بر تعطیلی پیش‌فرض پرسنل فیکس:** پرسنل فیکس طبق روال در روزهای تعطیل رسمی OFF هستند؛ اما در صورت وجود درخواست حضور تأییدشده، کاربر در آن روز تعطیل در برنامه قرار می‌گیرد و آف نخواهد بود.
  - **اولویت بر مجوزهای شیفت پرسنل گردشی (`AllowedShiftPermissions`):** اگر کاربر گردشی در تنظیمات خود مجاز به شیفت خاصی نباشد (مثلاً فقط مجاز به عصر و شب بوده و مجاز به صبح نباشد)، با ثبت و تأیید درخواست شیفت صبح، آن شیفت علیرغم عدم داشتن مجوز قبلی در برنامه به او تخصیص می‌یابد.
  - **تطبیق ظرفیت روزهای تعطیل:** اگر برای یک روز تعطیل رسمی، ظرفیت شیفت در نیازمندی‌ها صفر یا کمتر از تعداد درخواست‌های تأییدشده باشد، سیستم به‌طور هوشمند ظرفیت مورد نیاز را پشتیبانی می‌کند تا مانع بروز خطای مازاد ظرفیت شود.
- **OFF تأییدشده** برای صبح یا کل‌روز → **شب روز قبل** در Optimize مسدود می‌شود
- **تأیید حضور** فقط تا ظرفیت شیفت/تخصص همان روز (`ShiftRequiredSpecialty`)
- برای درخواست شب: سهمیه ماهانه باید از قبل تنظیم شده باشد و از سهمیه تجاوز نشود
- Create Leave Range: `POST /api/ShiftRequest/CreateShiftRequestForLeave`
- Update/Delete/Approve: وضعیت باید `Approved` شود تا در زمان‌بندی لحاظ گردد

### 4.3) ترکیب مجاز در یک روز
- مجاز: صبح+عصر ، صبح+شب
- ممنوع: عصر+شب ، شب→صبح روز بعد
- **OFF تأییدشده** صبح/کل‌روز → شب روز قبل ممنوع

### 4.4) اولویت پر کردن موظفی
- **غیرطرحی** (`IsProjectPersonnel = false/null`): اول پر می‌شوند؛ با `OvertimeConsent` می‌توانند اضافه‌کار بگیرند
- **طرحی** (`IsProjectPersonnel = true`): فقط تا ساعت موظفی — بدون اضافه‌کار
- **ساعت موظفی دستی:** فیلد `MaxProductivityRequiredHours` — اگر مثبت باشد، محاسبه خودکار نادیده گرفته می‌شود
- **محاسبه خودکار موظفی بهره‌وری:** بر مبنای ماه استاندارد (۱۷۶ ساعت در ماه ۳۱ روزه) و سابقه خدمت بالینی پرسنل:
  - زیر ۵ سال: ۱۷۶ ساعت (بدون کسر ساعت)
  - ۵ تا ۹ سال: ۱۷۱ ساعت
  - ۱۰ تا ۱۲ سال: ۱۶۷ ساعت
  - ۱۳ تا ۱۷ سال: ۱۶۳ ساعت
  - ۱۸ سال به بالا: ۱۵۸ ساعت (سه نوبته گردشی) تا ۱۶۳ ساعت (ثابت روز یا دو نوبته)

### 4.5) مجوز نوع شیفت (`AllowedShiftPermissions`)
- پنج گزینه flags: صبح(1)، عصر(2)، شب(4)، صبح/عصر(8)، صبح/شب(16)
- `null` = مشتق از ShiftType/SubType/TwoShiftRotationPattern
- ترکیب روزانه نیاز به `MaxShiftsPerDay = 2` در تنظیمات دپارتمان دارد

## 5) اجرای شیفت‌بندی
پس از تعیین سهمیه شب و تکلیف تمام درخواست‌ها (نباید درخواست Pending باقی بماند):
1) در صورت وجود برنامه قبلی: `POST /DeleteMonthlySchedule` یا فعال بودن `allowMonthlyRescheduleWithAutoDelete`
2) اعتبارسنجی ورودی (اختیاری ولی توصیه‌شده)
   - Endpoint: `POST /api/ShiftScheduling/validate`
3) اجرای بهینه‌سازی و ذخیره
   - `POST /api/ShiftScheduling/optimize-and-save` یا نسخه async
   - سیستم سهمیه شب ماهانه و درخواست‌های تأییدشده را اعمال می‌کند
   - اگر سهمیه شب برآورده نشود، یا هر یک از قوانین سخت (مانند سقف روزهای متوالی کاری `MaxConsecutiveWorkdays`) نقض شود، عملیات صرفاً در حد هشدار نبوده و با خطا متوقف می‌شود.
4) فلگ‌های تست در `DepartmentSchedulingSettings`:
   - `allowCurrentMonthScheduling`
   - `allowMonthlyRescheduleWithAutoDelete`

## 6) نکات عدالت و چرخش
- با وزن‌های `FairShiftCountBalanceWeight`, `ExtraShiftRotationWeight`, `ShiftLabelBalanceWeight` می‌توانید شدت عدالت را تنظیم کنید.
- `FairnessLookbackMonths` مشخص می‌کند سابقه چند ماه قبل در چرخش و تعادل لحاظ شود.
- اگر می‌خواهید چرخش قوی‌تر شود، `ExtraShiftRotationWeight` را افزایش دهید.

## 7) سطح دسترسی‌ها
- عملیات شیفت‌بندی و تنظیمات دپارتمان: فقط نقش‌های `Admin` و `Supervisor`.
- ثبت/ویرایش درخواست شیفت: کاربر مربوط و سوپروایزر مطابق اکشن‌ها.

## 8) خطاهای رایج و رفع اشکال
- «Pending موجود است»: ابتدا تمام درخواست‌های بازه را بررسی و تأیید/رد کنید.
- «نبودن تقویم»: قبل از هر چیز تقویم سال را Seed کنید.
- «کمبود ظرفیت/تخصص»: ظرفیت‌های تخصصی شیفت‌ها را بازبینی کنید یا کاربران بیشتری با تخصص موردنیاز اضافه کنید.
- «استفاده از تاریخ میلادی در ورودی»: ورودی‌ها باید شمسی `yyyy/MM/dd` باشند؛ سیستم خودش تبدیل می‌کند.

## 9) چک‌لیست سریع اجرا
- Seed کردن تقویم سال
- تعریف شیفت‌ها و ظرفیت تخصصی برای دپارتمان
- تنظیم قواعد سخت/نرم دپارتمان
- ایجاد و فعال‌سازی کاربران (با دپارتمان/تخصص)
- ثبت، بررسی و تأیید/رد درخواست‌های شیفت
- اجرای `optimize` و سپس `save`

اگر در هر مرحله به نمونه Payload یا Endpoint بیشتری نیاز داشتید، بفرمایید تا اضافه کنم.

## 10) تنظیمات الگوریتم توسط ادمین
- ادمین می‌تواند تنظیمات الگوریتم‌ها را به‌صورت سراسری یا per-department ثبت/ویرایش کند (SA/OR-Tools/Hybrid).
- هنگام اجرای validate/optimize پارامترهای الگوریتم به‌صورت خودکار از تنظیمات ادمین (AlgorithmSettings) خوانده می‌شوند.
- Endpointهای CRUD برای مدیریت AlgorithmSettings در بخش 3.5 توضیح داده شده‌اند.
