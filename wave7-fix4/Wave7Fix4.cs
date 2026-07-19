using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BenjaminMenu.Wave7Fix4
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("com.benjaminmars.spt.benjaminmenu", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.benjaminmars.spt.benjaminmenu.wave7.runtime", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid="com.benjaminmars.spt.benjaminmenu.wave7.fix4.completepreset";
        public const string Name="Benjamin Menu Wave 7 FIX4 Complete Presets";
        public const string Version="1.5.0";
        static string stack="0", status="Waiting for a spawn action.";
        Harmony harmony;

        void Awake()
        {
            stack=Config.Bind("Wave 7 Item Spawner","Desired Stack Size","0","0 uses the real runtime maximum; other values are safely clamped.").Value??"0";
            Type t=Find("BenjaminMenu.Wave7.Wave7ItemSpawnerPlugin");
            if(t==null){Logger.LogError("Wave 7 runtime type missing; FIX4 disabled.");return;}
            MethodInfo create=t.GetMethod("CreateRuntimeItem",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo spawn=t.GetMethod("SpawnSelected",BindingFlags.Instance|BindingFlags.NonPublic);
            MethodInfo draw=t.GetMethod("DrawSelectedPanel",BindingFlags.Instance|BindingFlags.NonPublic);
            if(create==null||spawn==null||draw==null){Logger.LogError("Exact Wave 7 patch targets missing; FIX4 disabled.");return;}
            harmony=new Harmony(Guid);
            harmony.Patch(create,prefix:new HarmonyMethod(typeof(Plugin),nameof(CreatePrefix)));
            harmony.Patch(spawn,prefix:new HarmonyMethod(typeof(Plugin),nameof(SpawnPrefix)),postfix:new HarmonyMethod(typeof(Plugin),nameof(SpawnPostfix)));
            harmony.Patch(draw,postfix:new HarmonyMethod(typeof(Plugin),nameof(DrawPostfix)));
            Logger.LogInfo("FIX4 active: complete runtime weapon presets, bare-receiver rejection and adjustable stacks.");
        }
        void OnDestroy(){if(harmony!=null)harmony.UnpatchSelf();}

        static bool CreatePrefix(object __instance,object __0,int __1,ref object __result)
        {
            string id=Text(Member(__0,"TemplateId"));
            string category=Text(Member(__0,"Category"));
            string display=Text(Member(__0,"Name"));
            if(string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("Selected item has no template ID.");
            object factory=Field(__instance,"_itemFactory");
            if(factory==null)throw new InvalidOperationException("ItemFactoryClass is not resolved.");
            bool weapon=string.Equals(category,"Weapons",StringComparison.OrdinalIgnoreCase);
            object item=FactoryPreset(factory,id);
            string source="ItemFactoryClass.GetPresetItem";
            if(weapon&&Count(item)<=1)
            {
                string found;
                object preset=SavedPreset(factory,id,out found);
                if(preset==null)throw new InvalidOperationException("No complete runtime weapon preset exists for "+Label(display,id)+". Bare receiver rejected; nothing spawned.");
                item=Clone(preset); source=found;
            }
            if(item==null)throw new InvalidOperationException("Runtime item creation returned null for "+Label(display,id)+".");
            int nodes=Count(item);
            if(weapon&&nodes<=1)throw new InvalidOperationException("Complete weapon validation rejected a root-only receiver for "+Label(display,id)+".");
            Set(item,"StackObjectsCount",Math.Max(1,__1)); Set(item,"StackCount",Math.Max(1,__1));
            bool fir=PrivateBool(__instance,"GetBool","spawner.foundInRaid",true);
            bool full=PrivateBool(__instance,"GetBool","spawner.fullCondition",true);
            List<object> tree=Tree(item);
            if(fir)foreach(object x in tree){Set(x,"SpawnedInSession",true);Set(x,"FoundInRaid",true);Set(x,"IsFoundInRaid",true);}
            if(full){MethodInfo m=__instance.GetType().GetMethod("MaximizeItemCondition",BindingFlags.Instance|BindingFlags.NonPublic);if(m!=null)m.Invoke(__instance,new[]{item});}
            status=(weapon?"Complete weapon":"Runtime item")+" built from "+source+" ("+nodes+" nodes).";
            __result=item; return false;
        }

        static object FactoryPreset(object factory,string id)
        {
            MethodInfo m=factory.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .FirstOrDefault(x=>x.Name=="GetPresetItem"&&x.GetParameters().Length==1&&x.GetParameters()[0].ParameterType==typeof(string));
            if(m==null)throw new MissingMethodException("ItemFactoryClass.GetPresetItem(string) not found.");
            try{return m.Invoke(factory,new object[]{id});}catch(TargetInvocationException e){throw new InvalidOperationException("GetPresetItem failed: "+(e.InnerException??e).Message,e.InnerException??e);}
        }

        static object SavedPreset(object factory,string id,out string description)
        {
            description=""; IEnumerable presets=Member(factory,"SavedPresets") as IEnumerable;
            if(presets==null)return null;
            object best=null; int bestScore=-1,bestNodes=0,index=0;
            foreach(object p in presets)
            {
                index++; if(p==null)continue; object root=Member(p,"Item"); if(root==null)continue;
                bool encyclopedia=string.Equals(Text(Member(p,"Encyclopedia")),id,StringComparison.OrdinalIgnoreCase);
                bool rootMatch=string.Equals(Text(Member(root,"TemplateId")),id,StringComparison.OrdinalIgnoreCase);
                if(!encyclopedia&&!rootMatch)continue;
                int nodes=Count(root); if(nodes<=1)continue;
                int score=(encyclopedia?1000000:500000)+nodes; if(score<=bestScore)continue;
                best=root;bestScore=score;bestNodes=nodes;
                string n=Text(Member(p,"Name"));
                description=(encyclopedia?"SavedPresets encyclopedia match":"SavedPresets root-template match")+(string.IsNullOrWhiteSpace(n)?"":" '"+n+"'")+" ("+bestNodes+" nodes, entry "+index+")";
            }
            return best;
        }

        static object Clone(object root)
        {
            Type helper=Find("GClass3380"),item=Find("EFT.InventoryLogic.Item");
            if(helper==null||item==null)throw new MissingMemberException("GClass3380 or EFT.InventoryLogic.Item missing.");
            MethodInfo def=helper.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                .FirstOrDefault(x=>x.Name=="CloneItem"&&x.IsGenericMethodDefinition&&x.GetGenericArguments().Length==1&&x.GetParameters().Length==2);
            if(def==null)throw new MissingMethodException("GClass3380.CloneItem<T> missing.");
            try{return def.MakeGenericMethod(item).Invoke(null,new[]{root,(object)null});}catch(TargetInvocationException e){throw new InvalidOperationException("EFT preset clone failed: "+(e.InnerException??e).Message,e.InnerException??e);}
        }

        static void SpawnPrefix(object __instance,out int __state)
        {
            __state=-1; object selected=Call(__instance,"GetSelectedItem"); if(selected==null)return;
            FieldInfo f=selected.GetType().GetField("MaxStack",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(f==null)return;
            int max=Convert.ToInt32(f.GetValue(selected),CultureInfo.InvariantCulture); __state=max;
            int wanted; if(!int.TryParse((stack??"0").Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out wanted)||wanted<=0)return;
            f.SetValue(selected,Math.Max(1,Math.Min(wanted,Math.Max(1,max))));
        }
        static void SpawnPostfix(object __instance,int __state)
        {
            if(__state<0)return; object selected=Call(__instance,"GetSelectedItem"); if(selected==null)return;
            FieldInfo f=selected.GetType().GetField("MaxStack",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); if(f!=null)f.SetValue(selected,__state);
        }
        static void DrawPostfix()
        {
            GUILayout.BeginVertical("box"); GUILayout.Label("Stack control"); GUILayout.BeginHorizontal();
            GUILayout.Label("Stack size (0 = runtime max)",GUILayout.Width(190f)); stack=GUILayout.TextField(stack??"0",GUILayout.Width(130f));
            GUILayout.EndHorizontal(); GUILayout.Label(status); GUILayout.EndVertical();
        }

        static int Count(object root){return Tree(root).Count;}
        static List<object> Tree(object root)
        {
            var r=new List<object>(); if(root==null)return r; Type h=Find("GClass3380");
            if(h!=null){MethodInfo m=h.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(x=>x.Name=="GetAllItems"&&x.GetParameters().Length==1).FirstOrDefault(x=>x.GetParameters()[0].ParameterType.IsInstanceOfType(root));
                if(m!=null)try{IEnumerable e=m.Invoke(null,new[]{root}) as IEnumerable;if(e!=null)foreach(object x in e)if(x!=null)r.Add(x);}catch{} }
            if(r.Count==0)r.Add(root); return r;
        }
        static bool PrivateBool(object o,string name,string key,bool fallback){MethodInfo m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(bool)},null);if(m==null)return fallback;try{object v=m.Invoke(o,new object[]{key,fallback});return v is bool?(bool)v:fallback;}catch{return fallback;}}
        static object Call(object o,string name){MethodInfo m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);return m==null?null:m.Invoke(o,null);}
        static object Field(object o,string name){FieldInfo f=o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);return f==null?null:f.GetValue(o);}
        static object Member(object o,string name){if(o==null)return null;Type t=o.GetType();PropertyInfo p=t.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null&&p.GetIndexParameters().Length==0)try{return p.GetValue(o,null);}catch{}FieldInfo f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)try{return f.GetValue(o);}catch{}return null;}
        static void Set(object o,string name,object value){if(o==null)return;Type t=o.GetType();FieldInfo f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)try{f.SetValue(o,Convert.ChangeType(value,f.FieldType,CultureInfo.InvariantCulture));return;}catch{}PropertyInfo p=t.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(p!=null&&p.CanWrite)try{p.SetValue(o,Convert.ChangeType(value,p.PropertyType,CultureInfo.InvariantCulture),null);}catch{}}
        static Type Find(string name){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())try{Type t=a.GetType(name,false);if(t!=null)return t;}catch{}return null;}
        static string Text(object x){if(x==null)return "";try{return Convert.ToString(x,CultureInfo.InvariantCulture)??"";}catch{return x.ToString()??"";}}
        static string Label(string name,string id){return string.IsNullOrWhiteSpace(name)?id:name+" ("+id+")";}
    }
}
