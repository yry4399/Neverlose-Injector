#include "injector.h"
#include "utils.h"
#include "process.h"

// ============================================================================
// 构造/析构
// ============================================================================
Injector::Injector() = default;

Injector::~Injector() = default;

void Injector::SetTargetProcess(HANDLE hProcess)     { m_hProcess = hProcess; }
void Injector::SetInjectMethod(InjectMethod method)    { m_method = method; }
void Injector::SetDllPath(const std::wstring& path)    { m_dllPath = path; }

// ============================================================================
// 公共入口
// ============================================================================
bool Injector::Inject()
{
    if (!m_hProcess || m_dllPath.empty())
        return false;

    switch (m_method)
    {
    case InjectMethod::CreateRemoteThread_LoadLibrary:
        return InjectViaLoadLibrary();
    case InjectMethod::ManualMap:
        return InjectViaManualMap();
    }
    return false;
}

// ============================================================================
// 方式 1：CreateRemoteThread + LoadLibraryW
// ============================================================================
bool Injector::InjectViaLoadLibrary()
{
    // 1. 获取 DLL 绝对路径
    wchar_t fullPath[MAX_PATH] = {};
    GetFullPathNameW(m_dllPath.c_str(), MAX_PATH, fullPath, nullptr);
    size_t pathSize = (wcslen(fullPath) + 1) * sizeof(wchar_t);

    // 2. 在目标进程分配内存
    uintptr_t remotePath = AllocateMemoryInTarget(pathSize);
    if (!remotePath)
        return false;

    // 3. 写入 DLL 路径
    if (!WriteToTarget(remotePath, fullPath, pathSize))
    {
        VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remotePath), 0, MEM_RELEASE);
        return false;
    }

    // 4. 获取 LoadLibraryW 地址
    HMODULE hKernel32 = GetModuleHandleW(L"kernel32.dll");
    if (!hKernel32)
    {
        VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remotePath), 0, MEM_RELEASE);
        return false;
    }

    FARPROC pLoadLibraryW = GetProcAddress(hKernel32, "LoadLibraryW");
    if (!pLoadLibraryW)
    {
        VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remotePath), 0, MEM_RELEASE);
        return false;
    }

    // 5. 创建远程线程
    HANDLE hThread = CreateRemoteThread(
        m_hProcess,
        nullptr,
        0,
        reinterpret_cast<LPTHREAD_START_ROUTINE>(pLoadLibraryW),
        reinterpret_cast<LPVOID>(remotePath),
        0,
        nullptr
    );

    if (!hThread)
    {
        VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remotePath), 0, MEM_RELEASE);
        return false;
    }

    // 6. 等待完成
    bool ok = WaitForRemoteThread(hThread, 15000);

    CloseHandle(hThread);
    VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remotePath), 0, MEM_RELEASE);

    return ok;
}

