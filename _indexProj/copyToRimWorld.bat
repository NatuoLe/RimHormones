@echo off
echo ========================================
echo   Build and Copy Mod to RimWorld Mods
echo ========================================
echo.

set "PROJECT_DIR=%~dp0"
set "MOD_NAME=Rim-Hormones"
set "DEST_DIR=D:\Steam\steamapps\common\RimWorld\Mods\%MOD_NAME%"

echo Project Dir: %PROJECT_DIR%
echo Dest Dir: %DEST_DIR%
echo.

cd /d "%PROJECT_DIR%"

echo ========================================
echo   Step 1: Build C# Code
echo ========================================
echo.

if not exist "Assembly-CSharp.csproj" (
    echo Error: Assembly-CSharp.csproj not found
    goto :error
)

dotnet build Assembly-CSharp.csproj -c Release
if errorlevel 1 (
    echo.
    echo Build failed!
    goto :error
)

echo.
echo Build success!
echo.

echo ========================================
echo   Step 2: Copy to RimWorld Mods
echo ========================================
echo.

if not exist "%DEST_DIR%" (
    echo Creating dest dir...
    mkdir "%DEST_DIR%"
)

echo Copy About folder...
xcopy /E /I /Y "%PROJECT_DIR%..\About" "%DEST_DIR%\About"

echo Copy Defs folder...
xcopy /E /I /Y "%PROJECT_DIR%..\Defs" "%DEST_DIR%\Defs"

echo Copy Languages folder...
xcopy /E /I /Y "%PROJECT_DIR%..\Languages" "%DEST_DIR%\Languages"

echo Creating Assemblies folder and copy DLL...
if not exist "%DEST_DIR%\Assemblies" mkdir "%DEST_DIR%\Assemblies"
if exist "%PROJECT_DIR%bin\Release\net48\RimHormones.dll" (
    copy /Y "%PROJECT_DIR%bin\Release\net48\RimHormones.dll" "%DEST_DIR%\Assemblies\"
) else (
    echo Warning: RimHormones.dll not found at bin\Release\net48\
)

if exist "%PROJECT_DIR%0Harmony.dll" (
    copy /Y "%PROJECT_DIR%0Harmony.dll" "%DEST_DIR%\Assemblies\"
)

echo.
echo ========================================
echo   Done!
echo ========================================
echo.
pause
exit /b 0

:error
echo.
echo ========================================
echo   Failed
echo ========================================
echo.
pause
exit /b 1