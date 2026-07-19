using System;
using UnityEngine;

[assembly: System.Reflection.AssemblyVersion("5.4.23.2")]
[assembly: System.Reflection.AssemblyFileVersion("5.4.23.2")]

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BepInDependency : Attribute
    {
        public enum DependencyFlags { HardDependency = 1, SoftDependency = 2 }
        public BepInDependency(string DependencyGUID, DependencyFlags Flags = DependencyFlags.HardDependency) { }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        public Configuration.ConfigFile Config { get { return null; } }
        public Logging.ManualLogSource Logger { get { return null; } }
    }
}

namespace BepInEx.Configuration
{
    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description) { return null; }
    }

    public class ConfigEntry<T>
    {
        public T Value { get; set; }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogError(object data) { }
    }
}
