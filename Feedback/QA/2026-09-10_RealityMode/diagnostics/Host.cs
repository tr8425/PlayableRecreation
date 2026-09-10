using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Verse;
namespace PRQA {
 [StaticConstructorOnStartup] public static class Boot {
  static Boot() { var go=new GameObject("PR QA temporary observer"); UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<Host>(); }
 }
 public class Host:MonoBehaviour {
  public const string Root=@"C:\Users\tr842\AppData\Local\Temp\PR-QA-20260910";
  public static Dictionary<string,object> State=new Dictionary<string,object>();
  public static Action Watch;
  float next;
  void Update() {
   if(Time.realtimeSinceStartup<next)return; next=Time.realtimeSinceStartup+0.2f;
   try { if(Watch!=null)Watch(); } catch(Exception e) { File.AppendAllText(Root+"/watch-errors.txt",e+"\n"); Watch=null; }
   string f=Root+"/command.txt"; if(!File.Exists(f))return;
   string p=File.ReadAllText(f).Trim(); File.Delete(f);
   try { var a=Assembly.Load(File.ReadAllBytes(p)); var result=a.GetType("Command").GetMethod("Run").Invoke(null,null); File.WriteAllText(Root+"/result.txt",DateTime.Now.ToString("o")+"\n"+result); }
   catch(Exception e) { File.WriteAllText(Root+"/result.txt",e.ToString()); }
  }
 }
}
