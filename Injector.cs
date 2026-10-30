using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Loader
{
    /// <summary>
    /// 封装注入器的调用。
    /// 注入器与要注入的 DLL 在打包时经 set-dll.ps1 AES 加密为 cheat\injector.dat / cheat\payload.dat，
    /// 运行时解密到 %TEMP% 下随机文件名，注入完成后立即删除。
    /// </summary>
    internal static class Injector
    {
        private static readonly object _lock = new object();
        private static string _extractedLoaderPath;
        private static string _extractedDllPath;
        private static string _payloadName;          // 解密头部里的原始 DLL 文件名
        private static bool _payloadNameResolved;

        public enum Method
        {
            /// <summary>CreateRemoteThread + LoadLibraryW（经典，兼容性好）</summary>
            LoadLibrary,
            /// <summary>手动映射 Manual Map</summary>
            ManualMap
        }

        public sealed class Result
        {
            public bool Success { get; internal set; }
            public string Message { get; internal set; }
            public int ExitCode { get; internal set; }
        }

        // ─── 嵌入资源查找 ───

        private static string FindResource(string suffix)
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return null;
        }

        private static byte[] ReadResourceBytes(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("无法读取嵌入资源 " + resourceName);
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        private static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "NWLoader");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string RandomTempPath(string ext)
        {
            return Path.Combine(TempDir(), Guid.NewGuid().ToString("N").Substring(0, 12) + ext);
        }

        private static string MissingPayloadHint()
        {
            return "程序内没有内置 DLL。请先在项目目录运行 set-dll.ps1（或双击 set-dll.bat）选择 DLL 并重新打包。";
        }

        // ─── 解密 + 落盘 ───

        /// <summary>解密注入器到临时随机文件名 exe</summary>
        private static string EnsureLoaderExtracted()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_extractedLoaderPath) && File.Exists(_extractedLoaderPath))
                    return _extractedLoaderPath;

                string res = FindResource(".injector.dat");
                if (res == null)
                    throw new FileNotFoundException(
                        "程序内没有内置注入器。请运行 set-dll.ps1（或 set-dll.bat）重新打包。");

                byte[] plain = Crypto.Decrypt(ReadResourceBytes(res));
                string target = RandomTempPath(".exe");
                File.WriteAllBytes(target, plain);

                _extractedLoaderPath = target;
                return target;
            }
        }

        /// <summary>解密内置 DLL。明文格式: [2字节名字长][UTF8文件名][DLL字节]</summary>
        private static string EnsurePayloadExtracted()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_extractedDllPath) && File.Exists(_extractedDllPath))
                    return _extractedDllPath;

                string res = FindResource(".payload.dat");
                if (res == null)
                    throw new FileNotFoundException(MissingPayloadHint());

                byte[] plain = Crypto.Decrypt(ReadResourceBytes(res));
                if (plain.Length < 2)
                    throw new InvalidDataException("内置 DLL 数据损坏");

                int nameLen = plain[0] | (plain[1] << 8);
                if (nameLen < 1 || plain.Length < 2 + nameLen + 1)
                    throw new InvalidDataException("内置 DLL 数据损坏");

                _payloadName = Encoding.UTF8.GetString(plain, 2, nameLen);
                _payloadNameResolved = true;

                string target = RandomTempPath(".dll");
                using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                    fs.Write(plain, 2 + nameLen, plain.Length - 2 - nameLen);

                _extractedDllPath = target;
                return target;
            }
        }

        /// <summary>内置 DLL 的原始文件名（未内置时返回 null）</summary>
        public static string PayloadFileName
        {
            get
            {
                lock (_lock)
                {
                    if (_payloadNameResolved)
                        return _payloadName;

                    // 只解头部拿名字，不落盘
                    try
                    {
                        string res = FindResource(".payload.dat");
                        if (res == null) { _payloadNameResolved = true; return null; }

                        byte[] plain = Crypto.Decrypt(ReadResourceBytes(res));
                        if (plain.Length >= 2)
                        {
                            int nameLen = plain[0] | (plain[1] << 8);
                            if (nameLen >= 1 && plain.Length >= 2 + nameLen)
                                _payloadName = Encoding.UTF8.GetString(plain, 2, nameLen);
                        }
                    }
                    catch { _payloadName = null; }

                    _payloadNameResolved = true;
                    return _payloadName;
                }
            }
        }

        /// <summary>删除临时解密的文件</summary>
        public static void Cleanup()
        {
            lock (_lock)
            {
                TryDelete(_extractedLoaderPath); _extractedLoaderPath = null;
                TryDelete(_extractedDllPath); _extractedDllPath = null;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ─── 注入 ───

        /// <summary>
        /// 执行注入（异步）。DLL 与注入器均为内置加密资源，用完即删。
        /// </summary>
        public static async Task<Result> InjectAsync(
            string processName,
            Method method = Method.LoadLibrary,
            int waitSeconds = 30)
        {
            try
            {
                string loaderPath = EnsureLoaderExtracted();
                string dllPath = EnsurePayloadExtracted();

                string methodArg = method == Method.ManualMap ? "manual" : "loadlibrary";

                var args = new StringBuilder();
                args.Append("-target ").Append(Quote(processName));
                args.Append(" -dll ").Append(Quote(dllPath));
                args.Append(" -method ").Append(methodArg);
                args.Append(" -wait ").Append(waitSeconds);

                var psi = new ProcessStartInfo
                {
                    FileName = loaderPath,
                    Arguments = args.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Unicode,
                    StandardErrorEncoding = Encoding.Unicode
                };

                using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = false })
                {
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();

                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    await Task.Run(() => proc.WaitForExit());

                    int exitCode = proc.ExitCode;
                    string combined = (stdout.ToString() + "\n" + stderr.ToString()).Trim();

                    if (exitCode == 0)
                        return new Result { Success = true, Message = combined, ExitCode = 0 };

                    string msg = string.IsNullOrEmpty(combined)
                        ? $"注入失败 (退出码 {exitCode})"
                        : combined;
                    return new Result { Success = false, Message = msg, ExitCode = exitCode };
                }
            }
            catch (Exception ex)
            {
                return new Result { Success = false, Message = ex.Message, ExitCode = -2 };
            }
            finally
            {
                Cleanup();
            }
        }

        private static string Quote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            if (s.IndexOf(' ') >= 0 || s.IndexOf('"') >= 0)
                return "\"" + s.Replace("\"", "\\\"") + "\"";
            return s;
        }
    }
}
