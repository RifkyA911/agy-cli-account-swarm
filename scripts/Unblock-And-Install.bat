@echo off
setlocal
cd /d "%~dp0"
title Agy CLI Account Swarm - Unblock & Safe Installer

echo ==============================================================================
echo [Agy CLI Account Swarm] Windows Defender SmartScreen Safe Launcher
echo ==============================================================================
echo.
echo Removing Mark-of-the-Web (Zone.Identifier) stream from setup executable...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '%~dp0' -Filter '*.exe' | Unblock-File"

echo Unblocked successfully! Launching installer...
start "" "%~dp0Agy-CLI-Account-Swarm-Setup-v0.9.13-beta.exe"
echo Done.
exit /b 0
