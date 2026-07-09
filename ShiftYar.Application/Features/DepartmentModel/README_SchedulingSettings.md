# راهنمای تنظیمات پیشرفته شیفت‌بندی دپارتمان

## مقدمه

این راهنما توضیح می‌دهد که چگونه سوپروایزرها می‌توانند تنظیمات پیشرفته شیفت‌بندی را برای دپارتمان خود پیکربندی کنند تا الگوریتم شیفت‌بندی بتواند نیازهای خاص هر دپارتمان را در نظر بگیرد.

## تنظیمات حداقل شیفت برای پرسنل گردشی

### فعال‌سازی
- `EnforceMinimumShiftsForRotatingStaff`: فعال/غیرفعال کردن اعمال حداقل شیفت برای پرسنل گردشی

### برای شیفت گردشی سه نوبت کاری
- `MinMorningShiftsForThreeShiftRotation`: حداقل تعداد شیفت صبح مورد نیاز
- `MinEveningShiftsForThreeShiftRotation`: حداقل تعداد شیفت عصر مورد نیاز  
- `MinNightShiftsForThreeShiftRotation`: حداقل تعداد شیفت شب مورد نیاز

### برای شیفت گردشی دو نوبت کاری
- `MinFirstShiftForTwoShiftRotation`: حداقل تعداد شیفت اول مورد نیاز
- `MinSecondShiftForTwoShiftRotation`: حداقل تعداد شیفت دوم مورد نیاز

**نکته**: شیفت اول و دوم بر اساس الگوی چرخش کاربر تعیین می‌شود:
- صبح/عصر: اول=صبح، دوم=عصر
- صبح/شب: اول=صبح، دوم=شب  
- عصر/شب: اول=عصر، دوم=شب

## تنظیمات شب‌دوست/شب‌گریز

### فعال‌سازی
- `EnableNightShiftPreference`: فعال/غیرفعال کردن تنظیمات شب‌دوست/شب‌گریز

### نوع تنظیمات
- `NightShiftPreferenceType`: نوع تنظیمات شب
  - `0`: شب‌دوست - باقیمانده شیفت‌های شب به پرسنل با سابقه بیشتر اختصاص می‌یابد
  - `1`: شب‌گریز - باقیمانده شیفت‌های شب به پرسنل با سابقه کمتر اختصاص می‌یابد
  - `2`: خنثی - بدون ترجیح خاص

### وزن تنظیمات
- `NightShiftPreferenceWeight`: وزن تنظیمات شب‌دوست/شب‌گریز در الگوریتم

## تنظیمات مدیریت شیفت شب

### الزام حضور مدیر با سابقه
- `RequireExperiencedManagerForNightShift`: الزام حضور مدیر با سابقه در شیفت شب
- `MinExperienceYearsForNightShiftManager`: حداقل سال سابقه برای مدیر شیفت شب
- `NightShiftManagerRequirementWeight`: وزن الزام مدیر با سابقه در شیفت شب

## تنظیمات اولویت‌بندی تخصص

### فعال‌سازی
- `EnableSpecialtyPriorityInShifts`: فعال/غیرفعال کردن اولویت‌بندی تخصص در شیفت‌ها
- `SpecialtyPriorityWeight`: وزن اولویت‌بندی تخصص

## تنظیمات تعادل جنسیتی برای هر نوع شیفت

### فعال‌سازی
- `EnableGenderBalancePerShiftType`: فعال/غیرفعال کردن تعادل جنسیتی برای هر نوع شیفت
- `GenderBalancePerShiftWeight`: وزن تعادل جنسیتی برای هر نوع شیفت

## تنظیمات حداقل/حداکثر شیفت برای هر نوع شیفت

### فعال‌سازی
- `EnforceShiftTypeLimits`: اعمال محدودیت‌های نوع شیفت

### محدودیت‌های شیفت صبح
- `MinMorningShiftsPerMonth`: حداقل شیفت صبح ماهانه
- `MaxMorningShiftsPerMonth`: حداکثر شیفت صبح ماهانه

### محدودیت‌های شیفت عصر
- `MinEveningShiftsPerMonth`: حداقل شیفت عصر ماهانه
- `MaxEveningShiftsPerMonth`: حداکثر شیفت عصر ماهانه

### محدودیت‌های شیفت شب
- `MinNightShiftsPerMonth`: حداقل شیفت شب ماهانه
- `MaxNightShiftsPerMonth`: حداکثر شیفت شب ماهانه

