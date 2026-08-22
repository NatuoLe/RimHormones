@echo off
echo ========================================
echo   Build and Copy Mod to RimWorld Mods
echo ========================================
echo.

set "PROJECT_DIR=%~dp0"
set "MOD_NAME=Rim-Hormones"

rem --- destination mods root: override via 1st argument, else default ---
if "%~1"=="" (
    set "DEST_ROOT=F:\steam\steamapps\common\RimWorld\Mods"
) else (
    set "DEST_ROOT=%~1"
)
set "DEST_DIR=%DEST_ROOT%\%MOD_NAME%"

rem --- main assembly project (RimHormones.dll) ---
set "MAIN_PROJ=%PROJECT_DIR%_indexProj\Assembly-CSharp.csproj"
set "MAIN_OUT=%PROJECT_DIR%_indexProj\bin\Release\net48"

rem --- optional module project (MetabolicEssential.dll) ---
rem     Its csproj outputs into %PROJECT_DIR%MetabolicEssential\ (a subfolder OUTSIDE Assemblies),
rem     so RimWorld does NOT auto-load it. The main mod's MetaBolicLoadCtrl only Assembly.LoadFrom's
rem     it when the in-game setting is checked and the game is restarted.
set "META_PROJ=%PROJECT_DIR%_metabolicEssentialsExtendedProj\MetabolicEssential.csproj"
set "META_OUT=%PROJECT_DIR%MetabolicEssential"

set "SRC_ASM=%PROJECT_DIR%Assemblies"
set "DEST_ASM=%DEST_DIR%\Assemblies"

echo Project Dir: %PROJECT_DIR%
echo Dest Dir: %DEST_DIR%
echo.

cd /d "%PROJECT_DIR%"

rem --- SAFETY NOTE: a running RimWorld locks its loaded assemblies
rem     (RimHormones.dll), so the DLL cannot be overwritten. We do NOT abort
rem     outright (that would block every XML-only redeploy too). Instead we
rem     warn, still deploy all content, and only the DLL copy may fail — we
rem     detect that and tell the user to close the game + restart. ---
set "GAME_RUNNING=0"
tasklist /FI "IMAGENAME eq RimWorldWin64.exe" 2>NUL | find /I "RimWorldWin64.exe" >NUL
if not errorlevel 1 set "GAME_RUNNING=1"
tasklist /FI "IMAGENAME eq RimWorld.exe" 2>NUL | find /I "RimWorld.exe" >NUL
if not errorlevel 1 set "GAME_RUNNING=1"
if "%GAME_RUNNING%"=="1" (
    echo.
    echo ============================================================
    echo   NOTE: RimWorld appears to be running.
    echo   - XML / Defs / Languages will still be deployed now.
    echo     They apply on the next load / restart.
    echo   - RimHormones.dll is LOCKED by the running game and may NOT
    echo     update. Close the game completely and re-run this script
    echo     to refresh the DLL.
    echo ============================================================
    echo.
)

echo ========================================
echo   Step 1: Build RimHormones.dll
echo ========================================
echo.

if not exist "%MAIN_PROJ%" (
    echo Error: %MAIN_PROJ% not found
    goto :error
)

dotnet build "%MAIN_PROJ%" -c Release
if errorlevel 1 (
    echo.
    echo Build failed: RimHormones
    goto :error
)

if not exist "%MAIN_OUT%\RimHormones.dll" (
    echo Error: RimHormones.dll not found at %MAIN_OUT%
    goto :error
)

echo.
echo Build success: RimHormones.dll
echo.

echo ========================================
echo   Step 2: Stage RimHormones.dll into Assemblies
echo ========================================
echo.

rem needed so the module project can reference this DLL at build time,
rem and so the final deploy has it.
if not exist "%SRC_ASM%" mkdir "%SRC_ASM%"
copy /Y "%MAIN_OUT%\RimHormones.dll" "%SRC_ASM%\"
if exist "%MAIN_OUT%\RimHormones.pdb" copy /Y "%MAIN_OUT%\RimHormones.pdb" "%SRC_ASM%\"
if exist "%PROJECT_DIR%_indexProj\0Harmony.dll" copy /Y "%PROJECT_DIR%_indexProj\0Harmony.dll" "%SRC_ASM%\"

