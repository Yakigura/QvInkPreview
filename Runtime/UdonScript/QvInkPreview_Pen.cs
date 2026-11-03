using QvPen.UdonScript;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Yakigura.QvInkPreview
{
    public enum QvInkPreview_TailAxis { Custom, NegativeY, NegativeZ }

    [AddComponentMenu("Yakigura/QvInkPreview/Pen")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QvInkPreview_Pen : UdonSharpBehaviour
    {
        [Header("Auto Setup references")]
        [SerializeField] private QvPen_Pen pen;
        [SerializeField] private QvPen_PenManager penManager;
        [SerializeField] private TrailRenderer trailRenderer;
        [SerializeField] private Transform pointerTransform;
        [SerializeField] private Transform penTip;
        [SerializeField] private GameObject previewRoot;
        [SerializeField] private LineRenderer previewLine;

        [Header("Placement")]
        public float tailDistance = 0.02f;
        public float upOffset = 0.05f;
        public float maxVisibleDistance = 0f;
        public QvInkPreview_TailAxis tailAxis = QvInkPreview_TailAxis.Custom;
        public Vector3 customTailAxis = Vector3.zero;
        public Vector3 positionOffset = Vector3.zero;

        [System.NonSerialized] public bool isEnabledByManager = true;
        private bool hasOverrideGradient;
        private bool hasOverrideWidth;
        private Gradient overrideGradient;
        private float overrideWidth;
        private MaterialPropertyBlock propertyBlock;
        private Material cachedMaterial;
        private bool cachedRounded;
        private bool hasAppliedProperties;
        private float cachedWidth;
        private float cachedSourceWidth;
        private float cachedOutlineWidth;
        private float cachedOutlineHue;
        private float cachedOutlineSaturation;
        private float cachedOutlineBrightness;
        private Color cachedOutlineColor;
        private Color cachedCustomOutlineColor;

        public QvPen_Pen Pen => pen;
        public QvPen_PenManager PenManager => penManager;
        public GameObject PreviewRoot => previewRoot;
        public LineRenderer PreviewLine => previewLine;

        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (Utilities.IsValid(previewRoot)) previewRoot.SetActive(false);
            enabled = isEnabledByManager;
        }

        public void _SetEnabledByManager(bool value)
        {
            isEnabledByManager = value;
            if (!value && Utilities.IsValid(previewRoot)) previewRoot.SetActive(false);
            enabled = value;
        }

        public void _SetOverrideGradient(Gradient value)
        {
            overrideGradient = value;
            hasOverrideGradient = value != null;
        }

        public void _SetOverrideWidth(float value)
        {
            overrideWidth = value;
            hasOverrideWidth = true;
        }

        public void _ClearOverride()
        {
            hasOverrideGradient = false;
            hasOverrideWidth = false;
            overrideGradient = null;
        }

        private void Update()
        {
            // A queued Update can run after OFF. Keep this guard before every show path.
            if (!isEnabledByManager)
            {
                if (Utilities.IsValid(previewRoot) && previewRoot.activeSelf) previewRoot.SetActive(false);
                return;
            }
            var localPlayer = Networking.LocalPlayer;
            bool hasHead = Utilities.IsValid(localPlayer);
            Vector3 headPosition = hasHead
                ? localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position : Vector3.zero;
            bool tooFar = maxVisibleDistance > 0f && hasHead && Utilities.IsValid(penTip)
                && (headPosition - penTip.position).sqrMagnitude > maxVisibleDistance * maxVisibleDistance;
            // Cull before placement and renderer work. A zero limit preserves the original visibility.
            if (tooFar)
            {
                if (Utilities.IsValid(previewRoot) && previewRoot.activeSelf) previewRoot.SetActive(false);
                return;
            }
            bool drawing = Utilities.IsValid(trailRenderer) && trailRenderer.gameObject.activeSelf;
            bool erasing = Utilities.IsValid(pointerTransform) && pointerTransform.gameObject.activeSelf;
            if (drawing || erasing)
            {
                if (Utilities.IsValid(previewRoot) && previewRoot.activeSelf) previewRoot.SetActive(false);
                return;
            }
            if (!Utilities.IsValid(previewRoot) || !Utilities.IsValid(previewLine) || !Utilities.IsValid(penManager)) return;
            PlaceAtPenTail(headPosition, hasHead);
            if (!previewRoot.activeSelf) previewRoot.SetActive(true);

            var material = penManager.pcInkMaterial;
#if !UNITY_STANDALONE
            material = penManager.questInkMaterial;
#endif
            if (Utilities.IsValid(material) && previewLine.sharedMaterial != material)
                previewLine.sharedMaterial = material;
            var gradient = hasOverrideGradient ? overrideGradient : penManager.colorGradient;
            if (gradient != null) previewLine.colorGradient = gradient;
            float width = Mathf.Max(0.0001f, hasOverrideWidth ? overrideWidth : penManager.inkWidth);
            // The live trail carries per-stroke shader values such as outline color.
            // Copy them so this preview uses the same material properties as real ink.
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            propertyBlock.Clear();
            if (Utilities.IsValid(trailRenderer)) trailRenderer.GetPropertyBlock(propertyBlock);
#if UNITY_STANDALONE
            if (cachedMaterial != previewLine.sharedMaterial)
            {
                cachedMaterial = previewLine.sharedMaterial;
                var shader = Utilities.IsValid(cachedMaterial) ? cachedMaterial.shader : null;
                cachedRounded = Utilities.IsValid(shader)
                    && (shader == penManager.roundedTrailShader || shader.name.Contains("rounded_trail"));
                hasAppliedProperties = false;
            }
            bool rounded = cachedRounded;
#else
            bool rounded = false;
#endif
            float sourceWidth = propertyBlock.GetFloat("_Width");
            float outlineWidth = propertyBlock.GetFloat("_OutlineWidth");
            float outlineHue = propertyBlock.GetFloat("_OutlineHueDelta");
            float outlineSaturation = propertyBlock.GetFloat("_OutlineSaturationDelta");
            float outlineBrightness = propertyBlock.GetFloat("_OutlineBrightnessDelta");
            Color outlineColor = propertyBlock.GetColor("_OutlineColor");
            Color customOutlineColor = propertyBlock.GetColor("_OutlineCustomColor");
            bool propertiesChanged = !hasAppliedProperties || cachedWidth != width || cachedSourceWidth != sourceWidth
                || cachedOutlineWidth != outlineWidth || cachedOutlineHue != outlineHue
                || cachedOutlineSaturation != outlineSaturation || cachedOutlineBrightness != outlineBrightness
                || cachedOutlineColor != outlineColor || cachedCustomOutlineColor != customOutlineColor;
            if (rounded)
            {
                if (previewLine.widthMultiplier != 0f) previewLine.widthMultiplier = 0f;
                if (propertiesChanged) propertyBlock.SetFloat("_Width", width);
            }
            else if (previewLine.widthMultiplier != width) previewLine.widthMultiplier = width;
            if (propertiesChanged)
            {
                previewLine.SetPropertyBlock(propertyBlock);
                cachedWidth = width;
                cachedSourceWidth = sourceWidth;
                cachedOutlineWidth = outlineWidth;
                cachedOutlineHue = outlineHue;
                cachedOutlineSaturation = outlineSaturation;
                cachedOutlineBrightness = outlineBrightness;
                cachedOutlineColor = outlineColor;
                cachedCustomOutlineColor = customOutlineColor;
                hasAppliedProperties = true;
            }
        }

        private void PlaceAtPenTail(Vector3 headPosition, bool hasHead)
        {
            if (!Utilities.IsValid(previewRoot) || !Utilities.IsValid(pen)) return;
            Vector3 bodyPos = pen.transform.position;
            Vector3 tipPos = Utilities.IsValid(penTip) ? penTip.position : bodyPos;
            Vector3 tipAxis = tipPos - bodyPos;
            float halfLength = tipAxis.magnitude;
            if (halfLength < 1e-4f) halfLength = 0.1f;
            Vector3 localTail = Vector3.zero;
            if (tailAxis == QvInkPreview_TailAxis.NegativeY) localTail = Vector3.down;
            else if (tailAxis == QvInkPreview_TailAxis.NegativeZ) localTail = Vector3.back;
            else if (customTailAxis.sqrMagnitude > 1e-8f) localTail = customTailAxis.normalized;
            Vector3 tailDirection = localTail.sqrMagnitude > 1e-8f
                ? pen.transform.TransformDirection(localTail).normalized
                : (tipAxis.sqrMagnitude > 1e-8f ? -tipAxis.normalized : -pen.transform.forward);
            Vector3 targetPos = bodyPos + tailDirection * (halfLength + tailDistance)
                + Vector3.up * upOffset + pen.transform.TransformVector(positionOffset);
            if (hasHead)
            {
                Vector3 lookDir = targetPos - headPosition;
                previewRoot.transform.SetPositionAndRotation(targetPos,
                    lookDir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(lookDir.normalized, Vector3.up) : pen.transform.rotation);
            }
            else previewRoot.transform.SetPositionAndRotation(targetPos, pen.transform.rotation);
        }
    }
}
