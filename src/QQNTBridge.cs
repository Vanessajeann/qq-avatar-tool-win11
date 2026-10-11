using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AvatarTool {
// Optional adapter to an existing, user-configured local NapCat HTTP service.
// No QQ installation modification or automatic bridge installation.
public static class QQNTBridge {
    const int MaxResponseBytes=16*1024*1024;
    public static Uri Endpoint(string url) {
        Uri uri; if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || uri.Scheme!="http" || uri.Host!="127.0.0.1" || uri.AbsolutePath!="/" || uri.Query!="" || uri.Fragment!="" || uri.UserInfo!="")
            throw new ArgumentException("QQNT 桥接地址必须是 http://127.0.0.1:端口/，例如 http://127.0.0.1:3000/。");
        return uri;
    }
    static Dictionary<string,object> Call(Uri endpoint,string token,string action,object payload) {
        var json=new JavaScriptSerializer { MaxJsonLength=MaxResponseBytes };
        byte[] body=Encoding.UTF8.GetBytes(json.Serialize(payload));
        var request=(HttpWebRequest)WebRequest.Create(new Uri(endpoint,action)); request.Method="POST"; request.ContentType="application/json; charset=utf-8"; request.AllowAutoRedirect=false; request.Proxy=null; request.Timeout=20000; request.ReadWriteTimeout=20000; request.ContentLength=body.Length;
        if(!String.IsNullOrEmpty(token)) { if(token.Contains("\r") || token.Contains("\n")) throw new ArgumentException("Token 包含非法字符。"); request.Headers[HttpRequestHeader.Authorization]="Bearer "+token; }
        try {
            using(var s=request.GetRequestStream()) s.Write(body,0,body.Length);
            using(var response=(HttpWebResponse)request.GetResponse()) using(var s=response.GetResponseStream()) using(var buffer=new MemoryStream()) {
                if(response.ContentType!=null && response.ContentType.IndexOf("text/html",StringComparison.OrdinalIgnoreCase)>=0) throw new IOException("当前地址返回管理网页，请填写 OneBot HTTP 服务器端口（例如 3000），而非 WebUI 端口 6099。");
                byte[] chunk=new byte[8192]; int n; while((n=s.Read(chunk,0,chunk.Length))>0) { if(buffer.Length+n>MaxResponseBytes) throw new IOException("本地桥接响应超过 16 MiB。请核对 OneBot HTTP 地址；若发生在更换头像时，请先查看 QQ 当前头像，避免重复上传。"); buffer.Write(chunk,0,n); }
                Dictionary<string,object> result;
                try { result=json.DeserializeObject(Encoding.UTF8.GetString(buffer.ToArray())) as Dictionary<string,object>; } catch(ArgumentException) { throw new IOException("本地服务没有返回有效的 OneBot JSON。请核对 HTTP 服务器端口与 Token。"); }
                object status,retcode;
                if(result==null || !result.TryGetValue("status",out status) || !result.TryGetValue("retcode",out retcode)) throw new IOException("本地服务返回内容不符合 OneBot 格式，请核对 HTTP 服务器地址。");
                long code; if(!Int64.TryParse(Convert.ToString(retcode),out code)) throw new IOException("本地桥接返回了无效的 retcode。");
                if(Convert.ToString(status)!="ok" || code!=0) {
                    object raw; string message=result.TryGetValue("message",out raw)?Convert.ToString(raw):"";
                    if(String.IsNullOrWhiteSpace(message) && result.TryGetValue("wording",out raw)) message=Convert.ToString(raw);
                    throw new IOException("NapCat 接口 "+action+" 返回失败（retcode="+code+"）："+SafeMessage(message,token));
                }
                return result;
            }
        } catch(WebException e) {
            var response=e.Response as HttpWebResponse;
            if(response!=null) { using(response) { if(response.StatusCode==HttpStatusCode.Forbidden || response.StatusCode==HttpStatusCode.Unauthorized) throw new IOException("NapCat Token 验证失败。请填写 HTTP 服务器配置中的 Token。"); throw new IOException("NapCat 接口 "+action+" 返回 HTTP "+(int)response.StatusCode+"，请核对 OneBot HTTP 服务器地址。"); } }
            if(e.Status==WebExceptionStatus.Timeout) throw new IOException("NapCat 接口 "+action+" 请求超时。更换头像请求可能已处理，请先在 QQ 中核对头像。");
            throw new IOException("无法访问本地 NapCat HTTP 服务。请确认服务已启用、QQ 已登录，以及端口与 Token 正确。");
        }
    }
    public static string SafeMessage(string message,string token) {
        if(String.IsNullOrWhiteSpace(message)) return "没有提供错误原因，请查看 NapCat 日志。";
        message=Regex.Replace(message,@"base64://[A-Za-z0-9+/=\s]+","[图片数据已隐藏]",RegexOptions.IgnoreCase);
        if(!String.IsNullOrEmpty(token)) message=message.Replace(token,"[Token 已隐藏]");
        message=message.Replace("\r"," ").Replace("\n"," "); return message.Length>400?message.Substring(0,400)+"…":message;
    }
    public static string SetAvatar(string endpoint,string token,string uin,byte[] image) {
        Uri uri=Endpoint(endpoint); Protocol.Number(uin); Images.Validate(image);
        var result=Call(uri,token,"get_login_info",new Dictionary<string,object>()); object data,id;
        var details=result.TryGetValue("data",out data)?data as Dictionary<string,object>:null;
        if(details==null || !details.TryGetValue("user_id",out id) || Convert.ToString(id)!=uin) throw new ArgumentException("本地 NapCat 登录的 QQ 号与输入的 QQ 号不一致，已停止上传。");
        // Both apps run on this computer. A temporary local file prevents NapCat
        // from echoing megabytes of Base64 in its failure message/wording fields.
        string directory=Path.Combine(Path.GetTempPath(),"QQAvatarTool"); Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,Guid.NewGuid().ToString("N")+(Images.Png(image)?".png":Images.Gif(image)?".gif":".jpg"));
        try { File.WriteAllBytes(file,image); Call(uri,token,"set_qq_avatar",new Dictionary<string,object>{{"file",file}}); }
        finally { try { File.Delete(file); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
        return "QQNT 本地桥接返回成功（retcode=0）。请在 QQ 中确认头像；该接口用于普通头像，透明通道与 GIF/APNG 动画效果由 QQ 决定，小世界动态图层不在此接口支持范围内。";
    }
}
}
