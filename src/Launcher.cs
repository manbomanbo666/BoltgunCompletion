using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace BoltgunCompletion {
public static class AppPaths {
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public static string Runtime { get { return Path.Combine(Root, "runtime"); } }
    public static string Log { get { return Path.Combine(Runtime, "UE4SS.log"); } }
    public static string Dll { get { return Path.Combine(Runtime, "UE4SS.dll"); } }
    public static string GameExe(string game) { return Path.Combine(game,"Boltgun","Binaries","Win64","Boltgun-Win64-Shipping.exe"); }
    public static string Hash(string path) { using(var f=File.OpenRead(path)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
    public const string SupportedHash="bd32aecb2ef6feb3fd2edfb0095b7e534176c92c01d5fa5ccc43a0ddc639b01a";
    public static readonly string Revision=LoadRevision();
    static string LoadRevision() {try{return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"plugin-release.json"),Encoding.UTF8))["revision"];}catch{return "missing-release-manifest";}}
}
public static class GameLocator {
    public static string Find() {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}) {
            var value=Registry.GetValue(key,key.Contains("CURRENT_USER")?"SteamPath":"InstallPath",null) as string;
            if(!String.IsNullOrEmpty(value)) roots.Add(value);
        }
        foreach(string root in roots.ToArray()) {
            string library=Path.Combine(root,"steamapps","libraryfolders.vdf");
            if(!File.Exists(library)) continue;
            foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(library),"\"path\"\\s+\"([^\"]+)\"")) roots.Add(m.Groups[1].Value.Replace("\\\\","\\"));
        }
        foreach(string root in roots) {
            string manifest=Path.Combine(root,"steamapps","appmanifest_2005010.acf");
            if(!File.Exists(manifest)) continue;
            var m=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s+\"([^\"]+)\"");
            if(m.Success) {
                string result=Path.GetFullPath(Path.Combine(root,"steamapps","common",m.Groups[1].Value));
                if(File.Exists(AppPaths.GameExe(result))) return result;
            }
        }
        return null;
    }
    public static Process Running(string game) {
        if(String.IsNullOrEmpty(game)) return null;
        var matches=new List<Process>();
        foreach(var p in Process.GetProcessesByName("Boltgun-Win64-Shipping")) {
            try { if(String.Equals(Path.GetFullPath(p.MainModule.FileName),Path.GetFullPath(AppPaths.GameExe(game)),StringComparison.OrdinalIgnoreCase)) matches.Add(p); } catch { }
        }
        if(matches.Count>1)throw new InvalidOperationException("检测到多个一代游戏进程，请保留一个实例后再连接。");
        return matches.Count==1?matches[0]:null;
    }
    public static void Validate(string game) {
        if(!File.Exists(AppPaths.GameExe(game))) throw new InvalidOperationException("未找到一代游戏主程序。请选择 Boltgun 安装目录。");
        if(AppPaths.Hash(AppPaths.GameExe(game))!=AppPaths.SupportedHash) throw new InvalidOperationException("此游戏版本尚未适配，已停止接入。不会读取未经验证的地址。");
    }
}
public static class RuntimeConnector {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int id);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr address,UIntPtr size,uint allocation,uint protect);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualFreeEx(IntPtr p,IntPtr address,UIntPtr size,uint type);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr p,IntPtr address,byte[] data,UIntPtr size,out UIntPtr written);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi,ExactSpelling=true,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr p,IntPtr security,UIntPtr stack,IntPtr start,IntPtr param,uint flags,out uint threadId);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint ms);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static void Fail(string text) { throw new Win32Exception(Marshal.GetLastWin32Error(),text); }
    public static bool AlreadyLoaded(Process process) {
        foreach(ProcessModule m in process.Modules) {
            if(!m.ModuleName.Equals("UE4SS.dll",StringComparison.OrdinalIgnoreCase)) continue;
            if(!Path.GetFullPath(m.FileName).Equals(Path.GetFullPath(AppPaths.Dll),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("游戏已加载其他位置的 UE4SS。请先关闭游戏并停用其他加载器，避免同时加载。");
            return true;
        }
        return false;
    }
    public static void Connect(string game) {
        GameLocator.Validate(game);
        var process=GameLocator.Running(game);
        if(process==null) throw new InvalidOperationException("请先启动一代游戏，再点击连接。");
        bool alreadyLoaded=AlreadyLoaded(process);
        if(!File.Exists(AppPaths.Dll)) throw new FileNotFoundException("插件加载器缺失，请重新解压完整安装包。",AppPaths.Dll);
        string hashes=Path.Combine(AppPaths.Root,"package-files.json");
        var expected=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(hashes,Encoding.UTF8));
        foreach(var pair in expected) {
            string path=Path.GetFullPath(Path.Combine(AppPaths.Root,pair.Key.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(AppPaths.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || AppPaths.Hash(path)!=pair.Value)
                throw new InvalidOperationException("插件文件校验未通过："+pair.Key+"。请重新解压完整安装包。");
        }
        if(alreadyLoaded) return;
        IntPtr load=GetProcAddress(GetModuleHandle("kernel32.dll"),"LoadLibraryW");
        if(load==IntPtr.Zero) Fail("找不到 Windows 加载入口。");
        ProcessModule local=null;
        foreach(ProcessModule m in Process.GetCurrentProcess().Modules) if(load.ToInt64()>=m.BaseAddress.ToInt64() && load.ToInt64()<m.BaseAddress.ToInt64()+m.ModuleMemorySize) { local=m; break; }
        if(local==null) throw new InvalidOperationException("无法定位 Windows 加载模块。");
        ProcessModule remote=null;
        foreach(ProcessModule m in process.Modules) if(m.ModuleName.Equals(local.ModuleName,StringComparison.OrdinalIgnoreCase)) { remote=m; break; }
        if(remote==null) throw new InvalidOperationException("目标进程缺少 Windows 加载模块。");
        IntPtr entry=new IntPtr(remote.BaseAddress.ToInt64()+load.ToInt64()-local.BaseAddress.ToInt64());
        byte[] data=Encoding.Unicode.GetBytes(Path.GetFullPath(AppPaths.Dll)+"\0");
        IntPtr handle=OpenProcess(0x2|0x8|0x20|0x400|0x10,false,process.Id);
        if(handle==IntPtr.Zero) Fail("无法连接游戏进程，请确认助手和游戏以同一用户权限运行。");
        IntPtr buffer=IntPtr.Zero,thread=IntPtr.Zero;
        bool release=true;
        try {
            buffer=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)data.Length,0x3000,0x04);
            if(buffer==IntPtr.Zero) Fail("无法分配插件加载参数。");
            UIntPtr written;
            if(!WriteProcessMemory(handle,buffer,data,(UIntPtr)data.Length,out written) || written.ToUInt64()!=(ulong)data.Length) Fail("无法传递插件加载路径。");
            uint id;
            thread=CreateRemoteThread(handle,IntPtr.Zero,UIntPtr.Zero,entry,buffer,0,out id);
            if(thread==IntPtr.Zero) Fail("无法启动插件加载。");
            uint result=WaitForSingleObject(thread,45000);
            if(result!=0) { release=false; throw new InvalidOperationException("加载仍在等待中，请检查 runtime/UE4SS.log；关闭游戏可完整停止插件。"); }
            process.Refresh();
            if(!AlreadyLoaded(process)) throw new InvalidOperationException("Windows 未能加载插件，请检查依赖和日志。");
        } finally {
            if(thread!=IntPtr.Zero) CloseHandle(thread);
            if(buffer!=IntPtr.Zero && release) VirtualFreeEx(handle,buffer,UIntPtr.Zero,0x8000);
            CloseHandle(handle);
        }
    }
}
public static class Program {
    [STAThread] public static int Main(string[] args) {
        try {
            if(args.Contains("--render-preview"))return SelfTests.Render(args.Last());
            string game=GameLocator.Find();
            if(args.Contains("--check")) { Console.WriteLine(game??"NOT_FOUND"); if(game!=null) { GameLocator.Validate(game); Console.WriteLine("SUPPORTED"); } return game==null?1:0; }
            if(args.Contains("--connect")) {
                if(game==null) throw new InvalidOperationException("未找到游戏");
                var tail=new LogTail(AppPaths.Log);var states=new SnapshotStore();var elapsed=Stopwatch.StartNew();
                RuntimeConnector.Connect(game);
                bool ready=false;long lastSeq=0;
                int timeout=args.Contains("--watch")?120000:20000;
                while(elapsed.ElapsedMilliseconds<timeout) {
                    tail.Poll(line=>states.Accept(line,elapsed.ElapsedMilliseconds),null);
                    var snapshot=states.Current;
                    if(snapshot!=null && snapshot.seq!=lastSeq) {
                        lastSeq=snapshot.seq;
                        Console.WriteLine(new JavaScriptSerializer().Serialize(snapshot));
                        if(snapshot.mode!="initializing"&&snapshot.mode!="error") {
                            ready=true;if(!args.Contains("--watch")){Console.WriteLine("BGC_READY");return 0;}
                        }
                    }
                    Thread.Sleep(250);
                }
                if(!ready)throw new InvalidOperationException("加载器存在，但未收到有效的游戏数据；插件尚未就绪。请保留 runtime/UE4SS.log。");
                return 0;
            }
            if(args.Contains("--selftest")) return SelfTests.Run();
            if(args.Contains("--launch")) { Process.Start(new ProcessStartInfo("steam://rungameid/2005010"){UseShellExecute=true}); return 0; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(game)); return 0;
        } catch(Exception error) { if(args.Length>0) Console.Error.WriteLine(error); else MessageBox.Show(error.Message,"爆弹枪完美通关助手",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
}
}
