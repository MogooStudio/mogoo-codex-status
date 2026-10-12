using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Controls;

namespace MogooCodexStatus.Tests {
  class NativeDragTests {
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    static Native.Rect Rect(int x,int y,int w,int h) {return new Native.Rect {Left=x,Top=y,Right=x+w,Bottom=y+h};}
    [STAThread] static int Main(string[] args) {
      try {
        var drag=new DragMotion();var start=new Native.ScreenPoint(510,205);Native.ScreenPoint position;
        drag.Begin(start,Rect(400,180,650,32));
        for(int i=0;i<100;i++)Check(!drag.Update(start,out position),"A held stationary mouse must not move the strip");
        Check(!drag.End(),"A click must not save a new position");
        drag.Begin(start,Rect(400,180,650,32));
        Check(drag.Update(new Native.ScreenPoint(570,185),out position) && position.X==460 && position.Y==160,"The strip must follow the absolute screen cursor delta");
        for(int i=0;i<100;i++)Check(!drag.Update(new Native.ScreenPoint(570,185),out position) && position.X==460 && position.Y==160,"Window-generated mouse events must not accumulate displacement");
        Check(drag.Update(start,out position) && position.X==400 && position.Y==180,"Returning the cursor must restore the exact original position");
        Check(drag.End() && !drag.End(),"Capture loss and mouse release must finish only once");
        Check(!drag.Update(new Native.ScreenPoint(900,900),out position),"Later mouse events must not move a finished drag");
        foreach(double scale in new [] {1.0,1.5,2.0}) {
          var parent=Rect(-1920,-100,1920,1080);var window=Rect(-1420,40,(int)(650*scale),(int)(32*scale));
          var offset=DragMotion.Offset(window,parent,650,scale);
          Check(Math.Abs(parent.Right-(650+offset.X)*scale-window.Left)<0.001 && Math.Abs(parent.Top+offset.Y*scale-window.Top)<0.001,"Saved offsets must reconstruct the same physical position on mixed-DPI and negative-coordinate monitors");
          int width=window.Right-window.Left,inset=(int)Math.Round(8*scale);
          var dock=DragMotion.DockPosition(parent,window.Left,width,scale);
          Check(dock.X==window.Left && dock.Y==parent.Top+(int)Math.Round(5*scale),"Top docking must preserve horizontal position and scale its top inset");
          Check(DragMotion.DockPosition(parent,parent.Left-1000,width,scale).X==parent.Left+inset,"Dragging past the left edge must snap inside the parent");
          Check(DragMotion.DockPosition(parent,parent.Right+1000,width,scale).X==parent.Right-width-inset,"Dragging past the right edge must snap inside the parent");
          var saved=DragMotion.Offset(Rect(dock.X,dock.Y,width,window.Bottom-window.Top),parent,650,scale);
          var followed=DragMotion.DockPosition(parent,(int)Math.Round(parent.Right-(650+saved.X)*scale),width,scale);
          Check(followed.X==dock.X && followed.Y==dock.Y,"Automatic following must keep exactly the snapped position after saving");
        }
        Check(DragMotion.DockPosition(Rect(100,200,300,500),999,300,1).X==100,"A strip that fills the parent must not gain horizontal overflow from padding");
        string data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);string config=Path.Combine(data,"config.json");Json.Write(config,new {dataDir=data});
        var windowUnderTest=new StatusWindow(config,true);IntPtr handle=new WindowInteropHelper(windowUnderTest).EnsureHandle();
        Native.SetWindowPos(handle,IntPtr.Zero,320,180,650,32,0x14); // Hidden test window; never moves the user's mouse or activates another app.
        Native.Rect before,after;Check(Native.GetWindowRect(handle,out before),"Test window geometry unavailable");
        var controller=(DragMotion)typeof(StatusWindow).GetField("drag",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(windowUnderTest);
        controller.Begin(start,before);var tick=typeof(StatusWindow).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic);
        for(int i=0;i<8;i++)tick.Invoke(windowUnderTest,new object[]{false});
        Check(Native.GetWindowRect(handle,out after) && before.Left==after.Left && before.Top==after.Top && before.Right==after.Right,"Timer ticks must neither reposition nor resize the held window");
        Check(Json.Flag(Json.Read(Path.Combine(data,"runtime.json")),"dragging"),"Timer must suspend window detection and layout while held");
        controller.End();tick.Invoke(windowUnderTest,new object[]{false});
        Check(!Json.Flag(Json.Read(Path.Combine(data,"runtime.json")),"dragging"),"Automatic window detection must resume after release");
        var surface=(UIElement)typeof(StatusWindow).GetField("dragSurface",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(windowUnderTest);
        controller.Begin(start,after);surface.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0) {RoutedEvent=Mouse.LostMouseCaptureEvent});
        Check(!controller.Active,"The actual capture-loss event must end dragging");
        controller.Begin(start,after);surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=UIElement.MouseLeftButtonUpEvent});
        Check(!controller.Active,"The actual mouse-up event must end dragging");
        var parentUnderTest=new Window {WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};
        IntPtr parentHandle=new WindowInteropHelper(parentUnderTest).EnsureHandle();
        Native.SetWindowPos(parentHandle,IntPtr.Zero,100,100,1600,900,0x14);
        Native.Rect parentFrame;Check(Native.FrameBounds(parentHandle,out parentFrame) && parentFrame.Right>parentFrame.Left,"Hidden parent geometry unavailable");
        double parentDpi=Native.GetDpiForWindow(parentHandle),parentScale=parentDpi>0 ? parentDpi/96 : 1;
        typeof(StatusWindow).GetField("dragParent",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(windowUnderTest,parentHandle);
        var endDrag=typeof(StatusWindow).GetMethod("EndDrag",BindingFlags.Instance|BindingFlags.NonPublic);
        var changeMode=typeof(StatusWindow).GetMethod("ChangeDisplayMode",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(bool compact in new [] {false,true}) {
        changeMode.Invoke(windowUnderTest,new object[]{compact});
        int stripWidth=(int)(windowUnderTest.Width*parentScale),stripHeight=(int)(windowUnderTest.Height*parentScale);
        foreach(int releasedLeft in new [] {parentFrame.Left+100,parentFrame.Left-stripWidth-200,parentFrame.Right+200}) {
          Native.SetWindowPos(handle,IntPtr.Zero,releasedLeft,parentFrame.Top+250,stripWidth,stripHeight,0x14);
          Check(Native.GetWindowRect(handle,out before),"Release position unavailable");controller.Begin(start,before);
          Check(controller.Update(new Native.ScreenPoint(start.X+1,start.Y),out position),"Test drag must count as moved");
          var dock=DragMotion.DockPosition(parentFrame,before.Left,stripWidth,parentScale);
          endDrag.Invoke(windowUnderTest,new object[]{true});
          Check(!controller.Active && Native.GetWindowRect(handle,out after) && after.Left==dock.X && after.Top==dock.Y && after.Right-after.Left==stripWidth,"Finishing a moved drag must snap the native window once without resizing");
          var saved=Json.Read(config);
          Check(Json.Number(saved,"offsetTop")==5,"Release must save the fixed top docking inset");
          Check((int)Math.Round(parentFrame.Right-(windowUnderTest.Width+Json.Number(saved,"offsetRight"))*parentScale)==after.Left,"Release must persist the snapped horizontal position");
          endDrag.Invoke(windowUnderTest,new object[]{true});
          Check(Native.GetWindowRect(handle,out before) && before.Left==after.Left && before.Top==after.Top,"Repeated release/capture loss must not move a docked window again");
        }
        }
        Json.Write(Path.Combine(data,"selection.json"),new {hwnd=parentHandle.ToInt64()});
        typeof(StatusWindow).GetField("selection",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(windowUnderTest,Json.Read(Path.Combine(data,"selection.json")));
        var collapse=(Button)typeof(StatusWindow).GetField("fullModeButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(windowUnderTest);
        var expand=(Button)typeof(StatusWindow).GetField("compactModeButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(windowUnderTest);
        changeMode.Invoke(windowUnderTest,new object[]{false});
        int fullWidth=(int)Math.Round(windowUnderTest.Width*parentScale);
        foreach(int left in new [] {parentFrame.Left+100,parentFrame.Right-(int)Math.Round(140*parentScale)-8}) {
          Native.SetWindowPos(handle,IntPtr.Zero,left,parentFrame.Top+5,fullWidth,(int)(32*parentScale),0x14);
          collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var saved=Json.Read(config);
          int compactLeft=(int)Math.Round(parentFrame.Right-(140+Json.Number(saved,"offsetRight"))*parentScale);
          Check(compactLeft==left,"Icon collapse must retain the left position rather than jump to the right edge");
          Native.SetWindowPos(handle,IntPtr.Zero,compactLeft,parentFrame.Top+5,(int)(140*parentScale),(int)(28*parentScale),0x14);
          using(var locked=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var current=(Dictionary<string,object>)typeof(StatusWindow).GetField("config",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(windowUnderTest);
            Check(windowUnderTest.Width==140 && Json.Number(current,"offsetRight")==Json.Number(saved,"offsetRight"),"A failed expansion must keep both mode and position in memory");
          }
          expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));saved=Json.Read(config);
          int expected=DragMotion.DockPosition(parentFrame,compactLeft,fullWidth,parentScale).X;
          Check((int)Math.Round(parentFrame.Right-(windowUnderTest.Width+Json.Number(saved,"offsetRight"))*parentScale)==expected,"Icon expansion must retain the left position or clamp inside the parent at its right edge");
        }
        Check(!Native.IsWindowVisible(handle) && !Native.IsWindowVisible(parentHandle),"Regression windows must remain hidden");
        parentUnderTest.Close();windowUnderTest.Close();
        Console.WriteLine("PASS: stable dragging/top snapping, DPI bounds, icon-switch left anchoring/right-edge clamping, and failed-save position preservation");return 0;
      } catch(Exception error) {Console.Error.WriteLine(error.ToString());return 1;}
    }
  }
}