echo.

echo ========================================
echo   Step 3: Build MetabolicEssential.dll
echo ========================================
echo.

if not exist "%META_PROJ%" (
    echo Warning: %META_PROJ% not found, skip optional module.
    goto :afterMeta
)

dotnet build "%META_PROJ%" -c Release
if errorlevel 1 (
    echo.
    echo Build failed: MetabolicEssential
    goto :error
)

if not exist "%META_OUT%\MetabolicEssential.dll" (
    echo Error: MetabolicEssential.dll not found at %META_OUT%
    goto :error
)

echo.
echo Build success: MetabolicEssential.dll
echo.

:afterMeta

echo ========================================
echo   Step 4: Clean up obsolete files
echo ========================================
echo.

rem SAFETY: the optional module must NOT live inside Assemblies\ (RimWorld auto-loads everything there,
rem which would bypass the in-game toggle). Delete any stray copy from the previous "Assemblies" layout.
if exist "%SRC_ASM%\MetabolicEssential.dll" (
    echo Removing stray Assemblies\MetabolicEssential.dll from project...
    del "%SRC_ASM%\MetabolicEssential.dll"
)
if exist "%SRC_ASM%\MetabolicEssential.pdb" (
    del "%SRC_ASM%\MetabolicEssential.pdb"
)
if exist "%DEST_ASM%\MetabolicEssential.dll" (
    echo Removing stray Assemblies\MetabolicEssential.dll from deployed mod...
    del "%DEST_ASM%\MetabolicEssential.dll"
)
if exist "%DEST_ASM%\MetabolicEssential.pdb" (
    del "%DEST_ASM%\MetabolicEssential.pdb"
)

if exist "%DEST_DIR%\Defs\TraitDefs\Trait_PhysiqueAptitudes.xml" (
    echo Removing old Trait_PhysiqueAptitudes.xml from Defs...
    del "%DEST_DIR%\Defs\TraitDefs\Trait_PhysiqueAptitudes.xml"
)

rem SAFETY: xcopy (Step 5) is additive and won't delete stale files in the destination.
rem These defs were renamed to .disabled to block the water-well feature; remove the
rem old enabled copies from the deployed mod so they don't re-appear.
if exist "%DEST_DIR%\Defs\ThingDefs\Thing_MEE_WaterBuildings.xml" (
    echo Removing blocked, renamed-to-disabled Thing_MEE_WaterBuildings.xml from deployed Defs...
    del "%DEST_DIR%\Defs\ThingDefs\Thing_MEE_WaterBuildings.xml"
)
if exist "%DEST_DIR%\Defs\RecipeDefs\Recipe_MEE_WaterBottle.xml" (
    echo Removing blocked Recipe_MEE_WaterBottle.xml from deployed Defs...
    del "%DEST_DIR%\Defs\RecipeDefs\Recipe_MEE_WaterBottle.xml"
)
if exist "%DEST_DIR%\Defs\WorkGiverDefs\WorkGiver_MEE_Water.xml" (
    echo Removing blocked WorkGiver_MEE_Water.xml from deployed Defs...
    del "%DEST_DIR%\Defs\WorkGiverDefs\WorkGiver_MEE_Water.xml"
)

