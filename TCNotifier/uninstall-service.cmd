@echo off
pushd %~dp0

echo %~dp0

TCNotifier.exe --op:Uninstall || exit 1 & popd

popd