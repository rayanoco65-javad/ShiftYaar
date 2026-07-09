# سیستم جابجایی شیفت (Shift Exchange System)

## نمای کلی
این سیستم امکان جابجایی شیفت بین دو کاربر را با تأیید سوپروایزر فراهم می‌کند. جابجایی فقط در صورت تأیید سوپروایزر انجام می‌شود.

## اجزای سیستم

### 1. موجودیت‌ها (Entities)

#### ShiftExchange
- **شناسه**: `Id`
- **کاربر درخواست کننده**: `RequestingUserId`
- **کاربر پیشنهاد دهنده**: `OfferingUserId`
- **شیفت درخواست کننده**: `RequestingShiftAssignmentId`
- **شیفت پیشنهاد دهنده**: `OfferingShiftAssignmentId`
- **وضعیت**: `Status` (Pending, Approved, Rejected, Executed, Cancelled)
- **تاریخ درخواست**: `RequestDate`
- **دلیل**: `Reason`
- **سوپروایزر**: `SupervisorId`
- **نظر سوپروایزر**: `SupervisorComment`
- **تاریخ تأیید**: `ApprovalDate`
- **تاریخ اجرا**: `ExecutionDate`

### 2. وضعیت‌های درخواست (ExchangeStatus)
- `Pending`: در انتظار تأیید
- `Approved`: تأیید شده
- `Rejected`: رد شده
- `Executed`: اجرا شده
- `Cancelled`: لغو شده

### 3. DTOs

#### ShiftExchangeDtoAdd
برای ایجاد درخواست جدید:
- `RequestingUserId`: شناسه کاربر درخواست کننده
- `OfferingUserId`: شناسه کاربر پیشنهاد دهنده
- `RequestingShiftAssignmentId`: شناسه شیفت درخواست کننده
- `OfferingShiftAssignmentId`: شناسه شیفت پیشنهاد دهنده
- `Reason`: دلیل درخواست

#### ShiftExchangeDtoGet
برای دریافت اطلاعات:
- تمام فیلدهای موجودیت به همراه نام‌های کامل کاربران

#### ShiftExchangeApprovalDto
برای تأیید/رد درخواست:
- `Id`: شناسه درخواست
- `IsApproved`: تأیید یا رد
- `SupervisorComment`: نظر سوپروایزر

### 4. سرویس‌ها (Services)

#### IShiftExchangeService
- `GetByIdAsync(int id)`: دریافت درخواست بر اساس شناسه
- `GetAllAsync()`: دریافت تمام درخواست‌ها
- `GetByUserIdAsync(int userId)`: دریافت درخواست‌های کاربر
- `GetPendingApprovalsAsync(int supervisorId)`: دریافت درخواست‌های در انتظار تأیید
- `CreateAsync(ShiftExchangeDtoAdd dto)`: ایجاد درخواست جدید
- `UpdateAsync(ShiftExchangeDtoUpdate dto)`: ویرایش درخواست
- `ApproveAsync(ShiftExchangeApprovalDto dto)`: تأیید/رد درخواست
- `ExecuteExchangeAsync(int exchangeId)`: اجرای جابجایی
- `CancelAsync(int exchangeId)`: لغو درخواست
- `DeleteAsync(int id)`: حذف درخواست

### 5. کنترلر API

#### ShiftExchangeController
- `GET /api/ShiftExchange`: دریافت تمام درخواست‌ها
- `GET /api/ShiftExchange/{id}`: دریافت درخواست بر اساس شناسه
- `GET /api/ShiftExchange/user/{userId}`: دریافت درخواست‌های کاربر
- `GET /api/ShiftExchange/pending-approvals/{supervisorId}`: دریافت درخواست‌های در انتظار تأیید
- `POST /api/ShiftExchange`: ایجاد درخواست جدید
- `PUT /api/ShiftExchange`: ویرایش درخواست
- `POST /api/ShiftExchange/approve`: تأیید/رد درخواست
- `POST /api/ShiftExchange/execute/{exchangeId}`: اجرای جابجایی
- `POST /api/ShiftExchange/cancel/{exchangeId}`: لغو درخواست
- `DELETE /api/ShiftExchange/{id}`: حذف درخواست

## فرآیند جابجایی شیفت

### 1. ایجاد درخواست
1. کاربر درخواست کننده درخواست جابجایی ایجاد می‌کند
2. سیستم بررسی می‌کند که شیفت‌ها متعلق به کاربران هستند
3. سیستم سوپروایزر دپارتمان را پیدا می‌کند
4. درخواست با وضعیت `Pending` ایجاد می‌شود

### 2. تأیید درخواست
1. سوپروایزر درخواست را بررسی می‌کند
2. سوپروایزر درخواست را تأیید یا رد می‌کند
3. وضعیت درخواست به `Approved` یا `Rejected` تغییر می‌کند

### 3. اجرای جابجایی
1. پس از تأیید، جابجایی اجرا می‌شود
2. کاربران شیفت‌ها را با یکدیگر جابجا می‌کنند
3. وضعیت درخواست به `Executed` تغییر می‌کند

## قوانین کسب و کار

### محدودیت‌ها
- فقط درخواست‌های در انتظار تأیید قابل ویرایش هستند
- فقط درخواست‌های تأیید شده قابل اجرا هستند
- درخواست‌های اجرا شده قابل لغو نیستند
- درخواست‌های اجرا شده قابل حذف نیستند

### بررسی‌ها
- بررسی تکراری نبودن درخواست
- بررسی تعلق شیفت‌ها به کاربران
- بررسی وجود سوپروایزر دپارتمان

## نصب و راه‌اندازی

### 1. اضافه کردن Migration
```powershell
# اجرای اسکریپت PowerShell
.\add-shift-exchange-migration.ps1
```

### 2. اعمال Migration
```bash
dotnet ef database update
```

### 3. تست API
```bash
# ایجاد درخواست جدید
POST /api/ShiftExchange
{
  "requestingUserId": 1,
  "offeringUserId": 2,
  "requestingShiftAssignmentId": 10,
  "offeringShiftAssignmentId": 20,
  "reason": "دلیل جابجایی"
}

# تأیید درخواست
POST /api/ShiftExchange/approve
{
  "id": 1,
  "isApproved": true,
  "supervisorComment": "تأیید شد"
}

# اجرای جابجایی
POST /api/ShiftExchange/execute/1
```

## امنیت
- تمام عملیات نیاز به احراز هویت دارند
- فقط سوپروایزر می‌تواند درخواست‌ها را تأیید کند
- کاربران فقط می‌توانند درخواست‌های خود را ویرایش کنند

## لاگ‌گیری
- تمام عملیات در سیستم لاگ‌گیری می‌شوند
- تغییرات وضعیت درخواست‌ها ثبت می‌شوند
- تاریخ و زمان تمام عملیات ذخیره می‌شود
