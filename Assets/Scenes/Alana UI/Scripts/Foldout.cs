using UnityEngine;
using UnityEngine.EventSystems;

namespace Training
{
    public class Foldout : MonoBehaviour, IPointerClickHandler
    {
        [Header("References")]
        public RectTransform group;      // the group object
        public GameObject content;
        public RectTransform header;
        public RectTransform chevron;

        [Header("State")]
        public bool startOpen = true;

        private float openHeight;
        private bool open;

        void Start()
        {
            // If using Layout Groups, we grab the height after layout calculation
            openHeight = group.rect.height; 
            Set(startOpen);
        }

        // This automatically fires when the assigned GameObject is clicked 
        // (Requires a Graphic component like an Image on the GameObject for raycasting)
        public void OnPointerClick(PointerEventData eventData)
        {
            // Optional check: ensure they clicked specifically on the header or its children
            if (header == null || eventData.pointerCurrentRaycast.gameObject == header.gameObject || 
                eventData.pointerCurrentRaycast.gameObject.transform.IsChildOf(header))
            {
                Toggle();
            }
        }

        public void Toggle() 
        { 
            Set(!open); 
        }

        void Set(bool value)
        {
            open = value;
            if (content != null)
                content.SetActive(open);

            float h = open ? openHeight : header.rect.height;
            group.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            
            if (chevron != null) 
                chevron.localRotation = Quaternion.Euler(0, 0, open ? 0 : -180);
            
            if (group.parent is RectTransform parentRect)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }
}