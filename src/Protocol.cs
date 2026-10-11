using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AvatarTool {
public sealed class PB {
    readonly MemoryStream data = new MemoryStream();
    public static void Var(Stream s, ulong n) { while (n >= 128) { s.WriteByte((byte)(n | 128)); n >>= 7; } s.WriteByte((byte)n); }
    public PB Int(int field, ulong value) { Var(data, (ulong)(field << 3)); Var(data, value); return this; }
    public PB Bin(int field, byte[] value) { Var(data, (ulong)((field << 3) | 2)); Var(data, (ulong)value.Length); data.Write(value,0,value.Length); return this; }
    public PB Text(int field, string value) { return Bin(field, Encoding.UTF8.GetBytes(value)); }
    public byte[] Bytes() { return data.ToArray(); }
    static ulong ReadVar(byte[] b, ref int p) {
        ulong v=0; for(int i=0;i<10;i++) { if(p>=b.Length) throw new InvalidDataException("响应中的 protobuf 被截断。"); byte c=b[p++]; if(i==9 && c>1) throw new InvalidDataException("protobuf 整数溢出。"); v |= (ulong)(c&127) << (7*i); if((c&128)==0) return v; }
        throw new InvalidDataException("protobuf 整数过长。");
    }
    public static Dictionary<int,List<byte[]>> Parse(byte[] b) {
        var result=new Dictionary<int,List<byte[]>>(); int p=0;
        while(p<b.Length) {
            ulong tag=ReadVar(b,ref p); ulong fieldValue=tag>>3; if(fieldValue==0 || fieldValue>536870911) throw new InvalidDataException("非法 protobuf 字段。"); int field=(int)fieldValue, wire=(int)(tag&7); byte[] value;
            if(wire==0) value=BitConverter.GetBytes(ReadVar(b,ref p));
            else { ulong len; if(wire==2) len=ReadVar(b,ref p); else if(wire==1) len=8; else if(wire==5) len=4; else throw new InvalidDataException("不支持的 protobuf 编码。");
                if(len>(ulong)(b.Length-p)) throw new InvalidDataException("protobuf 字段长度越界。"); value=new byte[(int)len]; Buffer.BlockCopy(b,p,value,0,value.Length); p+=value.Length; }
            if(!result.ContainsKey(field)) result[field]=new List<byte[]>(); result[field].Add(value);
        } return result;
    }
}
public static class Protocol {
    public const string DefaultHost="120.232.130.72";
    public static byte[] Hex(string text) {
        text=Regex.Replace(text ?? "", @"\s+", "");
        if(text.Length!=208 || !Regex.IsMatch(text,@"\A[0-9a-fA-F]+\z")) throw new ArgumentException("UKey 必须是原工具使用的 104 字节票据（208 个十六进制字符），不能用 skey、p_skey 或 Cookie 代替。");
        var b=new byte[text.Length/2]; for(int i=0;i<b.Length;i++) b[i]=Convert.ToByte(text.Substring(i*2,2),16); return b;
    }
    public static string Number(string n) { if(!Regex.IsMatch(n ?? "",@"\A[1-9][0-9]{4,19}\z")) throw new ArgumentException("请输入有效的 QQ 号或群号。"); return n; }
    public static void Host(string h) { if(String.IsNullOrWhiteSpace(h) || Uri.CheckHostName(h)==UriHostNameType.Unknown || h.Contains(":") || h.Contains("/")) throw new ArgumentException("服务器只允许 IPv4 地址或域名。"); }
    public static void BE(Stream s, uint n) { s.WriteByte((byte)(n>>24)); s.WriteByte((byte)(n>>16)); s.WriteByte((byte)(n>>8)); s.WriteByte((byte)n); }
    public static uint U32(byte[] b,int p) { return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3]; }
    public static byte[] Frame(byte[] head,byte[] body) { using(var s=new MemoryStream()) { s.WriteByte(0x28); BE(s,(uint)head.Length); BE(s,(uint)body.Length); s.Write(head,0,head.Length); s.Write(body,0,body.Length); s.WriteByte(0x29); return s.ToArray(); } }
    public static byte[] Build(string uin, byte[] image, byte[] ticket, int kind) {
        Number(uin); if(ticket.Length!=104) throw new ArgumentException("票据长度错误。"); if(kind!=5 && kind!=90 && kind!=21) throw new ArgumentException("上传类型错误。");
        byte[] md5; using(var m=MD5.Create()) md5=m.ComputeHash(image);
        var baseHead=new PB().Int(1,1).Text(2,uin).Text(3,"PicUp.DataUp").Int(4,92607).Int(5,0).Int(6,537064446).Int(7,4096).Int(8,(ulong)kind);
        var segment=new PB().Int(2,(ulong)image.Length).Int(3,0).Int(4,(ulong)image.Length).Bin(6,ticket).Bin(8,md5).Bin(9,md5);
        var h=new PB().Bin(1,baseHead.Bytes()).Bin(2,segment.Bytes()); if(kind==90) h.Bin(3,new byte[]{0,5,0,0});
        return Frame(h.Bytes(),image);
    }
    static byte[] ReadExactly(Stream s,int n) { var b=new byte[n]; int p=0; while(p<n) { int r=s.Read(b,p,n-p); if(r==0) throw new EndOfStreamException("服务器提前关闭连接，响应不完整。"); p+=r; } return b; }
    public static byte[] ReadFrame(Stream s) {
        byte[] first=ReadExactly(s,9); if(first[0]!=0x28) throw new InvalidDataException("服务器未返回原头像协议，可能是接口已失效。");
        uint h=U32(first,1),body=U32(first,5); if(h>1024*1024 || body>8*1024*1024) throw new InvalidDataException("服务器响应过大。");
        byte[] head=ReadExactly(s,(int)h); ReadExactly(s,(int)body); if(s.ReadByte()!=0x29) throw new InvalidDataException("响应结束标记无效。"); return head;
    }
    public static string Response(byte[] head) {
        var p=PB.Parse(head); List<byte[]> values;
        if(p.TryGetValue(7,out values) && values.Count>0 && values[0].Length>0)
            return "收到原工具使用的 field 7 成功标记。请在 QQ 中刷新并核对实际头像；不同客户端可能只显示静态图。";
        return "服务器已响应，但没有原工具使用的 field 7 成功标记。无法确认上传成功；票据可能过期，或旧接口已变化。";
    }
    public static string Upload(string host,string uin,byte[] image,byte[] ticket,int kind) {
        Host(host); byte[] packet=Build(uin,image,ticket,kind);
        using(var c=new TcpClient()) {
            var connect=c.BeginConnect(host,kind==90?443:80,null,null);
            using(var wait=connect.AsyncWaitHandle) { if(!wait.WaitOne(8000)) throw new TimeoutException("连接服务器超时。"); c.EndConnect(connect); }
            c.SendTimeout=20000; c.ReceiveTimeout=20000;
            using(var stream=c.GetStream()) { stream.Write(packet,0,packet.Length); return Response(ReadFrame(stream)); }
        }
    }
    public static string GroupId(string group) {
        Number(group); ulong n=UInt64.Parse(group), left=n/1000000, tail=n%1000000;
        if(left<=10) left+=202; else if(left<=19) left+=469; else if(left<=66) left+=2080;
        else if(left<=156) left+=1943; else if(left<=209) left+=1990; else if(left<=309) left+=3890;
        else if(left<=335) left+=3490; else if(left<=386) left+=2265; else if(left<=499) left+=3490;
        else return group;
        return (left*1000000+tail).ToString();
    }
    public static string UploadGroup(string host,string uin,string group,byte[] image,byte[] ticket,bool avatar) {
        Host(host); Number(uin); string gid=GroupId(group);
        string url="http://"+host+"/cgi-bin/httpconn?htcmd=0x6ff0607&ver=5971&uin="+uin+"&groupuin="+gid+"&seq=0&filetype=2&imagetype=2&userdata=1&range=0&filesize="+image.Length+"&ukey="+BitConverter.ToString(ticket).Replace("-","")+"&sigtype=1"+(avatar?"&subver=101&subcmd=1":"");
        var request=(HttpWebRequest)WebRequest.Create(url); request.Method="POST"; request.ContentType="application/octet-stream"; request.AllowAutoRedirect=false; request.Timeout=20000; request.ReadWriteTimeout=20000; request.ContentLength=image.Length;
        try { using(var s=request.GetRequestStream()) s.Write(image,0,image.Length); using(var response=(HttpWebResponse)request.GetResponse()) {
            bool hint=(response.Headers.ToString().IndexOf(group,StringComparison.Ordinal)>=0);
            return "群上传服务器返回 HTTP "+(int)response.StatusCode+"。"+(hint?"响应头包含原工具使用的群号标记。":"未找到原工具使用的群号标记。")+" 请在 QQ 中确认实际结果；HTTP 200 本身不代表更换成功。";
        }} catch(WebException) { throw new IOException("群上传请求失败（可能是票据过期、群权限不足或旧接口失效）。"); }
    }
}
}
