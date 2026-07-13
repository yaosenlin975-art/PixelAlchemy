@echo off
REM ================================================================================
REM NoitaCA Fantasy.Net 服务端发布脚本
REM Responsibility: Publish Main entry project to ./publish in Release.
REM ================================================================================
setlocal

REM 切到仓库根目录 (脚本放在 Server/ 下,父目录即仓库根)
pushd "%~dp0\..\"

dotnet publish Server/Main/Main.csproj -c Release -o ./publish
if errorlevel 1 goto :error

popd
echo [PUBLISH OK] NoitaCA server published to ./publish.
exit /b 0

:error
popd
echo [PUBLISH FAIL] dotnet publish failed.
exit /b 1
