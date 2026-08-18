@echo off
REM Republishes AevumLux.TestIdentityServer into the TestIdp\ folder the main app looks in.
REM Run this after changing TestIdentityServer code so the bundled copy picks up your changes.
REM The app's own auto-publish-on-first-run only fires when this folder is empty/missing, so
REM after the first run you must run this bat manually to pick up code changes.

set TARGET=%~dp0AevumLux\bin\x64\Debug\net8.0-windows10.0.19041.0\TestIdp

dotnet publish "%~dp0AevumLux.TestIdentityServer\AevumLux.TestIdentityServer.csproj" -c Release -r win-x64 --self-contained true -o "%TARGET%"
