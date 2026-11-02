# Build + run the desktop app.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host "Compilant..." -ForegroundColor Cyan
dotnet build -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilació ha fallat." }

$exe = Join-Path $PSScriptRoot 'src\AlbionMarket.App\bin\Debug\net10.0-windows\AlbionMarket.App.exe'
Write-Host "Obrint $exe" -ForegroundColor Green
Start-Process -FilePath $exe
