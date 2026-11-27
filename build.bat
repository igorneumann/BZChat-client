@echo off
REM Compila release\BezaresChat.exe con el compilador de C# que incluye Windows (.NET Framework 4.x). No requiere instalar nada.
REM Cierra antes Bezares Chat si lo tienes abierto desde release\ (un .exe en uso no se puede sobrescribir).
cd /d "%~dp0"
if not exist release mkdir release
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:release\BezaresChat.exe ^
  /win32icon:icon.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Web.Extensions.dll /r:System.Security.dll ^
  BezaresChat.cs
if errorlevel 1 (echo ERROR al compilar. Si Bezares Chat esta abierto desde release\, cierralo y vuelve a intentarlo. & pause & exit /b 1)
echo Listo: release\BezaresChat.exe
