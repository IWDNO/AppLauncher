@echo off
rem Сборка App Launcher встроенным в Windows компилятором C# (.NET Framework 4.x).
rem Результат: bin\AppLauncher.exe
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 ^
  /out:bin\AppLauncher.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /recurse:src\*.cs
if errorlevel 1 (
  echo.
  echo Сборка не удалась. Если exe занят — закройте все окна App Launcher.
  exit /b 1
)
echo Готово: bin\AppLauncher.exe
