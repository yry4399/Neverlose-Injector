#include "utils.h"
#include <fstream>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <cstdint>

// ============================================================================
// 反调试
// ============================================================================

void Util::InstallAntiDebug()
{
    // 设置异常处理过滤器，一旦检测到调试器则终止进程
    SetUnhandledExceptionFilter([](EXCEPTION_POINTERS*) -> LONG {
        TerminateProcess(GetCurrentProcess(), 0);
        return EXCEPTION_EXECUTE_HANDLER;
    });
}

bool Util::IsDebuggerPresentEx()
{
    // 方法 1：IsDebuggerPresent API
    if (IsDebuggerPresent())
        return true;

    // 方法 2：NtQueryInformationProcess 查询 ProcessDebugPort
    typedef LONG(NTAPI* pNtQueryInformationProcess)(
        HANDLE, UINT, PVOID, ULONG, PULONG);

    auto hNtdll = GetModuleHandleW(L"ntdll.dll");
    if (hNtdll)
    {
        auto pNtQuery = reinterpret_cast<pNtQueryInformationProcess>(
            GetProcAddress(hNtdll, "NtQueryInformationProcess"));

        if (pNtQuery)
        {
            // ProcessDebugPort = 7
            DWORD_PTR debugPort = 0;
            if (pNtQuery(GetCurrentProcess(), 7, &debugPort, sizeof(debugPort), nullptr) == 0)
            {
                if (debugPort != 0)
                    return true;
            }
        }
    }

    return false;
}

bool Util::IsBeingDebuggedPEB()
{
#if defined(_WIN64)
    // x64: PEB 在 gs:[0x60]，BeingDebugged 在 0x02
    BYTE beingDebugged = 0;
    __asm__ __volatile__ (
        "mov %%gs:0x60, %%rax\n\t"
        "mov 0x02(%%rax), %%al\n\t"
        : "=a"(beingDebugged)
        :
        : "memory"
    );
    return beingDebugged != 0;
#else
    BYTE beingDebugged = 0;
    __asm__ __volatile__ (
        "mov %%fs:0x30, %%eax\n\t"
        "mov 0x02(%%eax), %%al\n\t"
        : "=a"(beingDebugged)
        :
        : "memory"
    );
    return beingDebugged != 0;
#endif
}

// ============================================================================
// 字符串工具
// ============================================================================

std::wstring Util::ToWide(const std::string& s)
{
    if (s.empty()) return {};
    int len = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, nullptr, 0);
    if (len <= 0) return {};
    std::wstring result(len, 0);
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), -1, result.data(), len);
    return result;
}

std::string Util::ToNarrow(const std::wstring& s)
{
    if (s.empty()) return {};
    int len = WideCharToMultiByte(CP_UTF8, 0, s.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (len <= 0) return {};
    std::string result(len, 0);
    WideCharToMultiByte(CP_UTF8, 0, s.c_str(), -1, result.data(), len, nullptr, nullptr);
    return result;
}

// ============================================================================
// 文件工具
// ============================================================================

std::vector<uint8_t> Util::ReadFileToMemory(const std::wstring& path)
{
    // 用 _wfopen 避免 wstring 到 char 的编码问题
    FILE* fp = _wfopen(path.c_str(), L"rb");
    if (!fp) return {};

    if (_fseeki64(fp, 0, SEEK_END) != 0)
    {
        fclose(fp);
        return {};
    }
    auto size = _ftelli64(fp);
    if (size <= 0)
    {
        fclose(fp);
        return {};
    }
    _fseeki64(fp, 0, SEEK_SET);

    std::vector<uint8_t> buffer(static_cast<size_t>(size));
    size_t read = fread(buffer.data(), 1, static_cast<size_t>(size), fp);
    fclose(fp);

    if (read != static_cast<size_t>(size))
        return {};

    return buffer;
}

bool Util::FileExists(const std::wstring& path)
{
    return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
}

std::wstring Util::GetExecutableDirectory()
{
    wchar_t path[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    std::wstring fullPath(path);
    auto pos = fullPath.find_last_of(L"\\/");
    if (pos != std::wstring::npos)
        return fullPath.substr(0, pos);
    return L".";
}

// ============================================================================
// 错误处理
// ============================================================================

std::wstring Util::GetLastErrorAsString()
{
    DWORD err = GetLastError();
    if (err == 0)
        return L"(no error)";

    wchar_t buf[512] = {};
    DWORD flags = FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS;
    DWORD len = FormatMessageW(flags, nullptr, err, 0, buf, 512, nullptr);
    if (len > 0)
    {
        while (len > 0 && (buf[len - 1] == L'\r' || buf[len - 1] == L'\n'))
            buf[--len] = 0;
        return std::wstring(buf);
    }
    return L"(FormatMessage failed)";
}

// ============================================================================
// 内存操作
// ============================================================================

uintptr_t Util::AllocateRemoteMemory(HANDLE hProcess, size_t size, DWORD protect)
{
    return reinterpret_cast<uintptr_t>(
        VirtualAllocEx(hProcess, nullptr, size, MEM_COMMIT | MEM_RESERVE, protect));
}

bool Util::WriteRemoteMemory(HANDLE hProcess, uintptr_t remoteAddr, const void* data, size_t size)
{
    SIZE_T written = 0;
    return WriteProcessMemory(hProcess,
        reinterpret_cast<LPVOID>(remoteAddr), data, size, &written) == TRUE;
}

bool Util::ReadRemoteMemory(HANDLE hProcess, uintptr_t remoteAddr, void* out, size_t size)
{
    SIZE_T read = 0;
    return ReadProcessMemory(hProcess,
        reinterpret_cast<LPCVOID>(remoteAddr), out, size, &read) == TRUE;
}

bool Util::VirtualProtectRemote(HANDLE hProcess, uintptr_t remoteAddr, size_t size,
    DWORD newProtect, DWORD* oldProtect)
{
    return VirtualProtectEx(hProcess,
        reinterpret_cast<LPVOID>(remoteAddr), size, newProtect, oldProtect) == TRUE;
}
