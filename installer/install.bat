@echo off
REM snitch.out one-shot installer - just run this file.
REM Checks dependencies, pulls the repo, compiles, installs, launches.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (
  echo.
  echo Installer failed. Press any key to close.
  pause >nul
)
