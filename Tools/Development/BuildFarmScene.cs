// Run with: unity command script eval --file Tools/Development/BuildFarmScene.cs
// Authoring only: the shipped game uses the saved scene, prefabs and data assets.
using System;
using System.Linq;
using System.Collections.Generic;
using GrowWhatever;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

class FarmSceneAuthoring
{
    const string Atlas = "Assets/Art/Growables/growables-atlas.png";
    const string ScenePath = "Assets/Scenes/Farm.unity";
    static readonly Color Ink = new Color32(237, 231, 202, 255);
    static readonly Color Muted = new Color32(179, 190, 162, 255);
    static readonly Color Panel = new Color32(35, 57, 48, 255);
    static readonly Color ButtonColor = new Color32(74, 103, 72, 255);
    static TMP_FontAsset font;

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("The current scene has unsaved changes; refusing to replace it.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Farm scene already exists; inspect and edit it instead of rebuilding.");
        foreach (string folder in new[] { "Assets/Data", "Assets/Data/Growables", "Assets/Prefab", "Assets/Prefab/Farm", "Assets/Prefab/UI" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }
        SliceAtlas();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(x => x.name);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Import the installed TMP essentials first.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Opening a scene unloads unused assets; load authoring references afterwards.
        sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(x => x.name);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var carrot = Definition("carrot", "Carrot", 2, 30, 3, 1, sprites);
        var wheat = Definition("wheat", "Wheat", 4, 60, 3, 2, sprites);

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.backgroundColor = new Color32(23, 39, 33, 255);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var session = new GameObject("Farm Session").AddComponent<FarmSession>();
        var serializedSession = new SerializedObject(session);
        var definitions = serializedSession.FindProperty("growables");
        definitions.arraySize = 2;
        definitions.GetArrayElementAtIndex(0).objectReferenceValue = carrot;
        definitions.GetArrayElementAtIndex(1).objectReferenceValue = wheat;
        serializedSession.ApplyModifiedPropertiesWithoutUndo();

        var canvas = new GameObject("Farm Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        // Keep the hierarchy inactive until all serialized references have been wired.
        canvas.SetActive(false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 900);
        scaler.matchWidthOrHeight = 0.5f;

        var backdrop = Box("Backdrop", canvas.transform, Vector2.zero, new Vector2(1440, 900), new Color32(23, 39, 33, 255));
        Stretch(backdrop);
        var header = Box("Header", canvas.transform, new Vector2(0, 365), new Vector2(1360, 110), Panel);
        Label("Title", header, "GROW / WHATEVER", new Vector2(-405, 18), new Vector2(490, 40), 32, Ink, TextAlignmentOptions.Left);
        Label("Subtitle", header, "A little garden. Unexpected possibilities.", new Vector2(-405, -22), new Vector2(490, 28), 18, Muted, TextAlignmentOptions.Left);
        var wallet = Label("Wallet", header, "20 coins", new Vector2(235, 12), new Vector2(180, 36), 26, Ink);
        var shopButton = Button("ShopButton", header, "Seed shop", new Vector2(520, 0), new Vector2(230, 52));

        var farm = Box("Garden", canvas.transform, new Vector2(-220, -5), new Vector2(900, 600), new Color32(46, 67, 49, 255));
        Label("GardenTitle", farm, "YOUR NINE LITTLE PLOTS", new Vector2(-190, 268), new Vector2(460, 28), 20, Ink, TextAlignmentOptions.Left);
        Label("GardenHint", farm, "Click to plant or harvest. Keep a plot by switching bulk harvest off.", new Vector2(0, 232), new Vector2(840, 28), 17, Muted);
        var plots = Rect("Plots - sorted by ground row", farm, new Vector2(0, -14), new Vector2(840, 490));
        var plotTemplate = MakePlot(plots, sprites["soil"]);
        var plotPrefab = PrefabUtility.SaveAsPrefabAsset(plotTemplate, "Assets/Prefab/Farm/FarmPlot.prefab");
        UnityEngine.Object.DestroyImmediate(plotTemplate);
        for (int i = 0; i < 9; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(plotPrefab, plots);
            instance.name = $"Plot {i / 3 + 1}-{i % 3 + 1}";
            var rect = (RectTransform)instance.transform;
            rect.anchoredPosition = new Vector2((i % 3 - 1) * 275, 150 - (i / 3) * 155);
            // Later ground rows render later, independent of plant image height.
            rect.SetAsLastSibling();
            var view = instance.GetComponent<FarmPlotView>();
            Reference(view, "session", session);
            Integer(view, "plotIndex", i);
        }

        var shop = Box("Seed Shop Panel", canvas.transform, new Vector2(475, -5), new Vector2(390, 600), Panel);
        Label("ShopTitle", shop, "THE SEED COUNTER", new Vector2(0, 262), new Vector2(340, 36), 24, Ink);
        Label("ShopHint", shop, "Buy one seed. Sell one item.\nSeeds sell back at their full price.", new Vector2(0, 214), new Vector2(345, 50), 17, Muted);
        var rowTemplate = MakeShopRow(shop);
        var rowPrefab = PrefabUtility.SaveAsPrefabAsset(rowTemplate, "Assets/Prefab/UI/SeedShopRow.prefab");
        UnityEngine.Object.DestroyImmediate(rowTemplate);
        int rowIndex = 0;
        foreach (var definition in new[] { carrot, wheat })
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab, shop);
            instance.name = definition.DisplayName + " Shop Row";
            ((RectTransform)instance.transform).anchoredPosition = new Vector2(0, 70 - rowIndex++ * 215);
            var view = instance.GetComponent<SeedShopRowView>();
            Reference(view, "session", session);
            Reference(view, "definition", definition);
            var buy = instance.transform.Find("BuyButton/Label").GetComponent<TMP_Text>();
            buy.text = $"Buy / {definition.ToSpec().SeedPrice}c";
        }

