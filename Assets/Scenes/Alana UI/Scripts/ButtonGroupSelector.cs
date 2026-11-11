// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Collections.Generic;

// namespace Training
// {
//     public class ButtonGroupSelector : MonoBehaviour
//     {
//         [Header("Colors - Background")]
//         [SerializeField] private Color normalColor = new Color(0.32f, 0.34f, 0.32f);
//         [SerializeField] private Color selectedColor = new Color(0.36f, 0.92f, 0.36f);

//         [Header("Colors - Text")]
//         [SerializeField] private Color normalTextColor = Color.white;
//         [SerializeField] private Color selectedTextColor = Color.black;

//         [Header("Buttons")]
//         [Tooltip("List of buttons in this group. Leave empty to automatically grab all Button components in children.")]
//         [SerializeField] private List<Button> groupButtons = new List<Button>();

//         [Tooltip("The index of the button selected by default when the scene starts.")]
//         [SerializeField] private int defaultSelectedIndex = 0;

//         private int currentIndex = -1;

//         private void Awake()
//         {
//             // If the list wasn't populated in the inspector, grab them automatically from children
//             if (groupButtons == null || groupButtons.Count == 0)
//             {
//                 groupButtons = new List<Button>(GetComponentsInChildren<Button>());
//             }
//         }

//         private void Start()
//         {
//             // Register click listeners for each button
//             for (int i = 0; i < groupButtons.Count; i++)
//             {
//                 int index = i; // Capture for closure
//                 if (groupButtons[index] != null)
//                 {
//                     groupButtons[index].onClick.AddListener(() => OnButtonClicked(index));
//                 }
//             }

//             // Set initial selection
//             if (groupButtons.Count > 0)
//             {
//                 SelectButton(Mathf.Clamp(defaultSelectedIndex, 0, groupButtons.Count - 1), false);
//             }
//         }

//         private void OnDestroy()
//         {
//             // Clean up listeners
//             for (int i = 0; i < groupButtons.Count; i++)
//             {
//                 if (groupButtons[i] != null)
//                 {
//                     groupButtons[i].onClick.RemoveAllListeners();
//                 }
//             }
//         }

//         private void OnButtonClicked(int index)
//         {
//             SelectButton(index, true);
//         }

//         public void SelectButton(int index, bool triggerCustomActions = true)
//         {
//             if (index < 0 || index >= groupButtons.Count)
//                 return;

//             currentIndex = index;

//             for (int i = 0; i < groupButtons.Count; i++)
//             {
//                 if (groupButtons[i] == null) continue;

//                 bool isSelected = (i == currentIndex);

//                 // Update Background Image color
//                 Image btnImage = groupButtons[i].GetComponent<Image>();
//                 if (btnImage != null)
//                 {
//                     btnImage.color = isSelected ? selectedColor : normalColor;
//                 }

//                 // Update Text color (checks for TextMeshProUGUI first, then standard legacy UI Text)
//                 TextMeshProUGUI tmpText = groupButtons[i].GetComponentInChildren<TextMeshProUGUI>();
//                 if (tmpText != null)
//                 {
//                     tmpText.color = isSelected ? selectedTextColor : normalTextColor;
//                 }
//                 else
//                 {
//                     Text legacyText = groupButtons[i].GetComponentInChildren<Text>();
//                     if (legacyText != null)
//                     {
//                         legacyText.color = isSelected ? selectedTextColor : normalTextColor;
//                     }
//                 }
//             }
//         }

//         public int GetSelectedIndex()
//         {
//             return currentIndex;
//         }
//     }
// }
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Put this on any object that holds a row/section of buttons (Random | Set on preview,
/// Train | Evaluate, 1x | 4x | Headless ...). Add the buttons to the list in the Inspector,
/// pick the colors, and clicking a button highlights it and un-highlights the others.
/// It works alongside your own OnClick handlers: it only adds one extra listener.
/// </summary>
public class ButtonGroupSelector : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Button button;

        [Tooltip("Optional. Leave empty and the first TMP_Text under the button is used.")]
        public TMP_Text label;
    }

    [Header("Buttons in this group")]
    public List<Entry> entries = new List<Entry>();

    [Header("Selected")]
    public Color selectedBackground = new Color32(0x4F, 0xC3, 0xE0, 255);
    public Color selectedText = new Color32(0x10, 0x10, 0x10, 255);

    [Header("Not selected")]
    public Color normalBackground = new Color32(0x17, 0x17, 0x17, 255);
    public Color normalText = new Color32(0xB1, 0xB1, 0xB1, 255);

    [Header("Behaviour")]
    [Tooltip("Which button starts selected. -1 = none.")]
    public int startIndex = 0;

    [Tooltip("If on, clicking the selected button again clears the selection.")]
    public bool allowDeselect = false;

    [Tooltip("Turns off the button's own color tint so it cannot fight with these colors.")]
    public bool disableButtonTint = true;

    [Header("Events")]
    [Tooltip("Fires with the new index (or -1) whenever the selection changes.")]
    public UnityEvent<int> onSelectionChanged;

    public int SelectedIndex { get; private set; } = -1;

    readonly List<UnityAction> listeners = new List<UnityAction>();

    void Awake()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e == null || e.button == null) { listeners.Add(null); continue; }

            if (e.label == null) e.label = e.button.GetComponentInChildren<TMP_Text>(true);
            if (disableButtonTint) e.button.transition = Selectable.Transition.None;

            int index = i;                                   // capture a copy for the lambda
            UnityAction action = () => OnClicked(index);
            listeners.Add(action);
            e.button.onClick.AddListener(action);
        }
    }

    void Start()
    {
        Apply(startIndex);
        SelectedIndex = startIndex;
    }

    void OnDestroy()
    {
        for (int i = 0; i < entries.Count && i < listeners.Count; i++)
        {
            if (entries[i] != null && entries[i].button != null && listeners[i] != null)
                entries[i].button.onClick.RemoveListener(listeners[i]);
        }
    }

    void OnClicked(int index)
    {
        if (index == SelectedIndex)
        {
            if (allowDeselect) Select(-1);
            return;
        }
        Select(index);
    }

    /// <summary>Select a button by its position in the list (-1 clears). Safe to call from code.</summary>
    public void Select(int index, bool notify = true)
    {
        if (index < -1 || index >= entries.Count) return;
        SelectedIndex = index;
        Apply(index);
        if (notify) onSelectionChanged?.Invoke(index);
    }

    /// <summary>Select a button by reference, e.g. Select(randomButton).</summary>
    public void Select(Button button, bool notify = true)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].button == button) { Select(i, notify); return; }
    }

    void Apply(int selected)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e == null || e.button == null) continue;

            bool on = i == selected;
            Graphic bg = e.button.targetGraphic != null ? e.button.targetGraphic : e.button.GetComponent<Graphic>();
            if (bg != null) bg.color = on ? selectedBackground : normalBackground;

            TMP_Text text = e.label != null ? e.label : e.button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.color = on ? selectedText : normalText;
        }
    }

    // Right-click the component header > "Preview start selection" to see the colors in the editor.
    [ContextMenu("Preview start selection")]
    void Preview() { Apply(startIndex); }
}