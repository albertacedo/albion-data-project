@echo off
REM Wrapper so the script also works when PowerShell script execution is disabled.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0session-end.ps1" %*
