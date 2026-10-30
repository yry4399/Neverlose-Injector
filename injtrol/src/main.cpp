#include <windows.h>
#include <iostream>
#include <string>
#include <vector>
#include <cstdio>

#include "injector.h"
#include "process.h"
#include "utils.h"

// ============================================================================
// 字符串资源（硬编码在原 PE .rdata 段中的关键字符串）
// ============================================================================
namespace Strings
{
    constexpr const wchar_t* kUsage =
        L"Loader - Windows DLL Injector\n"
        L"\n"
        L"Usage:\n"
        L"  loader.exe -target <process_name> -dll <path_to_dll> [options]\n"
        L"\n"
        L"Options:\n"
        L"  -method loadlibrary   注入方式：CreateRemoteThread + LoadLibraryW（默认）\n"
        L"  -method manual        注入方式：手动映射（Manual Map）\n"
        L"  -pid <id>             直接指定目标进程 PID\n"
        L"  -wait <seconds>       等待目标进程启动超时（默认 30 秒）\n"
        L"  -h, -help             显示帮助\n"
        L"\n"
        L"Examples:\n"
        L"  loader.exe -target cs2.exe -dll \"C:\\mods\\mydll.dll\"\n"
        L"  loader.exe -pid 1234 -dll \"C:\\mods\\mydll.dll\" -method manual\n";

    constexpr const wchar_t* kModuleKernel32 = L"kernel32.dll";
    constexpr const wchar_t* kModuleNtdll   = L"ntdll.dll";
    constexpr const wchar_t* kLoadLibraryW   = L"LoadLibraryW";
}

// ============================================================================
// 命令行参数解析
// ============================================================================
struct CommandLine
{
    std::wstring targetName;
    std::wstring dllPath;
    DWORD targetPid = 0;
    InjectMethod method = InjectMethod::CreateRemoteThread_LoadLibrary;
    int waitSeconds = 30;
    bool showHelp = false;
};

static CommandLine ParseCommandLine(int argc, wchar_t* argv[])
{
    CommandLine cmd;

    for (int i = 1; i < argc; ++i)
    {
        std::wstring arg = argv[i];
        if (arg == L"-h" || arg == L"-help" || arg == L"/?")
        {
            cmd.showHelp = true;
        }
        else if (arg == L"-target" && i + 1 < argc)
        {
            cmd.targetName = argv[++i];
        }
        else if (arg == L"-dll" && i + 1 < argc)
        {
            cmd.dllPath = argv[++i];
        }
        else if (arg == L"-pid" && i + 1 < argc)
        {
            cmd.targetPid = (DWORD)_wtoi(argv[++i]);
        }
        else if (arg == L"-method" && i + 1 < argc)
        {
            std::wstring m = argv[++i];
            if (m == L"manual") cmd.method = InjectMethod::ManualMap;
            else cmd.method = InjectMethod::CreateRemoteThread_LoadLibrary;
        }
        else if (arg == L"-wait" && i + 1 < argc)
        {
            cmd.waitSeconds = _wtoi(argv[++i]);
        }
    }

    return cmd;
}

