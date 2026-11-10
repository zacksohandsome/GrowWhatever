// Run through Unity Pipeline script eval --file in the saved Farm scene, Edit Mode.
using System;
using System.Linq;
using GrowWhatever;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

class BulkTradingUI
{
    static TMP_FontAsset font;
    static Color ink = new Color32(237,231,202,255), muted = new Color32(179,190,162,255);

    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Farm.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Farm scene in Edit Mode first.");
        var canvas = GameObject.Find("Farm Canvas");
        var session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
        if (canvas == null || session == null || canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            throw new InvalidOperationException("Expected farm Canvas, raycaster and session.");
        if (canvas.transform.Find("Trade Quantity") != null) throw new InvalidOperationException("Bulk UI is already authored.");
        font = canvas.GetComponentsInChildren<TMP_Text>(true).First().font;
        var background = canvas.transform.Cast<Transform>().Select(x =>
        {
            var group = x.GetComponent<CanvasGroup>();
            return group != null ? group : x.gameObject.AddComponent<CanvasGroup>();
        }).ToArray();

        var overlay = Box("Trade Quantity", canvas.transform, Vector2.zero, Vector2.zero, new Color(0,0,0,0.78f));
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        var outside = overlay.gameObject.AddComponent<UnityEngine.UI.Button>();
        outside.targetGraphic = overlay.GetComponent<UnityEngine.UI.Image>();
        outside.transition = UnityEngine.UI.Selectable.Transition.None;
        outside.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var dialog = Box("Dialog", overlay, Vector2.zero, new Vector2(650,460), new Color32(35,57,48,255));
        // A raycastable panel catches empty-space clicks inside the dialog before the outside button.
        dialog.gameObject.AddComponent<UnityEngine.UI.Button>().transition = UnityEngine.UI.Selectable.Transition.None;
        dialog.GetComponent<UnityEngine.UI.Button>().navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var title = Label("Title", dialog, "BULK TRADE", new Vector2(0,185), new Vector2(600,42), 25, ink);
        title.enableAutoSizing = true; title.fontSizeMin = 18; title.fontSizeMax = 25;
        var details = Label("Details", dialog, "", new Vector2(0,90), new Vector2(580,130), 20, ink);
        var inputRect = Box("QuantityInput", dialog, new Vector2(0,-6), new Vector2(300,46), new Color32(22,38,32,255));
        var viewport = Rect("Text Area", inputRect, Vector2.zero, new Vector2(280,42));
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var inputText = Label("Text", viewport, "1", Vector2.zero, new Vector2(280,42), 24, ink);
        inputText.textWrappingMode = TextWrappingModes.NoWrap;
        var input = inputRect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport; input.textComponent = inputText;
        input.targetGraphic = inputRect.GetComponent<UnityEngine.UI.Image>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 64;
        input.customCaretColor = true; input.caretColor = ink; input.text = "1";
        var view = overlay.gameObject.AddComponent<TradeQuantityView>();
        Ref(view,"session",session); Ref(view,"title",title); Ref(view,"details",details);
        Ref(view,"quantityInput",input); Ref(view,"outsideButton",outside);
        string[] labels = { "MIN", "-10", "-1", "+1", "+10", "MAX" };
        string[] fields = { "minimumButton", "minusTenButton", "minusOneButton", "plusOneButton", "plusTenButton", "maximumButton" };
        for (int i=0; i<labels.Length; i++)
            Ref(view,fields[i],Button(fields[i],dialog,labels[i],new Vector2(-250+i*100,-69),new Vector2(90,38),18));
        Ref(view,"feedback",Label("Feedback",dialog,"",new Vector2(0,-124),new Vector2(590,58),16,new Color32(242,209,123,255)));
        Ref(view,"confirmButton",Button("ConfirmButton",dialog,"Confirm trade",new Vector2(-150,-188),new Vector2(270,44),20));
        Ref(view,"cancelButton",Button("CancelButton",dialog,"Cancel",new Vector2(150,-188),new Vector2(270,44),20));
        var serialized = new SerializedObject(view);
        var groups = serialized.FindProperty("backgroundGroups"); groups.arraySize = background.Length;
        for (int i=0; i<background.Length; i++) groups.GetArrayElementAtIndex(i).objectReferenceValue = background[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false);

        EditPrefab("Assets/Prefab/UI/SeedShopRow.prefab", true);
        EditPrefab("Assets/Prefab/UI/RecipeRow.prefab", false);
        foreach (var row in canvas.GetComponentsInChildren<SeedShopRowView>(true))
            Ref(row,"tradeDialog",view);
        foreach (var row in canvas.GetComponentsInChildren<RecipeRowView>(true))
            Ref(row,"tradeDialog",view);
        canvas.transform.Find("Seed Shop Panel/ShopHint").GetComponent<TMP_Text>().text = "Trade one item, or choose Bulk for a quantity.";
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return new { scene = scene.path, shopRows = canvas.GetComponentsInChildren<SeedShopRowView>(true).Length,
            recipeRows = canvas.GetComponentsInChildren<RecipeRowView>(true).Length, dialog = "Trade Quantity" };
    }

    static void EditPrefab(string path, bool shop)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = shop ? (Component)root.GetComponent<SeedShopRowView>() : root.GetComponent<RecipeRowView>();
            if (shop)
            {
                Move(root,"BuyButton",new Vector2(55,-26),new Vector2(100,24));
                Move(root,"SellSeedButton",new Vector2(-115,-54),new Vector2(100,24));
                Move(root,"SellProduceButton",new Vector2(55,-54),new Vector2(100,24));
                Ref(view,"bulkBuyButton",Button("BulkBuyButton",root.transform,"Bulk",new Vector2(135,-26),new Vector2(50,24),12));
                Ref(view,"bulkSellSeedButton",Button("BulkSellSeedButton",root.transform,"Bulk",new Vector2(-35,-54),new Vector2(50,24),12));
                Ref(view,"bulkSellProduceButton",Button("BulkSellProduceButton",root.transform,"Bulk",new Vector2(135,-54),new Vector2(50,24),12));
            }
            else
            {
                Move(root,"SellButton",new Vector2(55,-40),new Vector2(100,30));
                Ref(view,"bulkSellButton",Button("BulkSellButton",root.transform,"Bulk",new Vector2(135,-40),new Vector2(50,30),12));
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Move(GameObject root, string name, Vector2 position, Vector2 size)
    {
        var target = root.transform.Find(name);
        if (target == null) throw new InvalidOperationException("Missing existing button: " + name);
        var rect = (RectTransform)target; rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = target.GetComponentInChildren<TMP_Text>(); label.fontSize = 12;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name,typeof(RectTransform)); root.transform.SetParent(parent,false);
        var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f,0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    { var rect = Rect(name,parent,position,size); rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = color; return rect; }
    static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var rect = Rect(name,parent,position,size); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
    }
    static UnityEngine.UI.Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Box(name,parent,position,size,new Color32(74,103,72,255));
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var label = Label("Label",rect,text,Vector2.zero,size,fontSize,ink);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
    static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
}
return BulkTradingUI.Apply();
