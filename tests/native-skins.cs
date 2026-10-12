using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MogooCodexStatus.Tests {
  class NativeSkinTests {
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    static object Field(object value,string name) {return value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static double Luminance(string value) {
      var color=(Color)ColorConverter.ConvertFromString(value);double result=0;var weights=new [] {0.2126,0.7152,0.0722};int i=0;
      foreach(byte channel in new [] {color.R,color.G,color.B}) {double n=channel/255.0;result+=weights[i++]*(n<=0.04045 ? n/12.92 : Math.Pow((n+0.055)/1.055,2.4));}return result;
    }
    static double Contrast(string a,string b) {double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);}
    static void Wait(int milliseconds) {var frame=new DispatcherFrame();var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate {timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Invoke(object value,string method,params object[] args) {value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static double Right(FrameworkElement value,Visual root) {return value.TransformToAncestor(root).Transform(new Point(value.ActualWidth,0)).X;}
    static string Text(TextBlock value) {return new TextRange(value.ContentStart,value.ContentEnd).Text;}
    static string Quotas(StackPanel panel) {string result="";foreach(TextBlock child in panel.Children)result+=Text(child);return result;}
    [STAThread] static int Main(string[] args) {
      try {
        string data=Path.GetFullPath(args[0]);Directory.CreateDirectory(data);string config=Path.Combine(data,"config.json");
        Json.Write(config,new {dataDir=data,offsetRight=310,offsetTop=5});
        var window=new StatusWindow(config,true);IntPtr handle=new WindowInteropHelper(window).EnsureHandle();
        var button=(Button)Field(window,"skinButton");var drag=(DragMotion)Field(window,"drag");
        Check(((StatusSkin)Field(window,"skin")).Id=="codex","The default skin must remain Codex");
        Check(!button.Focusable,"The skin button must not take keyboard focus");
        var choices=(List<MenuItem>)Field(window,"skinChoices");
        Check(StatusSkin.All.Length==2 && choices.Count==2,"Only the Codex light and dark skins may be offered");
        foreach(var expected in new [] {"codex-light","codex","codex-light","codex"}) {
          button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
          Check(((StatusSkin)Field(window,"skin")).Id==expected && Json.Text(Json.Read(config),"skin")==expected,"A real skin-button click must change and persist the next skin");
          Check(!drag.Active && window.Height==32,"Switching skins must not start dragging or change the compact height");
          var reloaded=new StatusWindow(config,true);Check(((StatusSkin)Field(reloaded,"skin")).Id==expected,"Restart must reload the selected skin");reloaded.Close();
          int checkedItems=0;foreach(var item in choices)if(item.IsChecked)checkedItems++;
          Check(checkedItems==1,"Exactly one skin choice must remain checked");
        }
        choices[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(((StatusSkin)Field(window,"skin")).Id=="codex-light","The direct-choice menu must select the requested skin");
        using(var locked=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.Read)) {
          button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
          Check(((StatusSkin)Field(window,"skin")).Id=="codex-light","A failed save must keep the current skin");
          Check(Json.Text(Json.Read(config),"skin")=="codex-light","A failed save must preserve the last saved choice");
          Check(((string)button.ToolTip).Contains("未保存"),"A failed save must offer visible retry feedback");
        }
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Json.Text(Json.Read(config),"skin")=="codex","Switching must recover after the saving failure ends");
        var settings=Json.Read(config);Check(Json.Number(settings,"offsetRight")==310 && Json.Number(settings,"offsetTop")==5,"Skin changes must preserve docking preferences");
        var down=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=UIElement.MouseLeftButtonDownEvent,Source=button};
        typeof(StatusWindow).GetMethod("BeginDrag",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{button,down});
        Check(!drag.Active,"A mouse press on the skin button must never begin dragging");
        foreach(var skin in StatusSkin.All) {
          Check(skin.Radius==14 && skin.Font==StatusSkin.All[0].Font,"Both colors must retain the Codex shape and font");
          foreach(var foreground in new [] {skin.Text,skin.Secondary,skin.Warning,skin.Danger})Check(Contrast(skin.Background,foreground)>=4.5,"Normal, secondary, and warning text must remain readable in "+skin.Id);
        }
        Check(!Native.IsWindowVisible(handle),"Skin tests must keep their own window hidden");window.Close();
        Json.Write(config,new {dataDir=data,skin="unknown-skin"});var fallback=new StatusWindow(config,true);
        Check(((StatusSkin)Field(fallback,"skin")).Id=="codex","Unknown saved skins must fall back to Codex");fallback.Close();
        foreach(var legacy in new [] {"frost","midnight","terminal"}) {
          Json.Write(config,new {dataDir=data,skin=legacy,offsetRight=310,offsetTop=5});var migrated=new StatusWindow(config,true);
          Check(((StatusSkin)Field(migrated,"skin")).Id==(legacy=="frost" ? "codex-light" : "codex"),"Legacy preferences must preserve their light/dark direction");
          Check(((Border)migrated.Content).CornerRadius.TopLeft==14,"Legacy skins must also use the Codex shape");migrated.Close();
        }
        Json.Write(config,new {dataDir=data,skin="codex",offsetRight=310,offsetTop=5,previewWidth=480});
        string modeAt=DateTime.UtcNow.ToString("o");Json.Write(Path.Combine(data,"state.json"),new {updatedAt=modeAt,context=new {usedPercent=33.94,updatedAt=modeAt},limits=new [] {new {name="codex",label="7天",remainingPercent=24,usedPercent=76,resetsAt=1791960668,updatedAt=modeAt}}});
        var modeWindow=new StatusWindow(config,true);var mode=(MenuItem)Field(modeWindow,"modeChoice");
        var collapse=(Button)Field(modeWindow,"fullModeButton");var expand=(Button)Field(modeWindow,"compactModeButton");
        Check(!collapse.Focusable && !expand.Focusable && collapse.Cursor==Cursors.Hand && expand.Cursor==Cursors.Hand,"Both mode icons must be direct controls without stealing keyboard focus");
        Check(!mode.IsChecked && modeWindow.Width==480 && modeWindow.Height==32,"Existing installations must start in full mode");
        foreach(var selected in StatusSkin.All) {
          var modeSkinChoices=(List<MenuItem>)Field(modeWindow,"skinChoices");modeSkinChoices[Array.IndexOf(StatusSkin.All,selected)].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
          collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));modeWindow.RenderPreview(Path.Combine(data,"layout",selected.Id+"-compact.png"));
          Check(mode.IsChecked && modeWindow.Width==140 && modeWindow.Height==28 && Json.Text(Json.Read(config),"displayMode")=="compact","Clicking the left icon must switch to and save the compact capsule");
          var capsule=(StackPanel)Field(modeWindow,"compactView");var caption=(TextBlock)Field(modeWindow,"compactLabel");var full=(Grid)Field(modeWindow,"fullView");var icon=(Border)Field(modeWindow,"compactIcon");var root=(Border)modeWindow.Content;
          Check(capsule.Children.Count==2 && caption.Text=="Codex Status" && full.Visibility==Visibility.Collapsed,"Compact mode must display exactly one icon and Codex Status, without extra controls");
          Check(capsule.Visibility==Visibility.Visible && icon.ActualWidth==18 && Right(caption,root)<=139 && caption.ActualHeight<=18,"Compact contents must fit without trimming");
          Check(((string)capsule.ToolTip).Contains("上下文") && ((string)capsule.ToolTip).Contains("7天") && ((string)capsule.ToolTip).Contains("点击左侧图标"),"Compact mode must keep live metric details and a direct expansion hint on hover");
          Check(expand.ActualWidth==24 && expand.ActualHeight==24 && Right(caption,root)<=139,"The icon hit area and caption must fit the capsule");
          foreach(var source in new DependencyObject[] {Field(modeWindow,"fullGlyph") as DependencyObject,Field(modeWindow,"compactGlyph") as DependencyObject}) {
            var iconDown=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=UIElement.MouseLeftButtonDownEvent,Source=source};
            Invoke(modeWindow,"BeginDrag",source,iconDown);Check(!((DragMotion)Field(modeWindow,"drag")).Active,"Pressing either icon or its glyph must never start dragging");
          }
          Check(((SolidColorBrush)caption.Foreground).Color==(Color)ColorConverter.ConvertFromString(selected.Text),"The capsule must follow the chosen skin");
          var reloaded=new StatusWindow(config,true);Check(reloaded.Width==140 && reloaded.Height==28 && ((MenuItem)Field(reloaded,"modeChoice")).IsChecked,"Restart must reload compact mode");reloaded.Close();
          using(var locked=new FileStream(config,FileMode.Open,FileAccess.Read,FileShare.Read)) {
            expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(mode.IsChecked && modeWindow.Width==140 && Json.Text(Json.Read(config),"displayMode")=="compact","A failed mode save must preserve the current capsule and saved mode");
            Check(((string)mode.ToolTip).Contains("未保存"),"A failed mode save must expose retry feedback");
            Check(((string)expand.ToolTip).Contains("重试"),"A failed icon save must expose retry feedback on that same icon");
          }
          expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));modeWindow.RenderPreview(Path.Combine(data,"layout",selected.Id+"-restored.png"));
          Check(!mode.IsChecked && modeWindow.Width==480 && modeWindow.Height==32 && full.Visibility==Visibility.Visible && capsule.Visibility==Visibility.Collapsed,"Retry must restore the full layout and its previous width");
          var held=(DragMotion)Field(modeWindow,"drag");held.Begin(new Native.ScreenPoint(5,5),new Native.Rect {Left=0,Top=0,Right=480,Bottom=32});
          collapse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!mode.IsChecked && modeWindow.Width==480 && Json.Text(Json.Read(config),"displayMode")=="full","A held drag must never change display mode or width");held.End();
          mode.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));mode.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(!mode.IsChecked,"The optional menu must stay synchronized with direct icon switching");
          var saved=Json.Read(config);Check(Json.Number(saved,"offsetRight")==310 && Json.Number(saved,"offsetTop")==5 && Json.Text(saved,"skin")==selected.Id,"Mode changes must preserve docking offsets and skin");
        }
        modeWindow.Close();
        var layout=new StatusWindow(config,true);var layoutChoices=(List<MenuItem>)Field(layout,"skinChoices");
        foreach(var selected in StatusSkin.All) {
          layoutChoices[Array.IndexOf(StatusSkin.All,selected)].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
          foreach(int width in new [] {280,360,480,560})foreach(string status in new [] {"normal","warning","waiting","stale"}) {
            layout.Width=width;string at=DateTime.UtcNow.ToString("o");object context=status=="waiting" ? null : (object)new {usedPercent=status=="warning" ? 99.9 : 33.94,capacityTokens=256000,updatedAt=at};
            double now=(DateTimeOffset.UtcNow-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds;
            Json.Write(Path.Combine(data,"state.json"),new {updatedAt=at,context=context,limits=new [] {new {name="codex",label="5小时",remainingPercent=status=="warning" ? 3 : 100,usedPercent=0,stale=status=="stale",resetsAt=now+8310,updatedAt=at},new {name="codex",label="7天",remainingPercent=24,usedPercent=76,stale=status=="stale",resetsAt=now+3*86400+18*3600,updatedAt=at}}});
            layout.RenderPreview(Path.Combine(data,"layout",selected.Id+"-"+width+"-"+status+".png"));
            var root=(Border)layout.Content;var contextLabel=(TextBlock)Field(layout,"contextText");var quota=(StackPanel)Field(layout,"limits");var reset=(TextBlock)Field(layout,"resetText");var switcher=(Button)Field(layout,"skinButton");
            double quotaLeft=quota.TransformToAncestor(root).Transform(new Point()).X,buttonLeft=switcher.TransformToAncestor(root).Transform(new Point()).X;
            Check(Right(contextLabel,root)<=quotaLeft+0.1,"Context text must not overlap quotas: "+selected.Id+"/"+width+"/"+status);
            foreach(FrameworkElement child in quota.Children) {
              Check(Right(child,root)<=buttonLeft-7.9,"Quota text must not overlap the skin button: "+selected.Id+"/"+width+"/"+status);
              if(reset.Visibility==Visibility.Visible)Check(Right(child,root)<=reset.TransformToAncestor(root).Transform(new Point()).X,"Quota text must not overlap reset time");
            }
            if(reset.Visibility==Visibility.Visible)Check(Right(reset,root)<=buttonLeft-7.9,"Reset time must not overlap the skin button");
            Check(contextLabel.ActualHeight<=24 && switcher.ActualHeight==24,"Text and skin controls must fit the compact height");
            Check(Text(contextLabel)==(status=="waiting" ? "上下文 等待上报" : status=="warning" ? "上下文 100% / 256k" : "上下文 34% / 256k"),"Context must show the reported percentage and capacity, or a missing-report state");
            Check(Quotas(quota).Contains("余"),"The quota group must preserve its remaining direction at every width");
            foreach(TextBlock child in quota.Children)if(child.Tag==null && (Text(child).Contains("%") || Text(child).Contains("缓存")))Check(child.TextTrimming==TextTrimming.None && child.ActualWidth+0.1>=child.DesiredSize.Width,"Percentages and cache status must never be clipped or ellipsized");
            if(status=="stale")Check(Quotas(quota).Contains("缓存"),"Cached quotas must keep their visible cache marker");
            if(width==560 && status=="normal") {
              Check(reset.Visibility==Visibility.Visible && Text(reset).Contains("5小时重置"),"The compact normal strip must keep the reset owner visible");
              var box=(StackPanel)Field(layout,"contextBox");double first=quotaLeft-Right(box,root),second=reset.TransformToAncestor(root).Transform(new Point()).X-Right(quota,root);
              Check(Math.Abs(first-second)<=1.1 && first>=15,"Both group gaps must be evenly distributed at the normal width");
              Check(Quotas(quota).Contains("5小时100%") && Quotas(quota).Contains("7天24%"),"Two provided periods must remain visible at the normal width");
            }
          }
        }
        layout.Width=480;layoutChoices[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        string current=DateTime.UtcNow.ToString("o");double timestamp=(DateTimeOffset.UtcNow-new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero)).TotalSeconds;
        foreach(string scenario in new [] {"single","missing-reset","pending","minute","unmatched","ambiguous","unknown-quota","mixed-reset","duplicate-period"}) {
          object[] quotaRows=scenario=="mixed-reset" ? new object[] {new {name="codex",label="5小时",remainingPercent=10,resetsAt=timestamp-30,updatedAt=current},new {name="codex",label="7天",remainingPercent=24,resetsAt=timestamp+3600,updatedAt=current}} :
            scenario=="duplicate-period" ? new object[] {new {name="first",label="7天",remainingPercent=24,resetsAt=timestamp+3600,updatedAt=current},new {name="second",label="7天",remainingPercent=60,resetsAt=timestamp+86400,updatedAt=current}} :
            new object[] {new {name="codex",label="7天",remainingPercent=scenario=="unknown-quota" ? (double?)null : 24,usedPercent=76,resetsAt=scenario=="missing-reset" ? 0 : scenario=="pending" ? timestamp-30 : scenario=="minute" ? timestamp+40 : timestamp+3*86400+18*3600+40,updatedAt=current}};
          Json.Write(Path.Combine(data,"state.json"),new {updatedAt=current,selectedTitle="test conversation",selectionStatus=scenario=="unmatched" ? "unknown" : scenario,context=scenario=="unmatched" || scenario=="ambiguous" ? null : (object)new {usedPercent=33.94,usedTokens=87704,capacityTokens=258400,updatedAt=current},limits=quotaRows});
          layout.RenderPreview(Path.Combine(data,"layout","semantics-"+scenario+".png"));
          var contextLabel=(TextBlock)Field(layout,"contextText");var quota=(StackPanel)Field(layout,"limits");var reset=(TextBlock)Field(layout,"resetText");string text=Text(reset),quotaText=Quotas(quota);
          if(scenario=="single") {
            Check(quotaText.Contains("7天24%") && !quotaText.Contains("5小时"),"A single reported seven-day window must not fabricate another period");
            Check(reset.Visibility==Visibility.Visible && text.Contains("7天重置") && text.Contains("3天18小时"),"The 480px single-window strip must retain an owned countdown");
            Check(((string)contextLabel.ToolTip).Contains("33.9%") && ((string)contextLabel.ToolTip).Contains("87,704"),"Context details must retain precise usage and token counts");
            Check(Text(contextLabel)=="上下文 34% / 258.4k","Context capacity must follow the reported window instead of a fixed model size");
            Check(((string)reset.ToolTip).Contains(DateTime.UtcNow.Year.ToString()) && ((string)reset.ToolTip).Contains("北京时间"),"Full reset timestamps belong in the tooltip");
          }
          if(scenario=="missing-reset")Check(text=="7天重置 未提供","A missing reset time must not look like an expired timestamp");
          if(scenario=="pending")Check(text=="7天 待确认重置" && quotaText.Contains("24%"),"An expired timestamp must wait for confirmation without restoring quota");
          if(scenario=="minute")Check(text=="7天重置 <1分钟","Countdowns must stop at minutes and never display seconds");
          if(scenario=="unmatched" || scenario=="ambiguous")Check(Text(contextLabel).Contains(scenario=="unmatched" ? "未匹配" : "聊天重名"),"Unknown conversations must keep their distinct state");
          if(scenario=="unknown-quota")Check(quotaText.Contains("7天—") && !quotaText.Contains("7天0%"),"Missing quota percentages must not default to zero");
          if(scenario=="mixed-reset")Check(text.StartsWith("7天重置") && ((string)reset.ToolTip).Contains("5小时") && ((string)reset.ToolTip).Contains("待确认重置"),"Show the next upcoming reset while keeping pending windows in details");
          if(scenario=="duplicate-period")Check(quotaText.Contains("first") && text.Contains("first") && ((string)reset.ToolTip).Contains("second"),"Equal periods in different buckets must have distinct reset owners");
        }
        layout.Close();
        // An automatically launched strip belongs to the desktop process, not an OS session.
        using(var owner=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"") {UseShellExecute=false,CreateNoWindow=true})) {
          try {
            var owned=new StatusWindow(config,false,owner.Id);bool closed=false;owned.Closed+=delegate {closed=true;};new WindowInteropHelper(owned).EnsureHandle();
            Invoke(owned,"Tick",false);Check(!closed,"The strip must remain alive while its desktop owner is alive");
            owner.Kill();owner.WaitForExit();Invoke(owned,"Tick",false);
            var ended=Json.Read(Path.Combine(data,"runtime.json"));Check(closed && Json.Text(ended,"reason")=="codex-exited","The strip must close when its desktop owner exits");
          } finally {if(!owner.HasExited) {owner.Kill();owner.WaitForExit();}}
        }
        Json.Write(config,new {dataDir=data,skin="codex"});var motion=new StatusWindow(config,false);var face=(Border)motion.Content;
        string frames=Path.Combine(data,"motion");Directory.CreateDirectory(frames);
        Invoke(motion,"ApplySkin",StatusSkin.Find("codex-light"),true);Wait(1);motion.RenderPreview(Path.Combine(frames,"skin-start.png"));
        Check(((SolidColorBrush)face.Background).Color==(Color)ColorConverter.ConvertFromString(StatusSkin.Find("codex-light").Background),"Light/dark changes must take effect immediately to keep text contrast");
        var turn=(RotateTransform)Field(motion,"skinTurn");double startAngle=turn.Angle;Wait(90);motion.RenderPreview(Path.Combine(frames,"skin-middle.png"));double middleAngle=turn.Angle;
        Wait(150);motion.RenderPreview(Path.Combine(frames,"skin-end.png"));
        if(SystemParameters.ClientAreaAnimation)Check(startAngle<middleAngle && middleAngle<0 && Math.Abs(turn.Angle)<0.001,"The palette must rotate through an actual intermediate angle when switching skins");
        Invoke(motion,"ApplySkin",StatusSkin.Find("codex"),true);
        Check(((SolidColorBrush)face.Background).Color==(Color)ColorConverter.ConvertFromString(StatusSkin.Find("codex").Background),"Switching back to dark must also keep text contrast");
        Invoke(motion,"ChangeDisplayMode",true);Wait(1);motion.RenderPreview(Path.Combine(frames,"mode-start.png"));var modeView=(StackPanel)Field(motion,"compactView");double modeStart=modeView.Opacity;
        Wait(60);motion.RenderPreview(Path.Combine(frames,"mode-middle.png"));double modeMiddle=modeView.Opacity;
        Wait(100);motion.RenderPreview(Path.Combine(frames,"mode-end.png"));
        if(SystemParameters.ClientAreaAnimation)Check(modeStart<modeMiddle && modeView.Opacity==1,"Mode changes must bring in the destination contents without animating window position");
        Invoke(motion,"ChangeDisplayMode",false);Wait(140);
        Invoke(motion,"AppearOnce");Wait(1);motion.RenderPreview(Path.Combine(frames,"entry-start.png"));double entryStart=face.Opacity;
        Wait(70);motion.RenderPreview(Path.Combine(frames,"entry-middle.png"));double entryMiddle=face.Opacity;
        Wait(120);motion.RenderPreview(Path.Combine(frames,"entry-end.png"));
        if(SystemParameters.ClientAreaAnimation)Check(entryStart<entryMiddle && face.Opacity==1,"First appearance must fade to full opacity");
        Invoke(motion,"AppearOnce");Check(face.Opacity==1,"Reshowing the strip must not replay entry motion");
        motion.RenderPreview(Path.Combine(frames,"press-start.png"));Invoke(motion,"PressFeedback",true);Wait(80);motion.RenderPreview(Path.Combine(frames,"press-middle.png"));
        Check(Math.Abs(((ScaleTransform)Field(motion,"buttonScale")).ScaleX-0.94)<0.001,"Pressing must give immediate button feedback");
        Invoke(motion,"PressFeedback",false);Wait(150);motion.RenderPreview(Path.Combine(frames,"press-end.png"));
        Check(Math.Abs(((ScaleTransform)Field(motion,"buttonScale")).ScaleX-1)<0.001,"Release must restore the button");
        var modeButton=(Button)Field(motion,"fullModeButton");var modeScale=(ScaleTransform)Field(motion,"fullModeScale");
        Invoke(motion,"ModeFeedback",modeButton,modeScale,true);motion.RenderPreview(Path.Combine(frames,"icon-press-start.png"));Wait(35);motion.RenderPreview(Path.Combine(frames,"icon-press-middle.png"));
        for(int i=0;i<30 && Math.Abs(modeScale.ScaleX-0.92)>=0.001;i++)Wait(10);
        Check(Math.Abs(modeScale.ScaleX-0.92)<0.001,"The mode icon must show press feedback without moving the strip: "+modeScale.ScaleX);
        Invoke(motion,"ModeFeedback",modeButton,modeScale,false);for(int i=0;i<30 && Math.Abs(modeScale.ScaleX-1)>=0.001;i++)Wait(10);motion.RenderPreview(Path.Combine(frames,"icon-press-end.png"));
        Check(Math.Abs(modeScale.ScaleX-1)<0.001,"The mode icon must recover its normal size after release");motion.Close();
        Console.WriteLine("PASS: two skins, direct mode-icon switching/restart/save failure/drag isolation, 32 full layouts, 9 reset states, contrast, equal gaps, and motion frames");return 0;
      }catch(Exception error) {Console.Error.WriteLine(error.ToString());return 1;}
    }
  }
}
