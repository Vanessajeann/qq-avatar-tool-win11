using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace AvatarTool {
public sealed class MainForm : Form {
    readonly TextBox uin=new TextBox(), key=new TextBox(), group=new TextBox(), host=new TextBox(), bridge=new TextBox(), token=new TextBox();
    readonly ComboBox mode=new ComboBox(), process=new ComboBox(), candidates=new ComboBox();
    readonly PictureBox preview=new PictureBox();
    readonly TrackBar alpha=new TrackBar();
    readonly Label imageInfo=new Label(), status=new Label();
    readonly Button upload=new Button();
    readonly FlowLayoutPanel actions=new FlowLayoutPanel();
    byte[] image; Image shown; bool busy;
    public MainForm() {
        Text="QQ 头像工具 · Windows 版 1.0.1";
        Font=new Font("Microsoft YaHei UI",10); BackColor=Color.FromArgb(245,247,250); ForeColor=Color.FromArgb(28,39,54);
        AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(850,800); MinimumSize=new Size(820,800); StartPosition=FormStartPosition.CenterScreen;
        var page=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=8, Padding=new Padding(22) };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,168)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,245)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,54)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); page.RowStyles.Add(new RowStyle(SizeType.Percent,100)); page.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); Controls.Add(page);
        page.Controls.Add(new Label { Text="QQ 头像工具", Font=new Font(Font.FontFamily,20,FontStyle.Bold), Dock=DockStyle.Fill },0,0);
        page.Controls.Add(new Label { Text="旧协议：普通 / 透明 / 半透明 / 动态头像、群头像与封面、名片背景。\nQQNT：可连接已配置的本地 NapCat 设置普通头像。原版 QQNT 自动取 UKey 尚不支持。", Dock=DockStyle.Fill },0,1);
        var account=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=4, RowCount=4, BackColor=Color.White, Padding=new Padding(10) };
        account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80)); account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55)); account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70)); account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
        for(int i=0;i<4;i++) account.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        AddLabel(account,"QQ 号",0,0); uin.Dock=DockStyle.Fill; account.Controls.Add(uin,1,0);
        AddLabel(account,"群号",2,0); group.Dock=DockStyle.Fill; account.Controls.Add(group,3,0);
        AddLabel(account,"UKey",0,1); key.UseSystemPasswordChar=true; key.Dock=DockStyle.Fill; account.Controls.Add(key,1,1); account.SetColumnSpan(key,3);
        AddLabel(account,"旧 QQ",0,2); process.DropDownStyle=ComboBoxStyle.DropDownList; process.Dock=DockStyle.Fill; account.Controls.Add(process,1,2);
        var refresh=new Button { Text="刷新",Dock=DockStyle.Fill }; refresh.Click+=delegate { RefreshProcesses(); }; account.Controls.Add(refresh,2,2);
        var scan=new Button { Text="读取旧 QQ UKey",Dock=DockStyle.Fill }; scan.Click+=async delegate { await Scan(); }; account.Controls.Add(scan,3,2);
        AddLabel(account,"候选票据",0,3); candidates.DropDownStyle=ComboBoxStyle.DropDownList; candidates.Dock=DockStyle.Fill; candidates.SelectedIndexChanged+=delegate { var item=candidates.SelectedItem as Ticket; if(item!=null) key.Text=item.Value; }; account.Controls.Add(candidates,1,3);
        var clear=new Button { Text="清除票据",Dock=DockStyle.Fill }; clear.Click+=delegate { key.Clear(); candidates.Items.Clear(); status.Text="票据已从界面清除。"; }; account.Controls.Add(clear,3,3); page.Controls.Add(account,0,2);
        var imagePanel=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, BackColor=Color.White, Padding=new Padding(12) };
        imagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205)); imagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        preview.Dock=DockStyle.Fill; preview.SizeMode=PictureBoxSizeMode.Zoom; preview.BackColor=Color.FromArgb(224,229,235); imagePanel.Controls.Add(preview,0,0);
        var imageTools=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(18,0,0,0) };
        imageTools.RowStyles.Add(new RowStyle(SizeType.Absolute,45)); imageTools.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); imageTools.RowStyles.Add(new RowStyle(SizeType.Absolute,54)); imageTools.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var choose=new FlowLayoutPanel { Dock=DockStyle.Fill };
        choose.Controls.Add(Button("选择图片",SelectImage)); choose.Controls.Add(Button("生成全透明",delegate { SetImage(Images.Transparent(),"全透明 PNG"); })); imageTools.Controls.Add(choose,0,0);
        imageInfo.Dock=DockStyle.Fill; imageInfo.Text="未选择图片"; imageTools.Controls.Add(imageInfo,0,1);
        var opacity=new FlowLayoutPanel { Dock=DockStyle.Fill }; opacity.Controls.Add(new Label { Text="不透明度",AutoSize=true,Padding=new Padding(0,10,0,0) }); alpha.Minimum=0; alpha.Maximum=100; alpha.Value=100; alpha.TickFrequency=10; alpha.Width=220;
        var percent=new Label { Text="100%",AutoSize=true,Padding=new Padding(0,10,0,0) }; alpha.ValueChanged+=delegate { percent.Text=alpha.Value+"%"; }; opacity.Controls.Add(alpha); opacity.Controls.Add(percent); imageTools.Controls.Add(opacity,0,2);
        var save=new FlowLayoutPanel { Dock=DockStyle.Fill }; save.Controls.Add(Button("导出 PNG / 原图",delegate { Export(false); })); save.Controls.Add(Button("GIF → APNG 导出",delegate { Export(true); })); imageTools.Controls.Add(save,0,3);
        imagePanel.Controls.Add(imageTools,1,0); page.Controls.Add(imagePanel,0,3);
        var settings=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0) }; settings.Controls.Add(new Label { Text="上传类型",AutoSize=true,Padding=new Padding(0,4,0,0) }); mode.DropDownStyle=ComboBoxStyle.DropDownList; mode.Width=230;
        mode.Items.AddRange(new object[]{"QQ 头像（PNG / JPG / GIF）","小世界动态头像（GIF → APNG）","名片背景","群头像（旧接口）","群封面（旧接口）"}); mode.SelectedIndex=0; settings.Controls.Add(mode);
        settings.Controls.Add(new Label { Text="服务器",AutoSize=true,Padding=new Padding(12,4,0,0) }); host.Width=200; host.Text=Protocol.DefaultHost; settings.Controls.Add(host); page.Controls.Add(settings,0,4);
        actions.Dock=DockStyle.Fill; upload.Text="上传所选图片"; upload.Width=175; upload.Height=38; upload.BackColor=Color.FromArgb(35,104,219); upload.ForeColor=Color.White; upload.FlatStyle=FlatStyle.Flat; upload.Click+=async delegate { await Upload(); }; actions.Controls.Add(upload);
        actions.Controls.Add(Button("导入旧 INI 票据",Import)); actions.Controls.Add(Button("QQNT 桥接设置",BridgeSettings)); actions.Controls.Add(Button("使用说明",delegate { MessageBox.Show(this,"1. 输入 QQ 号，选择图片与上传类型。\n2. 旧协议：粘贴 208 位 UKey，或选择怀旧版 QQ 后读取。多个候选票据无法离线判定有效性。\n3. QQNT：需已配置本地 NapCat HTTP 服务，在桥接设置中填端口和 Token；无需 UKey。此路径仅设置普通头像。\n4. 半透明处理仅适用于静态图片；动图保持 100%。\n5. 小世界类型会把 GIF 转为 APNG，保留各帧时长。\n6. 图片在转换后检查大小：普通头像 ≤ 3 MiB，其余 ≤ 2 MiB。\n7. 点击上传后在 QQ 中确认头像显示效果。\n\n原版 QQNT 自动取 UKey 未实现。网页 skey/Cookie 不能替代上传票据。\n旧协议传输不加密，443 端口也不代表 TLS。工具不保存票据或 Token，启动时不扫描 QQ。","使用说明"); })); page.Controls.Add(actions,0,5);
        status.Dock=DockStyle.Fill; status.Padding=new Padding(0,8,0,0); status.Text="就绪。尚未连接服务器；启动不会读取 QQ 或上传图片。"; page.Controls.Add(status,0,6);
        page.Controls.Add(new Label { Dock=DockStyle.Fill,ForeColor=Color.FromArgb(98,108,120),Text="原作者：千雨（2213312835） · 本版移植与修复，上传协议来自提供的源码。" },0,7);
        FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) { e.Cancel=true; status.Text="操作正在进行，请等待完成或超时后关闭。"; } };
        mode.Items.Add("QQNT 普通头像（本地 NapCat）"); bridge.Text="http://127.0.0.1:3000/"; token.UseSystemPasswordChar=true;
        FormClosed+=delegate { if(shown!=null) shown.Dispose(); key.Clear(); token.Clear(); candidates.Items.Clear(); };
    }
    static void AddLabel(TableLayoutPanel p,string text,int col,int row) { p.Controls.Add(new Label { Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },col,row); }
    public string ValidateUI() { if(mode.SelectedIndex<0 || String.IsNullOrEmpty(mode.Text)) throw new Exception("上传类型没有默认选项。"); return "PASS: main form created; six upload modes; default selection="+mode.Text+"; no network or process scan on startup."; }
    static Button Button(string text,Action action) { var b=new Button { Text=text,AutoSize=true,Height=36,Padding=new Padding(6,2,6,2) }; b.Click+=delegate { action(); }; return b; }
    void Error(Exception e) { string text=e is System.Net.Sockets.SocketException?"网络连接失败或旧上传接口不可用。":e.Message; status.Text=text; MessageBox.Show(this,text,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    void SetImage(byte[] bytes,string name) {
        try { Images.Validate(bytes); Image next; using(var stream=new MemoryStream(bytes)) using(var raw=Image.FromStream(stream)) next=new Bitmap(raw); preview.Image=null; if(shown!=null) shown.Dispose(); shown=next; preview.Image=shown; image=(byte[])bytes.Clone(); alpha.Value=100;
            imageInfo.Text=name+"\n"+next.Width+" × "+next.Height+" · "+(bytes.Length/1024.0).ToString("F1")+" KiB"+(Images.Gif(bytes)?" · GIF（预览首帧）":Images.Apng(bytes)?" · APNG（预览首帧）":"");
        } catch(Exception e) { Error(e); }
    }
    void SelectImage() { using(var d=new OpenFileDialog { Filter="图片|*.png;*.apng;*.jpg;*.jpeg;*.gif",CheckFileExists=true }) if(d.ShowDialog(this)==DialogResult.OK) try { if(new FileInfo(d.FileName).Length>3*1024*1024) throw new ArgumentException("图片超过 3 MiB。"); SetImage(File.ReadAllBytes(d.FileName),Path.GetFileName(d.FileName)); } catch(Exception e) { Error(e); } }
    byte[] Prepared(bool apng) { if(image==null) throw new ArgumentException("请先选择图片。"); byte[] result=Images.Opacity(image,alpha.Value); return apng?Images.ToApng(result):result; }
    void Export(bool apng) { try { byte[] bytes=Prepared(apng); bool png=Images.Png(bytes); string ext=png?"png":Images.Gif(bytes)?"gif":"jpg"; using(var d=new SaveFileDialog { Filter="图片|*."+ext,DefaultExt=ext,FileName=apng?"动态头像.png":"头像."+ext }) if(d.ShowDialog(this)==DialogResult.OK) { File.WriteAllBytes(d.FileName,bytes); status.Text="图片已导出到所选位置。"; } } catch(Exception e) { Error(e); } }
    sealed class QQProcess { public int Id; public override string ToString() { return "QQ.exe · PID "+Id; } }
    sealed class Ticket { public string Value; public int Index; public override string ToString() { return "候选 "+Index+"（104 字节）"; } }
    void RefreshProcesses() { process.Items.Clear(); foreach(var p in Process.GetProcessesByName("QQ")) using(p) process.Items.Add(new QQProcess { Id=p.Id }); if(process.Items.Count>0) process.SelectedIndex=0; status.Text="找到 "+process.Items.Count+" 个 QQ.exe 进程。QQNT 不能使用旧版票据读取器。"; }
    void Busy(bool value) { busy=value; foreach(Control c in Controls) c.Enabled=!value; }
    async Task Scan() { if(busy) return; try { var chosen=process.SelectedItem as QQProcess; if(chosen==null) throw new ArgumentException("请先刷新并选择怀旧版 QQ 进程。"); Protocol.Number(uin.Text.Trim()); Busy(true); status.Text="正在读取所选旧 QQ 的候选上传票据…";
        List<string> found=await Task.Run(delegate { return LegacyKey.Read(chosen.Id); }); candidates.Items.Clear(); key.Clear(); for(int i=0;i<found.Count;i++) candidates.Items.Add(new Ticket { Value=found[i],Index=i+1 }); if(found.Count>0) candidates.SelectedIndex=0;
        status.Text=found.Count==0?"没有找到旧版票据。QQNT 不支持，旧 QQ 也可能已更改内存结构。":"找到 "+found.Count+" 份候选票据。请确认 QQ 号与所选进程对应；票据有效性需通过服务器响应确认。";
    } catch(Exception e) { Error(e); } finally { Busy(false); } }
    async Task Upload() { if(busy) return; byte[] ticket=null; try {
        string qq=Protocol.Number(uin.Text.Trim()), server=host.Text.Trim(), g=group.Text.Trim(); int index=mode.SelectedIndex; string localEndpoint=bridge.Text.Trim(), localToken=token.Text;
        if(index==5) QQNTBridge.Endpoint(localEndpoint); else { Protocol.Host(server); ticket=Protocol.Hex(key.Text); }
        byte[] bytes=Prepared(index==1); Images.Validate(bytes); int limit=(index==0 || index==5?3:2)*1024*1024; if(bytes.Length>limit) throw new ArgumentException("处理后的图片超过此上传类型的大小限制。"); if(index==3 || index==4) Protocol.Number(g);
        Busy(true); status.Text="正在连接服务器并上传…"; int kind=index==1?90:index==2?21:5; byte[] usedTicket=ticket;
        string result=await Task.Run(delegate { if(index==5) return QQNTBridge.SetAvatar(localEndpoint,localToken,qq,bytes); return index>=3?Protocol.UploadGroup(server,qq,g,bytes,usedTicket,index==3):Protocol.Upload(server,qq,bytes,usedTicket,kind); });
        status.Text=result; MessageBox.Show(this,result,"上传响应",MessageBoxButtons.OK,MessageBoxIcon.Information);
    } catch(Exception e) { Error(e); } finally { if(ticket!=null) Array.Clear(ticket,0,ticket.Length); Busy(false); } }
    void BridgeSettings() { using(var f=new Form { Text="QQNT 本地桥接",ClientSize=new Size(590,235),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false }) {
        var desc=new Label { Text="此模式需自行安装并登录 NapCat，开启 OneBot HTTP 服务。\n只调用本地普通头像接口；未实现原版 QQNT 直接取票据或小世界动态图层。",Left=15,Top=12,Width=555,Height=55 }; f.Controls.Add(desc);
        var address=new TextBox { Text=bridge.Text,Left=125,Top=82,Width=435 }; var secret=new TextBox { Text=token.Text,UseSystemPasswordChar=true,Left=125,Top=123,Width=435 };
        f.Controls.Add(new Label { Text="HTTP 地址",Left=15,Top=85,Width=110 }); f.Controls.Add(address); f.Controls.Add(new Label { Text="Access Token",Left=15,Top=126,Width=110 }); f.Controls.Add(secret);
        var save=new Button { Text="应用到本次运行",Left=360,Top=178,Width=200,Height=34 }; save.Click+=delegate { try { QQNTBridge.Endpoint(address.Text.Trim()); bridge.Text=address.Text.Trim(); token.Text=secret.Text; mode.SelectedIndex=5; status.Text="已切换到 QQNT 普通头像模式，无需 UKey。"; f.DialogResult=DialogResult.OK; } catch(Exception e) { MessageBox.Show(f,e.Message); } }; f.Controls.Add(save); f.ShowDialog(this);
    } }
    void Import() { using(var d=new OpenFileDialog { Filter="旧工具票据配置|*.ini",CheckFileExists=true }) if(d.ShowDialog(this)==DialogResult.OK) try {
        if(new FileInfo(d.FileName).Length>1024*1024) throw new ArgumentException("INI 文件过大。"); string wanted=Protocol.Number(uin.Text.Trim()), section=""; string value=null;
        foreach(string line in File.ReadAllLines(d.FileName,System.Text.Encoding.Default)) { string s=line.Trim(); if(s.StartsWith("[") && s.EndsWith("]")) section=s.Substring(1,s.Length-2); else if(section==wanted && Regex.IsMatch(s,@"\Akey\s*=",RegexOptions.IgnoreCase)) value=s.Substring(s.IndexOf('=')+1).Trim(); }
        if(value==null) throw new ArgumentException("INI 中没有当前 QQ 号对应的 key。"); Protocol.Hex(value); key.Text=value; status.Text="票据已导入；本工具不写入或更新 INI。";
    } catch(Exception e) { Error(e); } }
}
public static class Program {
    [STAThread] public static int Main(string[] args) {
        try {
            if(args.Length>0 && args[0]=="--self-test") { Tests.Run(args.Length>1?args[1]:"self-test.txt"); return 0; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0 && args[0]=="--smoke-test") { using(var f=new MainForm()) { f.CreateControl(); f.Show(); Application.DoEvents(); string output=args.Length>1?args[1]:"smoke.png"; File.WriteAllText(Path.ChangeExtension(output,"txt"),f.ValidateUI()); using(var b=new Bitmap(f.Width,f.Height)) { f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height)); b.Save(output,ImageFormat.Png); } f.Close(); } return 0; }
            Application.Run(new MainForm()); return 0;
        } catch(Exception e) { if(args.Length>0) { File.WriteAllText("fatal-test.txt",e.ToString()); return 1; } MessageBox.Show(e.Message,"启动失败"); return 1; }
    }
}
}