// ============================================================================
// 方式 2：Manual Map 手动映射
// ============================================================================
bool Injector::InjectViaManualMap()
{
    // 1. 读取 DLL 文件
    auto dllData = Util::ReadFileToMemory(m_dllPath);
    if (dllData.empty())
        return false;

    // 2. 解析 PE 头
    if (dllData.size() < sizeof(IMAGE_DOS_HEADER))
        return false;

    auto* dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(dllData.data());
    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
        return false;

    auto* ntHeader = reinterpret_cast<IMAGE_NT_HEADERS*>(dllData.data() + dosHeader->e_lfanew);
    if (ntHeader->Signature != IMAGE_NT_SIGNATURE)
        return false;

    auto& fileHeader = ntHeader->FileHeader;
    auto& optHeader = ntHeader->OptionalHeader;

    // 3. 在目标进程分配 SizeOfImage 大小的内存
    size_t imageSize = optHeader.SizeOfImage;
    uintptr_t remoteImageBase = AllocateMemoryInTarget(imageSize);
    if (!remoteImageBase)
        return false;

    // 4. 复制 PE 头部
    if (!WriteToTarget(remoteImageBase, dllData.data(), optHeader.SizeOfHeaders))
    {
        VirtualFreeEx(m_hProcess, reinterpret_cast<LPVOID>(remoteImageBase), 0, MEM_RELEASE);
        return false;
    }

    // 5. 复制节区
    auto* sections = IMAGE_FIRST_SECTION(ntHeader);
    for (WORD i = 0; i < fileHeader.NumberOfSections; ++i)
    {
        const auto& sec = sections[i];
        if (sec.SizeOfRawData == 0 || (sec.Characteristics & IMAGE_SCN_MEM_DISCARDABLE))
            continue;

        uintptr_t remoteVA = remoteImageBase + sec.VirtualAddress;
        uintptr_t localOffset = sec.PointerToRawData;
        uint32_t copySize = sec.SizeOfRawData;

        if (sec.Misc.VirtualSize > copySize)
        {
            WriteToTarget(remoteVA, dllData.data() + localOffset, copySize);
            std::vector<uint8_t> zeros(sec.Misc.VirtualSize - copySize, 0);
            WriteToTarget(remoteVA + copySize, zeros.data(), zeros.size());
        }
        else
        {
            WriteToTarget(remoteVA, dllData.data() + localOffset, copySize);
        }
    }

    // 6. 更新保护属性
    for (WORD i = 0; i < fileHeader.NumberOfSections; ++i)
    {
        const auto& sec = sections[i];
        DWORD protect = PAGE_READONLY;
        DWORD c = sec.Characteristics;

        if (c & IMAGE_SCN_MEM_EXECUTE)
            protect = (c & IMAGE_SCN_MEM_WRITE) ? PAGE_EXECUTE_READWRITE : PAGE_EXECUTE_READ;
        else if (c & IMAGE_SCN_MEM_WRITE)
            protect = PAGE_READWRITE;

        Util::VirtualProtectRemote(m_hProcess, remoteImageBase + sec.VirtualAddress,
            sec.Misc.VirtualSize, protect, nullptr);
    }

    // 7. 处理重定位表
    auto& relocDir = optHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC];
    if (relocDir.Size > 0)
    {
        uintptr_t relocRVA = relocDir.VirtualAddress;
        uintptr_t localRelocVA = reinterpret_cast<uintptr_t>(dllData.data()) + relocRVA;

        // 重定位 delta（有符号）
        int64_t delta = static_cast<int64_t>(remoteImageBase) -
                        static_cast<int64_t>(optHeader.ImageBase);

        if (delta != 0)
        {
            auto* relocBlock = reinterpret_cast<IMAGE_BASE_RELOCATION*>(localRelocVA);
            uintptr_t relocEnd = localRelocVA + relocDir.Size;

            while (reinterpret_cast<uintptr_t>(relocBlock) < relocEnd && relocBlock->SizeOfBlock > 0)
            {
                size_t entries = (relocBlock->SizeOfBlock - sizeof(IMAGE_BASE_RELOCATION)) / sizeof(WORD);
                auto* relocEntries = reinterpret_cast<WORD*>(
                    reinterpret_cast<uintptr_t>(relocBlock) + sizeof(IMAGE_BASE_RELOCATION));

                for (size_t j = 0; j < entries; ++j)
                {
                    WORD type = relocEntries[j] >> 12;
                    WORD offset = relocEntries[j] & 0x0FFF;

                    if (type == IMAGE_REL_BASED_DIR64)
                    {
                        uintptr_t targetAddr = remoteImageBase + relocBlock->VirtualAddress + offset;
                        uintptr_t originalValue = 0;
                        Util::ReadRemoteMemory(m_hProcess, targetAddr, &originalValue, sizeof(originalValue));
                        uint64_t newVal = static_cast<uint64_t>(originalValue) + static_cast<uint64_t>(delta);
                        Util::WriteRemoteMemory(m_hProcess, targetAddr, &newVal, sizeof(newVal));
                    }
                }

                relocBlock = reinterpret_cast<IMAGE_BASE_RELOCATION*>(
                    reinterpret_cast<uintptr_t>(relocBlock) + relocBlock->SizeOfBlock);
            }
        }
    }

    // 8. 刷新指令缓存
    FlushRemoteInstructionCache(remoteImageBase, imageSize);

    // 注意：完整的 Manual Map 还需要处理导入表、TLS 回调、DllMain 调用等
    // 这些需要在目标进程中执行 LoadLibrary/GetProcAddress 来填充 IAT
    // 这里提供框架，完整实现需要更多远程线程协作

    return true;
}

// ============================================================================
// 辅助方法
// ============================================================================

uintptr_t Injector::AllocateMemoryInTarget(size_t size)
{
    LPVOID addr = VirtualAllocEx(
        m_hProcess,
        nullptr,
        size,
        MEM_COMMIT | MEM_RESERVE,
        PAGE_READWRITE
    );
    return reinterpret_cast<uintptr_t>(addr);
}

bool Injector::WriteToTarget(uintptr_t remoteAddr, const void* data, size_t size)
{
    SIZE_T written = 0;
    return WriteProcessMemory(m_hProcess,
        reinterpret_cast<LPVOID>(remoteAddr), data, size, &written) == TRUE &&
        written == size;
}

bool Injector::FlushRemoteInstructionCache(uintptr_t addr, size_t size)
{
    return FlushInstructionCache(m_hProcess,
        reinterpret_cast<LPCVOID>(addr), size) == TRUE;
}

bool Injector::WaitForRemoteThread(HANDLE hThread, DWORD timeoutMs)
{
    if (!hThread) return false;
    DWORD result = WaitForSingleObject(hThread, timeoutMs);
    return result == WAIT_OBJECT_0;
}
