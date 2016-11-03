@echo off
pushd %~dp0

echo %~dp0

TCNotifier.exe --builds:%~dp0builds.txt --persons:%~dp0persons.txt --op:Install || exit 1 & popd

popd