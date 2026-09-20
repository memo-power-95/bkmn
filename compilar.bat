@echo off
rem Compila Rebith en modo Release. Requiere Visual Studio 2019/2022 o el SDK de .NET.
rem Resultado: Rebith.App\bin\Release\net48\  (copie esa carpeta completa)
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if %errorlevel%==0 (
  dotnet build Rebith.App\Rebith.App.csproj -c Release
) else (
  for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
  "%MSBUILD%" Rebith.App\Rebith.App.csproj /restore /p:Configuration=Release
)
if errorlevel 1 (
  echo.
  echo *** La compilacion fallo. ***
  pause
  exit /b 1
)
echo.
echo Listo: Rebith.App\bin\Release\net48\Rebith.exe
pause
