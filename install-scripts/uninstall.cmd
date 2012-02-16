@echo off
echo Window will close itself when finished...
echo.
set DESTDIR=c:\stateservice
REM remove registry entry
reg delete HKLM\Software\rdkl.us\StateService /f
REM uninstall the service
installutil /u %DESTDIR%\stateservice.exe
del %DESTDIR%\stateservice.exe