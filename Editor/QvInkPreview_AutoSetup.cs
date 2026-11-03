using System;
using System.Collections.Generic;
using System.IO;
using QvPen.UdonScript;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.Udon;

namespace Yakigura.QvInkPreview.Editor
{
    public static class QvInkPreview_AutoSetup
    {
        private const string RootName = "QvInkPreview_Manager";
        private const string ChildName = "QvInkPreview";
        private const string ProgramDirectory = "Packages/com.yakigura.qvinkpreview/Runtime/UdonAsset";

        public static QvInkPreview_Manager AutoSetup()
        {
            AssetDatabase.Refresh();
            EnsureProgramAsset(typeof(QvInkPreview_Pen));
            EnsureProgramAsset(typeof(QvInkPreview_Manager));
            AssetDatabase.SaveAssets();
            UdonSharpProgramAsset.CompileAllCsPrograms(true, true);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var previews = new List<QvInkPreview_Pen>();
            foreach (var pm in UnityEngine.Object.FindObjectsOfType<QvPen_PenManager>(true))
            {
                if (pm == null || pm.gameObject.scene != scene) continue;
                var pen = pm.GetComponentInChildren<QvPen_Pen>(true);
                if (pen == null) continue;
                if (HasCompetingPreview(pen))
                {
                    RemoveOwnPreview(pen);
                    continue;
                }
                previews.Add(SetupPen(pen, pm));
            }

            GameObject root = null;
            foreach (var candidate in scene.GetRootGameObjects())
                if (candidate.name == RootName) { root = candidate; break; }
            if (root == null) root = new GameObject(RootName);
            var manager = GetOrAdd<QvInkPreview_Manager>(root);
            var so = new SerializedObject(manager);
            var array = so.FindProperty("previews");
            array.arraySize = previews.Count;
            for (int i = 0; i < previews.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = previews[i];
            if (manager.toggle == null)
            {
                foreach (var toggle in UnityEngine.Object.FindObjectsOfType<Toggle>(true))
                    if (toggle.gameObject.scene == scene && toggle.gameObject.name == "QvInkPreviewToggle")
                    { so.FindProperty("toggle").objectReferenceValue = toggle; break; }
            }
            so.ApplyModifiedProperties();
            if (manager.toggle != null) WireToggle(manager.toggle, manager);
            Copy(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[QvInkPreview AutoSetup] previews=" + previews.Count);
            return manager;
        }

        private static QvInkPreview_Pen SetupPen(QvPen_Pen pen, QvPen_PenManager pm)
        {
            QvInkPreview_Pen preview = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<QvInkPreview_Pen>(true))
            {
                if (candidate.gameObject.scene != pen.gameObject.scene) continue;
                if (candidate.Pen == pen || candidate.transform.IsChildOf(pen.transform))
                {
                    if (preview == null) preview = candidate;
                    else UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                }
            }
            GameObject holder;
            if (preview == null)
            {
                holder = new GameObject(ChildName);
                holder.transform.SetParent(pen.transform, false);
                preview = GetOrAdd<QvInkPreview_Pen>(holder);
            }
            else
            {
                holder = preview.gameObject;
                if (holder.transform.parent != pen.transform) holder.transform.SetParent(pen.transform, false);
            }

            var penSo = new SerializedObject(pen);
            var pointer = penSo.FindProperty("pointer")?.objectReferenceValue as Transform;
            var tip = penSo.FindProperty("inkPosition")?.objectReferenceValue as Transform;
            var inkLine = penSo.FindProperty("inkPrefab")?.objectReferenceValue as LineRenderer;
            var trail = pm.GetComponentInChildren<TrailRenderer>(true);

            var visual = holder.transform.Find("Preview");
            if (visual == null)
            {
                var go = new GameObject("Preview");
                go.transform.SetParent(holder.transform, false);
                visual = go.transform;
            }
            var line = visual.GetComponent<LineRenderer>();
            if (line == null) line = visual.gameObject.AddComponent<LineRenderer>();
            ConfigureLine(line, inkLine, pm);
            visual.gameObject.SetActive(false);

            var so = new SerializedObject(preview);
            so.FindProperty("pen").objectReferenceValue = pen;
            so.FindProperty("penManager").objectReferenceValue = pm;
            so.FindProperty("trailRenderer").objectReferenceValue = trail;
            so.FindProperty("pointerTransform").objectReferenceValue = pointer;
            so.FindProperty("penTip").objectReferenceValue = tip;
            so.FindProperty("previewRoot").objectReferenceValue = visual.gameObject;
            so.FindProperty("previewLine").objectReferenceValue = line;
            so.ApplyModifiedProperties();
            Copy(preview);
            return preview;
        }

        private static void ConfigureLine(LineRenderer line, LineRenderer ink, QvPen_PenManager pm)
        {
            line.useWorldSpace = false;
            var lineSo = new SerializedObject(line);
            var colorSpace = ink != null ? new SerializedObject(ink).FindProperty("m_ApplyActiveColorSpace") : null;
            var targetColorSpace = lineSo.FindProperty("m_ApplyActiveColorSpace");
            if (targetColorSpace != null) targetColorSpace.boolValue = colorSpace != null && colorSpace.boolValue;
            lineSo.ApplyModifiedProperties();
            line.alignment = ink != null ? ink.alignment : LineAlignment.View;
            line.numCapVertices = ink != null ? ink.numCapVertices : 0;
            line.numCornerVertices = ink != null ? ink.numCornerVertices : 0;
            line.textureMode = ink != null ? ink.textureMode : LineTextureMode.Stretch;
            line.textureScale = ink != null ? ink.textureScale : Vector2.one;
            line.widthCurve = ink != null ? ink.widthCurve : AnimationCurve.Linear(0, 1, 1, 1);
            line.loop = ink != null && ink.loop;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            line.generateLightingData = false;
            const int count = 10;
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                points[i] = new Vector3(0, Mathf.Lerp(-0.03f, 0.03f, (float)i / (count - 1)), 0);
            line.positionCount = count;
            line.SetPositions(points);
            line.sharedMaterial = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                ? pm.questInkMaterial : pm.pcInkMaterial;
        }

        private static bool HasCompetingPreview(QvPen_Pen pen)
        {
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null || mb.gameObject.scene != pen.gameObject.scene || mb is QvInkPreview_Pen) continue;
                var previewData = new SerializedObject(mb);
                // A component with its own line and a binding to this pen is a competing renderer.
                // A label/style adapter has no previewLine and does not block shared setup.
                if (previewData.FindProperty("previewLine")?.objectReferenceValue == null) continue;
                var binding = previewData.FindProperty("binding")?.objectReferenceValue;
                if (binding == null) continue;
                if (new SerializedObject(binding).FindProperty("pen")?.objectReferenceValue == pen) return true;
            }
            return false;
        }