// ============================================================================
// 主流程
// ============================================================================
static int MainLogic(const CommandLine& cmd)
{
    // 反调试初始化
    Util::InstallAntiDebug();

    // 验证参数
    if (cmd.showHelp || (cmd.targetName.empty() && cmd.targetPid == 0) || cmd.dllPath.empty())
    {
        wprintf(L"%s\n", Strings::kUsage);
        return cmd.showHelp ? 0 : 1;
    }

    if (!Util::FileExists(cmd.dllPath))
    {
        wprintf(L"[ERROR] DLL 文件不存在: %s\n", cmd.dllPath.c_str());
        return 1;
    }

    // 查找目标进程
    ProcessInfo targetProc;

    if (cmd.targetPid != 0)
    {
        // 直接按 PID 打开
        targetProc.pid = cmd.targetPid;
        targetProc.hProcess = ProcessManager::OpenTargetProcess(cmd.targetPid);
        if (!targetProc.hProcess)
        {
            wprintf(L"[ERROR] 无法打开进程 PID=%lu (%s)\n",
                cmd.targetPid, Util::GetLastErrorAsString().c_str());
            return 1;
        }
    }
    else
    {
        // 按名称查找，等待启动
        wprintf(L"[INFO] 查找目标进程: %s (等待 %d 秒)...\n",
            cmd.targetName.c_str(), cmd.waitSeconds);

        auto processes = ProcessManager::FindProcessesByName(cmd.targetName);
        DWORD waitedMs = 0;
        while (processes.empty() && waitedMs < (DWORD)cmd.waitSeconds * 1000)
        {
            Sleep(500);
            waitedMs += 500;
            processes = ProcessManager::FindProcessesByName(cmd.targetName);
        }

        if (processes.empty())
        {
            wprintf(L"[ERROR] 未找到目标进程: %s\n", cmd.targetName.c_str());
            return 1;
        }

        targetProc = processes.front();
        wprintf(L"[INFO] 找到进程: PID=%lu Name=%s Path=%s WoW64=%s\n",
            targetProc.pid, targetProc.name.c_str(),
            targetProc.path.c_str(),
            targetProc.isWoW64 ? L"yes" : L"no");

        // 确认 hProcess 有效
        if (!targetProc.hProcess)
        {
            targetProc.hProcess = ProcessManager::OpenTargetProcess(targetProc.pid);
        }

        if (!targetProc.hProcess)
        {
            wprintf(L"[ERROR] 无法打开进程 PID=%lu (%s)\n",
                targetProc.pid, Util::GetLastErrorAsString().c_str());
            return 1;
        }
    }

    // 执行注入
    Injector injector;
    injector.SetTargetProcess(targetProc.hProcess);
    injector.SetInjectMethod(cmd.method);
    injector.SetDllPath(cmd.dllPath);

    const wchar_t* methodName =
        cmd.method == InjectMethod::ManualMap ? L"Manual Map" : L"CreateRemoteThread+LoadLibrary";

    wprintf(L"[INFO] 执行注入...\n");
    wprintf(L"       方式 : %s\n", methodName);
    wprintf(L"       PID  : %lu\n", targetProc.pid);
    wprintf(L"       DLL  : %s\n", cmd.dllPath.c_str());

    bool ok = injector.Inject();

    if (ok)
    {
        wprintf(L"[SUCCESS] 注入完成！\n");
    }
    else
    {
        wprintf(L"[FAIL] 注入失败 (%s)\n", Util::GetLastErrorAsString().c_str());
    }

    if (targetProc.hProcess)
        CloseHandle(targetProc.hProcess);

    return ok ? 0 : 1;
}

// ============================================================================
// 入口点 —— WinMain（GUI 子系统，隐藏控制台）
// ============================================================================
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance,
    LPSTR lpCmdLine, int nShowCmd)
{
    // 解析命令行（宽字符版本）
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv)
        return 1;

    auto cmd = ParseCommandLine(argc, argv);
    LocalFree(argv);

    // 为 GUI 子系统附加控制台（便于调试输出）
    if (AttachConsole(ATTACH_PARENT_PROCESS))
    {
        FILE* fp = nullptr;
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        freopen_s(&fp, "CONIN$", "r", stdin);
    }

    int result = MainLogic(cmd);

    FreeConsole();
    return result;
}

// ============================================================================
// 备用 C 运行时入口（兼容编译）
// ============================================================================
int main(int argc, char* argv[])
{
    // 将 char** 转换为 wchar_t**
    std::vector<std::wstring> wideArgs;
    wideArgs.push_back(L"loader.exe");
    for (int i = 1; i < argc; ++i)
        wideArgs.push_back(Util::ToWide(argv[i]));

    std::vector<wchar_t*> argvPtrs;
    for (auto& s : wideArgs)
        argvPtrs.push_back(const_cast<wchar_t*>(s.c_str()));

    auto cmd = ParseCommandLine(wideArgs.size(), argvPtrs.data());
    return MainLogic(cmd);
}
