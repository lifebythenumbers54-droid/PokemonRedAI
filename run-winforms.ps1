# Pokemon Red AI - Windows Forms Application
# Run this script from the project root directory

Write-Host "Pokemon Red AI Player - Windows Forms" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Display .NET version
$dotnetVersion = dotnet --version
Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Gray

# Restore packages
Write-Host ""
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Package restore failed" -ForegroundColor Red
    exit 1
}

# Build the WinForms project
Write-Host ""
Write-Host "Building WinForms application..." -ForegroundColor Yellow
dotnet build src/PokemonRedAI.WinForms --configuration Debug --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Run the WinForms application
Write-Host "Starting Pokemon Red AI..." -ForegroundColor Yellow
Write-Host ""

dotnet run --project src/PokemonRedAI.WinForms --configuration Debug --no-build
