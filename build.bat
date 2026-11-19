@echo off
REM Compila BezaresChat.exe con el compilador de C# que incluye Windows (.NET Framework 4.x). No requiere instalar nada.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:BezaresChat.exe ^
  /win32icon:icon.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Web.Extensions.dll /r:System.Security.dll ^
  BezaresChat.cs
if errorlevel 1 (echo ERROR al compilar & pause & exit /b 1)
echo Listo: BezaresChat.exe
