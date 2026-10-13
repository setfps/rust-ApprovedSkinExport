using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Rust.Workshop;
using UnityEngine;

namespace ApprovedSkinExport
{
    [BepInPlugin("local.approved.skinexport", "setfps", "1.0.1")]
    public sealed class Plugin : BasePlugin
    {
        internal static ManualLogSource ExportLog;

        public override void Load()
        {
            ExportLog = Log;
            AddComponent<ExportRunner>();
            Log.LogInfo("Waiting for Skinnable.All; export will run once on the Unity thread.");
        }
    }

    public sealed class ExportRunner : MonoBehaviour
    {
        private const float PollInterval = 1.0f;

        private float _nextCheck;
        private bool _finished;

        public ExportRunner(IntPtr nativePointer)
            : base(nativePointer)
        {
        }

        public void Update()
        {
            float now = Time.realtimeSinceStartup;

            if (_finished || now < _nextCheck)
            {
                return;
            }

            _nextCheck = now + PollInterval;

            try
            {
                Il2CppReferenceArray<Skinnable> skinnables = Skinnable.All;

                if (skinnables == null || skinnables.Length == 0)
                {
                    return;
                }

                _finished = true;
                ExportApprovedSkins();
            }
            catch (Exception error)
            {
                _finished = true;
                Plugin.ExportLog.LogError("Approved export failed: " + error);
            }
        }

        private static void ExportApprovedSkins()
        {
            IntPtr nativeClass = IL2CPP.GetIl2CppClass("Rust.Workshop.dll", "Rust.Workshop", "Approved");

            if (nativeClass == IntPtr.Zero)
            {
                throw new InvalidOperationException("Native Rust.Workshop.Approved was not found.");
            }

            IL2CPP.il2cpp_runtime_class_init(nativeClass);

            Il2CppSystem.Collections.Generic.Dictionary<ulong, ApprovedSkinInfo> approved = Approved.All;

            if (approved == null || approved.Count == 0)
            {
                throw new InvalidOperationException("Approved.All is empty after initialization.");
            }

            SkinCatalog catalog = new SkinCatalog();
            foreach (Il2CppSystem.Collections.Generic.KeyValuePair<ulong, ApprovedSkinInfo> pair in approved)
            {
                ApprovedSkinInfo skin = pair.Value;

                if (skin == null)
                {
                    throw new InvalidOperationException("Null entry in Approved.All.");
                }

                Skinnable skinnable = skin.Skinnable;
                string itemName = skinnable == null ? String.Empty : skinnable.ItemName;

                catalog.AddSkin(itemName, skin.WorkshopdId, skin.InventoryId, skin.Name);
            }

            string outputDirectory = Path.Combine(Path.Combine(Paths.BepInExRootPath, "exports"), "approved-skins");

            catalog.WriteFiles(outputDirectory);
            Plugin.ExportLog.LogInfo(catalog.GetExportSummary(outputDirectory));
        }
    }
}
