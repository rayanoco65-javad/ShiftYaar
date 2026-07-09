# مثال عملی استفاده از تنظیمات مسئول شیفت

## سناریو: دپارتمان ICU با الزام مسئول در همه شیفت‌ها

### مرحله 1: تنظیم کاربران مسئول شیفت

ابتدا باید کاربرانی که می‌توانند مسئول شیفت باشند را مشخص کنید:

```http
PUT /api/User/1
Content-Type: application/json

{
  "FullName": "دکتر احمد محمدی",
  "CanBeShiftManager": true,
  "DepartmentId": 1,
  "SpecialtyId": 1
}
```

### مرحله 2: تنظیم تنظیمات دپارتمان

```http
POST /api/DepartmentSchedulingSettings
Content-Type: application/json

{
  "DepartmentId": 1,
  "RequireManagerForMorningShift": true,
  "RequireManagerForEveningShift": true,
  "RequireManagerForNightShift": true,
  "ShiftManagerRequirementWeight": 2.5,
  "EnforceMinimumShiftsForRotatingStaff": true,
  "MinMorningShiftsForThreeShiftRotation": 3,
  "MinEveningShiftsForThreeShiftRotation": 3,
  "MinNightShiftsForThreeShiftRotation": 2,
  "EnableNightShiftPreference": true,
  "NightShiftPreferenceType": 0,
  "NightShiftPreferenceWeight": 1.5
}
```

### مرحله 3: درخواست شیفت‌بندی

```http
POST /api/ShiftScheduling
Content-Type: application/json

{
  "DepartmentId": 1,
  "StartDate": "1403/07/01",
  "EndDate": "1403/07/31",
  "AlgorithmType": 1
}
```

## سناریو: دپارتمان اورژانس با الزام مسئول فقط در شیفت شب

### تنظیمات دپارتمان

```http
POST /api/DepartmentSchedulingSettings
Content-Type: application/json

{
  "DepartmentId": 2,
  "RequireManagerForNightShift": true,
  "ShiftManagerRequirementWeight": 3.0,
  "RequireExperiencedManagerForNightShift": true,
  "MinExperienceYearsForNightShiftManager": 3,
  "NightShiftManagerRequirementWeight": 2.0,
  "EnforceMinimumShiftsForRotatingStaff": true,
  "MinNightShiftsForThreeShiftRotation": 4,
  "EnableNightShiftPreference": true,
  "NightShiftPreferenceType": 0,
  "NightShiftPreferenceWeight": 2.0
}
```

## سناریو: دپارتمان معمولی بدون الزام مسئول

### تنظیمات دپارتمان

```http
POST /api/DepartmentSchedulingSettings
Content-Type: application/json

{
  "DepartmentId": 3,
  "RequireManagerForMorningShift": false,
  "RequireManagerForEveningShift": false,
  "RequireManagerForNightShift": false,
  "EnforceMinimumShiftsForRotatingStaff": true,
  "MinMorningShiftsForThreeShiftRotation": 4,
  "MinEveningShiftsForThreeShiftRotation": 4,
  "MinNightShiftsForThreeShiftRotation": 4,
  "EnableGenderBalancePerShiftType": true,
  "GenderBalancePerShiftWeight": 1.0
}
```

## نحوه عملکرد الگوریتم

### 1. بررسی الزام مسئول
- الگوریتم ابتدا بررسی می‌کند که آیا برای هر شیفت الزام مسئول وجود دارد یا نه
- اگر الزام مسئول وجود داشته باشد، حداقل یک کاربر با `CanBeShiftManager = true` باید در آن شیفت حضور داشته باشد

### 2. اولویت‌بندی کاربران
- کاربران مسئول اولویت بالاتری برای انتساب به شیفت‌های مورد نیاز دارند
- اگر الزام مسئول برای شیفتی فعال باشد، ابتدا مسئولان و سپس سایر کاربران انتساب می‌شوند

### 3. اعتبارسنجی نهایی
- پس از تکمیل شیفت‌بندی، سیستم بررسی می‌کند که آیا الزام مسئول در همه شیفت‌های مورد نیاز برآورده شده است یا نه
- اگر الزام مسئول نقض شده باشد، جریمه سنگینی اعمال می‌شود

## نکات مهم

1. **تنظیم کاربران مسئول**: قبل از تنظیم تنظیمات دپارتمان، باید کاربران مسئول را مشخص کنید
2. **وزن مسئول**: وزن بالاتر برای `ShiftManagerRequirementWeight` اولویت بیشتری به الزام مسئول می‌دهد
3. **تجربه مسئول**: می‌توانید حداقل سال تجربه برای مسئول شیفت شب تعیین کنید
4. **انعطاف‌پذیری**: هر دپارتمان می‌تواند تنظیمات خاص خود را داشته باشد

## خطاهای احتمالی

### خطای عدم وجود مسئول
```
{
  "IsSuccess": false,
  "Message": "برای شیفت شب در تاریخ 1403/07/15 هیچ مسئولی تعیین نشده است",
  "ErrorCode": "MANAGER_REQUIRED"
}
```

### خطای عدم وجود کاربر مسئول
```
{
  "IsSuccess": false,
  "Message": "در دپارتمان هیچ کاربری با قابلیت مسئول شیفت وجود ندارد",
  "ErrorCode": "NO_ELIGIBLE_MANAGERS"
}
```
