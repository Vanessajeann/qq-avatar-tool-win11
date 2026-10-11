using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AvatarTool {
// Only called after the user selects a legacy QQ process and clicks the button.
// Read-only: no DLL injection, remote thread, shellcode, or process writes.
public static class LegacyKey {
    [StructLayout(LayoutKind.Sequential)] struct MBI { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public UIntPtr RegionSize; public uint State,Protect,Type; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr read);
    [DllImport("kernel32.dll")] static extern UIntPtr VirtualQueryEx(IntPtr h,IntPtr p,out MBI mbi,UIntPtr len);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsWow64Process(IntPtr h,out bool wow);
    public static List<string> Read(int pid) {
        using(var p=Process.GetProcessById(pid)) {
            if(!String.Equals(p.ProcessName,"QQ",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请选择 QQ.exe 进程。");
            bool legacy=false; foreach(ProcessModule m in p.Modules) if(String.Equals(m.ModuleName,"KernelUtil.dll",StringComparison.OrdinalIgnoreCase)) legacy=true;
            if(!legacy) throw new ArgumentException("这个进程不是支持的怀旧版 QQ：未发现 KernelUtil.dll。QQNT 不使用此提取方式。");
        }
        IntPtr h=OpenProcess(0x410,false,pid); if(h==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"无法读取旧 QQ，请确认它正在运行；必要时以管理员身份运行本工具。");
        try {
            bool wow; if(Environment.Is64BitOperatingSystem && (!IsWow64Process(h,out wow) || !wow)) throw new ArgumentException("只支持怀旧版 32 位 QQ 进程。");
            byte[] pattern={0x88,1,1,0x92,1,0x68}; var result=new List<string>(); long address=0; var timer=Stopwatch.StartNew();
            int size=Marshal.SizeOf(typeof(MBI)); const int block=1024*1024, overlap=160;
            while(address<0x100000000L && timer.ElapsedMilliseconds<20000) {
                MBI mbi; if(VirtualQueryEx(h,new IntPtr(address),out mbi,new UIntPtr((uint)size))==UIntPtr.Zero) break;
                long begin=mbi.BaseAddress.ToInt64(),length=(long)mbi.RegionSize.ToUInt64(),end=begin+length;
                if(end<=address) break;
                uint protection=mbi.Protect&255; bool readable=protection==2 || protection==4 || protection==8 || protection==0x20 || protection==0x40 || protection==0x80;
                if(mbi.State==0x1000 && readable && (mbi.Protect&0x100)==0) {
                    for(long offset=begin;offset<end;offset+=block-overlap) {
                        if(timer.ElapsedMilliseconds>=20000) throw new TimeoutException("扫描旧 QQ 内存超时；请关闭多余 QQ 窗口后重试。");
                        int n=(int)Math.Min(block,end-offset); byte[] buffer=new byte[n]; UIntPtr read;
                        if(!ReadProcessMemory(h,new IntPtr(offset),buffer,new UIntPtr((uint)n),out read)) continue;
                        int used=(int)read.ToUInt64();
                        for(int i=21;i+117<used;i++) {
                            bool match=true; for(int j=0;j<pattern.Length;j++) if(buffer[i+j]!=pattern[j]) { match=false; break; }
                            if(!match) continue; int start=i-21;
                            if((buffer[start+1]!=16 && buffer[start+2]!=16) || buffer[start+131]!=152) continue;
                            byte[] ticket=new byte[104]; Buffer.BlockCopy(buffer,start+27,ticket,0,104); string key=BitConverter.ToString(ticket).Replace("-",""); if(!result.Contains(key)) result.Add(key);
                            if(result.Count>=32) return result;
                        }
                    }
                } address=end;
            }
            if(timer.ElapsedMilliseconds>=20000) throw new TimeoutException("扫描旧 QQ 内存超时。"); return result;
        } finally { CloseHandle(h); }
    }
}
}
