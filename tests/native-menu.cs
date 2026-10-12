using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MogooCodexStatus.Tests {
  class NativeMenuTests {
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    static object Field(object value,string name) {return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static void Wait(int milliseconds) {var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Render(StatusWindow window,ContextMenu menu,Native.Rect strip,Native.Rect popup,string file) {
      double dpi=Native.GetDpiForWindow(new WindowInteropHelper(window).Handle),scale=dpi>0 ? dpi/96 : 1;
      double x=(popup.Left-strip.Left)/scale,y=(popup.Top-strip.Top)/scale;
      double top=Math.Min(0,y),left=Math.Min(0,x),width=Math.Max(window.Width,x+menu.ActualWidth)-left,height=Math.Max(window.Height,y+menu.ActualHeight)-top;
      var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) {
        dc.DrawRectangle(new VisualBrush((Visual)window.Content),null,new System.Windows.Rect(-left,-top,window.Width,window.Height));
        dc.DrawRectangle(new VisualBrush(menu),null,new System.Windows.Rect(x-left,y-top,menu.ActualWidth,menu.ActualHeight));
      }
      var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width),(int)Math.Ceiling(height),96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
      var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(file))png.Save(stream);
    }
    [STAThread] static int Main(string[] args) {
      try {
        string data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);int checks=0;
        foreach(var skin in StatusSkin.All)foreach(bool compact in new [] {false,true})foreach(bool bottom in new [] {false,true}) {
          string config=Path.Combine(data,"config.json");Json.Write(config,new {dataDir=data,skin=skin.Id,displayMode=compact ? "compact" : "full",previewWidth=480});
          var window=new StatusWindow(config,true);try {
            IntPtr handle=new WindowInteropHelper(window).EnsureHandle();
            window.Left=-32000;window.Top=-32000;window.Show();Wait(25);window.Hide();
            window.RenderPreview(Path.Combine(data,"strip.png"));
            var menu=(ContextMenu)Field(window,"menu");
            Check(menu.Placement==PlacementMode.Custom && menu.PlacementTarget==window.Content,"Menus must anchor to the entire strip, including its compact mode");
            var candidates=menu.CustomPopupPlacementCallback(new Size(155,130),new Size(window.Width,window.Height),new Point());
            Check(candidates[0].Point.Y>=window.Height+6 && candidates[1].Point.Y+130<=-6,"Both popup placement candidates must leave a gap and avoid covering strip text");
            double scale=Native.GetDpiForWindow(handle)/96.0;if(scale<=0)scale=1;
            var area=SystemParameters.WorkArea;int top=bottom ? (int)Math.Round((area.Bottom-window.Height-8)*scale) : (int)Math.Round((area.Top+80)*scale);
            Native.SetWindowPos(handle,IntPtr.Zero,(int)Math.Round((area.Left+100)*scale),top,(int)Math.Round(window.Width*scale),(int)Math.Round(window.Height*scale),0x14);
            Native.Rect strip,popup;Check(Native.GetWindowRect(handle,out strip),"Strip geometry unavailable");
            menu.IsOpen=true;Wait(80);var source=PresentationSource.FromVisual(menu) as HwndSource;
            Check(source!=null && Native.IsWindowVisible(source.Handle) && Native.GetWindowRect(source.Handle,out popup),"The real native menu popup must be visible (open="+menu.IsOpen+", source="+(source!=null)+", loaded="+menu.IsLoaded+")");
            Native.GetWindowRect(source.Handle,out popup);
            Check(bottom ? popup.Bottom<=strip.Top-3 : popup.Top>=strip.Bottom+3,"The popup must open below the strip, or above it when the bottom screen edge prevents that");
            foreach(object row in menu.Items) {
              var item=row as MenuItem;if(item==null)continue;var point=item.TransformToAncestor(menu).Transform(new Point(0,0));
              Check(item.ActualHeight>0 && point.Y>=0 && point.Y+item.ActualHeight<=menu.ActualHeight+1 && item.IsEnabled,"Every menu choice must fit and remain enabled");
            }
            var tick=typeof(StatusWindow).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic);
            for(int i=0;i<6;i++) {tick.Invoke(window,new object[]{false});Wait(10);}
            Check(menu.IsOpen && Json.Flag(Json.Read(Path.Combine(data,"runtime.json")),"menuOpen"),"Menu interaction must survive periodic foreground and docking checks");
            Native.Rect unchanged;Native.GetWindowRect(handle,out unchanged);
            Check(strip.Left==unchanged.Left && strip.Top==unchanged.Top && strip.Right==unchanged.Right && strip.Bottom==unchanged.Bottom,"Timer ticks must keep the popup's placement target fixed");
            var order=new List<IntPtr>();Native.EnumWindows(delegate(IntPtr hwnd,IntPtr unused) {order.Add(hwnd);return true;},IntPtr.Zero);
            Check(order.IndexOf(source.Handle)>=0 && order.IndexOf(source.Handle)<order.IndexOf(handle),"The popup must remain above the topmost strip after multiple timer ticks");
            if(!bottom)Render(window,menu,strip,popup,Path.Combine(data,skin.Id+(compact ? "-compact" : "-full")+"-menu.png"));
            menu.IsOpen=false;Wait(20);Check(!menu.IsOpen,"Closing the popup must release the menu hold");
            Check(!Native.IsWindowVisible(handle),"Regression owners must remain hidden and must not activate or move the user's applications");checks++;
          } finally {window.Close();}
        }
        Console.WriteLine("PASS: "+checks+" native popup layouts, complete choices, screen-edge fallback, z-order, and stable timer interaction");return 0;
      }catch(Exception error) {Console.Error.WriteLine(error.ToString());return 1;}
    }
  }
}
