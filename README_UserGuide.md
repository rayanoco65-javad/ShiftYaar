# راهنمای جامع راه‌اندازی و اجرای شیفت‌بندی (ShiftYar)

این راهنما برای کاربر مبتدی نوشته شده است تا بدون شناخت قبلی از پروژه بتواند داده‌های اولیه را وارد کند و عملیات شیفت‌بندی را به‌صورت صحیح و بهینه انجام دهد.

## 1) مفاهیم کلیدی
- تاریخ‌ها: ورودی‌ها در مرز API به‌صورت شمسی `yyyy/MM/dd` هستند؛ داخل سیستم همه‌چیز با تاریخ میلادی `DateTime` انجام می‌شود.
- قواعد سخت (Hard): الزاماتی که نقض‌پذیر نیستند (مثل یک شیفت در روز، حداقل استراحت، ظرفیت تخصص و ...).
- قواعد نرم (Soft/Weights): اهداف ترجیحی که با وزن قابل تنظیم‌اند (تعادل جنسیت، ترجیح تخصص، عدالت و چرخش شیفت اضافه...).
- درخواست شیفت (ShiftRequest): کاربر تاریخ/شیفت ترجیحی یا عدم‌حضورش را ثبت می‌کند؛ سوپروایزر پس از بررسی تأیید/رد می‌کند. فقط تأیید‌شده‌ها در برنامه لحاظ می‌شوند.

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

## 4) ثبت درخواست‌های شیفت توسط کارکنان
کارکنان تا قبل از شروع ماه جدید، درخواست‌های خود را ثبت می‌کنند؛ سپس سوپروایزر بررسی و تأیید/رد می‌کند.
- Create Request: `POST /api/ShiftRequest` با بدنه (مثال شیفت/عدم‌حضور یک‌روزه):
```
{
  "userId": 10,
  "requestPersianDate": "1404/01/05",
  "requestType": 1,
  "shiftLabel": 0,
  "requestAction": 0,
  "reason": "..."
}
```
- Create Leave Range: `POST /api/ShiftRequest/CreateShiftRequestForLeave` با بدنه:
```
{
  "userId": 10,
  "startPersianDate": "1404/01/10",
  "endPersianDate": "1404/01/15",
  "reason": "مرخصی"
}
```
- Update/Delete/Approve: از اکشن‌های مربوط به ویرایش توسط کاربر/سوپروایزر استفاده کنید؛ حتماً وضعیت `Approved` شود تا در زمان‌بندی لحاظ گردد.

## 5) اجرای شیفت‌بندی
پس از تعیین تکلیف تمام درخواست‌ها (نباید درخواست در وضعیت Pending باقی مانده باشد):
1) اعتبارسنجی ورودی (اختیاری ولی توصیه‌شده)
   - Endpoint: `POST /api/ShiftScheduling/validate`
   - Body نمونه:
```
{
  "departmentId": 1,
  "startDate": "1404/02/01",
  "endDate": "1404/02/31",
  "algorithm": 2
}
```
- پارامترهای الگوریتم به‌صورت خودکار از تنظیمات ادمین (AlgorithmSettings) خوانده می‌شوند و نیازی نیست سوپروایزر چیزی بداند یا پر کند.
2) اجرای بهینه‌سازی
   - Endpoint: `POST /api/ShiftScheduling/optimize`
   - ورودی مشابه بالا؛ تاریخ‌ها شمسی هستند و در کنترلر به میلادی تبدیل می‌شوند.
   - سیستم به‌صورت خودکار:
     - کاربران فعال دپارتمان را بارگذاری می‌کند.
     - درخواست‌های تاییدشده در بازه را اعمال می‌کند (ترجیحات/عدم‌حضور).
     - قوانین سخت/نرم و مقادیر عددی را از پایگاه داده می‌خواند.
     - پارامترهای الگوریتم را از تنظیمات ادمین (یا پیش‌فرض داخلی) می‌خواند.
3) مشاهده آمار الگوریتم
   - Endpoint: `POST /api/ShiftScheduling/statistics`
4) ذخیره خروجی
   - Endpoint: `POST /api/ShiftScheduling/save`
   - خروجی `optimize` را ارسال کنید. سیستم انتساب‌های قبلی بازه را حذف و نتایج جدید را با `ShiftDateId` ذخیره می‌کند.

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
