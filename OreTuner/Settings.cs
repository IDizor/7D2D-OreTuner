using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using bln = System.Boolean;
using flt = System.Single;

namespace OreTuner
{
    internal static class Settings
    {
        public static bln ModIsEnabled = true;
        public static flt OreVeinsAmount = 100.0f;
        public static bln BoulderOnTheSurface = true;
        public static bln ColoredDotOnTheMap = true;

        static Settings()
        {
            // load settings from json file
            var path = Path.ChangeExtension(Assembly.GetAssembly(typeof(OreTuner)).Location, "config");
            if (File.Exists(path))
            {
                try
                {
                    var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
                    typeof(Settings).GetFields(BindingFlags.Static | BindingFlags.Public).ToList().ForEach(f =>
                    {
                        if (settings.TryGetValue(f.Name, out object v))
                        {
                            f.SetValue(null, Convert.ChangeType(v, f.FieldType));
                        }
                    });
                }
                catch
                {
                    ModIsEnabled = false;
                    Debug.LogException(new Exception($"Mod {nameof(OreTuner)}: Failed to parse config file."));
                }
            }
            else
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(typeof(Settings)
                    .GetFields(BindingFlags.Static | BindingFlags.Public)
                    .ToDictionary(f => f.Name, f => f.GetValue(null)), Formatting.Indented));
            }
        }
    }
}
