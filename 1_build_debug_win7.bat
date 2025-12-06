@echo off
REM Сборка в режиме ОТЛАДКИ (Debug) под Windows (x86), без self-contained.
cd /d "%~dp0"

dotnet restore VupisjkaWpf.csproj
dotnet publish VupisjkaWpf.csproj -c Debug -r win-x86 -o publish_debug_win7 --self-contained false

echo.
echo Если выше нет красных ошибок, EXE лежит в папке publish_debug_win7.
pause
