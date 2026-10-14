@echo off
setlocal
rem Chay tu dung Backend de global.json chon SDK .NET 8. Khong doc .env cua team.
pushd "%~dp0.."
dotnet build JungleBackend.sln
if errorlevel 1 goto failed
dotnet run --project Tests/NetworkChecks/NetworkChecks.csproj
if errorlevel 1 goto failed
rem Co the dat PYTHON_EXE la duong dan Python 3 rieng tren may.
if not defined PYTHON_EXE set "PYTHON_EXE=python"
"%PYTHON_EXE%" -X utf8 Tests/auth_smoke.py
if errorlevel 1 goto failed
"%PYTHON_EXE%" -X utf8 Tests/room_config_checks.py
if errorlevel 1 goto failed
"%PYTHON_EXE%" -X utf8 Tools/room_benchmark.py --functional-only
if errorlevel 1 goto failed
echo PASS: API, Swagger, MongoDB, RoomCode, JWT, UDP and tick checks.
popd
exit /b 0
:failed
echo FAIL: Read the check output above. Docker Desktop and Python 3 must be available.
popd
exit /b 1
