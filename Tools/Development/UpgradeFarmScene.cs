// Author M2-M5 onto the saved M1 assets. Run in Edit Mode with script eval --file.
using System;
using System.Linq;
using GrowWhatever;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

class FarmExpansion
{
    static TMP_FontAsset font;
    static Color ink = new Color32(237,231,202,255), muted = new Color32(179,190,162,255);
    static Color panelColor = new Color32(35,57,48,255), cardColor = new Color32(43,66,53,255);
    static FarmSession session;

    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Farm.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Farm scene in Edit Mode first.");
        var canvas = GameObject.Find("Farm Canvas");
        session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
        if (canvas == null || session == null) throw new InvalidOperationException("Expected M1 scene.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Growables/growables-atlas.png").OfType<Sprite>().ToDictionary(x=>x.name);
        var carrot=AssetDatabase.LoadAssetAtPath<GrowableDefinition>("Assets/Data/Growables/Carrot.asset");
        var wheat=AssetDatabase.LoadAssetAtPath<GrowableDefinition>("Assets/Data/Growables/Wheat.asset");
        var iron=Growable("IronSprout","iron","Iron sprout",8,90,2,6,false,sprites["iron-growing"],sprites["iron-mature"]);
        var moon=Growable("Moonflower","moonflower","Moonflower",15,120,1,25,true,sprites["moonflower-growing"],sprites["moonflower-mature"]);
        var config=AssetDatabase.LoadAssetAtPath<FarmConfiguration>("Assets/Data/FarmConfiguration.asset");
        if(config==null)
        { config=ScriptableObject.CreateInstance<FarmConfiguration>(); AssetDatabase.CreateAsset(config,"Assets/Data/FarmConfiguration.asset"); }
        Ref(session,"configuration",config);
        var data=new SerializedObject(session); var definitions=data.FindProperty("growables"); definitions.arraySize=4;
        var all=new[]{carrot,wheat,iron,moon};
        for(int i=0;i<all.Length;i++) definitions.GetArrayElementAtIndex(i).objectReferenceValue=all[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        AuthorAudio();

        var header=canvas.transform.Find("Header");
        header.Find("Wallet").GetComponent<RectTransform>().anchoredPosition=new Vector2(-85,12);
        var shopButton=header.Find("ShopButton").GetComponent<UnityEngine.UI.Button>();
        SetRect((RectTransform)shopButton.transform,new Vector2(140,0),new Vector2(180,52));
        var processingButton=Button("ProcessingButton",header,"Workshop",new Vector2(335,0),new Vector2(180,52));
        var labButton=Button("LabButton",header,"Genetic Lab",new Vector2(530,0),new Vector2(180,52));

        var shop=canvas.transform.Find("Seed Shop Panel");
        Label("ShopTitle",shop,"THE SEED COUNTER",new Vector2(0,270),new Vector2(350,30),23,ink);
        Label("ShopHint",shop,"Buy or sell one item at a time.",new Vector2(0,238),new Vector2(350,22),14,muted);
        CompactShopPrefab();
        var shopPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/SeedShopRow.prefab");
        for(int i=0;i<all.Length;i++)
        {
            string name=all[i].DisplayName+" Shop Row";
            var existing=shop.Find(name);
            var row=existing!=null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab,shop);
            row.name=name; SetRect((RectTransform)row.transform,new Vector2(0,157-i*132),new Vector2(350,122));
            var view=row.GetComponent<SeedShopRowView>(); Ref(view,"session",session); Ref(view,"definition",all[i]);
            row.transform.Find("BuyButton/Label").GetComponent<TMP_Text>().text=$"Buy / {all[i].ToSpec().SeedPrice}c";
        }

