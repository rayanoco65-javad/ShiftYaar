I'll add a comprehensive Markdown guide file with step-by-step operations and explicit call sequences (controllers, services, methods) to the repository.

[3 tools called]

### راهنمای راهبری شیفت‌بندی از صفر تا صد (ویژه اپراتور)

- این راهنما تمام مراحل لازم برای آماده‌سازی داده‌ها، تنظیم قوانین، اجرای الگوریتم، و ذخیره‌سازی برنامه شیفت را پوشش می‌دهد.
- در هر بخش، ترتیب اجرای اکشن‌ها (Controller Actions)، سرویس‌ها و متدهای اصلی نیز ذکر شده است.

## 1) پیش‌نیازها
- نقش دسترسی: Admin یا Supervisor
- دیتابیس آماده و مایگریشن‌ها اعمال شده باشد.
- حداقل یک بیمارستان، یک دپارتمان، چند تخصص، شیفت‌های روزانه، و کاربران فعال تعریف شده باشند.

## 2) آماده‌سازی داده‌های پایه

### 2.1) ایجاد بیمارستان
- اطلاعات: نام، کد سیام، استان/شهر، آدرس، ایمیل/وب‌سایت، وضعیت فعال، شماره‌های تماس.
- ترتیب اجرای اکشن/سرویس (نمونه الگوی پروژه):
  - Controller: HospitalController (الگوی عمومی CRUD)
  - Service: IHospitalService (CRUD)
  - Repository: IEfRepository<Hospital>

### 2.2) ایجاد دپارتمان برای بیمارستان
- اطلاعات: نام، توضیحات، IsActive، Supervisor، IsNightLover.
- ترتیب اجرای اکشن/سرویس:
  - Controller: `DepartmentController`
    - GET: GetDepartments → Service: `IDepartmentService.GetFilteredDepartmentsAsync`
    - GET: GetDepartment(id) → `IDepartmentService.GetByIdAsync`
    - POST: CreateDepartment(dto) → `IDepartmentService.CreateAsync`
    - PUT: UpdateDepartment(id,dto) → `IDepartmentService.UpdateAsync`
    - DELETE: DeleteDepartment(id) → `IDepartmentService.DeleteAsync`
  - Repository: IEfRepository<Department>

### 2.3) تعریف تخصص‌ها (Specialty)
- برای هر دپارتمان، تخصص‌ها را ثبت کنید.
- ترتیب اجرای اکشن/سرویس:
  - Controller: `SpecialtyController`
    - GET: GetSpecialties → Service: `ISpecialtyService.GetSpecialties`
    - GET: GetSpecialty(id) → `ISpecialtyService.GetSpecialty`
    - POST: CreateSpecialty(dto) → `ISpecialtyService.CreateSpecialty`
    - PUT: UpdateSpecialty(id,dto) → `ISpecialtyService.UpdateSpecialty`
    - DELETE: DeleteSpecialty(id) → `ISpecialtyService.DeleteSpecialty`
  - Repository: IEfRepository<Specialty>

### 2.4) تعریف شیفت‌ها (Shift)
- برای هر دپارتمان، شیفت‌های Morning/Evening/Night با StartTime/EndTime.
- ترتیب اجرای اکشن/سرویس: مشابه الگوی CRUD (ShiftController/Service/Repository).

### 2.5) تعریف ظرفیت تخصصی هر شیفت (ShiftRequiredSpecialty)
- تعیین نیاز هر تخصص در هر شیفت:
  - RequiredTotalCount، RequiredMaleCount/RequiredFemaleCount، OnCallTotalCount و ...
- ترتیب اجرای اکشن/سرویس:
  - Controller: `ShiftRequiredSpecialtyController`
    - CRUD طبق الگوی تخصص
  - Service: `IShiftRequiredSpecialtyService`
  - Repository: IEfRepository<ShiftRequiredSpecialty>

### 2.6) تعریف کاربران (User)
- اطلاعات: نام کامل، کد پرسنلی، جنسیت، DepartmentId، SpecialtyId، IsActive، CanBeShiftManager، نوع/الگوی شیفت.
- ترتیب اجرای اکشن/سرویس: UserController/UserService/IEfRepository<User>

