@echo off
REM Compila release\BezaresChat.exe con el compilador de C# que incluye Windows (.NET Framework 4.x). No requiere instalar nada.
REM Cierra antes Bezares Chat si lo tienes abierto desde release\ (un .exe en uso no se puede sobrescribir).
cd /d "%~dp0"
if not exist release mkdir release
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
REM Version del programa (fichero VERSION, sube 0.1 en cada commit): se muestra abajo a la derecha.
set VER=1.0
if exist VERSION set /p VER=<VERSION
> "%TEMP%\bzc-version.cs" echo [assembly: System.Reflection.AssemblyInformationalVersion("%VER%")]
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:release\BezaresChat.exe ^
  /win32icon:icon.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:System.Web.Extensions.dll /r:System.Security.dll ^
  BezaresChat.cs "%TEMP%\bzc-version.cs"
if errorlevel 1 (echo ERROR al compilar. Si Bezares Chat esta abierto desde release\, cierralo y vuelve a intentarlo. & pause & exit /b 1)
echo Listo: release\BezaresChat.exe
