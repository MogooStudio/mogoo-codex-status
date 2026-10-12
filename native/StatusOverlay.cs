using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MogooCodexStatus {
  public static class Json {
    public static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
    public static Dictionary<string,object> Read(string file) { try { return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file,Encoding.UTF8)); } catch { return new Dictionary<string,object>(); } }
    public static string Text(Dictionary<string,object> d,string key,string fallback="") { object v; return d!=null && d.TryGetValue(key,out v) && v!=null ? Convert.ToString(v,CultureInfo.InvariantCulture) : fallback; }
    public static double Number(Dictionary<string,object> d,string key,double fallback=0) { double v; return Double.TryParse(Text(d,key),NumberStyles.Float,CultureInfo.InvariantCulture,out v) ? v : fallback; }
    public static bool Flag(Dictionary<string,object> d,string key) { return Text(d,key).Equals("True",StringComparison.OrdinalIgnoreCase); }
    public static Dictionary<string,object> Object(Dictionary<string,object> d,string key) { object v; return d!=null && d.TryGetValue(key,out v) ? v as Dictionary<string,object> : null; }
    public static bool Write(string file,object value) {
      Directory.CreateDirectory(Path.GetDirectoryName(file)); string temp=file+".native.tmp";
      File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value),new UTF8Encoding(false));
      for(int i=0;i<3;i++) { try { if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file); return true; } catch(IOException) {Thread.Sleep(30);} }
      return false;
    }
  }
  public static class Native {
    public static Dictionary<string,object> DesktopInfo() {
      var result=new Dictionary<string,object> {{"pid",0}};
      foreach(var process in Process.GetProcesses())try {using(process) {
        if((process.ProcessName=="ChatGPT" || process.ProcessName=="Codex") && process.MainWindowHandle!=IntPtr.Zero && process.MainModule.FileName.IndexOf("WindowsApps\\OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)>=0) {result["pid"]=process.Id;return result;}
      }}catch{}
      return result;
    }
    [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] public struct ScreenPoint {public int X,Y;public ScreenPoint(int x,int y) {X=x;Y=y;}}
    public delegate bool EnumCallback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback,IntPtr data);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out ScreenPoint point);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr window,int attr,out Rect rect,int size);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int cx,int cy,uint flags);
    public static bool FrameBounds(IntPtr window,out Rect rect) {return DwmGetWindowAttribute(window,9,out rect,16)==0 || GetWindowRect(window,out rect);}
    public static List<IntPtr> Windows() {
      var windows=new List<IntPtr>();
      EnumWindows(delegate(IntPtr h,IntPtr p) {
        if(!IsWindowVisible(h)) return true;
        uint id; GetWindowThreadProcessId(h,out id);
        try {using(var process=Process.GetProcessById((int)id)) {
          if(process.ProcessName.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("Codex",StringComparison.OrdinalIgnoreCase)) windows.Add(h);
        }} catch {}
        return true;
      },IntPtr.Zero);
      return windows;
    }
    public static Dictionary<string,object> Inspect() {
      var foreground=GetForegroundWindow(); var windows=Windows();
      IntPtr target=windows.Contains(foreground) ? foreground : windows.Count==1 ? windows[0] : IntPtr.Zero;
      var result=new Dictionary<string,object>(); result["updatedAt"]=DateTime.UtcNow.ToString("o");
      result["hwnd"]=target.ToInt64(); result["foreground"]=target!=IntPtr.Zero && target==foreground;
      result["codexMode"]=false; result["title"]="";
      if(target==IntPtr.Zero || IsIconic(target)) return result;
      try {
        var root=AutomationElement.FromHandle(target);
        var doc=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
        if(doc==null) return result;
        // Read only the document title and mode control, never conversation text or browser documents.
        var queue=new Queue<AutomationElement>();queue.Enqueue(doc);bool codex=false;int seen=0;var clock=Stopwatch.StartNew();
        while(queue.Count>0 && seen++<180 && clock.ElapsedMilliseconds<2500) {
          var item=queue.Dequeue();string name=item.Current.Name;
          if(name=="切换模式，当前模式：Codex" || name=="Switch mode, current mode: Codex" || name=="与 Codex 协作" || name=="Work with Codex") {codex=true;break;}
          if(item!=doc && item.Current.ControlType==ControlType.Document)continue;
          var children=item.FindAll(TreeScope.Children,System.Windows.Automation.Condition.TrueCondition);foreach(AutomationElement child in children)queue.Enqueue(child);
        }
        result["codexMode"]=codex; result["title"]=doc.Current.Name;
      } catch {result["error"]="accessibility-unavailable";}
      result["updatedAt"]=DateTime.UtcNow.ToString("o");
      return result;
    }
  }
  public sealed class DragMotion {
    public const double TopInset=5;
    Native.ScreenPoint cursorOrigin,windowOrigin,lastPosition;
    public bool Active {get;private set;}
    public bool Moved {get;private set;}
    public void Begin(Native.ScreenPoint cursor,Native.Rect window) {
      cursorOrigin=cursor;windowOrigin=new Native.ScreenPoint(window.Left,window.Top);lastPosition=windowOrigin;Moved=false;Active=true;
    }
    public bool Update(Native.ScreenPoint cursor,out Native.ScreenPoint position) {
      position=lastPosition;if(!Active)return false;
      position=new Native.ScreenPoint(windowOrigin.X+cursor.X-cursorOrigin.X,windowOrigin.Y+cursor.Y-cursorOrigin.Y);
      if(position.X==lastPosition.X && position.Y==lastPosition.Y)return false;
      lastPosition=position;Moved=true;return true;
    }
    public bool End() {bool moved=Active && Moved;Active=false;return moved;}
    public static Native.ScreenPoint DockPosition(Native.Rect parent,int left,int width,double scale) {
      int inset=Math.Min((int)Math.Round(8*scale),Math.Max(0,(parent.Right-parent.Left-width)/2));
      int minimum=parent.Left+inset,maximum=Math.Max(minimum,parent.Right-width-inset);
      return new Native.ScreenPoint(Math.Max(minimum,Math.Min(maximum,left)),parent.Top+(int)Math.Round(TopInset*scale));
    }
    public static Point Offset(Native.Rect window,Native.Rect parent,double width,double scale) {
      return new Point((parent.Right-window.Left)/scale-width,(window.Top-parent.Top)/scale);
    }
  }
  public sealed class StatusSkin {
    public readonly string Id,Name,Background,Border,Text,Secondary,Track,Accent,Warning,Danger,Hover,Font;
    public readonly double Radius;
    StatusSkin(string id,string name,string background,string border,string text,string secondary,string track,string accent,string warning,string danger,string hover,double radius,string font) {
      Id=id;Name=name;Background=background;Border=border;Text=text;Secondary=secondary;Track=track;Accent=accent;Warning=warning;Danger=danger;Hover=hover;Radius=radius;Font=font;
    }
    public bool IsLight {get {return Id=="codex-light";}}
    public static readonly StatusSkin[] All={
      // oil-git's product-demo palettes, applied to the same Codex composer shape.
      new StatusSkin("codex","Codex 深色","#282c34","#363d49","#d7dae0","#abb2bf","#363d49","#98c379","#e5c07b","#efc0c4","#363d49",14,"Segoe UI, Microsoft YaHei UI"),
      new StatusSkin("codex-light","Codex 浅色","#fafafa","#d9dce2","#383a42","#5c6370","#e9ebef","#407338","#986801","#a63c45","#e9ebef",14,"Segoe UI, Microsoft YaHei UI")
    };
    public static StatusSkin Find(string id) {if(id=="frost")id="codex-light";foreach(var skin in All)if(skin.Id==id)return skin;return All[0];}
    public static StatusSkin Next(StatusSkin skin) {return All[(Array.IndexOf(All,skin)+1)%All.Length];}
  }
  public class StatusWindow : Window {
    public const double MaximumWidth=560,SingleLimitWidth=480,CompactWidth=140;
    readonly string configPath,dataDir; readonly Dictionary<string,object> config;
    readonly int desktopPid;Process desktop;bool desktopGone;
    readonly TextBlock contextText=new TextBlock(), resetText=new TextBlock();
    readonly StackPanel limits=new StackPanel();
    readonly StackPanel contextBox=new StackPanel();
    readonly ColumnDefinition firstGap=new ColumnDefinition(),secondGap=new ColumnDefinition();
    readonly DragMotion drag=new DragMotion();readonly Border dragSurface;IntPtr dragParent;
    readonly bool previewMode;readonly Button skinButton=new Button();readonly Border buttonFace=new Border();readonly ContextMenu menu=new ContextMenu();
    double menuAnchorX;IntPtr menuParent;
    readonly List<MenuItem> skinChoices=new List<MenuItem>();readonly ScaleTransform buttonScale=new ScaleTransform(1,1);
    readonly RotateTransform skinTurn=new RotateTransform();
    readonly Grid fullView=new Grid();readonly StackPanel compactView=new StackPanel();
    readonly Button fullModeButton=new Button(),compactModeButton=new Button();
    readonly Border fullIcon=new Border();readonly System.Windows.Shapes.Path fullGlyph=new System.Windows.Shapes.Path();
    readonly ScaleTransform fullModeScale=new ScaleTransform(1,1),compactModeScale=new ScaleTransform(1,1);
    readonly Border compactIcon=new Border();readonly System.Windows.Shapes.Path compactGlyph=new System.Windows.Shapes.Path();
    readonly TextBlock compactLabel=new TextBlock();readonly MenuItem modeChoice=new MenuItem {Header="精简模式",IsCheckable=true};
    bool compactMode,displayError;double expandedWidth;
    StatusSkin skin;bool appeared;
    Process worker; bool ending,started; IntPtr handle; Dictionary<string,object> selection;
    readonly DispatcherTimer timer=new DispatcherTimer();
    static Brush Color(string color) {return (Brush)new BrushConverter().ConvertFromString(color);}
    public StatusWindow(string configFile,bool preview,int parentDesktopPid=0) {
      desktopPid=parentDesktopPid;
      if(!preview && desktopPid>0) {try {desktop=Process.GetProcessById(desktopPid);desktopGone=desktop.HasExited;}catch(ArgumentException) {desktopGone=true;}}
      configPath=configFile;config=Json.Read(configFile);dataDir=Json.Text(config,"dataDir");previewMode=preview;
      if(String.IsNullOrEmpty(dataDir)) throw new InvalidOperationException("Invalid configuration");
      expandedWidth=preview ? Math.Max(280,Math.Min(MaximumWidth,Json.Number(config,"previewWidth",MaximumWidth))) : SingleLimitWidth;
      Title="mogoo-codex-status";Width=expandedWidth;Height=32;MinWidth=280;WindowStyle=WindowStyle.None;
      ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
      ShowInTaskbar=false;ShowActivated=false;Topmost=true;Focusable=false;
      FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=13;
      UseLayoutRounding=true;SnapsToDevicePixels=true;
      var border=new Border {BorderThickness=new Thickness(1),Padding=new Thickness(14,3,8,3)};
      dragSurface=border;
      var grid=fullView;grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      firstGap.Width=secondGap.Width=new GridLength(1,GridUnitType.Star);grid.ColumnDefinitions.Add(firstGap);
      grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      grid.ColumnDefinitions.Add(secondGap);
      grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      contextBox.Orientation=Orientation.Horizontal;contextBox.VerticalAlignment=VerticalAlignment.Center;
      SetupModeButton(fullModeButton,fullIcon,fullGlyph,fullModeScale);fullModeButton.Margin=new Thickness(0,0,8,0);contextBox.Children.Add(fullModeButton);
      contextText.Foreground=Color("#f3f3f3");contextText.VerticalAlignment=VerticalAlignment.Center;contextBox.Children.Add(contextText);
      grid.Children.Add(contextBox);
      limits.Orientation=Orientation.Horizontal;limits.VerticalAlignment=VerticalAlignment.Center; Grid.SetColumn(limits,2);grid.Children.Add(limits);
      resetText.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(resetText,4);grid.Children.Add(resetText);
      var icon=new System.Windows.Shapes.Path {Data=Geometry.Parse("M8,1 C4.1,1 1,4.1 1,8 C1,11.9 4.1,15 8,15 L9.2,15 C10.3,15 10.9,13.8 10.2,13 L9.7,12.4 C9.2,11.8 9.6,11 10.4,11 L12,11 C14.2,11 15,9.6 15,8 C15,4.1 11.9,1 8,1 M4.5,7 L4.5,7.1 M6.5,4.5 L6.5,4.6 M10,4.5 L10,4.6 M12,7 L12,7.1"),Width=15,Height=15,StrokeThickness=1.3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
      icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") {Source=skinButton});
      icon.RenderTransform=skinTurn;icon.RenderTransformOrigin=new Point(0.5,0.5);
      buttonFace.CornerRadius=new CornerRadius(6);buttonFace.Background=Brushes.Transparent;buttonFace.Child=icon;
      skinButton.Width=24;skinButton.Height=24;skinButton.Margin=new Thickness(8,0,0,0);skinButton.Padding=new Thickness(0);skinButton.Focusable=false;skinButton.Cursor=Cursors.Hand;
      skinButton.Content=buttonFace;skinButton.RenderTransform=buttonScale;skinButton.RenderTransformOrigin=new Point(0.5,0.5);
      var buttonTemplate=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));buttonTemplate.VisualTree=presenter;skinButton.Template=buttonTemplate;
      AutomationProperties.SetName(skinButton,"切换皮肤");Grid.SetColumn(skinButton,5);grid.Children.Add(skinButton);
      skinButton.Click+=delegate {ChangeSkin(StatusSkin.Next(skin));};
      skinButton.MouseEnter+=delegate {SetBrush(buttonFace,Border.BackgroundProperty,skin.Hover,true);};
      skinButton.MouseLeave+=delegate {SetBrush(buttonFace,Border.BackgroundProperty,"#00000000",true);PressFeedback(false);};
      skinButton.PreviewMouseLeftButtonDown+=delegate {PressFeedback(true);};skinButton.PreviewMouseLeftButtonUp+=delegate {PressFeedback(false);};
      skinButton.LostMouseCapture+=delegate {PressFeedback(false);};
      compactView.Orientation=Orientation.Horizontal;compactView.HorizontalAlignment=HorizontalAlignment.Center;compactView.VerticalAlignment=VerticalAlignment.Center;
      SetupModeButton(compactModeButton,compactIcon,compactGlyph,compactModeScale);
      compactLabel.Text="Codex Status";compactLabel.FontWeight=FontWeights.SemiBold;compactLabel.Margin=new Thickness(8,0,0,0);compactLabel.VerticalAlignment=VerticalAlignment.Center;
      compactView.Children.Add(compactModeButton);compactView.Children.Add(compactLabel);
      var host=new Grid();host.Children.Add(fullView);host.Children.Add(compactView);border.Child=host;Content=border;
      border.MouseLeftButtonDown+=BeginDrag;
      border.MouseMove+=delegate(object sender,MouseEventArgs e) {if(!drag.Active)return;if(e.LeftButton!=MouseButtonState.Pressed) {EndDrag(true);return;} MoveDrag();e.Handled=true;};
      border.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) {if(!drag.Active)return;MoveDrag();EndDrag(true);e.Handled=true;};
      border.LostMouseCapture+=delegate {EndDrag(true);};
      foreach(var choice in StatusSkin.All) {var selectedSkin=choice;var item=new MenuItem {Header=choice.Name,IsCheckable=true};item.Click+=delegate {ChangeSkin(selectedSkin);};skinChoices.Add(item);menu.Items.Add(item);}
      menu.Items.Add(new Separator());menu.Items.Add(modeChoice);modeChoice.Click+=delegate {ChangeDisplayMode(!compactMode);};
      menu.Items.Add(new Separator());var exit=new MenuItem {Header="退出状态条"};exit.Click+=delegate {Close();};menu.Items.Add(exit);border.ContextMenu=menu;
      menu.PlacementTarget=border;menu.Placement=PlacementMode.Custom;menu.CustomPopupPlacementCallback=MenuPlacement;
      border.ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e) {
        menuAnchorX=e.CursorLeft>=0 ? Mouse.GetPosition(border).X : border.ActualWidth/2;
        menuParent=new IntPtr((long)Json.Number(selection,"hwnd"));EndDrag(true);
      };
      menu.Opened+=delegate {EndDrag(true);Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(RaiseMenu));};
      menu.Closed+=delegate {menuParent=IntPtr.Zero;};
      ApplySkin(StatusSkin.Find(Json.Text(config,"skin")),false);
      ApplyDisplayMode(Json.Text(config,"displayMode")=="compact",false);
      SourceInitialized+=delegate {
        handle=new WindowInteropHelper(this).Handle;var source=HwndSource.FromHwnd(handle);source.AddHook(Hook);
        Native.SetWindowLongPtr(handle,-20,new IntPtr(Native.GetWindowLongPtr(handle,-20).ToInt64() | 0x08000000L | 0x80L));
      };
      Loaded+=delegate {
        if(started)return;started=true;
        if(!preview) {StartWorker();Task.Run((Action)Collect);}
        timer.Interval=TimeSpan.FromMilliseconds(500);timer.Tick+=delegate {Tick(preview);};timer.Start();Tick(preview);
      };
      Closed+=delegate {ending=true;menu.IsOpen=false;EndDrag(false);timer.Stop();try {if(worker!=null && !worker.HasExited) {worker.StandardInput.WriteLine("stop");if(!worker.WaitForExit(2500))worker.Kill();}}catch{} if(desktop!=null)desktop.Dispose();};
    }
    CustomPopupPlacement[] MenuPlacement(Size popup,Size target,Point offset) {
      double x=Math.Max(0,Math.Min(Math.Max(0,target.Width-popup.Width),menuAnchorX));
      return new [] {
        new CustomPopupPlacement(new Point(x,target.Height+6),PopupPrimaryAxis.Horizontal),
        new CustomPopupPlacement(new Point(x,-popup.Height-6),PopupPrimaryAxis.Horizontal)
      };
    }
    void RaiseMenu() {
      if(!menu.IsOpen)return;var source=PresentationSource.FromVisual(menu) as HwndSource;
      if(source!=null)Native.SetWindowPos(source.Handle,new IntPtr(-1),0,0,0,0,0x13); // Keep the popup above the strip without moving or activating it.
    }
    bool Animations {get {return !previewMode && SystemParameters.ClientAreaAnimation;}}
    void SetupModeButton(Button button,Border face,System.Windows.Shapes.Path glyph,ScaleTransform scale) {
      face.Width=18;face.Height=18;face.CornerRadius=new CornerRadius(9);
      glyph.Data=Geometry.Parse("M1,7 L4,7 L6,2 L8,12 L10,7 L13,7");glyph.Width=13;glyph.Height=12;glyph.StrokeThickness=1.5;
      glyph.StrokeStartLineCap=glyph.StrokeEndLineCap=PenLineCap.Round;glyph.StrokeLineJoin=PenLineJoin.Round;
      glyph.HorizontalAlignment=HorizontalAlignment.Center;glyph.VerticalAlignment=VerticalAlignment.Center;face.Child=glyph;
      button.Width=24;button.Height=24;button.Padding=new Thickness(0);button.Focusable=false;button.Cursor=Cursors.Hand;
      button.Background=Brushes.Transparent;button.Content=face;face.RenderTransform=scale;face.RenderTransformOrigin=new Point(0.5,0.5);
      var template=new ControlTemplate(typeof(Button));var root=new FrameworkElementFactory(typeof(Grid));root.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
      var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));
      presenter.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);root.AppendChild(presenter);template.VisualTree=root;button.Template=template;
      button.Click+=delegate {ChangeDisplayMode(!compactMode);};
      button.MouseEnter+=delegate {ModeFeedback(button,scale,false);};button.MouseLeave+=delegate {ModeFeedback(button,scale,false);};
      button.PreviewMouseLeftButtonDown+=delegate {ModeFeedback(button,scale,true);};button.PreviewMouseLeftButtonUp+=delegate {ModeFeedback(button,scale,false);};
      button.LostMouseCapture+=delegate {ModeFeedback(button,scale,false);};
    }
    void ModeFeedback(Button button,ScaleTransform scale,bool pressed) {
      double target=pressed ? 0.92 : button.IsMouseOver ? 1.06 : 1;
      if(Animations) {var animation=new DoubleAnimation(target,TimeSpan.FromMilliseconds(pressed ? 70 : 120));scale.BeginAnimation(ScaleTransform.ScaleXProperty,animation);scale.BeginAnimation(ScaleTransform.ScaleYProperty,animation);}
      else {scale.ScaleX=target;scale.ScaleY=target;}
    }
    void SetBrush(DependencyObject target,DependencyProperty property,string value,bool animate) {
      var old=target.GetValue(property) as SolidColorBrush;var next=(SolidColorBrush)Color(value);var brush=new SolidColorBrush(next.Color);
      target.SetValue(property,brush);
      if(animate && Animations && old!=null)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(old.Color,next.Color,TimeSpan.FromMilliseconds(180)));
    }
    void PressFeedback(bool pressed) {
      double target=pressed ? 0.94 : 1;
      if(Animations) {var animation=new DoubleAnimation(target,TimeSpan.FromMilliseconds(pressed ? 70 : 120));buttonScale.BeginAnimation(ScaleTransform.ScaleXProperty,animation);buttonScale.BeginAnimation(ScaleTransform.ScaleYProperty,animation);}
      else {buttonScale.ScaleX=target;buttonScale.ScaleY=target;}
    }
    void AppearOnce() {if(appeared)return;appeared=true;if(Animations)dragSurface.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));}
    void ApplySkin(StatusSkin next,bool animate) {
      bool sameSurface=skin!=null && skin.IsLight==next.IsLight;
      if(animate && Animations)skinTurn.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(-18,0,TimeSpan.FromMilliseconds(180)));
      // Never interpolate light and dark text through low-contrast gray. Use button feedback for that change.
      animate=animate && sameSurface;
      skin=next;FontFamily=new FontFamily(next.Font);dragSurface.CornerRadius=new CornerRadius(next.Radius);
      SetBrush(dragSurface,Border.BackgroundProperty,next.Background,animate);SetBrush(dragSurface,Border.BorderBrushProperty,next.Border,animate);
      SetBrush(contextText,TextBlock.ForegroundProperty,next.Text,animate);SetBrush(resetText,TextBlock.ForegroundProperty,next.Secondary,animate);
      SetBrush(skinButton,Control.ForegroundProperty,next.Secondary,animate);
      SetBrush(compactLabel,TextBlock.ForegroundProperty,next.Text,animate);SetBrush(compactIcon,Border.BackgroundProperty,next.Text,animate);SetBrush(compactGlyph,System.Windows.Shapes.Shape.StrokeProperty,next.Background,animate);
      SetBrush(fullIcon,Border.BackgroundProperty,next.Text,animate);SetBrush(fullGlyph,System.Windows.Shapes.Shape.StrokeProperty,next.Background,animate);
      SetBrush(buttonFace,Border.BackgroundProperty,skinButton.IsMouseOver ? next.Hover : "#00000000",animate);
      menu.Background=Color(next.Background);menu.Foreground=Color(next.Text);menu.BorderBrush=Color(next.Border);
      for(int i=0;i<skinChoices.Count;i++)skinChoices[i].IsChecked=StatusSkin.All[i]==next;
      skinButton.ToolTip="皮肤："+next.Name+"\n单击切换到 "+StatusSkin.Next(next).Name+"\n右键直接选择皮肤";
    }
    void ChangeSkin(StatusSkin next) {
      object previous;bool hadValue=config.TryGetValue("skin",out previous);config["skin"]=next.Id;bool saved=false;
      try {saved=Json.Write(configPath,config);}catch(IOException) {}catch(UnauthorizedAccessException) {}
      if(!saved) {if(hadValue)config["skin"]=previous;else config.Remove("skin");for(int i=0;i<skinChoices.Count;i++)skinChoices[i].IsChecked=StatusSkin.All[i]==skin;skinButton.ToolTip="皮肤未保存，请重试";return;}
      ApplySkin(next,true);Tick(previewMode);
    }
    void ApplyDisplayMode(bool compact,bool animate) {
      if(!compactMode)expandedWidth=Width;compactMode=compact;
      MinWidth=compact ? CompactWidth : 280;Width=compact ? CompactWidth : expandedWidth;Height=compact ? 28 : 32;
      dragSurface.Padding=compact ? new Thickness(10,1,10,1) : new Thickness(14,3,8,3);
      fullView.Visibility=compact ? Visibility.Collapsed : Visibility.Visible;compactView.Visibility=compact ? Visibility.Visible : Visibility.Collapsed;
      modeChoice.IsChecked=compact;modeChoice.ToolTip="只显示图标与 Codex Status；取消勾选恢复完整指标";
      fullModeButton.ToolTip="收起为精简模式";compactModeButton.ToolTip="展开完整指标";
      AutomationProperties.SetName(fullModeButton,"收起为精简模式");AutomationProperties.SetName(compactModeButton,"展开完整指标");
      fullView.ToolTip=displayError ? "显示模式未保存，请重试" : null;
      if(animate && Animations) {UIElement view=compact ? (UIElement)compactView : fullView;view.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(130)));}
    }
    void ChangeDisplayMode(bool compact) {
      if(drag.Active || compact==compactMode) {modeChoice.IsChecked=compactMode;return;}
      object previous,previousOffset;bool hadValue=config.TryGetValue("displayMode",out previous),hadOffset=config.TryGetValue("offsetRight",out previousOffset);
      // Keep the left-side control near the pointer; only move it when expansion reaches the window edge.
      Native.Rect parent,window;IntPtr target=new IntPtr((long)Json.Number(selection,"hwnd"));
      if(handle!=IntPtr.Zero && target!=IntPtr.Zero && Native.FrameBounds(target,out parent) && Native.GetWindowRect(handle,out window)) {
        double dpi=Native.GetDpiForWindow(target),scale=dpi>0 ? dpi/96 : 1,nextWidth=compact ? CompactWidth : expandedWidth;
        var dock=DragMotion.DockPosition(parent,window.Left,(int)Math.Round(nextWidth*scale),scale);config["offsetRight"]=(parent.Right-dock.X)/scale-nextWidth;
      }
      config["displayMode"]=compact ? "compact" : "full";bool saved=false;
      try {saved=Json.Write(configPath,config);}catch(IOException) {}catch(UnauthorizedAccessException) {}
      if(!saved) {
        if(hadValue)config["displayMode"]=previous;else config.Remove("displayMode");displayError=true;modeChoice.IsChecked=compactMode;modeChoice.ToolTip="显示模式未保存，请重试";
        if(hadOffset)config["offsetRight"]=previousOffset;else config.Remove("offsetRight");
        fullModeButton.ToolTip=compactModeButton.ToolTip="显示模式未保存，请点击图标重试";
        fullView.ToolTip=compactView.ToolTip="显示模式未保存，请重试";return;
      }
      displayError=false;ApplyDisplayMode(compact,true);Tick(previewMode);
    }
    void BeginDrag(object sender,MouseButtonEventArgs e) {
      var source=e.OriginalSource as DependencyObject;while(source!=null) {if(source==skinButton || source==fullModeButton || source==compactModeButton)return;source=source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);}
      if(e.ClickCount==2) {EndDrag(true);Json.Write(Path.Combine(dataDir,"refresh.json"),new {at=DateTime.UtcNow.ToString("o")});e.Handled=true;return;}
      EndDrag(true);Native.ScreenPoint cursor;Native.Rect rect;
      if(handle==IntPtr.Zero || !Native.GetCursorPos(out cursor) || !Native.GetWindowRect(handle,out rect))return;
      dragParent=new IntPtr((long)Json.Number(selection,"hwnd"));drag.Begin(cursor,rect);
      if(!dragSurface.CaptureMouse())drag.End();e.Handled=true;
    }
    void MoveDrag() {
      Native.ScreenPoint cursor,position;
      if(drag.Active && Native.GetCursorPos(out cursor) && drag.Update(cursor,out position))
        Native.SetWindowPos(handle,new IntPtr(-1),position.X,position.Y,0,0,0x11); // No resize or activation; screen pixels remain stable while the window moves.
    }
    void EndDrag(bool save) {
      if(!drag.Active)return;bool moved=drag.End();
      // End the drag before releasing capture, since releasing raises LostMouseCapture synchronously.
      if(dragSurface.IsMouseCaptured)dragSurface.ReleaseMouseCapture();
      if(!save || !moved || ending || dragParent==IntPtr.Zero)return;
      Native.Rect parent,window;
      if(!Native.FrameBounds(dragParent,out parent) || !Native.GetWindowRect(handle,out window))return;
      double dpi=Native.GetDpiForWindow(dragParent);double scale=dpi>0 ? dpi/96 : 1;
      var dock=DragMotion.DockPosition(parent,window.Left,window.Right-window.Left,scale);
      if(!Native.SetWindowPos(handle,new IntPtr(-1),dock.X,dock.Y,0,0,0x11) || !Native.GetWindowRect(handle,out window))return;
      Point offset=DragMotion.Offset(window,parent,Width,scale);
      config["offsetRight"]=offset.X;config["offsetTop"]=DragMotion.TopInset;
      try {Json.Write(configPath,config);}catch(IOException) {}
    }
    IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {if(message==0x21) {handled=true;return new IntPtr(3);} return IntPtr.Zero;}
    static string Quote(string s) {return "\""+s.Replace("\"","\\\"")+"\"";}
    void StartWorker() {
      var script=Path.Combine(Json.Text(config,"pluginRoot"),"server","worker.mjs");
      var start=new ProcessStartInfo(Json.Text(config,"nodePath"),"--no-warnings "+Quote(script)+" --config "+Quote(configPath)+" --parent-pid "+Process.GetCurrentProcess().Id) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,RedirectStandardInput=true};
      foreach(var key in new [] {"CODEX_THREAD_ID","CODEX_SESSION_ID","CODEX_APP_TOOLS_PIPE_PATH"}) start.EnvironmentVariables.Remove(key);
      worker=new Process {StartInfo=start};worker.ErrorDataReceived+=delegate{};worker.OutputDataReceived+=delegate{};
      worker.Start();worker.BeginErrorReadLine();worker.BeginOutputReadLine();
    }
    void Collect() {
      while(!ending) {
        try {Json.Write(Path.Combine(dataDir,"selection.json"),Native.Inspect());}catch{}
        Thread.Sleep(1200);
      }
    }
    static string Percent(double n) {return Double.IsNaN(n) || Double.IsInfinity(n) ? "—" : n.ToString("0.#",CultureInfo.InvariantCulture)+"%";}
    static string Capacity(double tokens) {return tokens>0 && !Double.IsInfinity(tokens) ? (tokens/1000).ToString("0.#",CultureInfo.InvariantCulture)+"k" : "—";}
    void TextParts(TextBlock block,string label,string value,string valueColor) {
      block.Inlines.Clear();block.Inlines.Add(new Run(label) {Foreground=Color(skin.Secondary)});
      block.Inlines.Add(new Run(value) {Foreground=Color(valueColor),FontWeight=FontWeights.SemiBold});
    }
    static string Period(Dictionary<string,object> limit,bool compact) {
      string label=Json.Text(limit,"label","限额");return compact ? label.Replace("小时","h").Replace("分钟","m") : label;
    }
    static Dictionary<string,object> NextReset(List<Dictionary<string,object>> rows) {
      Dictionary<string,object> future=null,pending=null;double now=(DateTimeOffset.UtcNow-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds;
      foreach(var row in rows) {double at=Json.Number(row,"resetsAt");if(at>now && (future==null || at<Json.Number(future,"resetsAt")))future=row;
        else if(at>0 && at<=now && (pending==null || at>Json.Number(pending,"resetsAt")))pending=row;}
      return future ?? pending ?? (rows.Count>0 ? rows[0] : null);
    }
    string LimitName(Dictionary<string,object> row,List<Dictionary<string,object>> rows,bool compact) {
      string label=Period(row,compact);int matches=0,index=0;
      foreach(var other in rows)if(Period(other,false)==Period(row,false)) {matches++;if(other==row)index=matches;}
      if(matches<2)return label;
      string name=Json.Text(row,"name",Json.Text(row,"id","限额"));if(name.Length>6)name=name.Substring(0,6)+"…";
      return label+"·"+name+index;
    }
    string LimitDetails(Dictionary<string,object> row,List<Dictionary<string,object>> rows,bool fresh) {
      return LimitName(row,rows,false)+" · "+Json.Text(row,"name")+"\n剩余 "+Percent(Json.Number(row,"remainingPercent",Double.NaN))+" · 已用 "+Percent(Json.Number(row,"usedPercent",Double.NaN))+
        "\n重置："+ResetDate(row)+"（北京时间） · "+Countdown(Json.Number(row,"resetsAt"))+"\n更新："+LocalTime(Json.Text(row,"updatedAt"))+(Json.Flag(row,"stale") || !fresh ? "（缓存数据）" : "");
    }
    void RenderLimits(List<Dictionary<string,object>> rows,int shown,bool compact,bool dense,bool fresh,string summary,Dictionary<string,object> focus) {
      limits.Children.Clear();
      limits.Children.Add(new TextBlock {Text=rows.Count==0 ? "限额 等待查询" : (compact ? "剩余" : "限额剩余")+(dense ? "" : " "),Foreground=Color(skin.Secondary),VerticalAlignment=VerticalAlignment.Center});
      for(int i=0;i<Math.Min(shown,rows.Count);i++) {
        if(i>0)limits.Children.Add(new TextBlock {Text=" · ",Foreground=Color(skin.Secondary),VerticalAlignment=VerticalAlignment.Center});
        var row=focus==null ? rows[i] : i==0 ? focus : rows[0]==focus ? rows[1] : rows[0];double remaining=Json.Number(row,"remainingPercent",Double.NaN);
        string tip=LimitDetails(row,rows,fresh);
        limits.Children.Add(new TextBlock {Text=LimitName(row,rows,compact),Tag=row,Foreground=Color(skin.Secondary),ToolTip=tip,VerticalAlignment=VerticalAlignment.Center});
        limits.Children.Add(new TextBlock {Text=Percent(remaining),FontWeight=FontWeights.SemiBold,Foreground=Color(remaining<=5 ? skin.Danger : remaining<=20 ? skin.Warning : skin.Text),ToolTip=tip,VerticalAlignment=VerticalAlignment.Center});
        if(Json.Flag(row,"stale") || !fresh)limits.Children.Add(new TextBlock {Text=" 缓存",Foreground=Color(skin.Secondary),ToolTip=tip,VerticalAlignment=VerticalAlignment.Center});
      }
      if(rows.Count>shown)limits.Children.Add(new TextBlock {Text="+"+(rows.Count-shown),Foreground=Color(skin.Secondary),Margin=new Thickness(4,0,0,0),ToolTip=summary,VerticalAlignment=VerticalAlignment.Center});
      limits.ToolTip=summary;
    }
    void RenderReset(Dictionary<string,object> nearest,List<Dictionary<string,object>> rows,bool compact) {
      string name=nearest==null ? "" : LimitName(nearest,rows,compact);double at=Json.Number(nearest,"resetsAt");string remaining=Countdown(at);
      TextParts(resetText,name+(remaining=="待确认重置" ? " " : "重置 "),compact ? remaining.Replace("小时","h").Replace("分钟","m").Replace("分","m") : remaining,skin.Text);
    }
    double ContentWidth() {
      var size=new Size(Double.PositiveInfinity,Double.PositiveInfinity);contextBox.Measure(size);limits.Measure(size);resetText.Measure(size);
      return contextBox.DesiredSize.Width+limits.DesiredSize.Width+(resetText.Visibility==Visibility.Visible ? resetText.DesiredSize.Width : 0)+firstGap.MinWidth+secondGap.MinWidth;
    }
    void Tick(bool preview) {
      if(!preview && desktopPid>0) {
        try {desktopGone=desktopGone || desktop==null || desktop.HasExited;}catch(InvalidOperationException) {desktopGone=true;}
        if(desktopGone) {Json.Write(Path.Combine(dataDir,"runtime.json"),new {visible=false,reason="codex-exited",pid=Process.GetCurrentProcess().Id,desktopPid=desktopPid,updatedAt=DateTime.UtcNow.ToString("o")});Close();return;}
      }
      if(File.Exists(Path.Combine(dataDir,"stop.signal"))) {try{File.Delete(Path.Combine(dataDir,"stop.signal"));}catch{} Close();return;}
      if(menu.IsOpen) {
        var source=PresentationSource.FromVisual(menu) as HwndSource;IntPtr foreground=Native.GetForegroundWindow();
        bool ownForeground=foreground==menuParent || foreground==handle || source!=null && foreground==source.Handle;
        if(previewMode || ownForeground && menuParent!=IntPtr.Zero && Native.IsWindowVisible(menuParent) && !Native.IsIconic(menuParent)) {
          // A popup has its own native window. Re-raising the strip would cover it,
          // and moving its placement target would leave the popup behind.
          Json.Write(Path.Combine(dataDir,"runtime.json"),new {visible=Native.IsWindowVisible(handle),menuOpen=true,pid=Process.GetCurrentProcess().Id,hwnd=handle.ToInt64(),updatedAt=DateTime.UtcNow.ToString("o")});return;
        }
        menu.IsOpen=false;
      }
      if(drag.Active) {
        // Hold the window and its layout still even if accessibility data is briefly stale.
        Json.Write(Path.Combine(dataDir,"runtime.json"),new {visible=Native.IsWindowVisible(handle),dragging=true,pid=Process.GetCurrentProcess().Id,hwnd=handle.ToInt64(),updatedAt=DateTime.UtcNow.ToString("o")});return;
      }
      selection=Json.Read(Path.Combine(dataDir,"selection.json"));
      var state=Json.Read(Path.Combine(dataDir,"state.json"));bool freshState=IsFresh(state,"updatedAt",10);
      var rows=new List<Dictionary<string,object>>();object rawRows;
      var windows=state.TryGetValue("limits",out rawRows) ? rawRows as System.Collections.IEnumerable : null;
      if(windows!=null)foreach(var raw in windows) {var row=raw as Dictionary<string,object>;if(row!=null)rows.Add(row);}
      if(!preview) {
        IntPtr parent=new IntPtr((long)Json.Number(selection,"hwnd"));
        bool fresh=IsFresh(selection,"updatedAt",10);
        if(!fresh || !Json.Flag(selection,"codexMode") || parent==IntPtr.Zero || Native.IsIconic(parent) || Native.GetForegroundWindow()!=parent) {
          if(IsVisible)Hide();
          string reason=!fresh ? "waiting-for-window" : !Json.Flag(selection,"codexMode") ? "not-codex-mode" : parent==IntPtr.Zero ? "no-window" : Native.IsIconic(parent) ? "minimized" : "not-foreground";
          Json.Write(Path.Combine(dataDir,"runtime.json"),new {visible=false,reason=reason,pid=Process.GetCurrentProcess().Id,updatedAt=DateTime.UtcNow.ToString("o")});return;
        }
        Native.Rect rect;if(!Native.FrameBounds(parent,out rect)) {Hide();return;}
        double dpi=Native.GetDpiForWindow(parent);if(dpi<=0)dpi=96;double scale=dpi/96;
        expandedWidth=Math.Min(rows.Count>1 ? MaximumWidth : SingleLimitWidth,Math.Max(280,(rect.Right-rect.Left)/scale-300));
        Width=compactMode ? CompactWidth : expandedWidth;
        int width=(int)(Width*scale);
        var dock=DragMotion.DockPosition(rect,(int)Math.Round(rect.Right-(Width+Json.Number(config,"offsetRight",140))*scale),width,scale);
        if(!IsVisible)Show();Native.SetWindowPos(handle,new IntPtr(-1),dock.X,dock.Y,width,(int)(Height*scale),0x10);
        AppearOnce();
        Json.Write(Path.Combine(dataDir,"runtime.json"),new {visible=Native.IsWindowVisible(handle),pid=Process.GetCurrentProcess().Id,hwnd=handle.ToInt64(),foreground=Native.GetForegroundWindow().ToInt64(),target=parent.ToInt64(),width=Width,height=Height,displayMode=compactMode ? "compact" : "full",updatedAt=DateTime.UtcNow.ToString("o")});
      }
      var context=freshState && (preview || Json.Text(state,"selectedTitle")==Json.Text(selection,"title")) ? Json.Object(state,"context") : null;
      string status=Json.Text(state,"selectionStatus");
      double pct=Json.Number(context,"usedPercent",Double.NaN);bool known=!Double.IsNaN(pct) && !Double.IsInfinity(pct);
      string value=known ? Percent(Math.Round(pct,0,MidpointRounding.AwayFromZero))+" / "+Capacity(Json.Number(context,"capacityTokens",Double.NaN)) : status=="ambiguous" ? "聊天重名" : status=="unmatched" || status=="unknown" && !String.IsNullOrEmpty(Json.Text(state,"selectedTitle")) ? "未匹配" : "等待上报";
      string valueColor=pct>=95 ? skin.Danger : pct>=80 ? skin.Warning : skin.Text;
      contextText.MaxWidth=Double.PositiveInfinity;TextParts(contextText,"上下文 ",value,valueColor);
      var thread=Json.Object(state,"thread");
      contextText.ToolTip=Json.Text(thread,"title",Json.Text(selection,"title"))+"\n"+(!known ? "尚未获得此聊天的 Token 用量" : "上下文已用≈"+Percent(pct)+"\n"+Json.Number(context,"usedTokens").ToString("N0")+" / "+Json.Number(context,"capacityTokens").ToString("N0")+" Token（最近上报，近似值）\n更新："+LocalTime(Json.Text(context,"updatedAt")));
      var nearest=NextReset(rows);var details=new StringBuilder();foreach(var row in rows) {if(details.Length>0)details.Append("\n\n");details.Append(LimitDetails(row,rows,freshState));}
      string summary=rows.Count==0 ? "尚未获得账号限额数据" : details.ToString();resetText.ToolTip=summary;
      compactView.ToolTip="上下文\n"+contextText.ToolTip+"\n\n"+summary+"\n\n"+(displayError ? "显示模式未保存，请重试\n" : "")+"点击左侧图标展开完整指标；按住文字或空白处拖动，松手吸附顶部。";
      if(compactMode)return;
      int shown=Math.Min(2,rows.Count);
      var focus=nearest!=null && rows.IndexOf(nearest)>=shown ? nearest : null;
      firstGap.Width=secondGap.Width=new GridLength(1,GridUnitType.Star);firstGap.MinWidth=secondGap.MinWidth=16;
      resetText.Visibility=rows.Count>0 ? Visibility.Visible : Visibility.Collapsed;
      if(rows.Count==0) {secondGap.MinWidth=0;secondGap.Width=new GridLength(0);}
      RenderLimits(rows,shown,false,false,freshState,summary,focus);RenderReset(nearest,rows,false);
      // Share the free space equally between groups, using actual font measurements before dropping detail.
      double available=Width-56;
      if(ContentWidth()>available)RenderReset(nearest,rows,true);
      if(ContentWidth()>available && shown>1) {
        shown=1;focus=nearest;
        RenderLimits(rows,shown,false,false,freshState,summary,focus);RenderReset(nearest,rows,true);
      }
      if(ContentWidth()>available) {resetText.Visibility=Visibility.Collapsed;secondGap.MinWidth=0;secondGap.Width=new GridLength(0);firstGap.MinWidth=12;}
      if(ContentWidth()>available)RenderLimits(rows,shown,true,false,freshState,summary,focus);
      if(ContentWidth()>available) {RenderLimits(rows,shown,true,true,freshState,summary,focus);firstGap.MinWidth=8;}
      if(ContentWidth()>available && rows.Count>0)((TextBlock)limits.Children[0]).Text="余";
      if(ContentWidth()>available) {
        // Only unusually long service labels need ellipsis; full data remains in both group tooltips.
        double extra=ContentWidth()-available;
        foreach(var child in limits.Children) {var label=child as TextBlock;if(label==null || label.Tag==null)continue;label.MaxWidth=Math.Max(24,label.DesiredSize.Width-extra);label.TextTrimming=TextTrimming.CharacterEllipsis;break;}
      }
    }
    public void RenderPreview(string file) {
      Tick(true);var border=(Border)Content;border.Measure(new Size(Width,Height));border.Arrange(new System.Windows.Rect(0,0,Width,Height));border.UpdateLayout();
      var bitmap=new RenderTargetBitmap((int)Width,(int)Height,96,96,PixelFormats.Pbgra32);bitmap.Render(border);
      var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));Directory.CreateDirectory(Path.GetDirectoryName(file));using(var stream=File.Create(file))png.Save(stream);
    }
    static bool IsFresh(Dictionary<string,object> d,string key,int seconds) {DateTimeOffset at;return DateTimeOffset.TryParse(Json.Text(d,key),out at) && (DateTimeOffset.UtcNow-at).TotalSeconds<seconds;}
    static string LocalTime(string value) {DateTimeOffset at;return DateTimeOffset.TryParse(value,out at) ? at.ToOffset(TimeSpan.FromHours(8)).ToString("MM-dd HH:mm:ss") : "未知";}
    static string ResetDate(Dictionary<string,object> limit) {double reset=Json.Number(limit,"resetsAt");return reset>0 ? new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(reset).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm") : "未提供";}
    static string Countdown(double reset) {if(reset<=0)return "未提供";var span=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(reset)-DateTimeOffset.UtcNow;if(span.TotalSeconds<=0)return "待确认重置";if(span.Days>0)return span.Days+"天"+span.Hours+"小时";if(span.Hours>0)return span.Hours+"小时"+span.Minutes+"分";return span.TotalMinutes<1 ? "<1分钟" : span.Minutes+"分钟";}
  }
  static class Program {
    [STAThread] static int Main(string[] args) {
      var options=new Dictionary<string,string>();for(int i=0;i<args.Length;i++)if(args[i].StartsWith("--")) {string key=args[i].Substring(2);options[key]=i+1<args.Length && !args[i+1].StartsWith("--") ? args[++i] : "true";}
      if(options.ContainsKey("inspect")) {Json.Write(options["inspect"],Native.Inspect());return 0;}
      if(options.ContainsKey("desktop-info")) {Json.Write(options["desktop-info"],Native.DesktopInfo());return 0;}
      string config;if(!options.TryGetValue("config",out config))return 2;
      var settings=Json.Read(config);string dataDir=Json.Text(settings,"dataDir");if(String.IsNullOrEmpty(dataDir))return 2;
      int desktopPid=0;if(options.ContainsKey("desktop-pid") && (!Int32.TryParse(options["desktop-pid"],out desktopPid) || desktopPid<=0))return 2;
      Directory.CreateDirectory(dataDir);
      if(options.ContainsKey("render")) {new StatusWindow(config,true).RenderPreview(options["render"]);return 0;}
      if(options.ContainsKey("preview")) {new Application().Run(new StatusWindow(config,true));return 0;}
      bool created;using(var mutex=new Mutex(true,"Local\\mogoo-codex-status",out created)) {
        if(!created)return 0;
        try {try{File.Delete(Path.Combine(dataDir,"stop.signal"));}catch{} new Application().Run(new StatusWindow(config,false,desktopPid));return 0;}
        catch(Exception ex) {try{Json.Write(Path.Combine(dataDir,"error.json"),new {type=ex.GetType().Name,at=DateTime.UtcNow.ToString("o")});}catch{} return 1;}
        finally {mutex.ReleaseMutex();}
      }
    }
  }
}
