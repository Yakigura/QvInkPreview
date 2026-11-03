using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace Yakigura.QvInkPreview
{
    [AddComponentMenu("Yakigura/QvInkPreview/Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QvInkPreview_Manager : UdonSharpBehaviour
    {
        public QvInkPreview_Pen[] previews = new QvInkPreview_Pen[0];
        public Toggle toggle;

        private void Start()
        {
            if (Utilities.IsValid(toggle)) _SetAllEnabled(toggle.isOn);
        }

        public void _OnToggleChanged()
        {
            if (Utilities.IsValid(toggle)) _SetAllEnabled(toggle.isOn);
        }

        public void _SetAllEnabled(bool value)
        {
            for (int i = 0; i < previews.Length; i++)
                if (Utilities.IsValid(previews[i])) previews[i]._SetEnabledByManager(value);
        }
    }
}
