using System;
using System.IO;
using QvPen.UdonScript;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yakigura.QvInkPreview.Editor
{
    public static class InkPreviewValidation
    {
        public static void RunAndExit()
        {
            int exit = 1;
            try
            {
                bool repair = Array.IndexOf(Environment.GetCommandLineArgs(), "-repairPreview") >= 0;
                int total = 0;
                bool compared = false;
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                    var scene = EditorSceneManager.OpenScene(path);
                    if (repair)
                    {
                        QvInkPreview_AutoSetup.AutoSetup();
                        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("save failed: " + path);
                    }
                    var previews = UnityEngine.Object.FindObjectsOfType<QvInkPreview_Pen>(true);
                    if (!compared && previews.Length > 0)
                    {
                        ComparePixels(previews[0]);
                        compared = true;
                    }
                    foreach (var indicator in previews)
                    {
                        var pen = indicator.Pen;
                        if (pen == null) throw new Exception(path + ": indicator pen missing");
                        var ink = new SerializedObject(pen).FindProperty("inkPrefab").objectReferenceValue as LineRenderer;
                        if (ink == null) throw new Exception(path + ": ink prefab missing");
                        var preview = indicator.PreviewLine;
                        if (preview == null) throw new Exception(path + ": preview line missing");
                        Require(new SerializedObject(preview).FindProperty("m_ApplyActiveColorSpace").boolValue
                            == new SerializedObject(ink).FindProperty("m_ApplyActiveColorSpace").boolValue,
                            path, indicator, "applyActiveColorSpace");
                        Require(preview.alignment == ink.alignment, path, indicator, "alignment");
                        Require(preview.numCapVertices == ink.numCapVertices, path, indicator, "numCapVertices");
                        Require(preview.numCornerVertices == ink.numCornerVertices, path, indicator, "numCornerVertices");
                        Require(preview.textureMode == ink.textureMode, path, indicator, "textureMode");
                        Require(preview.textureScale == ink.textureScale, path, indicator, "textureScale");
                        Require(preview.loop == ink.loop, path, indicator, "loop");
                        Require(CurveEqual(preview.widthCurve, ink.widthCurve), path, indicator, "widthCurve");
                        var pm = indicator.PenManager;
                        var expectedMaterial = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                            ? pm.questInkMaterial : pm.pcInkMaterial;
                        Require(preview.sharedMaterial == expectedMaterial, path, indicator, "material");
                        total++;
                    }
                }
                if (total == 0) throw new Exception("no previews validated");
                Debug.Log("INK_PREVIEW_VALIDATION PASS previews=" + total + " tolerance=8/255");
                exit = 0;
            }
            catch (Exception e) { Debug.LogError("INK_PREVIEW_VALIDATION FAIL " + e); }
            EditorApplication.Exit(exit);
        }

        private static bool CurveEqual(AnimationCurve a, AnimationCurve b)
        {
            if (a.length != b.length) return false;
            for (int i = 0; i < a.length; i++)
                if (Mathf.Abs(a.keys[i].time - b.keys[i].time) > 0.0001f
                    || Mathf.Abs(a.keys[i].value - b.keys[i].value) > 0.0001f) return false;
            return true;
        }

        private static void Require(bool condition, string path, QvInkPreview_Pen indicator, string setting)
        {
            if (!condition) throw new Exception(path + " / " + indicator.name + " mismatched " + setting);
        }

        private static void ComparePixels(QvInkPreview_Pen indicator)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new Exception("render comparison requires Linear color space");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log("INK_PREVIEW_RENDER SKIP graphics device is Null (batchmode -nographics)");
                return;
            }

            var ink = new SerializedObject(indicator.Pen).FindProperty("inkPrefab").objectReferenceValue as LineRenderer;
            var preview = indicator.PreviewLine;
            if (ink == null || preview == null) throw new Exception("render comparison reference missing");
            var cameraGo = new GameObject("Ink preview validation camera");
            var inkGo = new GameObject("Ink validation actual");
            var previewGo = new GameObject("Ink validation preview");
            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false, false);
            var previous = RenderTexture.active;
            try
            {
                inkGo.layer = previewGo.layer = 31;
                var actual = inkGo.AddComponent<LineRenderer>();
                var sample = previewGo.AddComponent<LineRenderer>();
                EditorUtility.CopySerialized(ink, actual);
                EditorUtility.CopySerialized(preview, sample);
                var material = indicator.PenManager.pcInkMaterial;
                SetLine(actual, material, -0.05f);
                SetLine(sample, material, 0.05f);

                var camera = cameraGo.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -1);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                camera.orthographic = true;
                camera.orthographicSize = 0.08f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 2f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                var a = pixels.GetPixel(96, 256);
                var b = pixels.GetPixel(416, 256);
                float difference = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
                int actualWidth = CountWidth(pixels, 96);
                int previewWidth = CountWidth(pixels, 416);
                var imagePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Logs/ink-preview-render.png");
                File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
                Debug.Log("INK_PREVIEW_RENDER actual=" + a + " preview=" + b + " maxDiff=" + difference
                    + " widthPixels=" + actualWidth + "/" + previewWidth + " image=" + imagePath);
                if (a.maxColorComponent < 0.1f || b.maxColorComponent < 0.1f)
                    throw new Exception("render comparison sampled blank pixels");
                if (difference > 8f / 255f)
                    throw new Exception("render comparison exceeded 8/255: " + difference);
                if (Mathf.Abs(actualWidth - previewWidth) > 1)
                    throw new Exception("render comparison width differs by more than 1 pixel");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(inkGo);
                UnityEngine.Object.DestroyImmediate(previewGo);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static int CountWidth(Texture2D pixels, int centerX)
        {
            int count = 0;
            for (int x = centerX - 48; x <= centerX + 48; x++)
                if (pixels.GetPixel(x, 256).r > 0.5f) count++;
            return count;
        }

        private static void SetLine(LineRenderer line, Material material, float x)
        {
            line.enabled = true;
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(x, -0.035f, 0));
            line.SetPosition(1, new Vector3(x, 0.035f, 0));
            line.widthMultiplier = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            line.colorGradient = gradient;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Width", 0.01f);
            line.SetPropertyBlock(block);
        }
    }
}
