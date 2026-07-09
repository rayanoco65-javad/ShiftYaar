# PowerShell script to add migration for ShiftExchange
Write-Host "Adding migration for ShiftExchange..." -ForegroundColor Green

# Navigate to the API project directory
Set-Location "ShiftYar.Api"

# Add migration
dotnet ef migrations add AddShiftExchange

Write-Host "Migration added successfully!" -ForegroundColor Green
Write-Host "To apply the migration, run: dotnet ef database update" -ForegroundColor Yellow
