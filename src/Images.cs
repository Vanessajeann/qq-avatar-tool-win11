using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace AvatarTool {
public static class Images {
    public static bool Gif(byte[] b) { return b.Length>=6 && Encoding.ASCII.GetString(b,0,6).StartsWith("GIF8"); }
    public static bool Png(byte[] b) { byte[] sig={137,80,78,71,13,10,26,10}; if(b.Length<8) return false; for(int i=0;i<8;i++) if(b[i]!=sig[i]) return false; return true; }
    public static bool Apng(byte[] b) {
        if(!Png(b)) return false; int p=8;
        while(p+12<=b.Length) { uint n=Protocol.U32(b,p); if(n>(uint)(b.Length-p-12)) throw new InvalidDataException("PNG 数据被截断。"); if(Encoding.ASCII.GetString(b,p+4,4)=="acTL") return true; p+=(int)n+12; } return false;
    }
    public static void Validate(byte[] b) {
        if(b==null || b.Length==0 || b.Length>3*1024*1024) throw new ArgumentException("图片必须非空且不超过 3 MiB。");
        bool jpg=b.Length>=3 && b[0]==255 && b[1]==216 && b[2]==255;
        if(!Png(b) && !Gif(b) && !jpg) throw new ArgumentException("只支持 PNG、APNG、JPG 和 GIF 图片。");
        using(var s=new MemoryStream(b)) using(var img=Image.FromStream(s,true,true)) {
            if(img.Width>4096 || img.Height>4096 || (long)img.Width*img.Height>16777216) throw new ArgumentException("图片尺寸不能超过 4096×4096。");
            int count=Gif(b)?img.GetFrameCount(FrameDimension.Time):1;
            if(count>300 || (long)count*img.Width*img.Height>128000000) throw new ArgumentException("动图帧数或总像素过大，请先压缩。");
        }
    }
    public static byte[] Transparent() { using(var b=new Bitmap(128,128,PixelFormat.Format32bppArgb)) using(var s=new MemoryStream()) { b.Save(s,ImageFormat.Png); return s.ToArray(); } }
    public static byte[] Opacity(byte[] source,int percent) {
        if(percent==100) return (byte[])source.Clone();
        if(Gif(source) || Apng(source)) throw new ArgumentException("半透明处理仅适用于静态图片；请保持动图透明度为 100%，避免丢失动画。");
        using(var stream=new MemoryStream(source)) using(var img=Image.FromStream(stream)) using(var result=new Bitmap(img.Width,img.Height,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(result)) using(var attr=new ImageAttributes()) using(var output=new MemoryStream()) {
            var m=new ColorMatrix(); m.Matrix33=percent/100f; attr.SetColorMatrix(m); g.DrawImage(img,new Rectangle(0,0,img.Width,img.Height),0,0,img.Width,img.Height,GraphicsUnit.Pixel,attr); result.Save(output,ImageFormat.Png); return output.ToArray();
        }
    }
    static uint Crc(byte[] b) { uint c=0xffffffff; foreach(byte x in b) { c^=x; for(int i=0;i<8;i++) c=(c&1)!=0?(c>>1)^0xedb88320:c>>1; } return c^0xffffffff; }
    static void Chunk(Stream s,string type,byte[] value) { byte[] t=Encoding.ASCII.GetBytes(type), crcData=new byte[value.Length+4]; Buffer.BlockCopy(t,0,crcData,0,4); Buffer.BlockCopy(value,0,crcData,4,value.Length); Protocol.BE(s,(uint)value.Length); s.Write(t,0,4); s.Write(value,0,value.Length); Protocol.BE(s,Crc(crcData)); }
    public static byte[] ToApng(byte[] source) {
        Validate(source); if(!Gif(source)) { if(!Png(source)) throw new ArgumentException("小世界动态头像只支持 GIF（自动转 APNG）或 PNG/APNG。"); return (byte[])source.Clone(); }
        using(var input=new MemoryStream(source)) using(var img=Image.FromStream(input)) using(var output=new MemoryStream()) {
            int count=img.GetFrameCount(FrameDimension.Time); int[] delays=new int[count];
            try { byte[] d=img.GetPropertyItem(0x5100).Value; for(int i=0;i<count && i*4+4<=d.Length;i++) delays[i]=BitConverter.ToInt32(d,i*4); } catch(ArgumentException) { }
            byte[] sig={137,80,78,71,13,10,26,10}; output.Write(sig,0,8); uint seq=0;
            for(int i=0;i<count;i++) {
                img.SelectActiveFrame(FrameDimension.Time,i);
                byte[] png;
                using(var canvas=new Bitmap(img.Width,img.Height,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(canvas)) using(var buffer=new MemoryStream()) { g.Clear(Color.Transparent); g.DrawImage(img,0,0,img.Width,img.Height); canvas.Save(buffer,ImageFormat.Png); png=buffer.ToArray(); }
                int p=8; var idats=new List<byte[]>();
                while(p+12<=png.Length) {
                    int len=(int)Protocol.U32(png,p); if(len<0 || len>png.Length-p-12) throw new InvalidDataException("转换后的 PNG 数据异常。"); string type=Encoding.ASCII.GetString(png,p+4,4); byte[] value=new byte[len]; Buffer.BlockCopy(png,p+8,value,0,len);
                    if(i==0 && type=="IHDR") { Chunk(output,"IHDR",value); using(var a=new MemoryStream()) { Protocol.BE(a,(uint)count); Protocol.BE(a,0); Chunk(output,"acTL",a.ToArray()); } }
                    if(type=="IDAT") idats.Add(value); p+=12+len;
                }
                using(var ctl=new MemoryStream()) { Protocol.BE(ctl,seq++); Protocol.BE(ctl,(uint)img.Width); Protocol.BE(ctl,(uint)img.Height); Protocol.BE(ctl,0); Protocol.BE(ctl,0); int delay=delays[i]<=0?10:Math.Min(delays[i],65535); ctl.WriteByte((byte)(delay>>8)); ctl.WriteByte((byte)delay); ctl.WriteByte(0); ctl.WriteByte(100); ctl.WriteByte(0); ctl.WriteByte(0); Chunk(output,"fcTL",ctl.ToArray()); }
                foreach(byte[] data in idats) {
                    if(i==0) Chunk(output,"IDAT",data);
                    else using(var fd=new MemoryStream()) { Protocol.BE(fd,seq++); fd.Write(data,0,data.Length); Chunk(output,"fdAT",fd.ToArray()); }
                }
                if(output.Length>3*1024*1024) throw new ArgumentException("GIF 转 APNG 后超过 3 MiB，请减少尺寸或帧数。");
            }
            Chunk(output,"IEND",new byte[0]); return output.ToArray();
        }
    }
}
}
