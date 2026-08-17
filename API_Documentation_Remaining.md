# مستندات کامل API پروژه ShiftYar

این فایل شامل مستندات کامل تمامی کنترلرها و اکشن‌های POST موجود در پروژه ASP.NET Core Web API می‌باشد.

---

## 📋 فهرست مطالب

1. [کنترلر احراز هویت (AuthController)](#1-کنترلر-احراز-هویت-authcontroller)
2. [کنترلر کاربران (UserController)](#2-کنترلر-کاربران-usercontroller)
3. [کنترلر بیمارستان (HospitalController)](#3-کنترلر-بیمارستان-hospitalcontroller)
4. [کنترلر شیفت (ShiftController)](#4-کنترلر-شیفت-shiftcontroller)
5. [کنترلر درخواست شیفت (ShiftRequestController)](#5-کنترلر-درخواست-شیفت-shiftrequestcontroller)
6. [کنترلر جابجایی شیفت (ShiftExchangeController)](#6-کنترلر-جابجایی-شیفت-shiftexchangecontroller)
7. [کنترلر زمان‌بندی شیفت (ShiftSchedulingController)](#7-کنترلر-زمان‌بندی-شیفت-shiftschedulingcontroller)
8. [کنترلر زمان‌بندی اضطراری (EmergencyReschedulingController)](#8-کنترلر-زمان‌بندی-اضطراری-emergencyreschedulingcontroller)
9. [کنترلر دپارتمان (DepartmentController)](#9-کنترلر-دپارتمان-departmentcontroller)
10. [کنترلر نام دپارتمان (DepartmentNameController)](#10-کنترلر-نام-دپارتمان-departmentnamecontroller)
11. [کنترلر تنظیمات زمان‌بندی دپارتمان (DepartmentSchedulingSettingsController)](#11-کنترلر-تنظیمات-زمان‌بندی-دپارتمان-departmentschedulingsettingscontroller)
12. [کنترلر تخصص (SpecialtyController)](#12-کنترلر-تخصص-specialtycontroller)
13. [کنترلر نام تخصص (SpecialtyNameController)](#13-کنترلر-نام-تخصص-specialtynamecontroller)
14. [کنترلر نیازمندی تخصص شیفت (ShiftRequiredSpecialtyController)](#14-کنترلر-نیازمندی-تخصص-شیفت-shiftrequiredspecialtycontroller)
15. [کنترلر نقش (RoleController)](#15-کنترلر-نقش-rolecontroller)
16. [کنترلر مجوز (PermissionController)](#16-کنترلر-مجوز-permissioncontroller)
17. [کنترلر نقش-مجوز (RolePermissionController)](#17-کنترلر-نقش-مجوز-rolepermissioncontroller)
18. [کنترلر تنظیمات الگوریتم (AlgorithmSettingsController)](#18-کنترلر-تنظیمات-الگوریتم-algorithmsettingscontroller)
19. [کنترلر تقویم (CalendarSeederController)](#19-کنترلر-تقویم-calendarseedercontroller)

---

## 1. کنترلر احراز هویت (AuthController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت احراز هویت کاربران، ورود به سیستم، ارسال کد OTP، بازیابی رمز عبور و مدیریت توکن‌های دسترسی می‌باشد.

### اکشن‌های POST

#### 1.1. LoginWithPassword
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/LoginWithPassword`
- **توضیح:** ورود به سیستم با استفاده از شماره تلفن و رمز عبور
- **ورودی‌ها:**
  - مدل: `LoginWithPasswordRequestDto`
- **خروجی:** 
  - در صورت موفقیت: توکن دسترسی (Access Token) و توکن تجدید (Refresh Token)
  - در صورت خطا: پیام خطا با کد وضعیت 401 یا 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

##### مدل ورودی: LoginWithPasswordRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| PhoneNumberMembership | string | بله | شماره تلفن عضویت کاربر | - |
| Password | string | بله | رمز عبور کاربر | - |

---

#### 1.2. SendOtp_Login
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/SendOtp_Login`
- **توضیح:** ارسال کد OTP برای ورود به سیستم
- **ورودی‌ها:**
  - مدل: `SendOtpRequestDto`
- **خروجی:** 
  - در صورت موفقیت: پیام "کد با موفقیت ارسال شد" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

##### مدل ورودی: SendOtpRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| PhoneNumberMembership | string | بله | شماره تلفن عضویت کاربر | - |

---

#### 1.3. LoginWithOtp
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/LoginWithOtp`
- **توضیح:** ورود به سیستم با استفاده از شماره تلفن و کد OTP
- **ورودی‌ها:**
  - مدل: `LoginWithOtpRequestDto`
- **خروجی:** 
  - در صورت موفقیت: توکن دسترسی و توکن تجدید
  - در صورت خطا: پیام خطا با کد وضعیت 401 یا 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

##### مدل ورودی: LoginWithOtpRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| PhoneNumberMembership | string | بله | شماره تلفن عضویت کاربر | - |
| OtpCode | string | بله | کد OTP دریافتی | - |

---

#### 1.4. SendOtp_ForgotPassword
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/SendOtp_ForgotPassword`
- **توضیح:** ارسال کد OTP برای بازیابی رمز عبور
- **ورودی‌ها:**
  - مدل: `ForgotPasswordRequestDto`
- **خروجی:** 
  - در صورت موفقیت: پیام "کد بازیابی ارسال شد" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

##### مدل ورودی: ForgotPasswordRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| PhoneNumberMembership | string | بله | شماره تلفن عضویت کاربر | - |

---

#### 1.5. ResetPassword
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/ResetPassword`
- **توضیح:** تغییر رمز عبور با استفاده از کد OTP
- **ورودی‌ها:**
  - مدل: `ResetPasswordRequestDto`
- **خروجی:** 
  - در صورت موفقیت: پیام "رمز عبور با موفقیت تغییر کرد" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 401 یا 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

##### مدل ورودی: ResetPasswordRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| PhoneNumberMembership | string | بله | شماره تلفن عضویت کاربر | - |
| OtpCode | string | بله | کد OTP دریافتی | - |
| NewPassword | string | بله | رمز عبور جدید | - |

---

#### 1.6. RefreshToken
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/RefreshToken`
- **توضیح:** تجدید توکن دسترسی با استفاده از توکن تجدید
- **ورودی‌ها:**
  - Body: `string` (Refresh Token)
- **خروجی:** 
  - در صورت موفقیت: توکن دسترسی جدید
  - در صورت خطا: پیام خطا با کد وضعیت 401 یا 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

---

#### 1.7. Logout
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/Logout`
- **توضیح:** خروج از سیستم و باطل کردن توکن تجدید
- **ورودی‌ها:**
  - Body: `string` (Refresh Token)
- **خروجی:** 
  - در صورت موفقیت: پیام "خروج با موفقیت انجام شد"
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

---

#### 1.8. LogoutAll
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/LogoutAll`
- **توضیح:** باطل کردن تمام توکن‌های کاربر از همه دستگاه‌ها
- **ورودی‌ها:**
  - Body: `string` (PhoneNumberMembership)
- **خروجی:** 
  - در صورت موفقیت: پیام "تمام توکن‌ها باطل شدند" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 404 یا 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (عمومی)

---

## 2. کنترلر کاربران (UserController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت کاربران سیستم شامل ایجاد، ویرایش، حذف و دریافت اطلاعات کاربران می‌باشد.

### اکشن‌های POST

#### 2.1. CreateUser
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateUser`
- **توضیح:** ایجاد کاربر جدید در سیستم
- **ورودی‌ها:**
  - مدل: `UserDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات کاربر ایجاد شده
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: UserDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| FullName | string | بله | نام کامل کاربر | [Required] با پیام "نام کامل الزامی است." |
| PhoneNumberMembership | string | بله | شماره تلفن عضویت | [Required] با پیام "شماره تلفن الزامی است." و [Phone] با پیام "شماره تلفن نامعتبر است." |
| NationalCode | string | بله | کد ملی | [Required] با پیام "کد ملی الزامی است." و [StringLength(10, MinimumLength = 10)] با پیام "کد ملی باید 10 رقم باشد." |
| PersonnelCode | string | خیر | کد پرسنلی | - |
| Gender | UserGender? | خیر | جنسیت کاربر | Enum: UserGender |
| DateOfEmployment | string | خیر | تاریخ استخدام (شمسی) | - |
| IsProjectPersonnel | bool? | خیر | پرسنل طرحی: فقط تا موظفی در شیفت‌بندی؛ غیرطرحی اولویت پر کردن موظفی | - |
| AllowedShiftPermissions | int? (flags) | خیر | مجوز نوع شیفت: Morning=1, Evening=2, Night=4, MorningEveningSameDay=8, MorningNightSameDay=16؛ null=مشتق از ShiftType | - |
| MaxProductivityRequiredHours | decimal? | خیر | ساعت موظفی ماهانه دستی؛ null/0 = محاسبه خودکار | 0 تا 744 |
| HardshipPercent | decimal? | خیر | ضریب سختی کار برای کاهش موظفی | - |
| OvertimeConsent | bool? | خیر | رضایت اضافه‌کار | - |
| Email | string | خیر | ایمیل | [EmailAddress] با پیام "ایمیل نامعتبر است." |
| Password | string | خیر | رمز عبور | اختیاری: برای ست کردن پسورد هنگام ایجاد کاربر |
| Province | string | خیر | استان | - |
| City | string | خیر | شهر | - |
| Address | string | خیر | آدرس | - |
| IsActive | bool? | خیر | وضعیت فعال بودن | - |
| CanBeShiftManager | bool? | خیر | آیا می‌تواند مسئول شیفت باشد | - |
| IncludedProductivityPlan | bool? | خیر | آیا در برنامه بهره‌وری قرار دارد | - |
| Image | string | خیر | تصویر کاربر | - |
| DepartmentId | int? | خیر | شناسه دپارتمان | - |
| SpecialtyId | int? | خیر | شناسه تخصص | - |
| ShiftType | ShiftTypes? | خیر | نوع شیفت کاربر | Enum: ShiftTypes |
| ShiftSubType | ShiftSubTypes? | خیر | زیرنوع شیفت | Enum: ShiftSubTypes |
| TwoShiftRotationPattern | TwoShiftRotationPattern? | خیر | الگوی چرخش دو شیفت | Enum: TwoShiftRotationPattern |
| OtherPhoneNumbers | List<string>? | خیر | شماره تلفن‌های دیگر | - |
| UserRoles | List<int>? | خیر | لیست شناسه نقش‌های کاربر | - |

---

#### 2.2. CreateUserSupervisorAndSendOtpForLogin
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateUserSupervisorAndSendOtpForLogin`
- **توضیح:** ایجاد کاربر با نقش سوپروایزر و ارسال OTP جهت لاگین
- **ورودی‌ها:**
  - مدل: `UserDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات کاربر ایجاد شده
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: UserDtoAdd
(همان مدل اکشن CreateUser - به جدول بالا مراجعه کنید)

---

## 3. کنترلر بیمارستان (HospitalController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت اطلاعات بیمارستان‌ها شامل ایجاد، ویرایش، حذف و دریافت اطلاعات بیمارستان‌ها می‌باشد.

### اکشن‌های POST

#### 3.1. CreateHospital
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateHospital`
- **توضیح:** افزودن بیمارستان جدید به سیستم
- **ورودی‌ها:**
  - مدل: `HospitalDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات بیمارستان ایجاد شده
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: HospitalDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| SiamCode | string | خیر | کد سیام بیمارستان | - |
| Name | string | خیر | نام بیمارستان | - |
| Province | string | خیر | استان | - |
| City | string | خیر | شهر | - |
| Address | string | خیر | آدرس | - |
| Email | string | خیر | ایمیل | - |
| Website | string | خیر | وب‌سایت | - |
| Description | string | خیر | توضیحات | - |
| IsActive | bool? | خیر | وضعیت فعال بودن | - |
| Logo | string | خیر | لوگو | - |
| PhoneNumbers | List<string>? | خیر | لیست شماره تلفن‌ها | - |

---

## 4. کنترلر شیفت (ShiftController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت شیفت‌ها شامل ایجاد، ویرایش، حذف و دریافت اطلاعات شیفت‌ها می‌باشد.

### اکشن‌های POST

#### 4.1. CreateShift
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateShift`
- **توضیح:** ایجاد شیفت جدید
- **ورودی‌ها:**
  - مدل: `ShiftDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات شیفت ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: ShiftDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| DepartmentId | int | بله | شناسه بخش | [Required] با پیام "شناسه بخش الزامی است" |
| Label | ShiftLabel | بله | نوع شیفت | [Required] با پیام "نوع شیفت الزامی است" - Enum: ShiftLabel |
| StartTime | TimeSpan | بله | زمان شروع | [Required] با پیام "زمان شروع الزامی است" |
| EndTime | TimeSpan | بله | زمان پایان | [Required] با پیام "زمان پایان الزامی است" |

---

## 5. کنترلر درخواست شیفت (ShiftRequestController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت درخواست‌های شیفت کاربران شامل ایجاد درخواست شیفت، درخواست مرخصی و مدیریت وضعیت درخواست‌ها می‌باشد.

### اکشن‌های POST

#### 5.1. CreateShiftRequest
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateShiftRequest`
- **توضیح:** ثبت درخواست شیفت جدید
- **ورودی‌ها:**
  - مدل: `ShiftRequestDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات درخواست ایجاد شده
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: ShiftRequestDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| UserId | int? | خیر | شناسه کاربر درخواست دهنده | - |
| RequestPersianDate | string | خیر | تاریخ شمسی مورد درخواست کاربر | - |
| RequestType | RequestType? | خیر | نوع درخواست | Enum: RequestType |
| ShiftLabel | ShiftLabel? | خیر | برچسب شیفت | Enum: ShiftLabel |
| RequestAction | RequestAction? | خیر | عمل درخواست | Enum: RequestAction |
| Reason | string | خیر | دلیل درخواست | - |

---

#### 5.2. CreateShiftRequestForLeave
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateShiftRequestForLeave`
- **توضیح:** ثبت درخواست مرخصی
- **ورودی‌ها:**
  - مدل: `ShiftRequestForLeaveDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات درخواست مرخصی ایجاد شده
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: ShiftRequestForLeaveDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| UserId | int? | خیر | شناسه کاربر درخواست دهنده | - |
| StartPersianDate | string | خیر | تاریخ شمسی شروع مرخصی | - |
| EndPersianDate | string | خیر | تاریخ شمسی پایان مرخصی | - |
| Reason | string | خیر | دلیل مرخصی | - |

---

## 6. کنترلر جابجایی شیفت (ShiftExchangeController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت درخواست‌های جابجایی شیفت بین کاربران شامل ایجاد درخواست، تأیید/رد توسط سوپروایزر و اجرای جابجایی می‌باشد.

### اکشن‌های POST

#### 6.1. Create
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftExchange/Create`
- **توضیح:** ایجاد درخواست جابجایی شیفت جدید
- **ورودی‌ها:**
  - مدل: `ShiftExchangeDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات درخواست جابجایی ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: ShiftExchangeDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| RequestingUserId | int | بله | شناسه کاربر درخواست کننده | [Required] با پیام "شناسه کاربر درخواست کننده الزامی است" |
| OfferingUserId | int | بله | شناسه کاربر پیشنهاد دهنده | [Required] با پیام "شناسه کاربر پیشنهاد دهنده الزامی است" |
| RequestingShiftAssignmentId | int | بله | شناسه شیفت درخواست کننده | [Required] با پیام "شناسه شیفت درخواست کننده الزامی است" |
| OfferingShiftAssignmentId | int | بله | شناسه شیفت پیشنهاد دهنده | [Required] با پیام "شناسه شیفت پیشنهاد دهنده الزامی است" |
| Reason | string | بله | دلیل درخواست | [Required] با پیام "دلیل درخواست الزامی است" و [StringLength(500)] با پیام "دلیل درخواست نمی‌تواند بیش از 500 کاراکتر باشد" |

---

#### 6.2. Approve
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftExchange/approve`
- **توضیح:** تأیید یا رد درخواست جابجایی شیفت توسط سوپروایزر
- **ورودی‌ها:**
  - مدل: `ShiftExchangeApprovalDto`
- **خروجی:** 
  - در صورت موفقیت: پیام تأیید یا رد با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 404
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: ShiftExchangeApprovalDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| Id | int | بله | شناسه درخواست | [Required] با پیام "شناسه درخواست الزامی است" |
| IsApproved | bool | بله | وضعیت تأیید | [Required] با پیام "وضعیت تأیید الزامی است" |
| SupervisorComment | string | خیر | نظر سوپروایزر | [StringLength(500)] با پیام "نظر سوپروایزر نمی‌تواند بیش از 500 کاراکتر باشد" |

---

#### 6.3. ExecuteExchange
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftExchange/execute/{exchangeId}`
- **توضیح:** اجرای جابجایی شیفت (پس از تأیید)
- **ورودی‌ها:**
  - Route Parameter: `exchangeId` (int)
- **خروجی:** 
  - در صورت موفقیت: پیام "جابجایی شیفت با موفقیت اجرا شد" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 404
- **QueryString:** ندارد
- **Route Parameter:** 
  - `exchangeId` (int): شناسه درخواست جابجایی
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

---

#### 6.4. Cancel
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftExchange/cancel/{exchangeId}`
- **توضیح:** لغو درخواست جابجایی شیفت
- **ورودی‌ها:**
  - Route Parameter: `exchangeId` (int)
- **خروجی:** 
  - در صورت موفقیت: پیام "درخواست جابجایی شیفت با موفقیت لغو شد" با کد وضعیت 200
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 404
- **QueryString:** ندارد
- **Route Parameter:** 
  - `exchangeId` (int): شناسه درخواست جابجایی
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

---

## 7. کنترلر زمان‌بندی شیفت (ShiftSchedulingController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت بهینه‌سازی شیفت‌بندی با استفاده از الگوریتم‌های مختلف (Simulated Annealing, OR-Tools, Hybrid) می‌باشد.

### اکشن‌های POST

#### 7.1. OptimizeShiftSchedule
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftScheduling/optimize`
- **توضیح:** اجرای الگوریتم بهینه‌سازی شیفت‌بندی
- **ورودی‌ها:**
  - مدل: `ShiftSchedulingRequestDto`
- **خروجی:** 
  - در صورت موفقیت: نتیجه بهینه‌سازی شامل لیست انتساب‌ها و آمارها
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 500
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: ShiftSchedulingRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| DepartmentId | int | بله | شناسه دپارتمان | [Range(1, int.MaxValue)] با پیام "شناسه دپارتمان باید مقدار مثبت داشته باشد." |
| StartDate | string | بله | تاریخ شروع بازه (شمسی) | [Required] با پیام "تاریخ شروع الزامی است." و [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")] با پیام "تاریخ شروع باید در قالب yyyy/MM/dd باشد." |
| EndDate | string | بله | تاریخ پایان بازه (شمسی) | [Required] با پیام "تاریخ پایان الزامی است." و [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")] با پیام "تاریخ پایان باید در قالب yyyy/MM/dd باشد." |
| Algorithm | SchedulingAlgorithm | خیر | الگوریتم انتخابی | Enum: SchedulingAlgorithm (پیش‌فرض: SimulatedAnnealing) |

**اعتبارسنجی‌های سفارشی:**
- تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد
- بازه تاریخ باید حداقل یک روز باشد
- طول بازه زمان‌بندی نمی‌تواند بیش از 62 روز باشد

---

#### 7.2. GetAlgorithmStatistics
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftScheduling/statistics`
- **توضیح:** دریافت آمارهای الگوریتم
- **ورودی‌ها:**
  - مدل: `ShiftSchedulingRequestDto`
- **خروجی:** 
  - در صورت موفقیت: آمارهای الگوریتم
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 500
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: ShiftSchedulingRequestDto
(همان مدل اکشن OptimizeShiftSchedule - به جدول بالا مراجعه کنید)

---

#### 7.3. ValidateConstraints
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftScheduling/validate`
- **توضیح:** اعتبارسنجی محدودیت‌های شیفت‌بندی
- **ورودی‌ها:**
  - مدل: `ShiftSchedulingRequestDto`
- **خروجی:** 
  - در صورت موفقیت: لیست خطاهای اعتبارسنجی (خالی در صورت عدم خطا)
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 500
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: ShiftSchedulingRequestDto
(همان مدل اکشن OptimizeShiftSchedule - به جدول بالا مراجعه کنید)

---

#### 7.4. SaveOptimizedSchedule
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftScheduling/save`
- **توضیح:** ذخیره نتیجه بهینه‌سازی در دیتابیس
- **ورودی‌ها:**
  - مدل: `ShiftSchedulingResultDto`
- **خروجی:** 
  - در صورت موفقیت: نتیجه ذخیره
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 500
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: ShiftSchedulingResultDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| Assignments | List<ShiftAssignmentDto> | بله | لیست انتساب‌ها | - |
| FinalScore | double | بله | امتیاز نهایی (هرچه کمتر بهتر) | - |
| TotalIterations | int | بله | تعداد تکرارهای الگوریتم | - |
| ExecutionTime | TimeSpan | بله | زمان اجرای الگوریتم | - |
| Violations | List<string> | بله | نقض‌های رخ‌داده | - |
| Statistics | ShiftSchedulingStatisticsDto | بله | آمار جانبی | - |
| AlgorithmUsed | SchedulingAlgorithm | بله | الگوریتم استفاده‌شده | Enum: SchedulingAlgorithm |
| AlgorithmStatus | string | بله | وضعیت الگوریتم | - |
| HybridResult | HybridResultDto? | خیر | جزئیات Hybrid در صورت استفاده | - |

---

#### 7.5. OptimizeAndSave
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/ShiftScheduling/optimize-and-save`
- **توضیح:** اجرای کامل فرآیند بهینه‌سازی و ذخیره
- **ورودی‌ها:**
  - مدل: `ShiftSchedulingRequestDto`
- **خروجی:** 
  - در صورت موفقیت: نتیجه کامل شامل بهینه‌سازی و ذخیره
  - در صورت خطا: پیام خطا با کد وضعیت 400 یا 500
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: ShiftSchedulingRequestDto
(همان مدل اکشن OptimizeShiftSchedule - به جدول بالا مراجعه کنید)

---

## 8. کنترلر زمان‌بندی اضطراری (EmergencyReschedulingController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت زمان‌بندی اضطراری شیفت‌ها با استفاده از الگوریتم Rolling Horizon می‌باشد.

### اکشن‌های POST

#### 8.1. RescheduleAsync
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/api/EmergencyRescheduling/rolling-horizon`
- **توضیح:** اجرای زمان‌بندی اضطراری با الگوریتم Rolling Horizon
- **ورودی‌ها:**
  - مدل: `EmergencyReschedulingRequestDto`
- **خروجی:** 
  - در صورت موفقیت: نتیجه زمان‌بندی اضطراری
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize]` و `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: EmergencyReschedulingRequestDto

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| DepartmentId | int | بله | شناسه دپارتمان | [Range(1, int.MaxValue)] با پیام "شناسه دپارتمان معتبر نیست." |
| StartDate | string | بله | تاریخ شروع (شمسی) | [Required] و [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")] |
| EndDate | string | بله | تاریخ پایان (شمسی) | [Required] و [RegularExpression(@"^\d{4}/\d{2}/\d{2}$")] |
| WindowSizeDays | int | خیر | طول پنجره (روز) | [Range(1, 21)] با پیام "طول پنجره باید بین 1 تا 21 روز باشد." (پیش‌فرض: 7) |
| OverlapDays | int | خیر | همپوشانی (روز) | [Range(0, 14)] با پیام "همپوشانی باید بین 0 تا 14 روز باشد." (پیش‌فرض: 1) |
| ImpactedUserIds | List<int> | خیر | لیست شناسه کاربران تأثیرپذیر | - |
| Algorithm | SchedulingAlgorithm | خیر | الگوریتم انتخابی | Enum: SchedulingAlgorithm (پیش‌فرض: SimulatedAnnealing) |

**اعتبارسنجی‌های سفارشی:**
- تاریخ پایان باید بعد از تاریخ شروع باشد
- همپوشانی باید کوچکتر از طول پنجره باشد
- شناسه کاربران تکراری مجاز نیست

---

## 9. کنترلر دپارتمان (DepartmentController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت دپارتمان‌ها شامل ایجاد، ویرایش، حذف و دریافت اطلاعات دپارتمان‌ها می‌باشد.

### اکشن‌های POST

#### 9.1. CreateDepartment
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateDepartment`
- **توضیح:** ایجاد دپارتمان جدید
- **ورودی‌ها:**
  - مدل: `DepartmentDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات دپارتمان ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: DepartmentDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| Name | string | بله | نام دپارتمان | [Required] با پیام "نام دپارتمان الزامی است." |
| Description | string | خیر | توضیحات | - |
| IsActive | bool? | خیر | وضعیت فعال بودن | - |
| HospitalId | int? | بله | شناسه بیمارستان | [Required] با پیام "شناسه بیمارستان الزامی است." |
| SupervisorId | int? | بله | شناسه مسئول بخش | [Required] با پیام "شناسه مسئول بخش الزامی است." |
| IsNightLover | bool? | خیر | آیا این بخش شب دوست است یا شب گریز | برای تقسیم شیفت‌های شب |

---

## 10. کنترلر نام دپارتمان (DepartmentNameController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت نام‌های دپارتمان شامل ایجاد، ویرایش، حذف و دریافت اطلاعات نام‌های دپارتمان می‌باشد.

### اکشن‌های POST

#### 10.1. CreateDepartmentName
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateDepartmentName`
- **توضیح:** ایجاد نام دپارتمان جدید
- **ورودی‌ها:**
  - مدل: `DepartmentNameDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات نام دپارتمان ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: DepartmentNameDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| Name | string | بله | نام دپارتمان | [Required] با پیام "نام دپارتمان الزامی است." |

---

## 11. کنترلر تنظیمات زمان‌بندی دپارتمان (DepartmentSchedulingSettingsController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت تنظیمات زمان‌بندی دپارتمان‌ها شامل قوانین، محدودیت‌ها و وزن‌های الگوریتم بهینه‌سازی می‌باشد.

### اکشن‌های POST

#### 11.1. CreateSetting
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateSetting`
- **توضیح:** ایجاد تنظیمات جدید زمان‌بندی دپارتمان
- **ورودی‌ها:**
  - مدل: `DepartmentSchedulingSettingsDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات تنظیمات ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز دارد - `[Authorize(Roles = "Admin,Supervisor")]`

##### مدل ورودی: DepartmentSchedulingSettingsDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| DepartmentId | int | بله | شناسه دپارتمان | [Required] |
| ForbidDuplicateDailyAssignments | bool? | خیر | ممنوعیت انتساب تکراری روزانه | - |
| EnforceMaxShiftsPerDay | bool? | خیر | اعمال حداکثر شیفت در روز | - |
| EnforceMinRestDays | bool? | خیر | اعمال حداقل روز استراحت | - |
| EnforceMaxConsecutiveShifts | bool? | خیر | اعمال حداکثر شیفت متوالی | - |
| EnforceWeeklyMaxShifts | bool? | خیر | اعمال حداکثر شیفت هفتگی | - |
| EnforceNightShiftMonthlyCap | bool? | خیر | اعمال سقف شیفت شب ماهانه | - |
| EnforceSpecialtyCapacity | bool? | خیر | اعمال ظرفیت تخصص | - |
| MinRestDaysBetweenShifts | int? | خیر | حداقل روز استراحت بین شیفت‌ها | - |
| MaxConsecutiveShifts | int? | خیر | حداکثر شیفت متوالی | - |
| MaxShiftsPerWeek | int? | خیر | حداکثر شیفت در هفته | - |
| MaxNightShiftsPerMonth | int? | خیر | حداکثر شیفت شب در ماه | - |
| MaxShiftsPerDay | int? | خیر | حداکثر شیفت در روز | - |
| MaxConsecutiveNightShifts | int? | خیر | حداکثر شیفت شب متوالی | - |
| GenderBalanceWeight | double? | خیر | وزن تعادل جنسیت | - |
| SpecialtyPreferenceWeight | double? | خیر | وزن ترجیح تخصص | - |
| UserUnwantedShiftWeight | double? | خیر | وزن شیفت ناخواسته کاربر | - |
| UserPreferredShiftWeight | double? | خیر | وزن شیفت ترجیحی کاربر | - |
| WeeklyMaxWeight | double? | خیر | وزن حداکثر هفتگی | - |
| MonthlyNightCapWeight | double? | خیر | وزن سقف شب ماهانه | - |
| FairShiftCountBalanceWeight | double? | خیر | وزن تعادل عادلانه تعداد شیفت | - |
| ExtraShiftRotationWeight | double? | خیر | وزن چرخش شیفت اضافی | - |
| ShiftLabelBalanceWeight | double? | خیر | وزن تعادل برچسب شیفت | - |
| FairnessLookbackMonths | int? | خیر | ماه‌های بازگشت برای عدالت | - |
| EnforceMinimumShiftsForRotatingStaff | bool? | خیر | اعمال حداقل شیفت برای پرسنل چرخشی | - |
| MinMorningShiftsForThreeShiftRotation | int? | خیر | حداقل شیفت صبح برای چرخش سه شیفت | - |
| MinEveningShiftsForThreeShiftRotation | int? | خیر | حداقل شیفت عصر برای چرخش سه شیفت | - |
| MinNightShiftsForThreeShiftRotation | int? | خیر | حداقل شیفت شب برای چرخش سه شیفت | - |
| MinFirstShiftForTwoShiftRotation | int? | خیر | حداقل شیفت اول برای چرخش دو شیفت | - |
| MinSecondShiftForTwoShiftRotation | int? | خیر | حداقل شیفت دوم برای چرخش دو شیفت | - |
| EnableNightShiftPreference | bool? | خیر | فعال‌سازی ترجیح شیفت شب | - |
| NightShiftPreferenceType | int? | خیر | نوع ترجیح شیفت شب | 0=شب‌دوست، 1=شب‌گریز، 2=خنثی |
| NightShiftPreferenceWeight | double? | خیر | وزن ترجیح شیفت شب | - |
| RequireManagerForEveningShift | bool? | خیر | الزام مسئول برای شیفت عصر | - |
| RequireManagerForNightShift | bool? | خیر | الزام مسئول برای شیفت شب | - |
| ShiftManagerRequirementWeight | double? | خیر | وزن الزام مسئول شیفت | - |
| EnableNightShiftDistributionBySeniority | bool? | خیر | فعال‌سازی توزیع شیفت شب بر اساس سابقه | - |
| NightShiftDistributionType | int? | خیر | نوع توزیع شیفت شب | 0=شب‌دوست، 1=شب‌گریز، 2=خنثی |
| NightShiftDistributionWeight | double? | خیر | وزن توزیع شیفت شب | - |
| SeniorityDistributionSlope | double? | خیر | شیب توزیع بر اساس سابقه | - |

---

## 12. کنترلر تخصص (SpecialtyController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت تخصص‌های دپارتمان شامل ایجاد، ویرایش، حذف و دریافت اطلاعات تخصص‌ها می‌باشد.

### اکشن‌های POST

#### 12.1. CreateSpecialty
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateSpecialty`
- **توضیح:** ایجاد تخصص جدید
- **ورودی‌ها:**
  - مدل: `SpecialtyDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات تخصص ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: SpecialtyDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| DepartmentId | int? | بله | شناسه دپارتمان | [Required] با پیام "تعیین دپارتمان الزامی است." |
| SpecialtyName | string | خیر | نام تخصص | مثل: هوشبری، اتاق عمل، پرستاری |

---

## 13. کنترلر نام تخصص (SpecialtyNameController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت نام‌های تخصص شامل ایجاد، ویرایش، حذف و دریافت اطلاعات نام‌های تخصص می‌باشد.

### اکشن‌های POST

#### 13.1. CreateSpecialtyName
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateSpecialtyName`
- **توضیح:** ایجاد نام تخصص جدید
- **ورودی‌ها:**
  - مدل: `SpecialtyNameDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات نام تخصص ایجاد شده با کد وضعیت 201
  - در صورت خطا: پیام خطا با کد وضعیت 400
- **QueryString:** ندارد
- **Route Parameter:** ندارد
- **Authentication/Authorization:** نیاز ندارد (بر اساس کد کنترلر)

##### مدل ورودی: SpecialtyNameDtoAdd

| فیلد | نوع داده | ضروری | توضیح | محدودیت/اعتبارسنجی |
|------|---------|-------|-------|---------------------|
| Name | string | بله | نام تخصص | [Required] با پیام "نام تخصص الزامی است." |

---

## 14. کنترلر نیازمندی تخصص شیفت (ShiftRequiredSpecialtyController)

### توضیح نقش کنترلر
این کنترلر مسئول مدیریت نیازمندی‌های تخصص برای هر شیفت شامل تعداد مورد نیاز نیروها بر اساس تخصص و جنسیت می‌باشد.

### اکشن‌های POST

#### 14.1. CreateShiftRequiredSpecialty
- **نوع اکشن:** POST
- **آدرس Endpoint:** `/CreateShiftRequiredSpecialty`
- **توضیح:** ایجاد نیازمندی تخصص برای شیفت
- **ورودی‌ها:**
  - مدل: `ShiftRequiredSpecialtyDtoAdd`
- **خروجی:** 
  - در صورت موفقیت: اطلاعات نیازمندی ایجاد شده با کد وضعیت 20

# مستندات API - اکشن‌های POST باقی‌مانده

این فایل شامل مستندات کامل اکشن‌های POST باقی‌مانده از پروژه ShiftYar است.

---

## 15. کنترلر نقش (RoleController)

### توضیح کنترلر
کنترلر `RoleController` مسئول مدیریت نقش‌های کاربری در سیستم است. نقش‌ها برای تعیین سطح دسترسی کاربران استفاده می‌شوند و می‌توانند به کاربران مختلف اختصاص داده شوند.

**Route Base:** `[action]` (از BaseController)

---

### اکشن: CreateRole

#### اطلاعات کلی
- **نام اکشن:** `CreateRole`
- **نوع اکشن:** `POST`
- **آدرس Endpoint:** `POST /CreateRole`
- **توضیح کاربرد:** این اکشن برای ایجاد یک نقش جدید در سیستم استفاده می‌شود. نقش‌ها برای مدیریت دسترسی‌ها و مجوزهای کاربران به کار می‌روند.

#### ورودی‌ها و خروجی‌ها
- **ورودی:** `RoleDtoAdd` (در Body)
- **خروجی:** `ApiResponse<RoleDtoGet>`
- **Route Parameters:** ندارد
- **Query Parameters:** ندارد

#### مدل ورودی: RoleDtoAdd

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `Name` | `string?` | **ضروری** | نام نقش که باید منحصر به فرد باشد | دارای `[Required]` با پیام خطا: "نام نقش الزامی است." |
| `Description` | `string?` | اختیاری | توضیحات مربوط به نقش | بدون محدودیت |
| `IsActive` | `bool?` | اختیاری | وضعیت فعال/غیرفعال بودن نقش | مقدار پیش‌فرض: `true` |

#### اعتبارسنجی‌ها
- فیلد `Name` الزامی است و نمی‌تواند خالی باشد
- در صورت عدم اعتبارسنجی، پاسخ `BadRequest` با پیام خطا برگردانده می‌شود

#### Authentication/Authorization
- **Authentication:** ندارد (عمومی)
- **Authorization:** ندارد

#### مثال درخواست
```json
POST /CreateRole
Content-Type: application/json

{
  "name": "Supervisor",
  "description": "نقش سوپروایزر برای مدیریت بخش",
  "isActive": true
}
```

#### مثال پاسخ موفق (201 Created)
```json
{
  "isSuccess": true,
  "data": {
    "id": 1,
    "name": "Supervisor",
    "description": "نقش سوپروایزر برای مدیریت بخش",
    "isActive": true
  },
  "message": null
}
```

#### مثال پاسخ خطا (400 BadRequest)
```json
{
  "isSuccess": false,
  "data": null,
  "message": "عملیات افزودن نقش با خطا مواجه شد : [جزئیات خطا]"
}
```

---

## 16. کنترلر مجوز (PermissionController)

### توضیح کنترلر
کنترلر `PermissionController` مسئول مدیریت مجوزهای دسترسی در سیستم است. مجوزها عملیات خاصی هستند که به نقش‌ها اختصاص داده می‌شوند و تعیین می‌کنند که کاربران چه کارهایی می‌توانند انجام دهند.

**Route Base:** `[action]` (از BaseController)

---

### اکشن: CreatePermission

#### اطلاعات کلی
- **نام اکشن:** `CreatePermission`
- **نوع اکشن:** `POST`
- **آدرس Endpoint:** `POST /CreatePermission`
- **توضیح کاربرد:** این اکشن برای ایجاد یک مجوز دسترسی جدید در سیستم استفاده می‌شود. مجوزها به نقش‌ها اختصاص داده می‌شوند و سطح دسترسی کاربران را تعیین می‌کنند.

#### ورودی‌ها و خروجی‌ها
- **ورودی:** `PermissionDtoAdd` (در Body)
- **خروجی:** `ApiResponse<PermissionDtoGet>`
- **Route Parameters:** ندارد
- **Query Parameters:** ندارد

#### مدل ورودی: PermissionDtoAdd

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `Name` | `string?` | **ضروری** | نام مجوز دسترسی که باید منحصر به فرد باشد | دارای `[Required]` با پیام خطا: "نام مجوز دسترسی الزامی است." |
| `Description` | `string?` | اختیاری | توضیحات مربوط به مجوز | بدون محدودیت |
| `IsActive` | `bool?` | اختیاری | وضعیت فعال/غیرفعال بودن مجوز | مقدار پیش‌فرض: `true` |

#### اعتبارسنجی‌ها
- فیلد `Name` الزامی است و نمی‌تواند خالی باشد
- در صورت عدم اعتبارسنجی، پاسخ `BadRequest` برگردانده می‌شود

#### Authentication/Authorization
- **Authentication:** ندارد (عمومی)
- **Authorization:** ندارد

#### مثال درخواست
```json
POST /CreatePermission
Content-Type: application/json

{
  "name": "ManageUsers",
  "description": "مجوز مدیریت کاربران",
  "isActive": true
}
```

#### مثال پاسخ موفق (201 Created)
```json
{
  "isSuccess": true,
  "data": {
    "id": 1,
    "name": "ManageUsers",
    "description": "مجوز مدیریت کاربران",
    "isActive": true
  },
  "message": null
}
```

#### مثال پاسخ خطا (400 BadRequest)
```json
{
  "isSuccess": false,
  "data": null,
  "message": null
}
```

---

## 17. کنترلر نقش-مجوز (RolePermissionController)

### توضیح کنترلر
کنترلر `RolePermissionController` مسئول مدیریت ارتباط بین نقش‌ها و مجوزها است. این کنترلر امکان اختصاص مجوزها به نقش‌ها و مدیریت این ارتباطات را فراهم می‌کند.

**Route Base:** `[action]` (از BaseController)

---

### اکشن: CreateRolePermission

#### اطلاعات کلی
- **نام اکشن:** `CreateRolePermission`
- **نوع اکشن:** `POST`
- **آدرس Endpoint:** `POST /CreateRolePermission`
- **توضیح کاربرد:** این اکشن برای اختصاص یک مجوز به یک نقش استفاده می‌شود. با استفاده از این اکشن می‌توانید مجوزهای مختلف را به نقش‌ها اضافه کنید.

#### ورودی‌ها و خروجی‌ها
- **ورودی:** `RolePermissionDtoAdd` (در Body)
- **خروجی:** `ApiResponse<RolePermissionDtoGet>`
- **Route Parameters:** ندارد
- **Query Parameters:** ندارد

#### مدل ورودی: RolePermissionDtoAdd

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `RoleId` | `int?` | اختیاری | شناسه نقش که می‌خواهید مجوز را به آن اختصاص دهید | باید یک شناسه معتبر نقش در سیستم باشد |
| `PermissionId` | `int?` | اختیاری | شناسه مجوزی که می‌خواهید به نقش اختصاص دهید | باید یک شناسه معتبر مجوز در سیستم باشد |

**نکته:** اگرچه فیلدها به صورت `nullable` تعریف شده‌اند، اما برای ایجاد ارتباط معتبر، هر دو فیلد باید مقدار داشته باشند.

#### اعتبارسنجی‌ها
- در صورت عدم وجود نقش یا مجوز با شناسه‌های ارسالی، خطا برگردانده می‌شود
- در صورت تکراری بودن ارتباط (نقش قبلاً این مجوز را داشته باشد)، خطا برگردانده می‌شود

#### Authentication/Authorization
- **Authentication:** ندارد (عمومی)
- **Authorization:** ندارد

#### مثال درخواست
```json
POST /CreateRolePermission
Content-Type: application/json

{
  "roleId": 1,
  "permissionId": 2
}
```

#### مثال پاسخ موفق (201 Created)
```json
{
  "isSuccess": true,
  "data": {
    "id": 1,
    "roleId": 1,
    "permissionId": 2,
    "roleName": "Supervisor",
    "permissionName": "ManageUsers"
  },
  "message": null
}
```

#### مثال پاسخ خطا (400 BadRequest)
```json
{
  "isSuccess": false,
  "data": null,
  "message": null
}
```

---

## 18. کنترلر تنظیمات الگوریتم (AlgorithmSettingsController)

### توضیح کنترلر
کنترلر `AlgorithmSettingsController` مسئول مدیریت تنظیمات الگوریتم‌های بهینه‌سازی شیفت‌بندی است. این کنترلر امکان تنظیم پارامترهای الگوریتم‌های Simulated Annealing، OR-Tools و Hybrid را فراهم می‌کند.

**Route Base:** `api/[controller]` → `api/AlgorithmSettings`

**Authentication/Authorization:** 
- نیازمند `[Authorize(Roles = "Admin")]` - فقط کاربران با نقش Admin می‌توانند از این کنترلر استفاده کنند

---

### اکشن: CreateSetting

#### اطلاعات کلی
- **نام اکشن:** `CreateSetting`
- **نوع اکشن:** `POST`
- **آدرس Endpoint:** `POST /api/AlgorithmSettings`
- **توضیح کاربرد:** این اکشن برای ایجاد تنظیمات جدید الگوریتم استفاده می‌شود. می‌توانید تنظیمات سراسری (برای همه دپارتمان‌ها) یا تنظیمات اختصاصی برای یک دپارتمان خاص ایجاد کنید.

#### ورودی‌ها و خروجی‌ها
- **ورودی:** `AlgorithmSettingsDtoAdd` (در Body)
- **خروجی:** `ApiResponse<AlgorithmSettingsDtoGet>`
- **Route Parameters:** ندارد
- **Query Parameters:** ندارد

#### مدل ورودی: AlgorithmSettingsDtoAdd

##### فیلدهای عمومی

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `DepartmentId` | `int?` | اختیاری | شناسه دپارتمان. اگر `null` باشد، تنظیمات سراسری است | اگر مقدار داشته باشد، باید شناسه معتبر دپارتمان باشد |
| `AlgorithmType` | `int` | **ضروری** | نوع الگوریتم: 1=Simulated Annealing, 2=OR-Tools, 3=Hybrid | دارای `[Required]` و `[Range(1, 3)]` با پیام: "نوع الگوریتم باید بین 1 تا 3 باشد" |

##### پارامترهای Simulated Annealing (SA)

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `SA_InitialTemperature` | `double?` | اختیاری | دمای اولیه الگوریتم | `[Range(0.1, double.MaxValue)]` - باید مثبت باشد |
| `SA_FinalTemperature` | `double?` | اختیاری | دمای نهایی الگوریتم | `[Range(0.001, double.MaxValue)]` - باید مثبت باشد |
| `SA_CoolingRate` | `double?` | اختیاری | نرخ کاهش دما | `[Range(0.1, 0.99)]` - باید بین 0.1 تا 0.99 باشد |
| `SA_MaxIterations` | `int?` | اختیاری | حداکثر تعداد تکرار | `[Range(100, int.MaxValue)]` - حداقل 100 |
| `SA_MaxIterationsWithoutImprovement` | `int?` | اختیاری | حداکثر تکرار بدون بهبود | `[Range(10, int.MaxValue)]` - حداقل 10 |

##### پارامترهای OR-Tools

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `ORT_MaxTimeInSeconds` | `int?` | اختیاری | حداکثر زمان حل (ثانیه) | `[Range(1, 3600)]` - بین 1 تا 3600 ثانیه |
| `ORT_NumSearchWorkers` | `int?` | اختیاری | تعداد تردهای جست‌وجو | `[Range(1, 16)]` - بین 1 تا 16 |
| `ORT_LogSearchProgress` | `bool?` | اختیاری | ثبت لاگ پیشرفت جست‌وجو | بدون محدودیت |
| `ORT_MaxSolutions` | `int?` | اختیاری | حداکثر تعداد راه‌حل | `[Range(1, 100)]` - بین 1 تا 100 |
| `ORT_RelativeGapLimit` | `double?` | اختیاری | حد گپ نسبی | `[Range(0.001, 1.0)]` - بین 0.001 تا 1.0 |

##### پارامترهای Hybrid

| فیلد | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|------|---------|---------------|-------|-----------|
| `HYB_Strategy` | `int?` | اختیاری | استراتژی Hybrid: 1=OrToolsFirst, 2=SimulatedAnnealingFirst, 3=Parallel, 4=Iterative, 5=Adaptive | `[Range(1, 5)]` - بین 1 تا 5 |
| `HYB_MaxIterations` | `int?` | اختیاری | حداکثر تکرار Hybrid | `[Range(1, int.MaxValue)]` - باید مثبت باشد |
| `HYB_ComplexityThreshold` | `double?` | اختیاری | آستانه پیچیدگی | `[Range(1.0, double.MaxValue)]` - باید مثبت باشد |

#### اعتبارسنجی‌ها
- فیلد `AlgorithmType` الزامی است و باید بین 1 تا 3 باشد
- تمام محدودیت‌های `Range` برای فیلدهای عددی اعمال می‌شوند
- در صورت عدم اعتبارسنجی، پاسخ `BadRequest` برگردانده می‌شود

#### Authentication/Authorization
- **Authentication:** بله - نیازمند JWT Token
- **Authorization:** بله - فقط نقش `Admin` می‌تواند از این اکشن استفاده کند
- **Attribute:** `[Authorize(Roles = "Admin")]`

#### مثال درخواست
```json
POST /api/AlgorithmSettings
Authorization: Bearer {token}
Content-Type: application/json

{
  "departmentId": null,
  "algorithmType": 1,
  "SA_InitialTemperature": 1000.0,
  "SA_FinalTemperature": 0.01,
  "SA_CoolingRate": 0.95,
  "SA_MaxIterations": 10000,
  "SA_MaxIterationsWithoutImprovement": 1000
}
```

#### مثال پاسخ موفق (201 Created)
```json
{
  "isSuccess": true,
  "data": {
    "id": 1,
    "departmentId": null,
    "departmentName": null,
    "algorithmType": 1,
    "algorithmTypeName": "Simulated Annealing",
    "SA_InitialTemperature": 1000.0,
    "SA_FinalTemperature": 0.01,
    "SA_CoolingRate": 0.95,
    "SA_MaxIterations": 10000,
    "SA_MaxIterationsWithoutImprovement": 1000,
    "createDate": "2024-01-15T10:30:00Z",
    "updateDate": null
  },
  "message": null
}
```

#### مثال پاسخ خطا (400 BadRequest)
```json
{
  "isSuccess": false,
  "data": null,
  "message": null
}
```

#### مثال پاسخ خطای دسترسی (401 Unauthorized)
```json
{
  "status": 401,
  "message": "Unauthorized"
}
```

---

## 19. کنترلر تقویم (CalendarSeederController)

### توضیح کنترلر
کنترلر `CalendarSeederController` مسئول مدیریت تقویم شمسی و میلادی در سیستم است. این کنترلر امکان ایجاد تقویم برای سال‌های مختلف، تعیین روزهای تعطیل و مدیریت تاریخ‌ها را فراهم می‌کند.

**Route Base:** `[action]` (از BaseController)

---

### اکشن: SeedYear

#### اطلاعات کلی
- **نام اکشن:** `SeedYear`
- **نوع اکشن:** `POST`
- **آدرس Endpoint:** `POST /SeedYear?year=1404`
- **توضیح کاربرد:** این اکشن برای ایجاد تقویم کامل یک سال شمسی استفاده می‌شود. تمام روزهای سال با نگاشت شمسی به میلادی و تعیین تعطیلات ایجاد می‌شوند.

#### ورودی‌ها و خروجی‌ها
- **ورودی:** ندارد (فقط Query Parameter)
- **خروجی:** `IActionResult` با پیام موفقیت
- **Route Parameters:** ندارد
- **Query Parameters:** 
  - `year` (int) - **ضروری** - سال شمسی مورد نظر (مثلاً 1404)

#### Query Parameter: year

| پارامتر | نوع داده | ضروری/اختیاری | توضیح | محدودیت‌ها |
|---------|---------|---------------|-------|-----------|
| `year` | `int` | **ضروری** | سال شمسی که می‌خواهید تقویم آن را ایجاد کنید | باید یک سال معتبر شمسی باشد (مثلاً 1400 تا 1500) |

#### اعتبارسنجی‌ها
- پارامتر `year` باید یک عدد معتبر باشد
- در صورت وجود تقویم برای سال مورد نظر، ممکن است خطا برگردانده شود یا تقویم به‌روزرسانی شود

#### Authentication/Authorization
- **Authentication:** ندارد (عمومی)
- **Authorization:** ندارد

#### مثال درخواست
```
POST /SeedYear?year=1404
```

#### مثال پاسخ موفق (200 OK)
```json
{
  "message": "Calendar for 1404 seeded successfully."
}
```

#### مثال پاسخ خطا (400 BadRequest)
```json
{
  "message": "عملیات بروزرسانی تقویم با خطا مواجه شد : [جزئیات خطا]"
}
```

#### نکات مهم
- این اکشن تمام روزهای سال شمسی را با نگاشت به تاریخ میلادی ایجاد می‌کند
- تعطیلات رسمی ایران به صورت خودکار در تقویم ثبت می‌شوند
- قبل از استفاده از سیستم شیفت‌بندی، باید تقویم سال مورد نظر ایجاد شده باشد
---

**تاریخ ایجاد مستندات:** 2024  
**نسخه API:** v1  
**پروژه:** ShiftYar



