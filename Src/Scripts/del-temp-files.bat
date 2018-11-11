del /f /s /q C:\Users\service.tfsbuild.ap\AppData\Local\Temp
for /D %%i IN (C:\Users\service.tfsbuild.ap\AppData\Local\Temp\*) DO RD /s /q "%%i"