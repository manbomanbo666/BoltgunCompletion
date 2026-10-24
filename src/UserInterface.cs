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
    public int RadarRange=100;
    public RadarAppearance Appearance=new RadarAppearance();
    public bool PinCenter=false,ShowPlayerArrow=true;
    public double HeightTolerance=5;
    static string PathName { get { return Path.Combine(AppPaths.Root,"data","settings.json"); } }
    internal static Settings FromJson(string json) {var settings=new JavaScriptSerializer().Deserialize<Settings>(json)??new Settings();settings.Normalize();return settings;}
    public void Normalize() {Opacity=RadarAppearance.Clamp(Opacity);if(Appearance==null)Appearance=new RadarAppearance();Appearance.Normalize();
        HeightTolerance=Double.IsNaN(HeightTolerance)||Double.IsInfinity(HeightTolerance)?5:Math.Max(0.5,Math.Min(30,HeightTolerance));}
    public static Settings Load() {try{return FromJson(File.ReadAllText(PathName,Encoding.UTF8));}catch{return new Settings();}}
    public void Save() {Normalize();Directory.CreateDirectory(Path.GetDirectoryName(PathName));File.WriteAllText(PathName,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));}
}
public sealed class OverlayForm:Form {
    bool edit,campaign,changingHandle,presenting;
    readonly RadarControl radar=new RadarControl();
    readonly TargetTracker targets=new TargetTracker();
    readonly Settings settings;
    int radarRange;
    IntPtr anchorWindow;
    string enemyCount="当前区域敌人：待核实";
    public string TrackingText {get;private set;}
    internal string EnemyCountText {get{return enemyCount;}}
    internal RadarControl Radar {get{return radar;}}
    internal int PresentationCount {get;private set;}
    public string PresentationError {get;private set;}
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.ExStyle|=0x08000000|0x80|0x80000;if(!edit)cp.ExStyle|=0x20;return cp;}}
    public OverlayForm(Settings settings) {
        this.settings=settings;settings.Normalize();
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.Manual;Bounds=new Rectangle(settings.OverlayX,settings.OverlayY,420,460);
        var area=Screen.FromRectangle(Bounds).WorkingArea;Height=Math.Min(Height,area.Height);
        Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));
        BackColor=Color.FromArgb(22,26,31);ForeColor=Color.FromArgb(225,233,237);Font=new Font("Microsoft YaHei UI",11);
        radarRange=ValidRange(settings.RadarRange);UpdateRadarSize();
    }
    Rectangle RadarBounds {get{return new Rectangle(8,36,Math.Max(1,ClientSize.Width-16),Math.Max(1,ClientSize.Height-44));}}
    void UpdateRadarSize(){radar.Size=RadarBounds.Size;}
    public void SetEdit(bool value){
        bool next=value&&!settings.PinCenter;if(next==edit){Present();return;}
        edit=next;var bounds=Bounds;changingHandle=true;
        try{RecreateHandle();Bounds=bounds;}finally{changingHandle=false;}
        UpdateRadarSize();Present();
    }
    public static int ValidRange(int value){return new[]{50,100,250,500}.Contains(value)?value:100;}
    public void SetRadarRange(int value){radarRange=ValidRange(value);}
    public void ApplyAppearance(){settings.Normalize();Present();}
    public void SetPinned(bool value) {
        if(value&&!settings.PinCenter){settings.OverlayX=Left;settings.OverlayY=Top;}
        settings.PinCenter=value;
        if(value){SetEdit(false);UpdateAnchor(anchorWindow);}else Location=new Point(settings.OverlayX,settings.OverlayY);
        Present();
    }
    public void UpdateAnchor(IntPtr window) {
        anchorWindow=window;if(!settings.PinCenter)return;
        Rectangle target;if(!OverlayAnchor.ClientBounds(window,out target))target=Screen.FromControl(this).Bounds;
        var point=OverlayAnchor.Center(target,RadarBounds);if(Location!=point)Location=point;
    }
    internal Bitmap RenderBitmap() {
        UpdateRadarSize();var bitmap=new Bitmap(Math.Max(1,ClientSize.Width),Math.Max(1,ClientSize.Height),System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(bitmap)) {
            g.Clear(Color.Transparent);
            using(var brush=new SolidBrush(RadarAppearance.Fade(BackColor,settings.Appearance.Background)))g.FillRectangle(brush,ClientRectangle);
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(var brush=new SolidBrush(RadarAppearance.Fade(campaign?ForeColor:Color.FromArgb(232,189,89),settings.Appearance.Counter)))
            using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                g.DrawString(enemyCount,Font,brush,new Rectangle(8,8,Math.Max(1,ClientSize.Width-16),28),format);
            radar.Draw(g,RadarBounds,settings.Appearance,settings.ShowPlayerArrow&&!settings.PinCenter,settings.HeightTolerance);
        }
        return bitmap;
    }
    void Present(){
        if(changingHandle||presenting||!IsHandleCreated||!Visible||ClientSize.Width<1||ClientSize.Height<1)return;
        presenting=true;
        try{using(var bitmap=RenderBitmap())LayeredWindow.Present(Handle,bitmap,Left,Top,settings.Opacity);PresentationCount++;PresentationError=null;}
        catch(System.ComponentModel.Win32Exception error){PresentationError=error.Message;}
        finally{presenting=false;}
    }
    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e){}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);Present();}
    protected override void OnShown(EventArgs e){base.OnShown(e);Present();}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(!changingHandle&&radar!=null){UpdateRadarSize();Present();}}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(edit&&!settings.PinCenter&&e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
    protected override void Dispose(bool disposing){if(disposing)radar.Dispose();base.Dispose(disposing);}
    public void UpdateState(string heading,string connection,string summary,GameSnapshot snapshot,bool active) {
        campaign=active&&snapshot!=null&&snapshot.mode=="campaign";
        string count="待核实";TrackingText=null;
        if(campaign) {
            targets.Update(snapshot);
            count=(snapshot.enemies??new TargetInfo[0]).Count(t=>t!=null&&t.known).ToString(System.Globalization.CultureInfo.InvariantCulture);
            TrackingText="当前区域已确认存活敌人："+count+"\r\n金色=秘密；蓝色=敌人。实心=范围内，空心=范围外。\r\n上三角=较高处，下三角=较低处，圆形=同高附近。\r\n高低以相对垂直差约 "+settings.HeightTolerance.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" 米为阈值，不代表已识别真实楼层。\r\n未确认的秘密状态以较暗标记区分；雷达不判断通路。\r\n未加载区域及未激活波次仍待核实。";
        }
        enemyCount="当前区域敌人："+count;
        radar.UpdateTargets(campaign?targets.Secrets:null,campaign?targets.Enemies:null,radarRange,campaign,
            snapshot!=null&&snapshot.player!=null&&snapshot.yaw.HasValue);Present();
    }
}
public sealed class MainForm:Form {
    protected override bool ShowWithoutActivation {get{return Environment.GetCommandLineArgs().Contains("--render-preview");}}
    readonly Settings settings=Settings.Load();
    readonly SnapshotStore store=new SnapshotStore();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer {Interval=250};
    readonly OverlayForm overlay;
    readonly Label status=new Label(),progress=new Label();
    readonly TextBox details=new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
    readonly TextBox gamePath=new TextBox();
    readonly Button connect=new Button();
    LogTail tail; Process gameProcess; bool connecting,editOverlay; int tick; string failure;
    readonly int ownerPid=Process.GetCurrentProcess().Id;
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    public MainForm(string discovered,bool saveSettings=true) {
        Text="爆弹枪 · 完美通关助手 0.2.1.0 透明度预览"; ClientSize=new Size(820,620); MinimumSize=new Size(760,600);
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
        details.Text="本体与剧情 DLC 使用相同判定。\r\n只有同次最终结算满足两项条件，才会标记完美通关。";details.BackColor=BackColor;details.ForeColor=ForeColor;details.Dock=DockStyle.Fill;layout.Controls.Add(details,0,5);
        var controls=new FlowLayoutPanel {Dock=DockStyle.Fill};
        var visible=new CheckBox {Text="显示悬浮面板",Checked=settings.OverlayVisible,AutoSize=true};visible.CheckedChanged+=(s,e)=>{settings.OverlayVisible=visible.Checked;settings.Save();}; controls.Controls.Add(visible);
        var edit=new CheckBox {Text="允许拖动面板",AutoSize=true,Enabled=!settings.PinCenter};edit.CheckedChanged+=(s,e)=>{editOverlay=edit.Checked;overlay.SetEdit(editOverlay);}; controls.Controls.Add(edit);
        controls.Controls.Add(new Label {Text="整体不透明度",AutoSize=true,Padding=new Padding(10,3,0,0)});
        var opacity=new TrackBar {Minimum=0,Maximum=100,Value=RadarAppearance.Clamp(settings.Opacity),Width=115,TickStyle=TickStyle.None};opacity.ValueChanged+=(s,e)=>{settings.Opacity=opacity.Value;overlay.ApplyAppearance();};opacity.MouseUp+=(s,e)=>settings.Save();opacity.KeyUp+=(s,e)=>settings.Save();controls.Controls.Add(opacity);
        controls.Controls.Add(Button("雷达设置…",()=>{using(var dialog=new AppearanceDialog(settings,()=>overlay.ApplyAppearance())){
            dialog.StartPosition=FormStartPosition.Manual;dialog.Location=OverlayAnchor.SettingsPosition(Screen.FromRectangle(overlay.Bounds).WorkingArea,overlay.Bounds,dialog.Size);dialog.ShowDialog(this);
        }}));layout.Controls.Add(controls,0,6);
        var radarControls=new FlowLayoutPanel {Dock=DockStyle.Fill};
        radarControls.Controls.Add(new Label {Text="雷达范围",AutoSize=true,Padding=new Padding(0,5,8,0)});
        var range=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=90};
        range.Items.AddRange(new object[]{"50 m","100 m","250 m","500 m"});
        range.SelectedIndex=Array.IndexOf(new[]{50,100,250,500},OverlayForm.ValidRange(settings.RadarRange));
        range.SelectedIndexChanged+=(s,e)=>{settings.RadarRange=new[]{50,100,250,500}[range.SelectedIndex];overlay.SetRadarRange(settings.RadarRange);settings.Save();};
        radarControls.Controls.Add(range);
        var pin=new Button {Text=settings.PinCenter?"解除居中固定":"固定屏幕中间"};Style(pin);
        var arrow=new CheckBox {Text="显示中心箭头（固定时隐藏）",AutoSize=true,Checked=settings.ShowPlayerArrow,Enabled=!settings.PinCenter};
        arrow.CheckedChanged+=(s,e)=>{settings.ShowPlayerArrow=arrow.Checked;overlay.ApplyAppearance();settings.Save();};
        pin.Click+=(s,e)=>{overlay.SetPinned(!settings.PinCenter);pin.Text=settings.PinCenter?"解除居中固定":"固定屏幕中间";edit.Enabled=!settings.PinCenter;
            if(settings.PinCenter)edit.Checked=false;arrow.Enabled=!settings.PinCenter;settings.Save();};
        radarControls.Controls.Add(pin);radarControls.Controls.Add(arrow);
        layout.Controls.Add(radarControls,0,7);
        layout.Controls.Add(new Label {Text="关闭助手只关闭面板。完整停用加载器需退出游戏；卸载时关闭游戏并删除插件目录。\r\n统计口径未核实前，不输出完美通关结论。",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,9),ForeColor=Color.FromArgb(147,169,181)},0,8);
        Controls.Add(layout);overlay=new OverlayForm(settings);
        Shown+=async(s,e)=>{if(Environment.GetCommandLineArgs().Contains("--auto-connect"))await Connect();};
        timer.Tick+=(s,e)=>RefreshState();timer.Start();
        FormClosed+=(s,e)=>{timer.Stop();if(saveSettings){if(!settings.PinCenter){settings.OverlayX=overlay.Left;settings.OverlayY=overlay.Top;}settings.Save();}overlay.Close();};
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
        else if(overlay.PresentationError!=null) header="雷达绘制异常 · "+overlay.PresentationError;
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
            if(s.mode=="results")details.Text="本关已结束。仅使用本次结算数据判定。\r\n\r\n"+String.Join(" · ",s.warnings??new string[0]);
        } else {progress.Text="击杀率  待核实\r\n秘密    待核实 / 待核实";details.Text=header;}
        IntPtr gameWindow=IntPtr.Zero;if(gameProcess!=null)try{gameWindow=gameProcess.MainWindowHandle;}catch(InvalidOperationException){}
        overlay.UpdateAnchor(gameWindow);
        overlay.UpdateState(s!=null?s.level:"未连接",header,progress.Text,s,active);
        if(active&&s.mode=="campaign")details.Text=overlay.TrackingText+"\r\n\r\n"+String.Join(" · ",s.warnings??new string[0]);
        uint foreground;GetWindowThreadProcessId(GetForegroundWindow(),out foreground);
        bool foregroundAllowed=foreground==ownerPid||(gameProcess!=null&&foreground==gameProcess.Id);
        bool shouldShow=settings.OverlayVisible&&foregroundAllowed&&(gameProcess!=null||editOverlay);
        if(shouldShow&&!overlay.Visible)overlay.Show();else if(!shouldShow&&overlay.Visible)overlay.Hide();
    }
}
}
