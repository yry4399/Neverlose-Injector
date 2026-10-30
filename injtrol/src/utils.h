#pragma once

#include <windows.h>
#include <string>
#include <vector>

// ============================================================================
// 工具函数 & 反调试
// ============================================================================
namespace Util
{
    // --- 反调试 ---
    void InstallAntiDebug();
    bool IsDebuggerPresentEx();
    bool IsBeingDebuggedPEB();

    // --- 字符串工具 ---
    std::wstring ToWide(const std::string& s);
    std::string ToNarrow(const std::wstring& s);

    // --- 文件工具 ---
    std::vector<uint8_t> ReadFileToMemory(const std::wstring& path);
    bool FileExists(const std::wstring& path);
    std::wstring GetExecutableDirectory();

    // --- 错误处理 ---
    std::wstring GetLastErrorAsString();

    // --- 内存操作 ---
    uintptr_t AllocateRemoteMemory(HANDLE hProcess, size_t size, DWORD protect);
    bool WriteRemoteMemory(HANDLE hProcess, uintptr_t remoteAddr, const void* data, size_t size);
    bool ReadRemoteMemory(HANDLE hProcess, uintptr_t remoteAddr, void* out, size_t size);
    bool VirtualProtectRemote(HANDLE hProcess, uintptr_t remoteAddr, size_t size, DWORD newProtect, DWORD* oldProtect);
}
