@echo off
REM Builds AevumLux.Installer\bin\Release\AevumLux.Installer.msi from scratch.
REM Publishes both AevumLux and AevumLux.TestIdentityServer as self-contained win-x64
REM executables into AevumLux.Installer\staging\, then builds the MSI from that staging
REM folder. staging\ is git-ignored generated output, not committed.
REM
REM Run this whenever you want to cut a new installer build.

setlocal enabledelayedexpansion

set STAGING=%~dp0AevumLux.Installer\staging\App

echo Reading version from AevumLux.csproj...
for /f "usebackq tokens=3 delims=<>" %%V in (`findstr /c:"<Version>" "%~dp0AevumLux\AevumLux.csproj"`) do set PRODUCT_VERSION=%%V
if "%PRODUCT_VERSION%"=="" goto :error_version
echo Version: %PRODUCT_VERSION%

echo Cleaning previous staging output...
if exist "%STAGING%" rmdir /s /q "%STAGING%"

echo.
echo Publishing AevumLux...
dotnet publish "%~dp0AevumLux\AevumLux.csproj" -c Release -r win-x64 --self-contained true -p:Platform=x64 -o "%STAGING%"
if errorlevel 1 goto :error

echo.
echo Publishing AevumLux.TestIdentityServer into TestIdp\...
dotnet publish "%~dp0AevumLux.TestIdentityServer\AevumLux.TestIdentityServer.csproj" -c Release -r win-x64 --self-contained true -o "%STAGING%\TestIdp"
if errorlevel 1 goto :error

echo.
echo Copying docs (ReadMe.txt, Scenarios.md) into the install payload...
copy /y "%~dp0AevumLux.Installer\ReadMe.txt" "%STAGING%\ReadMe.txt" >nul
copy /y "%~dp0AevumLux.TestIdentityServer\SCENARIOS.md" "%STAGING%\Scenarios.md" >nul

echo.
echo Building installer (version %PRODUCT_VERSION%)...
dotnet build "%~dp0AevumLux.Installer\AevumLux.Installer.wixproj" -c Release -p:DefineConstants=ProductVersion=%PRODUCT_VERSION%
if errorlevel 1 goto :error

echo.
echo Done. Installer at AevumLux.Installer\bin\Release\AevumLux.Installer.msi
exit /b 0

:error
echo.
echo Build failed.
exit /b 1

:error_version
echo.
echo Could not read ^<Version^> from AevumLux\AevumLux.csproj.
exit /b 1
