using System;
using System.Drawing;
using System.Windows.Forms;

namespace BoltgunCompletion {
public sealed class AppearanceDialog:Form {
    public AppearanceDialog(Settings settings,Action preview,bool persist=true) {
        Text="雷达设置 · 分项不透明度与高差";ClientSize=new Size(470,410);FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(22,26,31);ForeColor=Color.FromArgb(225,233,237);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=3,RowCount=10};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,55));
        var help=new Label {Text="0% 完全透明，100% 不透明；仍受整体不透明度影响。",Dock=DockStyle.Fill};
        layout.Controls.Add(help,0,0);layout.SetColumnSpan(help,3);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        var appearance=settings.Appearance;var sliders=new TrackBar[7];
        string[] names={"背景","距离圆环","十字线","秘密标记","敌人标记","玩家箭头","敌人数文字"};
        int[] values={appearance.Background,appearance.Rings,appearance.Axes,appearance.Secrets,appearance.Enemies,appearance.Player,appearance.Counter};
        Action<int,int> set=(i,value)=>{switch(i){case 0:appearance.Background=value;break;case 1:appearance.Rings=value;break;case 2:appearance.Axes=value;break;
            case 3:appearance.Secrets=value;break;case 4:appearance.Enemies=value;break;case 5:appearance.Player=value;break;case 6:appearance.Counter=value;break;}preview();};
        for(int i=0;i<sliders.Length;i++) {
            int index=i;layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));
            var value=new Label {Text=values[i]+"%",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};
            var slider=new TrackBar {Minimum=0,Maximum=100,Value=values[i],TickStyle=TickStyle.None,Dock=DockStyle.Fill,Margin=Padding.Empty};
            slider.ValueChanged+=(s,e)=>{value.Text=slider.Value+"%";set(index,slider.Value);};
            layout.Controls.Add(new Label {Text=names[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i+1);
            layout.Controls.Add(slider,1,i+1);layout.Controls.Add(value,2,i+1);sliders[i]=slider;
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        layout.Controls.Add(new Label {Text="平层容差（米）",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,8);
        var tolerance=new NumericUpDown {DecimalPlaces=1,Minimum=0.5m,Maximum=30,Increment=0.5m,Value=(decimal)settings.HeightTolerance,Width=100};
        tolerance.ValueChanged+=(s,e)=>{settings.HeightTolerance=(double)tolerance.Value;preview();};layout.Controls.Add(tolerance,1,8);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
        var done=new Button {Text="完成",AutoSize=true,BackColor=Color.FromArgb(43,54,63),ForeColor=ForeColor,FlatStyle=FlatStyle.Flat};done.Click+=(s,e)=>Close();
        var reset=new Button {Text="恢复分项默认",AutoSize=true,BackColor=Color.FromArgb(43,54,63),ForeColor=ForeColor,FlatStyle=FlatStyle.Flat};reset.Click+=(s,e)=>{foreach(var slider in sliders)slider.Value=100;};
        buttons.Controls.Add(done);buttons.Controls.Add(reset);layout.Controls.Add(buttons,0,9);layout.SetColumnSpan(buttons,3);
        Controls.Add(layout);if(persist)FormClosed+=(s,e)=>settings.Save();
    }
}
}
