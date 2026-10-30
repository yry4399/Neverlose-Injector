#pragma once

#include <windows.h>
#include <shellapi.h>   // CommandLineToArgvW
#include <tlhelp32.h>
#include <string>
#include <vector>
#include <cstdint>
#include <memory>

// ============================================================================
// 数据结构：描述一个进程及其注入相关信息
// ============================================================================
struct ProcessInfo
{
    DWORD pid = 0;
    DWORD parentPid = 0;
    std::wstring name;
    std::wstring path;
    bool isWoW64 = false;   // 是否是32位进程（在64位系统上）
    HANDLE hProcess = nullptr;
};

// ============================================================================
// 进程/模块枚举工具
// ============================================================================
class ProcessManager
{
public:
    // 根据可执行文件名查找进程（支持部分匹配）
    static std::vector<ProcessInfo> FindProcessesByName(const std::wstring& name);

    // 根据完整路径查找进程
    static std::vector<ProcessInfo> FindProcessesByPath(const std::wstring& path);

    // 打开进程（请求 PROCESS_ALL_ACCESS）
    static HANDLE OpenTargetProcess(DWORD pid);

    // 判断目标进程是否为 WoW64 (32位)
    static bool IsWoW64Process(HANDLE hProcess);

    // 获取当前系统架构是否为 x64
    static bool IsSystem64Bit();
};

// ============================================================================
// 模块信息
// ============================================================================
struct ModuleInfo
{
    std::wstring name;
    std::wstring path;
    uintptr_t baseAddress = 0;
    uint32_t size = 0;
};

class ModuleManager
{
public:
    static std::vector<ModuleInfo> EnumerateModules(HANDLE hProcess);
    static ModuleInfo FindModuleByName(HANDLE hProcess, const std::wstring& name);
};
