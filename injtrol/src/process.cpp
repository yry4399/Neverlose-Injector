#include "process.h"
#include "utils.h"
#include <algorithm>

// ============================================================================
// ProcessManager 实现
// ============================================================================

std::vector<ProcessInfo> ProcessManager::FindProcessesByName(const std::wstring& name)
{
    std::vector<ProcessInfo> result;

    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE)
        return result;

    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);

    if (Process32FirstW(hSnapshot, &entry))
    {
        do
        {
            std::wstring procName(entry.szExeFile);
            // 支持部分匹配，不区分大小写
            std::wstring lowerName = procName;
            std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(),
                [](wchar_t c) { return (wchar_t)towlower(c); });
            std::wstring lowerTarget = name;
            std::transform(lowerTarget.begin(), lowerTarget.end(), lowerTarget.begin(),
                [](wchar_t c) { return (wchar_t)towlower(c); });

            if (lowerName.find(lowerTarget) != std::wstring::npos)
            {
                ProcessInfo pi;
                pi.pid = entry.th32ProcessID;
                pi.parentPid = entry.th32ParentProcessID;
                pi.name = procName;
                pi.isWoW64 = false;
                result.push_back(pi);
            }
        } while (Process32NextW(hSnapshot, &entry));
    }

    CloseHandle(hSnapshot);

    // 填充路径和 WoW64 信息
    for (auto& pi : result)
    {
        pi.hProcess = OpenTargetProcess(pi.pid);
        if (pi.hProcess)
        {
            // 查询完整路径
            wchar_t pathBuf[MAX_PATH] = {};
            DWORD pathSize = MAX_PATH;
            if (QueryFullProcessImageNameW(pi.hProcess, 0, pathBuf, &pathSize))
                pi.path = pathBuf;

            // 查询 WoW64
            pi.isWoW64 = IsWoW64Process(pi.hProcess);
        }
    }

    return result;
}

std::vector<ProcessInfo> ProcessManager::FindProcessesByPath(const std::wstring& path)
{
    auto all = FindProcessesByName(L""); // 空字符串匹配所有
    std::vector<ProcessInfo> result;
    for (auto& pi : all)
    {
        if (!pi.path.empty() && pi.path == path)
            result.push_back(std::move(pi));
        else if (pi.hProcess)
            CloseHandle(pi.hProcess);
    }
    return result;
}

HANDLE ProcessManager::OpenTargetProcess(DWORD pid)
{
    return OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
}

bool ProcessManager::IsWoW64Process(HANDLE hProcess)
{
    if (!hProcess)
        return false;

    // IsWow64Process2 在较新的系统上也能用于原生进程
    typedef BOOL(WINAPI* IsWow64Process2_t)(HANDLE, USHORT*, USHORT*);
    static auto fn = reinterpret_cast<IsWow64Process2_t>(
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "IsWow64Process2"));

    if (fn)
    {
        USHORT processMachine = 0;
        USHORT nativeMachine = 0;
        if (fn(hProcess, &processMachine, &nativeMachine))
            return processMachine != 0; // 非0表示WoW64
    }

    // 回退到 IsWow64Process
    typedef BOOL(WINAPI* IsWow64Process_t)(HANDLE, PBOOL);
    static auto fn2 = reinterpret_cast<IsWow64Process_t>(
        GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "IsWow64Process"));

    if (fn2)
    {
        BOOL isWow = FALSE;
        if (fn2(hProcess, &isWow))
            return isWow == TRUE;
    }

    return false;
}

bool ProcessManager::IsSystem64Bit()
{
#if defined(_WIN64)
    return true;
#else
    BOOL is64 = FALSE;
    if (IsWow64Process(GetCurrentProcess(), &is64))
        return is64 == FALSE; // 32位程序在64位系统上会返回TRUE
    return false;
#endif
}

// ============================================================================
// ModuleManager 实现
// ============================================================================

std::vector<ModuleInfo> ModuleManager::EnumerateModules(HANDLE hProcess)
{
    std::vector<ModuleInfo> result;
    if (!hProcess) return result;

    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
        GetProcessId(hProcess));
    if (hSnapshot == INVALID_HANDLE_VALUE)
        return result;

    MODULEENTRY32W entry{};
    entry.dwSize = sizeof(entry);

    if (Module32FirstW(hSnapshot, &entry))
    {
        do
        {
            ModuleInfo mi;
            mi.name = entry.szModule;
            mi.path = entry.szExePath;
            mi.baseAddress = reinterpret_cast<uintptr_t>(entry.modBaseAddr);
            mi.size = entry.modBaseSize;
            result.push_back(mi);
        } while (Module32NextW(hSnapshot, &entry));
    }

    CloseHandle(hSnapshot);
    return result;
}

ModuleInfo ModuleManager::FindModuleByName(HANDLE hProcess, const std::wstring& name)
{
    auto modules = EnumerateModules(hProcess);
    std::wstring lowerTarget = name;
    std::transform(lowerTarget.begin(), lowerTarget.end(), lowerTarget.begin(),
        [](wchar_t c) { return (wchar_t)towlower(c); });

    for (auto& m : modules)
    {
        std::wstring lowerName = m.name;
        std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(),
            [](wchar_t c) { return (wchar_t)towlower(c); });
        if (lowerName.find(lowerTarget) != std::wstring::npos)
            return m;
    }
    return {};
}
