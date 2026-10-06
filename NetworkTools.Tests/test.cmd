@echo off
rem Runs the tests outside the game on Windows, after a mod build.
rem Under Mono every test runs, in both float modes. Without Mono, dotnet test runs them on the
rem .NET Framework, where the tests that allocate native collections skip themselves.
rem Set MONO to the path of mono.exe when Mono is not in its default folder.
setlocal
cd /d "%~dp0"

set RULE=================================================================================

if not defined MONO set MONO=%ProgramFiles%\Mono\bin\mono.exe

if not exist "%MONO%" (
    echo %RULE%
    echo  Runtime: .NET Framework. Mono not found at "%MONO%".
    echo  To run every test, install Mono: winget install Mono.Mono
    echo  The tests that allocate native collections skip themselves.
    echo %RULE%
    echo.
    dotnet test -c Release -p:BuildProjectReferences=false
    exit /b
)

echo %RULE%
echo  Runtime: Mono, "%MONO%". Every test runs.
echo %RULE%
echo.
echo Building the tests.
dotnet build -c Release -p:BuildProjectReferences=false -v q -nologo || exit /b

for /f "delims=" %%p in ('dotnet msbuild -getProperty:PkgNUnit_ConsoleRunner') do (
    set RUNNER=%%p\tools\nunit3-console.exe
)

set TESTS=bin\Release\net48\NetworkTools.Tests.dll
set OPTIONS=--inprocess --noresult --noheader

echo.
echo %RULE%
echo  Pass 1 of 2. Float: single precision, as in Burst jobs.
echo %RULE%
"%MONO%" "%RUNNER%" %TESTS% %OPTIONS% || exit /b

echo.
echo %RULE%
echo  Pass 2 of 2. Float: double precision, as under the game's Mono.
echo  MONO_ENV_OPTIONS=-O=-float32
echo %RULE%
set MONO_ENV_OPTIONS=-O=-float32
"%MONO%" "%RUNNER%" %TESTS% %OPTIONS%
