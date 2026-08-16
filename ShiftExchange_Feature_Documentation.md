# سیستم جابجایی شیفت (Shift Exchange System)

## نمای کلی
این سیستم امکان جابجایی شیفت بین دو کاربر را با تأیید سوپروایزر فراهم می‌کند. جابجایی فقط در صورت تأیید سوپروایزر انجام می‌شود.

## اجزای سیستم

### 1. موجودیت‌ها (Entities)

#### ShiftExchange
- **شناسه**: `Id`
- **کاربر درخواست کننده**: `RequestingUserId`
- **کاربر پیشنهاد دهنده / دریافت‌کننده**: `OfferingUserId`
- **شیفت درخواست کننده**: `RequestingShiftAssignmentId`
- **شیفت پیشنهاد دهنده**: `OfferingShiftAssignmentId` (در واگذاری `Transfer` خالی است)
- **نوع عملیات**: `ExchangeType` (`Swap` = جابجایی دوطرفه، `Transfer` = واگذاری به کاربر آزاد)
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

### 2.1 نوع عملیات (ExchangeType)
- `Swap` (0): **جابجایی دوطرفه** — هر دو کاربر باید شیفت داشته باشند؛ پس از اجرا `UserId` دو انتساب عوض می‌شود.
- `Transfer` (1): **واگذاری** — کاربر A شیفت خود را به کاربر B می‌دهد؛ B در **همان تاریخ و همان شیفت** نباید انتساب داشته باشد. پس از اجرا فقط انتساب A به B منتقل می‌شود و A آزاد می‌شود.

### 3. DTOs

#### ShiftExchangeDtoAdd
برای ایجاد درخواست جدید:
- `RequestingUserId`: شناسه کاربر درخواست‌کننده / واگذارکننده
- `OfferingUserId`: شناسه کاربر مقابل / دریافت‌کننده
- `RequestingShiftAssignmentId`: شناسه شیفت واگذارکننده (همیشه الزامی)
- `OfferingShiftAssignmentId`: شناسه شیفت کاربر مقابل — **فقط برای `Swap` الزامی**؛ برای `Transfer` باید `null` باشد
- `ExchangeType`: `0 = Swap` (پیش‌فرض)، `1 = Transfer`
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
1. کاربر درخواست را ثبت می‌کند (`Swap` یا `Transfer`)
2. سیستم بررسی می‌کند شیفت متعلق به کاربر درخواست‌کننده است
3. برای `Swap`: شیفت کاربر مقابل هم باید وجود داشته باشد
4. برای `Transfer`: کاربر مقابل در همان زمان شیفت نباید داشته باشد؛ هر دو کاربر باید هم‌دپارتمان باشند
5. سوپروایزر دپارتمان روی درخواست ثبت می‌شود
6. وضعیت: `Pending`

### 2. تأیید درخواست
1. سوپروایزر درخواست را بررسی می‌کند
2. تأیید یا رد
3. وضعیت → `Approved` یا `Rejected`

### 3. اجرا
1. پس از تأیید، `POST /api/ShiftExchange/execute/{id}` فراخوانی می‌شود
2. **Swap**: دو `UserId` جابجا می‌شوند
3. **Transfer**: `UserId` انتساب واگذارکننده به دریافت‌کننده تغییر می‌کند
4. وضعیت → `Executed`

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
- برای `Transfer`: کاربر دریافت‌کننده در همان slot شیفت نداشته باشد
- هر دو کاربر از یک دپارتمان باشند

## نصب و راه‌اندازی

Migration فیلد `ExchangeType`: `20260816120000_AddExchangeTypeToShiftExchange`

```bash
dotnet ef database update
```

### تست API

#### Swap (جابجایی دوطرفه)
```json
POST /api/ShiftExchange
{
  "requestingUserId": 1,
  "offeringUserId": 2,
  "requestingShiftAssignmentId": 10,
  "offeringShiftAssignmentId": 20,
  "exchangeType": 0,
  "reason": "جابجایی دوطرفه"
}
```

#### Transfer (واگذاری به کاربر آزاد)
```json
POST /api/ShiftExchange
{
  "requestingUserId": 1,
  "offeringUserId": 3,
  "requestingShiftAssignmentId": 10,
  "offeringShiftAssignmentId": null,
  "exchangeType": 1,
  "reason": "واگذاری شیفت به همکار آزاد"
}
```

#### تأیید و اجرا
```json
POST /api/ShiftExchange/approve
{
  "id": 1,
  "isApproved": true,
  "supervisorComment": "تأیید شد"
}

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
