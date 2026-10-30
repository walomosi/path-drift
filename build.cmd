@echo off
setlocal
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /out:bin\PathDrift.exe ^
  Program.cs MainForm.cs ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
if errorlevel 1 (
  echo Build FAILED.
  exit /b 1
)
echo Build OK: bin\PathDrift.exe
