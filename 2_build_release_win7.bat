@echo off
REM Сборка в режиме РЕЛИЗ (Release) под Windows (x86), без self-contained.
cd /d "%~dp0"

dotnet restore VupisjkaWpf.csproj
dotnet publish VupisjkaWpf.csproj -c Release -r win-x86 -o publish_release_win7 --self-contained false

echo.
echo Если выше нет красных ошибок, EXE лежит в папке publish_release_win7.
pause