        var footer = Box("Action Bar", canvas.transform, new Vector2(0, -368), new Vector2(1360, 104), Panel);
        var selected = Label("SelectedSeed", footer, "Carrot seeds / 5 in bag", new Vector2(-410, 15), new Vector2(470, 32), 22, Ink, TextAlignmentOptions.Left);
        var message = Label("Feedback", footer, "Choose a seed, then click an empty plot.", new Vector2(-410, -23), new Vector2(470, 36), 17, Muted, TextAlignmentOptions.Left);
        var plantAll = Button("PlantAllButton", footer, "Plant all (5)", new Vector2(0, 2), new Vector2(200, 52));
        var harvestAll = Button("HarvestAllButton", footer, "Harvest all (0)", new Vector2(225, 2), new Vector2(210, 52));
        var supply = Button("SupplyButton", footer, "Free seed", new Vector2(485, 2), new Vector2(230, 52));
        Label("CurrentGoal", canvas.transform, "FIRST STEPS / Grow a carrot, harvest it and sell your first produce.", new Vector2(0, -280), new Vector2(1320, 30), 17, Muted);

        var hud = canvas.AddComponent<FarmHudView>();
        Reference(hud, "session", session);
        Reference(hud, "wallet", wallet);
        Reference(hud, "selectedSeed", selected);
        Reference(hud, "message", message);
        Reference(hud, "plantAllLabel", plantAll.GetComponentInChildren<TMP_Text>());
        Reference(hud, "harvestAllLabel", harvestAll.GetComponentInChildren<TMP_Text>());
        Reference(hud, "plantAllButton", plantAll);
        Reference(hud, "harvestAllButton", harvestAll);
        Reference(hud, "supplyButton", supply);
        Reference(hud, "shopButton", shopButton);
        Reference(hud, "shopPanel", shop.gameObject);
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        canvas.SetActive(true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = session.gameObject;
        return new { scene = ScenePath, plots = 9, data = new[] { carrot.name, wheat.name }, sprites = sprites.Keys.ToArray() };
    }