        var processing=Box("Processing Panel",canvas.transform,new Vector2(475,-5),new Vector2(390,600),panelColor);
        Label("Title",processing,"THE WORKSHOP",new Vector2(0,270),new Vector2(350,32),24,ink);
        Label("Hint",processing,"One working slot + three waiting slots",new Vector2(0,234),new Vector2(350,24),15,muted);
        var recipePrefab=RecipePrefab(processing);
        string[] recipeIds={"flour","parts"};
        for(int i=0;i<recipeIds.Length;i++)
        {
            var existing=processing.Find(recipeIds[i]+" Recipe");
            var row=existing!=null?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(recipePrefab,processing);
            row.name=recipeIds[i]+" Recipe"; ((RectTransform)row.transform).anchoredPosition=new Vector2(0,145-i*130);
            var view=row.GetComponent<RecipeRowView>(); Ref(view,"session",session); String(view,"recipeId",recipeIds[i]);
        }
        var queueLabel=Label("Queue",processing,"",new Vector2(0,-173),new Vector2(340,210),16,muted,TextAlignmentOptions.TopLeft);
        var processingView=Get<ProcessingPanelView>(processing.gameObject); Ref(processingView,"session",session); Ref(processingView,"queueLabel",queueLabel);

        var lab=Box("Genetic Lab Panel",canvas.transform,new Vector2(475,-5),new Vector2(390,600),panelColor);
        Label("Title",lab,"THE GENETIC LAB",new Vector2(0,270),new Vector2(350,32),24,ink);
        Label("Hint",lab,"Research seeds. Purchase permanent upgrades.",new Vector2(0,238),new Vector2(355,24),14,muted);
        var purchasePrefab=PurchasePrefab(lab);
        string[] purchaseIds={"iron","moonflower","growth","yield"};
        for(int i=0;i<purchaseIds.Length;i++)
        {
            var existing=lab.Find(purchaseIds[i]+" Purchase");
            var row=existing!=null?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(purchasePrefab,lab);
            row.name=purchaseIds[i]+" Purchase"; ((RectTransform)row.transform).anchoredPosition=new Vector2(0,157-i*132);
            var view=row.GetComponent<ProgressionRowView>(); Ref(view,"session",session); String(view,"purchaseId",purchaseIds[i]); Bool(view,"isUpgrade",i>=2);
        }

        var garden=canvas.transform.Find("Garden");
        var save=Button("SaveButton",garden,"Save",new Vector2(354,268),new Vector2(95,28));
        var newFarm=Button("NewFarmButton",garden,"New farm",new Vector2(239,268),new Vector2(112,28));
        var goal=canvas.transform.Find("CurrentGoal").GetComponent<TMP_Text>(); goal.transform.SetParent(garden,false);
        SetRect(goal.rectTransform,new Vector2(0,-280),new Vector2(850,32)); goal.fontSize=14;
        var saveStatus=Label("SaveStatus",header,"",new Vector2(-85,-25),new Vector2(180,24),12,muted);

