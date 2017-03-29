@echo off
cls

if exist .paket\paket.exe (
    rem paket exists already
) else (
    rem paket does not exist
    .paket\paket.bootstrapper.exe
)

.paket\paket.exe restore
if errorlevel 1 (
    exit /b %errorlevel%
)

"packages\__commandlinetools\FAKE\tools\Fake.exe" build.fsx UseFxCop=FALSE
pause