rem SAFETY: same xcopy-additive problem for renamed/removed files. These stale
rem copies are NOT in the source tree anymore; if they survive in the deployed
rem mod they re-introduce old defs (e.g. the old MEE_RainwaterCollector referenced
rem a comp class that no longer exists -> load error). Remove them here so a
rem re-run of this script self-heals the deployed copy.
if exist "%DEST_DIR%\Defs\ThingDefs\Thing_MEE_RainwaterCollector.xml" (
    echo Removing stale Thing_MEE_RainwaterCollector.xml from deployed Defs...
    del "%DEST_DIR%\Defs\ThingDefs\Thing_MEE_RainwaterCollector.xml"
)
if exist "%DEST_DIR%\Defs\RecipeDefs\Recipe_MEE_GlucoseMash_FromPotato.xml" (
    echo Removing stale Recipe_MEE_GlucoseMash_FromPotato.xml from deployed Defs...
    del "%DEST_DIR%\Defs\RecipeDefs\Recipe_MEE_GlucoseMash_FromPotato.xml"
)
if exist "%DEST_DIR%\Patches\Trait_PhysiqueAptitudes.xml.bak" (
    echo Removing stale Trait_PhysiqueAptitudes.xml.bak from deployed Patches...
    del "%DEST_DIR%\Patches\Trait_PhysiqueAptitudes.xml.bak"
)
if exist "%DEST_DIR%\Languages\ChineseSimplified\DefInjected\HediffDef\Physique.xml" (
    echo Removing stale HediffDef\Physique.xml from deployed Languages...
    del "%DEST_DIR%\Languages\ChineseSimplified\DefInjected\HediffDef\Physique.xml"
)

echo.

echo ========================================
echo   Step 5: Copy content to RimWorld Mods
echo ========================================
echo.

if not exist "%DEST_DIR%" (
    echo Creating dest dir...
    mkdir "%DEST_DIR%"
)

echo Copy About folder...
xcopy /E /I /Y "%PROJECT_DIR%About" "%DEST_DIR%\About"

echo Copy Defs folder...
xcopy /E /I /Y "%PROJECT_DIR%Defs" "%DEST_DIR%\Defs"

echo Copy Patches folder...
xcopy /E /I /Y "%PROJECT_DIR%Patches" "%DEST_DIR%\Patches"

echo Copy Languages folder...
xcopy /E /I /Y "%PROJECT_DIR%Languages" "%DEST_DIR%\Languages"

echo Copy Textures folder...
xcopy /E /I /Y "%PROJECT_DIR%Textures" "%DEST_DIR%\Textures"

if exist "%PROJECT_DIR%Config" (
    echo Copy Config folder...
    xcopy /E /I /Y "%PROJECT_DIR%Config" "%DEST_DIR%\Config"
)

echo.

echo ========================================
echo   Step 6: Copy Assemblies and optional module
echo ========================================
echo.

if not exist "%DEST_ASM%" mkdir "%DEST_ASM%"

rem Deploy core assemblies. If the main DLL is locked by a running game,
rem the copy fails — detect it and warn instead of silently leaving a stale DLL.
copy /Y "%SRC_ASM%\RimHormones.dll" "%DEST_ASM%\"
if errorlevel 1 (
    echo.
    echo WARNING: RimHormones.dll could NOT be overwritten - locked by RimWorld.
    echo Close the game completely and re-run this script, or restart RimWorld
    echo to load the freshly built DLL on next launch.
    echo.
) else (
    echo Deployed core assembly: RimHormones.dll
)
if exist "%SRC_ASM%\0Harmony.dll" copy /Y "%SRC_ASM%\0Harmony.dll" "%DEST_ASM%\"

echo.
echo Deployed core assemblies:
dir /B "%DEST_ASM%\*.dll"

if exist "%META_OUT%\MetabolicEssential.dll" (
    echo.
    echo Copying optional module to MetabolicEssential\ subfolder...
    if not exist "%DEST_DIR%\MetabolicEssential" mkdir "%DEST_DIR%\MetabolicEssential"
    copy /Y "%META_OUT%\MetabolicEssential.dll" "%DEST_DIR%\MetabolicEssential\"
    echo Deployed optional module:
    dir /B "%DEST_DIR%\MetabolicEssential\*.dll"
)

echo.
echo ========================================
echo   Done!
echo   Restart RimWorld to apply changes (the Metabolic Essential toggle takes effect on restart).
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