## تنظیمات مسئول شیفت برای هر نوع شیفت

### الزام حضور مسئول
- `RequireManagerForMorningShift`: الزام حضور مسئول در شیفت صبح
- `RequireManagerForEveningShift`: الزام حضور مسئول در شیفت عصر
- `RequireManagerForNightShift`: الزام حضور مسئول در شیفت شب
- `ShiftManagerRequirementWeight`: وزن الزام حضور مسئول در شیفت‌ها

**نکته مهم**: این تنظیمات از فیلد `CanBeShiftManager` در مدل `User` استفاده می‌کند. اگر این فیلد برای کاربری `true` باشد، آن کاربر می‌تواند مسئول شیفت باشد.

### نحوه عملکرد
- اگر برای شیفتی الزام مسئول فعال باشد، الگوریتم اطمینان حاصل می‌کند که حداقل یک کاربر با `CanBeShiftManager = true` در آن شیفت حضور داشته باشد
- اگر الزام مسئول برای شیفتی فعال نباشد، همه کاربران می‌توانند در آن شیفت حضور داشته باشند
- کاربران مسئول اولویت بالاتری برای انتساب به شیفت‌های مورد نیاز دارند

## مثال‌های کاربردی

### مثال 1: دپارتمان شب‌دوست
```json
{
  "EnforceMinimumShiftsForRotatingStaff": true,
  "MinNightShiftsForThreeShiftRotation": 4,
  "EnableNightShiftPreference": true,
  "NightShiftPreferenceType": 0,
  "NightShiftPreferenceWeight": 2.5,
  "RequireExperiencedManagerForNightShift": true,
  "MinExperienceYearsForNightShiftManager": 3,
  "NightShiftManagerRequirementWeight": 3.0
}
```

### مثال 2: دپارتمان شب‌گریز
```json
{
  "EnforceMinimumShiftsForRotatingStaff": true,
  "MinNightShiftsForThreeShiftRotation": 2,
  "EnableNightShiftPreference": true,
  "NightShiftPreferenceType": 1,
  "NightShiftPreferenceWeight": 2.0,
  "MaxNightShiftsPerMonth": 6
}
```

### مثال 3: دپارتمان با اولویت تخصص
```json
{
  "EnableSpecialtyPriorityInShifts": true,
  "SpecialtyPriorityWeight": 1.5,
  "EnableGenderBalancePerShiftType": true,
  "GenderBalancePerShiftWeight": 1.0
}
```

### مثال 4: دپارتمان با الزام مسئول در همه شیفت‌ها
```json
{
  "RequireManagerForMorningShift": true,
  "RequireManagerForEveningShift": true,
  "RequireManagerForNightShift": true,
  "ShiftManagerRequirementWeight": 2.0
}
```

### مثال 5: دپارتمان با الزام مسئول فقط در شیفت شب
```json
{
  "RequireManagerForNightShift": true,
  "ShiftManagerRequirementWeight": 1.5,
  "RequireExperiencedManagerForNightShift": true,
  "MinExperienceYearsForNightShiftManager": 2,
  "NightShiftManagerRequirementWeight": 2.5
}
```

## نکات مهم

1. **اولویت تنظیمات**: تنظیمات دپارتمان بر تنظیمات سراسری اولویت دارد
2. **اعتبارسنجی**: سیستم به طور خودکار اعتبارسنجی می‌کند که حداقل شیفت بیشتر از حداکثر نباشد
3. **وزن‌ها**: تمام وزن‌ها باید نامنفی باشند
4. **سازگاری**: تنظیمات جدید با الگوریتم‌های موجود سازگار است
5. **مسئول شیفت**: فیلد `CanBeShiftManager` در مدل `User` تعیین می‌کند که کدام کاربران می‌توانند مسئول شیفت باشند
6. **الزام مسئول**: اگر برای شیفتی الزام مسئول فعال باشد، حداقل یک کاربر با `CanBeShiftManager = true` باید در آن شیفت حضور داشته باشد

## API Endpoints

### دریافت تنظیمات
```
GET /api/DepartmentSchedulingSettings
```

### ایجاد تنظیمات جدید
```
POST /api/DepartmentSchedulingSettings
```

### ویرایش تنظیمات
```
PUT /api/DepartmentSchedulingSettings/{id}
```

### حذف تنظیمات
```
DELETE /api/DepartmentSchedulingSettings/{id}
```

## پشتیبانی

برای سوالات و پشتیبانی، با تیم توسعه تماس بگیرید.
