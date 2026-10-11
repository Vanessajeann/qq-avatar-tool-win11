using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace AvatarTool {
public static class Tests {
    static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAILED: "+name); checks.Add("PASS: "+name); }
    static void Reject(Action a,string name) { bool rejected=false; try { a(); } catch { rejected=true; } Check(rejected,name); }
    sealed class Fragmented : MemoryStream { public Fragmented(byte[] b):base(b) {} public override int Read(byte[] b,int offset,int count) { return base.Read(b,offset,Math.Min(count,2)); } }
    public static void Run(string path) {
        checks.Clear(); byte[] key=new byte[104]; for(int i=0;i<key.Length;i++) key[i]=(byte)i;
        byte[] png=Images.Transparent(); Images.Validate(png); Check(Images.Png(png),"transparent PNG signature");
        using(var s=new MemoryStream(png)) using(var b=new Bitmap(s)) Check(b.GetPixel(64,64).A==0,"transparent PNG alpha is zero");
        byte[] staticPng; using(var b=new Bitmap(2,2)) using(var s=new MemoryStream()) { using(var g=Graphics.FromImage(b)) g.Clear(Color.Red); b.Save(s,System.Drawing.Imaging.ImageFormat.Png); staticPng=s.ToArray(); }
        byte[] semi=Images.Opacity(staticPng,50); using(var s=new MemoryStream(semi)) using(var b=new Bitmap(s)) Check(b.GetPixel(0,0).A>=126 && b.GetPixel(0,0).A<=128,"50 percent alpha");
        var packet=Protocol.Build("123456789",png,key,5); Check(packet[0]==0x28 && packet[packet.Length-1]==0x29,"frame delimiters");
        uint length=Protocol.U32(packet,1); Check(Protocol.U32(packet,5)==png.Length && packet.Length==length+png.Length+10,"big endian frame lengths");
        var head=new byte[(int)length]; Buffer.BlockCopy(packet,9,head,0,head.Length); var fields=PB.Parse(head); var common=PB.Parse(fields[1][0]); var segment=PB.Parse(fields[2][0]);
        Check(Encoding.UTF8.GetString(common[3][0])=="PicUp.DataUp" && BitConverter.ToUInt64(common[8][0],0)==5,"legacy command and avatar type");
        Check(BitConverter.ToString(segment[6][0])==BitConverter.ToString(key),"104 byte ticket field");
        Check(segment[8][0].Length==16 && BitConverter.ToString(segment[8][0])==BitConverter.ToString(segment[9][0]),"MD5 fields");
        var dynamicPacket=Protocol.Build("123456789",png,key,90); var dynamicHead=new byte[Protocol.U32(dynamicPacket,1)]; Buffer.BlockCopy(dynamicPacket,9,dynamicHead,0,dynamicHead.Length);
        Check(BitConverter.ToString(PB.Parse(dynamicHead)[3][0])=="00-05-00-00","small world extension field");
        Reject(delegate { Protocol.Hex("not-a-ticket"); },"invalid ticket rejected"); Reject(delegate { Protocol.Number("123&ukey=bad"); },"account query injection rejected"); Reject(delegate { Protocol.Host("x/path"); },"invalid server rejected");
        Reject(delegate { PB.Parse(new byte[]{0x3a,0xff,0x7f}); },"truncated protobuf rejected"); Reject(delegate { PB.Parse(new byte[]{0}); },"zero protobuf field rejected");
        Reject(delegate { Images.Validate(new byte[]{1,2,3}); },"invalid image rejected");
        byte[] success=new PB().Text(7,"avatar").Bytes(), response=Protocol.Frame(success,new byte[]{1,2,3});
        using(var s=new Fragmented(response)) Check(BitConverter.ToString(Protocol.ReadFrame(s))==BitConverter.ToString(success),"fragmented response read completely");
        Reject(delegate { using(var s=new MemoryStream(new byte[]{0x28,0,0,0,20,0,0,0,0})) Protocol.ReadFrame(s); },"truncated frame rejected");
        byte[] oversized={0x28,0x7f,0xff,0xff,0xff,0,0,0,0}; Reject(delegate { using(var s=new MemoryStream(oversized)) Protocol.ReadFrame(s); },"oversized response rejected");
        Check(Protocol.Response(success).Contains("field 7") && Protocol.Response(new PB().Int(1,1).Bytes()).Contains("无法确认"),"response status does not claim unverified completion");
        Check(Protocol.GroupId("20000001")=="2100000001" && Protocol.GroupId("157000001")=="2147000001" && Protocol.GroupId("500000001")=="500000001","group ID mapping");
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); int port=((IPEndPoint)listener.LocalEndpoint).Port;
        Task server=Task.Run(delegate { using(var client=listener.AcceptTcpClient()) using(var s=client.GetStream()) { for(int i=0;i<response.Length;i++) s.Write(response,i,1); } });
        using(var c=new TcpClient()) { c.Connect(IPAddress.Loopback,port); c.ReceiveTimeout=3000; using(var s=c.GetStream()) Check(Protocol.Response(Protocol.ReadFrame(s)).Contains("field 7"),"local socket fragmented receive"); } server.Wait(); listener.Stop();
        byte[] gif; using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("test.gif")) { using(var b=new MemoryStream()) { s.CopyTo(b); gif=b.ToArray(); } }
        Check(Images.Gif(gif),"animated GIF input"); Reject(delegate { Images.Opacity(gif,50); },"animated opacity does not silently flatten"); byte[] apng=Images.ToApng(gif); Check(Images.Apng(apng),"GIF to APNG animation control"); Images.Validate(apng);
        Reject(delegate { QQNTBridge.Endpoint("http://example.com/"); },"bridge excludes remote destinations");
        Reject(delegate { QQNTBridge.Endpoint("http://127.0.0.1:3000/path"); },"bridge rejects unexpected paths");
        BridgeTest(png,false); BridgeTest(png,true); BridgeTest(png,false,1); BridgeTest(png,false,2); BridgeTest(png,false,3);
        string dir=Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir,"converted-test.png"),apng); File.WriteAllBytes(Path.Combine(dir,"packet-test.bin"),packet);
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("partial.gif")) using(var b=new MemoryStream()) { s.CopyTo(b); File.WriteAllBytes(Path.Combine(dir,"partial-test.png"),Images.ToApng(b.ToArray())); }
        File.WriteAllText(path,String.Join(Environment.NewLine,checks)+Environment.NewLine+"Offline only: no QQ account, credential extraction, or Tencent upload exercised."+Environment.NewLine,Encoding.UTF8);
    }
    static void BridgeTest(byte[] image,bool mismatch,int failure=0) {
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); int port=((IPEndPoint)listener.LocalEndpoint).Port; int requests=0; bool bodyCorrect=false; string localFile="";
        int expectedRequests=(mismatch || failure==2 || failure==3)?1:2;
        Task server=Task.Run(delegate {
            for(int i=0;i<expectedRequests;i++) {
                using(var client=listener.AcceptTcpClient()) using(var stream=client.GetStream()) {
                    client.ReceiveTimeout=3000; var bytes=new List<byte>(); int n;
                    while((n=stream.ReadByte())>=0) { bytes.Add((byte)n); if(bytes.Count>=4 && Encoding.ASCII.GetString(bytes.ToArray(),bytes.Count-4,4)=="\r\n\r\n") break; if(bytes.Count>16384) throw new Exception("mock header too long"); }
                    string header=Encoding.ASCII.GetString(bytes.ToArray()); int length=0;
                    foreach(string line in header.Split(new string[]{"\r\n"},StringSplitOptions.None)) if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) length=Int32.Parse(line.Substring(15).Trim());
                    if(header.IndexOf("Expect: 100-continue",StringComparison.OrdinalIgnoreCase)>=0) { byte[] ack=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"); stream.Write(ack,0,ack.Length); }
                    byte[] body=new byte[length]; int p=0; while(p<length) { int read=stream.Read(body,p,length-p); if(read==0) throw new Exception("mock body truncated"); p+=read; }
                    requests++;
                    if(i==1) {
                        var json=new System.Web.Script.Serialization.JavaScriptSerializer(); var value=(Dictionary<string,object>)json.DeserializeObject(Encoding.UTF8.GetString(body));
                        localFile=Convert.ToString(value["file"]);
                        bodyCorrect=header.StartsWith("POST /set_qq_avatar ") && header.Contains("Bearer test-token") && Path.IsPathRooted(localFile) && File.Exists(localFile) && BitConverter.ToString(File.ReadAllBytes(localFile))==BitConverter.ToString(image);
                    }
                    string text=i==0?"{\"status\":\"ok\",\"retcode\":0,\"data\":{\"user_id\":"+(mismatch?"987654321":"123456789")+"}}":"{\"status\":\"ok\",\"retcode\":0,\"data\":null}";
                    string status="200 OK", type="application/json";
                    if(failure==1 && i==1) { string large="头像base64://"+new String('A',1200000)+"设置失败，未知的错误,1004022 token:test-token"; var json=new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength=16*1024*1024 }; text=json.Serialize(new Dictionary<string,object>{{"status","failed"},{"retcode",200},{"message",large},{"wording",large},{"data",null}}); }
                    if(failure==2) { type="text/html"; text="<html>NapCat WebUI</html>"; }
                    if(failure==3) { status="403 Forbidden"; text="{\"message\":\"token verify failed\"}"; }
                    byte[] reply=Encoding.UTF8.GetBytes(text), envelope=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+"\r\nContent-Type: "+type+"\r\nContent-Length: "+reply.Length+"\r\nConnection: close\r\n\r\n"); stream.Write(envelope,0,envelope.Length); stream.Write(reply,0,reply.Length);
                }
            }
        });
        try {
            if(failure>0) {
                string error=""; try { QQNTBridge.SetAvatar("http://127.0.0.1:"+port+"/","test-token","123456789",image); } catch(IOException e) { error=e.Message; }
                if(failure==1) Check(error.Contains("retcode=200") && error.Contains("1004022") && error.Length<500 && !error.Contains("base64://") && !error.Contains("test-token"),"large NapCat Base64 error preserves reason and redacts image/token");
                else if(failure==2) Check(error.Contains("管理网页"),"WebUI endpoint reported correctly");
                else Check(error.Contains("Token 验证失败"),"HTTP 403 identifies wrong token");
            }
            else if(mismatch) Reject(delegate { QQNTBridge.SetAvatar("http://127.0.0.1:"+port+"/","test-token","123456789",image); },"bridge prevents upload to wrong account");
            else Check(QQNTBridge.SetAvatar("http://127.0.0.1:"+port+"/","test-token","123456789",image).Contains("retcode=0"),"QQNT mock bridge success response");
            server.Wait(); Check(requests==expectedRequests && (expectedRequests==1 || bodyCorrect),mismatch?"mismatched login sends no avatar":"QQNT bridge request count and local image bytes case "+failure);
            if(localFile!="") Check(!File.Exists(localFile),"temporary image cleaned after response case "+failure);
        } finally { listener.Stop(); }
    }
}
}
