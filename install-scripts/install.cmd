@echo off
echo Window will close itself when finished...
echo.
set DESTDIR=c:\stateservice
REM install the registry entry
reg add HKLM\Software\rdkl.us\StateService /v SERVER_URL /t REG_SZ /d http://status-collector-fqdn/comp_mgmt/ /f
REM install the service
mkdir %DESTDIR%
copy stateservice.exe %DESTDIR%\
installutil %DESTDIR%\stateservice.exe
net start stateservice