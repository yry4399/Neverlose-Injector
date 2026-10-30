@echo off
REM ============================================================
REM  Loader Build Script (LLVM-MinGW / clang++)
REM ============================================================
REM  编译器: x86_64-w64-mingw32-clang++ (LLVM-MinGW UCRT)
REM  安装方式: winget install MartinStorsjo.LLVM-MinGW.UCRT
REM ============================================================

setlocal

set "CLANGPP=C:\Users\33814\AppData\Local\Microsoft\WinGet\Packages\MartinStorsjo.LLVM-MinGW.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\llvm-mingw-20261020-ucrt-x86_64\bin\x86_64-w64-mingw32-clang++.exe"
set "CFLAGS=-std=c++17 -O2 -s -static -DUNICODE -D_UNICODE -D_WIN32_WINNT=0x0601 -DWIN32_LEAN_AND_MEAN -Wno-unused-variable -Wno-unused-but-set-variable"
set "LIBS=-lkernel32 -lntdll"

if not exist "%CLANGPP%" (
    echo [ERROR] clang++ not found at: %CLANGPP%
    echo         Install with: winget install MartinStorsjo.LLVM-MinGW.UCRT
    exit /b 1
)

echo [1/3] Creating build directory...
if not exist build mkdir build

echo [2/3] Compiling GUI version (loader.exe)...
"%CLANGPP%" %CFLAGS% -mwindows src/main.cpp src/injector.cpp src/process.cpp src/utils.cpp -o build/loader.exe %LIBS%
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] GUI build failed
    exit /b 1
)

echo [3/3] Compiling CLI version (loader_cli.exe)...
"%CLANGPP%" %CFLAGS% -mconsole src/main.cpp src/injector.cpp src/process.cpp src/utils.cpp -o build/loader_cli.exe %LIBS%
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] CLI build failed
    exit /b 1
)

echo.
echo ============================================================
echo  Build SUCCESS!
echo ============================================================
echo  build\loader.exe      - GUI subsystem (no console window)
echo  build\loader_cli.exe  - Console subsystem (for debugging)
echo ============================================================
dir build\loader.exe
dir build\loader_cli.exe

endlocal