## 3) تنظیمات شیفت‌بندی دپارتمان (ذخیره در دیتابیس)

- یک‌بار برای هر دپارتمان تنظیم کنید؛ در هر اجرا به‌صورت خودکار اعمال می‌شود.

- قوانین قطعی (HardRules):
  - ForbidUnavailableDates: ممنوعیت اختصاص شیفت در تاریخ‌های غیرمجاز کاربر
  - ForbidDuplicateDailyAssignments: هر کاربر حداکثر یک شیفت در روز
  - EnforceMaxShiftsPerDay: اعمال سقف روزانه
  - EnforceMinRestDays: حداقل فاصله استراحت
  - EnforceMaxConsecutiveShifts: حداکثر شیفت‌های متوالی
  - EnforceWeeklyMaxShifts: سقف هفتگی
  - EnforceNightShiftMonthlyCap: سقف شب ماهانه
  - EnforceSpecialtyCapacity: جلوگیری از تجاوز ظرفیت تخصص/شیفت/روز

- وزن قوانین نرم (SoftWeights):
  - GenderBalanceWeight: وزن تعادل جنسیتی
  - SpecialtyPreferenceWeight: وزن ترجیح مطابقت تخصص
  - UserUnwantedShiftWeight: وزن جریمه شیفت‌های ناخواسته
  - UserPreferredShiftWeight: وزن پاداش شیفت‌های ترجیحی
  - WeeklyMaxWeight: وزن سقف هفتگی (اگر سخت نباشد)
  - MonthlyNightCapWeight: وزن سقف شب ماهانه (اگر سخت نباشد)

- ترتیب اکشن/سرویس:
  - Controller: `DepartmentSchedulingSettingsController`
    - GET: GetSettings(filter: DepartmentId, PageNumber, PageSize)
      - Service: `IDepartmentSchedulingSettingsService.GetSettingsAsync`
    - GET: GetSetting(id)
      - Service: `IDepartmentSchedulingSettingsService.GetSettingAsync`
    - POST: CreateSetting(dto: DepartmentSchedulingSettingsDtoAdd)
      - Service: `IDepartmentSchedulingSettingsService.CreateSettingAsync`
    - PUT: UpdateSetting(id,dto)
      - Service: `IDepartmentSchedulingSettingsService.UpdateSettingAsync`
    - DELETE: DeleteSetting(id)
      - Service: `IDepartmentSchedulingSettingsService.DeleteSettingAsync`
  - Mapping: `UserProfile` → Map بین `DepartmentSchedulingSettings` و DTOها
  - Persistence: `ShiftYarDbContext` → `DbSet<DepartmentSchedulingSettings>`

نکته: IsNightLover دپارتمان، پیش‌فرض وزن سخت‌گیری شب را تنظیم می‌کند؛ ولی تنظیمات اختصاصی در جدول DepartmentSchedulingSettings ارجح است.

## 4) ثبت محدودیت‌های اختصاصی کاربران برای بازه (اختیاری)
- برای همان بازه‌ی موردنظر:
  - UnavailableDates، PreferredShifts، UnwantedShifts
  - MaxConsecutiveShifts، MinRestDaysBetweenShifts، MaxShiftsPerWeek، MaxNightShiftsPerMonth
- ورودی این‌ها در درخواست شیفت‌بندی ارسال می‌شود و فقط همان دوره را تحت تأثیر قرار می‌دهد.

## 5) اجرای شیفت‌بندی

### 5.1) اعتبارسنجی ورودی (توصیه می‌شود)
- Controller: `ShiftSchedulingController` → POST `validate`
- Service: `ShiftSchedulingService.ValidateConstraintsAsync`
  - چک کردن: StartDate/EndDate، Department، پارامترهای الگوریتم، محدودیت‌های کاربر
- خروجی: لیست خطاهای احتمالی. ابتدا موارد را رفع کنید.