    static void SliceAtlas()
    {
        AssetDatabase.ImportAsset(Atlas, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("No sprite data provider.");
        provider.InitSpriteEditorDataProvider();
        var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (edit == null || !edit.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new InvalidOperationException("Sprite slicing is unsupported; no metadata changes applied.");
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        string[] names = { "carrot-growing", "carrot-mature", "wheat-growing", "wheat-mature", "iron-growing", "iron-mature", "moonflower-growing", "moonflower-mature", "soil" };
        var existing = provider.GetSpriteRects();
        if (existing.Length == names.Length && existing.Select(x => x.name).SequenceEqual(names)) return;
        // A new texture can inherit automatic slicing from project import defaults.
        // This atlas is authored here before any scene or definition references it.
        var rects = new List<SpriteRect>();
        for (int i = 0; i < names.Length; i++)
        {
            int col = i % 3;
            int row = i / 3;
            int x = Mathf.RoundToInt(col * width / 3f);
            int right = Mathf.RoundToInt((col + 1) * width / 3f);
            int y = Mathf.RoundToInt((2 - row) * height / 3f);
            int top = Mathf.RoundToInt((3 - row) * height / 3f);
            rects.Add(new SpriteRect { name = names[i], rect = new Rect(x, y, right - x, top - y),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0.2f), spriteID = GUID.Generate() });
        }
        var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIds == null) throw new InvalidOperationException("Sprite name mapping unsupported.");
        provider.SetSpriteRects(rects.ToArray());
        nameIds.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    static GrowableDefinition Definition(string id, string name, int price, float seconds, int yield, int sell, Dictionary<string, Sprite> sprites)
    {
        string path = $"Assets/Data/Growables/{name}.asset";
        var definition = AssetDatabase.LoadAssetAtPath<GrowableDefinition>(path);
        if (definition != null) return definition;
        definition = ScriptableObject.CreateInstance<GrowableDefinition>();
        var data = new SerializedObject(definition);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("displayName").stringValue = name;
        data.FindProperty("seedPrice").intValue = price;
        data.FindProperty("growthSeconds").floatValue = seconds;
        data.FindProperty("harvestYield").intValue = yield;
        data.FindProperty("producePrice").intValue = sell;
        data.FindProperty("growingSprite").objectReferenceValue = sprites[id + "-growing"];
        data.FindProperty("matureSprite").objectReferenceValue = sprites[id + "-mature"];
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    static GameObject MakePlot(Transform parent, Sprite soil)
    {
        var root = Rect("FarmPlot", parent, Vector2.zero, new Vector2(235, 145));
        var clickImage = root.gameObject.AddComponent<UnityEngine.UI.Image>();
        clickImage.color = new Color(1, 1, 1, 0.001f);
        var click = root.gameObject.AddComponent<UnityEngine.UI.Button>();
        click.targetGraphic = clickImage;
        click.transition = UnityEngine.UI.Selectable.Transition.None;
        var soilRect = Rect("Soil", root, new Vector2(0, 0), new Vector2(240, 135));
        var soilImage = soilRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        soilImage.sprite = soil;
        soilImage.preserveAspect = true;
        soilImage.raycastTarget = false;
        var plantRect = Rect("Plant", root, new Vector2(0, 20), new Vector2(112, 112));
        var plant = plantRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        plant.preserveAspect = true;
        plant.raycastTarget = false;
        plant.enabled = false;
        var state = Label("State", root, "Empty - plant", new Vector2(0, -32), new Vector2(230, 24), 16, Ink);
        var progress = Box("ProgressTrack", root, new Vector2(0, -50), new Vector2(170, 4), Panel);
        var fillRect = Box("Progress", progress, Vector2.zero, new Vector2(170, 4), new Color32(198, 182, 104, 255));
        var fill = fillRect.GetComponent<UnityEngine.UI.Image>();
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        fill.fillAmount = 0;
        var bulk = Button("BulkButton", root, "Bulk harvest: ON", new Vector2(0, -72), new Vector2(180, 26));
        bulk.GetComponentInChildren<TMP_Text>().fontSize = 14;
        var view = root.gameObject.AddComponent<FarmPlotView>();
        Reference(view, "plotButton", click);
        Reference(view, "bulkButton", bulk);
        Reference(view, "plantImage", plant);
        Reference(view, "progressFill", fill);
        Reference(view, "stateLabel", state);
        Reference(view, "bulkLabel", bulk.GetComponentInChildren<TMP_Text>());
        return root.gameObject;
    }

    static GameObject MakeShopRow(Transform parent)
    {
        var row = Box("SeedShopRow", parent, Vector2.zero, new Vector2(350, 194), new Color32(43, 66, 53, 255));
        var portraitRect = Rect("Portrait", row, new Vector2(-123, 40), new Vector2(84, 100));
        var portrait = portraitRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        var title = Label("Title", row, "Growable", new Vector2(41, 65), new Vector2(245, 30), 21, Ink, TextAlignmentOptions.Left);
        var inventory = Label("Inventory", row, "", new Vector2(41, 20), new Vector2(245, 58), 15, Muted, TextAlignmentOptions.Left);
        var select = Button("SelectButton", row, "Select", new Vector2(-88, -38), new Vector2(156, 34));
        var buy = Button("BuyButton", row, "Buy", new Vector2(88, -38), new Vector2(156, 34));
        var sellSeed = Button("SellSeedButton", row, "Sell seed", new Vector2(-88, -78), new Vector2(156, 34));
        var sellProduce = Button("SellProduceButton", row, "Sell produce", new Vector2(88, -78), new Vector2(156, 34));
        var view = row.gameObject.AddComponent<SeedShopRowView>();
        Reference(view, "title", title);
        Reference(view, "inventory", inventory);
        Reference(view, "portrait", portrait);
        Reference(view, "selectButton", select);
        Reference(view, "buyButton", buy);
        Reference(view, "sellSeedButton", sellSeed);
        Reference(view, "sellProduceButton", sellProduce);
        return row.gameObject;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = Rect(name, parent, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    static UnityEngine.UI.Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size)
    {
        var rect = Box(name, parent, position, size, ButtonColor);
        var image = rect.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color32(206, 213, 170, 255);
        colors.pressedColor = new Color32(169, 183, 133, 255);
        colors.disabledColor = new Color32(112, 123, 108, 180);
        button.colors = colors;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        var label = Label("Label", rect, text, Vector2.zero, size - new Vector2(10, 4), 18, Ink);
        Stretch(label.rectTransform);
        return button;
    }

    static void Reference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Integer(UnityEngine.Object target, string name, int value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}

return FarmSceneAuthoring.Build();
