@echo off
setlocal

echo ============================================
echo   CBT Exam System - Build Script
echo ============================================
echo.

set PROJECT=src\CbtExam.Desktop\CbtExam.Desktop.csproj
set OUTPUT=publish\CbtExam

echo [1/3] Closing running instances of CbtExam.exe (if any)...
taskkill /F /IM CbtExam.exe /T >nul 2>&1
echo.

echo [2/3] Building and Publishing self-contained single EXE...
dotnet publish %PROJECT% ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  --output %OUTPUT%

if %errorlevel% neq 0 (
    echo.
    echo FAILED: publish
    pause
    exit /b 1
)

echo.
echo [3/3] Done!
echo.
echo Output: %CD%\%OUTPUT%\CbtExam.exe
echo Size:
for %%f in ("%OUTPUT%\CbtExam.exe") do echo   %%~zf bytes
echo.
echo To run: Double-click CbtExam.exe
echo No .NET installation required on target machine.
echo.
pause