        var overlay=Box("New Farm Confirmation",canvas.transform,Vector2.zero,new Vector2(1440,900),new Color(0,0,0,0.78f));
        overlay.anchorMin=Vector2.zero; overlay.anchorMax=Vector2.one; overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var dialog=Box("Dialog",overlay,Vector2.zero,new Vector2(650,250),panelColor);
        Label("Title",dialog,"START A NEW FARM",new Vector2(0,80),new Vector2(580,40),27,ink);
        var recovery=Label("Message",dialog,"",new Vector2(0,12),new Vector2(590,80),18,muted);
        var confirm=Button("ConfirmButton",dialog,"Start new farm",new Vector2(-145,-75),new Vector2(250,45));
        var cancel=Button("CancelButton",dialog,"Keep current farm",new Vector2(145,-75),new Vector2(250,45));
        var hud=canvas.GetComponent<FarmHudView>();
        Ref(hud,"processingPanel",processing.gameObject); Ref(hud,"labPanel",lab.gameObject); Ref(hud,"newFarmPanel",overlay.gameObject);
        Ref(hud,"processingButton",processingButton); Ref(hud,"labButton",labButton); Ref(hud,"saveButton",save); Ref(hud,"newFarmButton",newFarm);
        Ref(hud,"confirmNewButton",confirm); Ref(hud,"cancelNewButton",cancel); Ref(hud,"goalLabel",goal); Ref(hud,"saveLabel",saveStatus); Ref(hud,"recoveryLabel",recovery);
        processing.gameObject.SetActive(false); lab.gameObject.SetActive(false); overlay.gameObject.SetActive(false); shop.gameObject.SetActive(true);
        PlayerSettings.runInBackground=true; PlayerSettings.companyName="GrowWhatever"; PlayerSettings.productName="GrowWhatever";
        PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900; PlayerSettings.resizableWindow=true;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return new { scene=scene.path,growables=4,recipes=2,research=2,upgrades=2,audioClips=8 };
    }

    static GrowableDefinition Growable(string file,string id,string name,int price,float seconds,int yield,int sell,bool aura,Sprite growing,Sprite mature)
    {
        string path=$"Assets/Data/Growables/{file}.asset";
        var definition=AssetDatabase.LoadAssetAtPath<GrowableDefinition>(path);
        bool create=definition==null;
        if(create) definition=ScriptableObject.CreateInstance<GrowableDefinition>();
        var s=new SerializedObject(definition);
        s.FindProperty("id").stringValue=id; s.FindProperty("displayName").stringValue=name;
        s.FindProperty("produceName").stringValue=aura?"Moon essence":"Iron ore";
        s.FindProperty("seedPrice").intValue=price; s.FindProperty("growthSeconds").floatValue=seconds;
        s.FindProperty("harvestYield").intValue=yield; s.FindProperty("producePrice").intValue=sell;
        s.FindProperty("initiallyUnlocked").boolValue=false; s.FindProperty("grantsNeighborBoost").boolValue=aura;
        s.FindProperty("neighborMultiplier").floatValue=1.25f;
        s.FindProperty("growingSprite").objectReferenceValue=growing; s.FindProperty("matureSprite").objectReferenceValue=mature;
        s.ApplyModifiedPropertiesWithoutUndo();
        if(create) AssetDatabase.CreateAsset(definition,path); else EditorUtility.SetDirty(definition);
        return definition;
    }

    static void AuthorAudio()
    {
        var root=GameObject.Find("Farm Audio")??new GameObject("Farm Audio");
        var source=Get<AudioSource>(root); source.playOnAwake=false; source.spatialBlend=0;
        var feedback=Get<FarmAudio>(root); Ref(feedback,"source",source);
        string[] files={"click.wav","sowing.mp3","harvest.wav","buyOrSell.wav","UnableOperate.wav","ResearchSuccess.mp3","UnlockOrLevelUp.wav","MissionClear.mp3"};
        var data=new SerializedObject(feedback); var clips=data.FindProperty("clips"); clips.arraySize=files.Length;
        for(int i=0;i<files.Length;i++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/"+files[i]);
            if(clip==null) throw new InvalidOperationException("Missing requested sound: "+files[i]);
            var entry=clips.GetArrayElementAtIndex(i); entry.FindPropertyRelative("sound").enumValueIndex=i;
            entry.FindPropertyRelative("clip").objectReferenceValue=clip;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); Ref(session,"audioFeedback",feedback);
        if(Camera.main.GetComponent<AudioListener>()==null) Camera.main.gameObject.AddComponent<AudioListener>();
    }

    static void CompactShopPrefab()
    {
        string path="Assets/Prefab/UI/SeedShopRow.prefab"; var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            ((RectTransform)root.transform).sizeDelta=new Vector2(350,122);
            SetRect((RectTransform)root.transform.Find("Portrait"),new Vector2(-132,28),new Vector2(60,64));
            var title=root.transform.Find("Title").GetComponent<TMP_Text>(); SetRect(title.rectTransform,new Vector2(40,40),new Vector2(245,24)); title.fontSize=17;
            var inventory=root.transform.Find("Inventory").GetComponent<TMP_Text>(); SetRect(inventory.rectTransform,new Vector2(40,10),new Vector2(245,40)); inventory.fontSize=12;
            string[] names={"SelectButton","BuyButton","SellSeedButton","SellProduceButton"};
            for(int i=0;i<names.Length;i++)
            {
                var button=root.transform.Find(names[i]); SetRect((RectTransform)button,new Vector2(i%2==0?-88:88,i<2?-26:-54),new Vector2(156,24));
                button.GetComponentInChildren<TMP_Text>().fontSize=14;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject RecipePrefab(Transform parent)
    {
        const string path="Assets/Prefab/UI/RecipeRow.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)return existing;
        var root=Box("RecipeTemplate",parent,Vector2.zero,new Vector2(350,116),cardColor);
        var title=Label("Title",root,"",new Vector2(0,39),new Vector2(330,26),20,ink);
        var details=Label("Details",root,"",new Vector2(0,4),new Vector2(330,44),14,muted);
        var queue=Button("QueueButton",root,"Queue once",new Vector2(-87,-40),new Vector2(156,30));
        var sell=Button("SellButton",root,"Sell one",new Vector2(87,-40),new Vector2(156,30));
        var view=root.gameObject.AddComponent<RecipeRowView>(); Ref(view,"title",title); Ref(view,"details",details); Ref(view,"queueButton",queue); Ref(view,"sellButton",sell);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab;
    }

    static GameObject PurchasePrefab(Transform parent)
    {
        const string path="Assets/Prefab/UI/ProgressionRow.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)return existing;
        var root=Box("PurchaseTemplate",parent,Vector2.zero,new Vector2(350,122),cardColor);
        var title=Label("Title",root,"",new Vector2(0,40),new Vector2(330,26),19,ink);
        var cost=Label("Cost",root,"",new Vector2(0,5),new Vector2(330,45),14,muted);
        var button=Button("PurchaseButton",root,"Research",new Vector2(0,-43),new Vector2(320,30));
        var view=root.gameObject.AddComponent<ProgressionRowView>(); Ref(view,"title",title); Ref(view,"costLabel",cost); Ref(view,"purchaseButton",button); Ref(view,"buttonLabel",button.GetComponentInChildren<TMP_Text>());
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab;
    }

    static T Get<T>(GameObject root) where T:Component
    { var component=root.GetComponent<T>(); return component!=null?component:root.AddComponent<T>(); }
    static void SetRect(RectTransform rect,Vector2 position,Vector2 size)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f); rect.anchoredPosition=position; rect.sizeDelta=size; }
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var existing=parent.Find(name); var root=existing!=null?existing.gameObject:new GameObject(name,typeof(RectTransform));
        root.transform.SetParent(parent,false); var rect=(RectTransform)root.transform; SetRect(rect,position,size); return rect;
    }
    static RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    { var rect=Rect(name,parent,position,size); var image=Get<UnityEngine.UI.Image>(rect.gameObject); image.color=color; image.raycastTarget=false; return rect; }
    static TMP_Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,float fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
    {
        var rect=Rect(name,parent,position,size); var label=Get<TextMeshProUGUI>(rect.gameObject); label.font=font; label.text=text; label.fontSize=fontSize;
        label.color=color; label.alignment=align; label.raycastTarget=false; label.textWrappingMode=TextWrappingModes.Normal; return label;
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size)
    {
        var rect=Box(name,parent,position,size,new Color32(74,103,72,255)); var image=rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget=true;
        var button=Get<UnityEngine.UI.Button>(rect.gameObject); button.targetGraphic=image;
        button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
        var label=Label("Label",rect,text,Vector2.zero,size,16,ink); label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero; return button;
    }
    static void Ref(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var s=new SerializedObject(target); s.FindProperty(field).objectReferenceValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static void String(UnityEngine.Object target,string field,string value)
    { var s=new SerializedObject(target); s.FindProperty(field).stringValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static void Bool(UnityEngine.Object target,string field,bool value)
    { var s=new SerializedObject(target); s.FindProperty(field).boolValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
}
return FarmExpansion.Apply();