### 5.2) بهینه‌سازی (Optimize)
- Controller: `ShiftSchedulingController` → POST `optimize`
- Service: `ShiftSchedulingService.OptimizeShiftScheduleAsync`
  - Load constraints: `ShiftSchedulingService.LoadConstraintsAsync`
    - بارگذاری Users/Shift/RequiredSpecialties
    - بارگذاری DepartmentSchedulingSettings برای DepartmentId و نگاشت به `HardRules` و `SoftWeights`
  - Build parameters: `SimulatedAnnealingParameters` از ورودی
  - Run algorithm: `new SimulatedAnnealingScheduler(constraints, parameters).Optimize()`
    - GenerateInitialSolution()
    - Loop (iterate while temp > FinalTemperature and iterations remain):
      - GenerateNeighbor()
      - IsFeasible(neighbor) → رد همسایه‌های ناقض HardRules
      - CalculateSolutionScore(neighbor) → اعمال SoftWeights (جریمه/پاداش)
      - پذیرش/رد بر اساس بهبود امتیاز یا احتمال متروپولیس
    - خروجی: بهترین راه‌حل (bestSolution)
  - Convert to DTO: `ConvertSolutionToResultAsync`

### 5.3) بررسی خروجی
- FinalScore، Violations، آمارها:
  - TotalShifts، TotalUsers، Satisfied/ViolatedConstraints، AverageShiftsPerUser
  - ShiftsByType، ShiftsByUser
- در صورت نیاز، وزن‌ها/ظرفیت‌ها/پارامترهای الگوریتم را تنظیم و دوباره Optimize را اجرا کنید.

### 5.4) ذخیره‌سازی برنامه (Save)
- Controller: `ShiftSchedulingController` → POST `save`
- Service: `ShiftSchedulingService.SaveOptimizedScheduleAsync`
  - ایجاد `ShiftAssignment` برای هر انتساب و ذخیره در DB
  - نکته: حذف یا آرشیو انتساب‌های قبلی بازه را نیز می‌توان افزود

## 6) ترتیب کلی فراخوانی‌ها (Flow خلاصه)

- آماده‌سازی داده‌ها:
  - DepartmentController → DepartmentService → IEfRepository<Department>
  - SpecialtyController → SpecialtyService → IEfRepository<Specialty>
  - ShiftController → ShiftService → IEfRepository<Shift>
  - ShiftRequiredSpecialtyController → IShiftRequiredSpecialtyService → IEfRepository<ShiftRequiredSpecialty>
  - UserController → UserService → IEfRepository<User>

- تنظیمات دپارتمان:
  - DepartmentSchedulingSettingsController → IDepartmentSchedulingSettingsService → IEfRepository<DepartmentSchedulingSettings>

- اجرای شیفت‌بندی:
  - ShiftSchedulingController.validate → ShiftSchedulingService.ValidateConstraintsAsync
  - ShiftSchedulingController.optimize → ShiftSchedulingService.OptimizeShiftScheduleAsync
    - LoadConstraintsAsync → بارگذاری داده‌ها و تنظیمات
    - SimulatedAnnealingScheduler.Optimize
      - GenerateInitialSolution
      - GenerateNeighbor
      - IsFeasible
      - CalculateSolutionScore
  - ShiftSchedulingController.save → ShiftSchedulingService.SaveOptimizedScheduleAsync

## 7) نکات عیب‌یابی
- ظرفیت تخصصی کافی نیست: RequiredTotalCount را افزایش دهید یا کاربران فعال آن تخصص را بیشتر کنید.
- نقض مکرر قوانین قطعی: سخت‌گیری HardRules را تعدیل کنید یا داده‌ها (استراحت/توالی/ظرفیت) را اصلاح کنید.
- زمان اجرای طولانی: کاهش MaxIterations یا افزایش CoolingRate؛ کوتاه‌کردن بازه زمانی.
- لاگ‌ها: پوشه `ShiftYar.Api/logs`.

## 8) چک‌لیست اجرای ماهانه
- تخصص‌ها و شیفت‌ها و ظرفیت‌ها به‌روز است؟
- کاربران فعال و دپارتمان/تخصص صحیح دارند؟
- تنظیمات دپارتمانی (Hard/Soft) ذخیره شده و منطقی است؟
- بازه زمانی درست است؟
- اعتبارسنجی بدون خطاست؟
- خروجی نمونه بررسی و تأیید شده؟

اگر بخواهید، همین سند را به‌صورت PDF به‌همراه اسکرین‌شات از UI و نمونه درخواست/پاسخ API آماده می‌کنم.