        private static void RemoveOwnPreview(QvPen_Pen pen)
        {
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<QvInkPreview_Pen>(true))
                if (candidate.gameObject.scene == pen.gameObject.scene
                    && (candidate.Pen == pen || candidate.transform.IsChildOf(pen.transform)))
                    UnityEngine.Object.DestroyImmediate(candidate.gameObject);
        }

        private static void WireToggle(Toggle toggle, QvInkPreview_Manager manager)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(manager);
            if (udon == null) udon = manager.GetComponent<UdonBehaviour>();
            if (udon == null) throw new Exception("manager UdonBehaviour is missing");
            for (int i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
                if (toggle.onValueChanged.GetPersistentTarget(i) == udon)
                    UnityEventTools.RemovePersistentListener(toggle.onValueChanged, i);
            UnityEventTools.AddStringPersistentListener(toggle.onValueChanged, udon.SendCustomEvent, "_OnToggleChanged");
        }

        private static T GetOrAdd<T>(GameObject go) where T : UdonSharpBehaviour
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddUdonSharpComponent<T>();
        }

        private static void Copy(UdonSharpBehaviour behaviour)
        {
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        }

        private static void EnsureProgramAsset(Type scriptType)
        {
            if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(scriptType) != null) return;
            if (!Directory.Exists(ProgramDirectory)) Directory.CreateDirectory(ProgramDirectory);
            string scriptPath = null;
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + scriptType.Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == scriptType) { scriptPath = path; break; }
            }
            if (scriptPath == null) throw new Exception("script missing: " + scriptType.FullName);
            var asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            asset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            asset.CompiledVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(asset, ProgramDirectory + "/" + scriptType.Name + ".asset");
        }
    }
}
