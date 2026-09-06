using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class CreatorToolsBaronessMiniBossTemplate : MonoBehaviour
    {
        internal bool IsTemplate;
    }

    // Reuses the already serialized Baroness preload. Only uninitialized
    // prefab copies are retained; never copy a mini-boss from a running fight.
    internal sealed class NativeBaronessMiniBossCache : IDisposable
    {
        private static bool capturingTemplate;
        private readonly NativeBaronessHeadTossCache source;
        private readonly Action<string> logInfo;
        private readonly Action<string> logWarning;
        private readonly Dictionary<string, BaronessLevelMiniBossBase> templates =
            new Dictionary<string, BaronessLevelMiniBossBase>(StringComparer.Ordinal);
        private bool captureFailed;
        private bool disposed;

        private static readonly string[] Items =
        {
            CreatorToolsInteractionIds.BaronessCupcake,
            CreatorToolsInteractionIds.BaronessGumball,
            CreatorToolsInteractionIds.BaronessWaffle,
            CreatorToolsInteractionIds.BaronessCandyCorn,
            CreatorToolsInteractionIds.BaronessJawbreaker
        };
        private static readonly string[] PrefabFields =
        {
            "cupcakePrefab", "gumballPrefab", "wafflePrefab",
            "candyCornPrefab", "jawBreakerPrefab"
        };

        internal NativeBaronessMiniBossCache(
            NativeBaronessHeadTossCache source,
            Action<string> logInfo, Action<string> logWarning)
        {
            this.source = source;
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool Ready { get { return !disposed && templates.Count == Items.Length; } }
        internal bool Failed { get { return captureFailed || source.Failed; } }

        internal static bool Supports(string item)
        {
            return Array.IndexOf(Items, item) >= 0;
        }

        internal void Update()
        {
            if (disposed || Ready || captureFailed)
                return;
            var castle = source.CachedCastle;
            if (castle == null)
                return;
            try
            {
                for (var i = 0; i < Items.Length; i++)
                {
                    var field = AccessTools.Field(typeof(BaronessLevelCastle), PrefabFields[i]);
                    var prefab = field == null ? null :
                        field.GetValue(castle) as BaronessLevelMiniBossBase;
                    if (prefab == null)
                        throw new InvalidOperationException("Missing native prefab: " + PrefabFields[i]);
                    BaronessLevelMiniBossBase clone = null;
                    capturingTemplate = true;
                    try
                    {
                        clone = UnityEngine.Object.Instantiate(prefab);
                        clone.gameObject.SetActive(false);
                        clone.gameObject.name = "CreatorTools_" + Items[i] + "_Template";
                        clone.gameObject.AddComponent<CreatorToolsBaronessMiniBossTemplate>().IsTemplate = true;
                        UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);
                        templates.Add(Items[i], clone);
                    }
                    catch
                    {
                        if (clone != null)
                            UnityEngine.Object.Destroy(clone.gameObject);
                        throw;
                    }
                    finally { capturingTemplate = false; }
                }
                if (logInfo != null)
                    logInfo("Los cinco mini jefes nativos de la Baronesa están disponibles en el catálogo.");
            }
            catch (Exception exception)
            {
                captureFailed = true;
                DestroyTemplates();
                if (logWarning != null)
                    logWarning("Could not cache Baroness mini-bosses: " + exception);
            }
        }

        internal BaronessLevelMiniBossBase CreateInactive(string item, Transform parent)
        {
            BaronessLevelMiniBossBase template;
            if (!Ready || !templates.TryGetValue(item, out template) || template == null)
                throw new InvalidOperationException("The native mini-boss is not available: " + item);
            // Instantiating an inactive prefab defers Awake until activation.
            // Clear only the template guard before running the native lifecycle.
            var actor = UnityEngine.Object.Instantiate(template);
            actor.gameObject.name = "CreatorTools_" + item;
            actor.transform.SetParent(parent, true);
            actor.GetComponent<CreatorToolsBaronessMiniBossTemplate>().IsTemplate = false;
            return actor;
        }

        internal static bool ShouldSuppressLifecycle(object instance)
        {
            if (capturingTemplate)
                return true;
            var component = instance as Component;
            if (component == null)
                return false;
            var marker = component.GetComponentInParent<CreatorToolsBaronessMiniBossTemplate>();
            return marker != null && marker.IsTemplate;
        }

        private void DestroyTemplates()
        {
            foreach (var template in templates.Values)
                if (template != null)
                    UnityEngine.Object.Destroy(template.gameObject);
            templates.Clear();
        }

        public void Dispose()
        {
            disposed = true;
            DestroyTemplates();
        }
    }
}
