param (
    [string]$Name
)

if (-not $Name) {
    Write-Host "❌ AddMigration"
    exit
}

# تعریف پروژه‌ها
$project = "ShiftYar.Infrastructure"
$startupProject = "ShiftYar.API"
$outputDir = "Persistence/Migrations"

# تولید تاریخ و زمان برای یکتایی اسم
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$migrationName = "${timestamp}_$Name"

Write-Host "🛠 ایجاد Migration: $migrationName"

# اجرای دستور Add-Migration
dotnet ef migrations add $migrationName `
    --project $project `
    --startup-project $startupProject `
    --output-dir $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ خطا در ایجاد migration"
    exit $LASTEXITCODE
}

# اجرای Update-Database
Write-Host "📥 به‌روزرسانی دیتابیس..."
dotnet ef database update `
    --project $project `
    --startup-project $startupProject

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ خطا در به‌روزرسانی دیتابیس"
    exit $LASTEXITCODE
}

# لاگ کردن
$logPath = "$PSScriptRoot/migration-log.txt"
$logEntry = "$(Get-Date -Format "yyyy-MM-dd HH:mm:ss") - $migrationName"
Add-Content -Path $logPath -Value $logEntry

Write-Host "✅ Migration '$migrationName' ایجاد و دیتابیس به‌روزرسانی شد."
Write-Host "📝 لاگ ذخیره شد در: $logPath"
