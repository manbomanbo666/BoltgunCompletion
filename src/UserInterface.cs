using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace BoltgunCompletion {
public sealed class Settings {
    public string GamePath; public int OverlayX=40; public int OverlayY=100; public int Opacity=88;
    public bool OverlayVisible=true;
    static string PathName { get { return Path.Combine(AppPaths.Root,"data","settings.json"); } }
    public static Settings Load() { try { return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(PathName,Encoding.UTF8))??new Settings(); } catch { return new Settings(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(PathName)); File.WriteAllText(PathName,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false)); }
}
public sealed class OverlayForm:Form {
    bool edit;
    readonly Label title,status,progress,secrets,enemies,notes;
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var cp=base.CreateParams; cp.ExStyle|=0x08000000|0x80; if(!edit) cp.ExStyle|=0x20|0x80000; return cp; } }
    public OverlayForm(Settings settings) {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
        AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.Manual; Bounds=new Rectangle(settings.OverlayX,settings.OverlayY,620,440);
        var area=Screen.FromRectangle(Bounds).WorkingArea;
        Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
        BackColor=Color.FromArgb(22,26,31); ForeColor=Color.FromArgb(225,233,237);
        Opacity=Math.Max(0.35,Math.Min(1,settings.Opacity/100.0));
        Font=new Font("Microsoft YaHei UI",10);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=5,Margin=Padding.Empty};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
        title=TextLabel("爆弹枪 · 未连接",ForeColor);title.Font=new Font(Font.FontFamily,12,FontStyle.Bold);
        status=TextLabel("等待连接游戏",Color.FromArgb(163,181,192));
        progress=TextLabel("击杀率  待核实\r\n秘密    待核实 / 待核实",ForeColor);progress.Font=new Font(Font.FontFamily,11);
        notes=TextLabel("位置为直线提示，可能隔墙或跨楼层。",Color.FromArgb(163,181,192));notes.Font=new Font(Font.FontFamily,9);
        layout.Controls.Add(title,0,0);layout.Controls.Add(status,0,1);layout.Controls.Add(progress,0,2);
        var columns=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,17));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        secrets=TextLabel("等待有效的关卡数据",ForeColor);
        enemies=TextLabel("等待有效的关卡数据",ForeColor);
        columns.Controls.Add(TargetColumn("秘密",Color.FromArgb(239,199,105),secrets),0,0);
        var divider=new Panel {Dock=DockStyle.Fill,Margin=new Padding(8,3,8,12),BackColor=Color.FromArgb(76,91,103)};
        columns.Controls.Add(divider,1,0);
        columns.Controls.Add(TargetColumn("怪物",Color.FromArgb(126,204,226),enemies),2,0);
        layout.Controls.Add(columns,0,3);layout.Controls.Add(notes,0,4);Controls.Add(layout);
        MouseEventHandler drag=(sender,e)=> { if(edit&&e.Button==MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero); } };
        AttachDrag(this,drag);
    }
    static Label TextLabel(string text,Color color) {return new Label {Text=text,ForeColor=color,Dock=DockStyle.Fill,Margin=Padding.Empty};}
    static Control TargetColumn(string heading,Color color,Label body) {
        var column=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
        column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        column.RowStyles.Add(new RowStyle(SizeType.Absolute,30));column.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var label=TextLabel(heading,color);label.Font=new Font("Microsoft YaHei UI",11,FontStyle.Bold);
        column.Controls.Add(label,0,0);column.Controls.Add(body,0,1);return column;
    }
    static void AttachDrag(Control control,MouseEventHandler drag) {control.MouseDown+=drag;foreach(Control child in control.Controls)AttachDrag(child,drag);}
    static void SetText(Label label,string value) {if(label.Text!=value)label.Text=value;}
    public void SetEdit(bool value) { edit=value; RecreateHandle(); }
    public void UpdateState(string heading,string connection,string summary,GameSnapshot snapshot,bool active) {
        SetText(title,"爆弹枪 · "+heading);SetText(status,connection);SetText(progress,summary);
        string secretText="等待有效的关卡数据",enemyText=secretText;
        string footer="位置为直线提示，可能隔墙或跨楼层。";
        if(active && snapshot!=null) {
            if(snapshot.mode=="results") {secretText="本关已结束\r\n以本次最终结算为准";enemyText=secretText;}
            else {
                secretText=DisplayText.Nearest(snapshot,snapshot.secrets,"秘密",true);
                enemyText=DisplayText.Nearest(snapshot,snapshot.enemies,"怪物",true);
                footer="仅提示已确认的位置；未激活波次仍待核实。";
            }
            if(snapshot.warnings!=null && snapshot.warnings.Length>0)footer=String.Join(" · ",snapshot.warnings);
        }
        SetText(secrets,secretText);SetText(enemies,enemyText);SetText(notes,footer);
    }
}
public sealed class MainForm:Form {
    protected override bool ShowWithoutActivation {get{return Environment.GetCommandLineArgs().Contains("--render-preview");}}
    readonly Settings settings=Settings.Load();
    readonly SnapshotStore store=new SnapshotStore();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=250};
    readonly OverlayForm overlay;
    readonly Label status=new Label(),progress=new Label(),details=new Label();
    readonly TextBox gamePath=new TextBox();
    readonly Button connect=new Button();
    LogTail tail; Process gameProcess; bool connecting,editOverlay; int tick; string failure;
    readonly int ownerPid=Process.GetCurrentProcess().Id;
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    public MainForm(string discovered) {
        Text="爆弹枪 · 完美通关助手 0.1"; ClientSize=new Size(820,620); MinimumSize=new Size(760,600);
        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",10);
        BackColor=Color.FromArgb(22,26,31); ForeColor=Color.FromArgb(225,233,237);
        settings.GamePath=File.Exists(AppPaths.GameExe(settings.GamePath??""))?settings.GamePath:discovered;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=9};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,94)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
        layout.Controls.Add(new Label {Text="击杀 100% · 秘密全收集",Font=new Font(Font.FontFamily,17,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
        var paths=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2}; paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
        gamePath.Text=settings.GamePath??"";gamePath.Dock=DockStyle.Fill; paths.Controls.Add(gamePath,0,0);
        var browse=Button("选择游戏",()=>{using(var dialog=new FolderBrowserDialog {Description="选择一代 Boltgun 安装目录"}) if(dialog.ShowDialog()==DialogResult.OK) {settings.GamePath=dialog.SelectedPath;gamePath.Text=dialog.SelectedPath;settings.Save();}}); paths.Controls.Add(browse,1,0);layout.Controls.Add(paths,0,1);
        var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill};
        connect.Text="连接游戏"; Style(connect);connect.Click+=async(sender,e)=>await Connect();buttons.Controls.Add(connect);
        buttons.Controls.Add(Button("启动一代游戏",()=>Process.Start(new ProcessStartInfo("steam://rungameid/2005010"){UseShellExecute=true})));
        buttons.Controls.Add(Button("打开插件目录",()=>Process.Start(new ProcessStartInfo(AppPaths.Root){UseShellExecute=true})));
        layout.Controls.Add(buttons,0,2);
        status.Text="未连接 · 请先启动游戏";status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(232,189,89);layout.Controls.Add(status,0,3);
        progress.Text="击杀率  待核实\r\n秘密    待核实 / 待核实";progress.Font=new Font(Font.FontFamily,13);progress.Dock=DockStyle.Fill;layout.Controls.Add(progress,0,4);
        details.Text="本体与剧情 DLC 使用相同判定。\r\n只有同次最终结算满足两项条件，才会标记完美通关。";details.Dock=DockStyle.Fill;layout.Controls.Add(details,0,5);
        var controls=new FlowLayoutPanel {Dock=DockStyle.Fill};
        var visible=new CheckBox {Text="显示悬浮面板",Checked=settings.OverlayVisible,AutoSize=true};visible.CheckedChanged+=(s,e)=>{settings.OverlayVisible=visible.Checked;settings.Save();}; controls.Controls.Add(visible);
        var edit=new CheckBox {Text="允许拖动面板",AutoSize=true};edit.CheckedChanged+=(s,e)=>{editOverlay=edit.Checked;overlay.SetEdit(editOverlay);}; controls.Controls.Add(edit);
        controls.Controls.Add(new Label {Text="不透明度",AutoSize=true,Padding=new Padding(10,3,0,0)});
        var opacity=new TrackBar {Minimum=35,Maximum=100,Value=Math.Max(35,Math.Min(100,settings.Opacity)),Width=150,TickStyle=TickStyle.None};opacity.ValueChanged+=(s,e)=>{settings.Opacity=opacity.Value;overlay.Opacity=settings.Opacity/100.0;};opacity.MouseUp+=(s,e)=>settings.Save();controls.Controls.Add(opacity);layout.Controls.Add(controls,0,6);
        layout.Controls.Add(new Label {Text="窗口化 / 无边框模式 · 面板默认鼠标穿透，不拦截游戏基本操作",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(147,169,181)},0,7);
        layout.Controls.Add(new Label {Text="关闭助手只关闭面板。完整停用加载器需退出游戏；卸载时关闭游戏并删除插件目录。\r\n统计口径未核实前，不输出完美通关结论。",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,9),ForeColor=Color.FromArgb(147,169,181)},0,8);
        Controls.Add(layout);overlay=new OverlayForm(settings);
        Shown+=async(s,e)=>{if(Environment.GetCommandLineArgs().Contains("--auto-connect"))await Connect();};
        timer.Tick+=(s,e)=>RefreshState();timer.Start();
        FormClosed+=(s,e)=>{timer.Stop();settings.OverlayX=overlay.Left;settings.OverlayY=overlay.Top;settings.Save();overlay.Close();};
    }
    static void Style(Button b) {b.AutoSize=true;b.Padding=new Padding(10,3,10,3);b.FlatStyle=FlatStyle.Flat;b.BackColor=Color.FromArgb(43,54,63);b.ForeColor=Color.White;b.Margin=new Padding(0,0,10,0);}
    static Button Button(string text,Action action) {var b=new Button {Text=text};Style(b);b.Click+=(s,e)=>{try{action();}catch(Exception error){MessageBox.Show(error.Message,"操作未完成");}};return b;}
    async Task Connect() {
        if(connecting)return;
        settings.GamePath=gamePath.Text.Trim();settings.Save();failure=null;connecting=true;connect.Enabled=false;
        store.Clear();tail=null;status.Text="正在校验并连接…";
        try {
            string selected=settings.GamePath;
            // Capture the log cursor before injection, not after initialization output.
            var reader=new LogTail(AppPaths.Log);
            await Task.Run(()=>RuntimeConnector.Connect(selected));
            gameProcess=GameLocator.Running(selected);tail=reader;
            status.Text="加载器已加载，正在等待当前游戏数据…";
        } catch(Exception e) {failure=e.Message;status.Text=failure;} finally {connecting=false;connect.Enabled=true;}
    }
    void RefreshState() {
        tick++;
        if(tail!=null) try {tail.Poll(line=>store.Accept(line,clock.ElapsedMilliseconds),null);} catch(IOException) {} catch(UnauthorizedAccessException) {failure="无法读取插件日志";}
        bool running=gameProcess!=null;
        if(running) try {running=!gameProcess.HasExited;}catch{running=false;}
        if(gameProcess!=null&&!running) {store.Clear();tail=null;gameProcess=null;failure=null;}
        string header;
        var s=store.Current;
        if(connecting) header="正在连接…";
        else if(failure!=null) header=failure;
        else if(gameProcess==null) header="未连接 · 请启动一代游戏后点击连接";
        else if(store.RevisionMismatch) header="游戏中仍是旧插件版本，请退出游戏后重新启动并连接";
        else if(s==null) header="加载器已加载 · 等待有效数据（尚未就绪）";
        else if(!store.Fresh(clock.ElapsedMilliseconds)) header="数据中断 · 旧数据已停止作为实时提示";
        else if(s.mode=="error") header="运行时读取异常 · "+s.error;
        else if(s.mode=="initializing") header="插件初始化中";
        else if(s.mode=="main_menu") header="插件数据桥接就绪 · 主菜单";
        else if(s.mode=="loading") header="正在加载关卡";
        else if(s.mode=="dead") header="角色已死亡 · 等待重试";
        else if(s.mode=="unsupported") header="当前模式未适配 · "+s.level;
        else header="插件数据桥接就绪 · "+s.level+(s.verified?"":" · 口径待核实");
        status.Text=header;
        bool active=s!=null&&store.Fresh(clock.ElapsedMilliseconds)&&(s.mode=="campaign"||s.mode=="results");
        if(active) {
            progress.Text=s.Perfect?"最终结算：完美通关\r\n击杀率 100% · 秘密全收集":DisplayText.Progress(s);
            details.Text=(s.mode=="results"?"本关已结束。仅使用本次结算数据判定。":DisplayText.Nearest(s,s.secrets,"秘密")+"\r\n\r\n"+DisplayText.Nearest(s,s.enemies,"敌人"))+"\r\n\r\n"+String.Join(" · ",s.warnings??new string[0]);
        } else {progress.Text="击杀率  待核实\r\n秘密    待核实 / 待核实";details.Text=header;}
        overlay.UpdateState(s!=null?s.level:"未连接",header,progress.Text,s,active);
        uint foreground;GetWindowThreadProcessId(GetForegroundWindow(),out foreground);
        bool foregroundAllowed=foreground==ownerPid||(gameProcess!=null&&foreground==gameProcess.Id);
        bool shouldShow=settings.OverlayVisible&&foregroundAllowed&&(gameProcess!=null||editOverlay);
        if(shouldShow&&!overlay.Visible)overlay.Show();else if(!shouldShow&&overlay.Visible)overlay.Hide();
    }
}
}
