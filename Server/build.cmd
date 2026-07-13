@echo off
REM ================================================================================
REM NoitaCA Fantasy.Net 服务端构建脚本
REM Responsibility: Restore + build the entire Server.sln in Release.
REM ================================================================================
setlocal

REM 切到仓库根目录 (脚本放在 Server/ 下,父目录即仓库根)
pushd "%~dp0\..\"

dotnet restore
if errorlevel 1 goto :error

dotnet build Server/Server.sln -c Release
if errorlevel 1 goto :error

popd
echo [BUILD OK] NoitaCA server solution built successfully.
exit /b 0

:error
popd
echo [BUILD FAIL] dotnet build failed.
exit /b 1
