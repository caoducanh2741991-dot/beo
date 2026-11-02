using System;using System.IO;using System.Linq;using System.Drawing;using System.Drawing.Drawing2D;using System.Collections.Generic;using System.Windows.Forms;using System.Xml.Linq;
namespace V15Voice {
public sealed class BeoPalette {
 public Color Background,Surface,Alt,Text,Muted,Line,Accent,OnAccent,Soft,Error,Warning,Success;public bool Dark;
 public static BeoPalette Get(string id){bool dark=id=="dark"||id=="ocean";if(id=="system"){dark=SystemColors.Window.GetBrightness()<.5f;if(Environment.OSVersion.Platform==PlatformID.Win32NT)try{using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))if(k!=null)dark=Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}catch{}}
  if(id=="warm")return New(false,"F5F1EB","FFFCF7","EEE8DD","332D25","706250","DED3C1","805B27","FFFFFF","EFE3CF");
  if(id=="ocean")return New(true,"0B1C28","112B3B","19384B","ECF6FA","ADC8D8","34586E","9CE6E7","123435","21464C");
  return dark?New(true,"151820","1D222C","252D3B","EDF2F9","AAB9CE","384559","BBCBFF","1B2B56","293956"):New(false,"F4F5F7","FFFFFF","EEF1F5","202D3D","57677B","DCE3ED","3F57BA","FFFFFF","E9EDFD");
 }
 static Color C(string s){return ColorTranslator.FromHtml("#"+s);}static BeoPalette New(bool d,params string[] c){return new BeoPalette{Dark=d,Background=C(c[0]),Surface=C(c[1]),Alt=C(c[2]),Text=C(c[3]),Muted=C(c[4]),Line=C(c[5]),Accent=C(c[6]),OnAccent=C(c[7]),Soft=C(c[8]),Error=C(d?"512B35":"FBE9EC"),Warning=C(d?"493E25":"FFF3D6"),Success=C(d?"23463B":"E5F4EB")};}
}
public sealed class BeoAppearance {
 public string Theme="system";public int Scale=100,Page=1;
 public static BeoAppearance Load(string file){var x=StateFiles.Read(file);var a=new BeoAppearance();if(x!=null){string theme=StateFiles.S(x,"theme","system");if(new[]{"system","light","dark","warm","ocean"}.Contains(theme))a.Theme=theme;int scale=StateFiles.I(x,"scale",100);if(new[]{100,110,125}.Contains(scale))a.Scale=scale;int page=StateFiles.I(x,"page",1);a.Page=page==7?1:Math.Max(0,Math.Min(6,page));}return a;}
 public void Save(string file){StateFiles.Write(file,new XElement("appearance",new XAttribute("schema",1),new XAttribute("theme",Theme),new XAttribute("scale",Scale),new XAttribute("page",Page)));}
}
public class BeoButton:Button {
 public bool Primary,Selected,Navigation;public BeoPalette Palette;bool hover;
 public BeoButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;UseVisualStyleBackColor=false;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 public static GraphicsPath Round(RectangleF r,float radius){var path=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));path.AddArc(r.X,r.Y,d,d,180,90);path.AddArc(r.Right-d,r.Y,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.X,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;}
 protected override void OnPaint(PaintEventArgs e){var p=Palette??BeoPalette.Get("light");e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color bg=Primary?p.Accent:Selected?p.Soft:hover&&Enabled?p.Alt:p.Surface,fg=Primary?p.OnAccent:!Enabled?p.Muted:Selected?p.Accent:p.Text;
  e.Graphics.Clear(Parent==null?p.Background:Parent.BackColor);using(var shape=Round(new RectangleF(.7f,.7f,Math.Max(1,Width-1.5f),Math.Max(1,Height-1.5f)),8)){using(var brush=new SolidBrush(bg))e.Graphics.FillPath(brush,shape);if(!Navigation||Selected)using(var pen=new Pen(Selected?p.Accent:p.Line))e.Graphics.DrawPath(pen,shape);}
  var flags=TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(8,4,Math.Max(1,Width-16),Math.Max(1,Height-8)),fg,flags);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Math.Max(1,Width-10),Math.Max(1,Height-10)),fg,bg);
 }
}
public sealed class BeoTabs:TabControl {
 public BeoTabs(){Appearance=TabAppearance.FlatButtons;SizeMode=TabSizeMode.Fixed;ItemSize=new Size(1,1);Multiline=true;Padding=Point.Empty;}
 public override Rectangle DisplayRectangle{get{return new Rectangle(0,0,ClientSize.Width,ClientSize.Height);}}
}
public sealed class BeoTheme {
 readonly Dictionary<Control,Font> fonts=new Dictionary<Control,Font>();readonly HashSet<Control> hooked=new HashSet<Control>();bool applying;string activeTheme="system";double scale=1;public BeoPalette Palette;
 public static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control c in root.Controls)foreach(var item in Walk(c))yield return item;}
 public void Apply(Control root,string theme,int percent){if(applying)return;applying=true;try{var all=Walk(root).ToArray();foreach(var c in all)if(!fonts.ContainsKey(c)){float points=c.Font.SizeInPoints;var descriptor=System.ComponentModel.TypeDescriptor.GetProperties(c)["Font"];if(scale!=1&&descriptor!=null&&!descriptor.ShouldSerializeValue(c))points/=(float)scale;fonts[c]=new Font(c.Font.FontFamily,points,c.Font.Style);}
   activeTheme=theme;Palette=BeoPalette.Get(theme);scale=percent/100.0;root.SuspendLayout();foreach(var c in all){if(c.IsDisposed)continue;var f=fonts[c];float points=(float)(f.SizeInPoints*scale);if(Math.Abs(c.Font.SizeInPoints-points)>.01)c.Font=new Font(f.FontFamily,points,f.Style);Paint(c);if(!hooked.Contains(c)){hooked.Add(c);c.ControlAdded+=(s,e)=>{if(!applying)Apply(e.Control,activeTheme,(int)Math.Round(scale*100));};}}root.ResumeLayout(true);root.Invalidate(true);
  }finally{applying=false;}}
 void Paint(Control c){var p=Palette;bool muted=Convert.ToString(c.Tag)=="muted";c.ForeColor=muted?p.Muted:p.Text;
  if(c is Form)c.BackColor=p.Background;else if(c is TextBoxBase||c is ComboBox||c is NumericUpDown||c is ListBox)c.BackColor=p.Surface;else c.BackColor=c is TabPage||c is GroupBox?p.Surface:p.Background;
  var button=c as BeoButton;if(button!=null){button.Palette=p;button.BackColor=button.Primary?p.Accent:p.Surface;button.ForeColor=button.Primary?p.OnAccent:p.Text;button.FlatAppearance.BorderColor=p.Line;button.Invalidate();return;}
  var plain=c as Button;if(plain!=null){plain.FlatStyle=FlatStyle.Flat;plain.UseVisualStyleBackColor=false;plain.BackColor=p.Surface;plain.FlatAppearance.BorderColor=p.Line;}
  var combo=c as ComboBox;if(combo!=null){combo.FlatStyle=FlatStyle.Flat;combo.IntegralHeight=false;combo.DropDownHeight=360;}
  var progress=c as ProgressBar;if(progress!=null){progress.ForeColor=p.Accent;progress.BackColor=p.Alt;progress.Invalidate();}
  var grid=c as DataGridView;if(grid!=null){grid.BackgroundColor=p.Surface;grid.BorderStyle=BorderStyle.None;grid.GridColor=p.Line;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersDefaultCellStyle.BackColor=p.Alt;grid.ColumnHeadersDefaultCellStyle.ForeColor=p.Muted;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=p.Alt;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=p.Text;grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(7);grid.DefaultCellStyle.BackColor=p.Surface;grid.DefaultCellStyle.ForeColor=p.Text;grid.DefaultCellStyle.SelectionBackColor=p.Soft;grid.DefaultCellStyle.SelectionForeColor=p.Text;grid.DefaultCellStyle.Padding=new Padding(6);grid.AlternatingRowsDefaultCellStyle.BackColor=p.Surface;grid.RowTemplate.MinimumHeight=(int)(52*scale);grid.ColumnHeadersHeight=(int)(40*scale);}
  var menu=c as ToolStrip;if(menu!=null){menu.BackColor=p.Surface;menu.ForeColor=p.Text;foreach(ToolStripItem item in menu.Items){item.BackColor=p.Surface;item.ForeColor=p.Text;}}
 }
}
public sealed class BeoQueueGrid:DataGridView {
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space&&CurrentCell!=null&&CurrentCell.OwningColumn.Name=="selected"){OnCellContentClick(new DataGridViewCellEventArgs(CurrentCell.ColumnIndex,CurrentCell.RowIndex));e.Handled=true;e.SuppressKeyPress=true;return;}base.OnKeyDown(e);}
}
public sealed class BeoProgressBar:ProgressBar {
 public BeoProgressBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 public new int Value{get{return base.Value;}set{base.Value=value;Invalidate();}}
 protected override void OnPaint(PaintEventArgs e){var form=FindForm() as MainForm;var p=form==null?BeoPalette.Get("light"):form.CurrentBeoPalette;e.Graphics.Clear(p.Alt);using(var brush=new SolidBrush(p.Accent))e.Graphics.FillRectangle(brush,0,0,(int)(ClientSize.Width*(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum)),ClientSize.Height);}
}
public sealed class BeoProgressCell:DataGridViewTextBoxCell {
 public override Type ValueType{get{return typeof(int);}}
 static int Number(object value){int number;return Math.Max(0,Math.Min(100,Int32.TryParse(Convert.ToString(value).Trim().TrimEnd('%'),out number)?number:0));}
 protected override object GetFormattedValue(object value,int row,ref DataGridViewCellStyle style,System.ComponentModel.TypeConverter a,System.ComponentModel.TypeConverter b,DataGridViewDataErrorContexts context){return Number(value)+"%";}
 protected override void Paint(Graphics g,Rectangle clip,Rectangle bounds,int row,DataGridViewElementStates state,object value,object formatted,string error,DataGridViewCellStyle style,DataGridViewAdvancedBorderStyle border,DataGridViewPaintParts parts){base.Paint(g,clip,bounds,row,state,value,formatted,error,style,border,parts&~DataGridViewPaintParts.ContentForeground);int percent=Number(value);var form=DataGridView.FindForm() as MainForm;var p=form==null?BeoPalette.Get("light"):form.CurrentBeoPalette;int width=Math.Max(1,bounds.Width-18);using(var track=new SolidBrush(p.Alt))g.FillRectangle(track,bounds.X+9,bounds.Bottom-12,width,4);using(var fill=new SolidBrush(p.Accent))g.FillRectangle(fill,bounds.X+9,bounds.Bottom-12,(int)(width*percent/100.0),4);TextRenderer.DrawText(g,percent+"%",style.Font,new Rectangle(bounds.X+8,bounds.Y+3,bounds.Width-16,bounds.Height-17),style.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);}
}
}
