#pragma once

#include <windows.h>
#include <cstdint>
#include <string>

// ============================================================================
// 注入方式
// ============================================================================
enum class InjectMethod
{
    CreateRemoteThread_LoadLibrary,   // 经典 CreateRemoteThread + LoadLibraryW
    ManualMap                         // 手动映射（绕过 LoadLibrary）
};

// ============================================================================
// 注入器主类
// ============================================================================
class Injector
{
public:
    Injector();
    ~Injector();

    // 设置注入参数
    void SetTargetProcess(HANDLE hProcess);
    void SetInjectMethod(InjectMethod method);
    void SetDllPath(const std::wstring& path);

    // 执行注入，返回 true 表示成功
    bool Inject();

private:
    HANDLE m_hProcess = nullptr;
    InjectMethod m_method = InjectMethod::CreateRemoteThread_LoadLibrary;
    std::wstring m_dllPath;

    // --- 经典注入：CreateRemoteThread + LoadLibraryW ---
    bool InjectViaLoadLibrary();

    // --- 手动映射注入 ---
    bool InjectViaManualMap();
    uintptr_t AllocateMemoryInTarget(size_t size);
    bool WriteToTarget(uintptr_t remoteAddr, const void* data, size_t size);
    bool FlushRemoteInstructionCache(uintptr_t addr, size_t size);

    // 等待远程线程完成
    bool WaitForRemoteThread(HANDLE hThread, DWORD timeoutMs = 10000);
};
