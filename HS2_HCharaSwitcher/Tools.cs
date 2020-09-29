using System.Linq;
using System.Collections.Generic;

using HarmonyLib;

using Manager;
using CharaCustom;

using UnityEngine;
using UnityEngine.UI;

namespace HS2_HCharaSwitcher
{
    public static class Tools
    {
        private static HSceneSpriteCoordinatesCard comp;
        private static CanvasGroup canvas;
        private static Traverse ctrav;

        private static Button applyBtn;
        
        public static Toggle wakeToggle;

        public static bool isSelectedFemale;
        
        public static void CreateUI()
        {
            var UI = GameObject.Find("UI");
            if (UI == null)
                return;
            
            var orig = UI.transform.Find("ClothPanel/CoordinatesCard");
            if (orig == null)
                return;
            
            var copy = Object.Instantiate(orig, orig.transform.parent);
            
            comp = copy.gameObject.GetComponent<HSceneSpriteCoordinatesCard>();
            if (comp == null)
                return;
            
            ctrav = Traverse.Create(comp);
            
            copy.name = "CharacterCard";
            
            var oldPos = copy.localPosition;
            copy.localPosition = new Vector3(oldPos.x, -350, oldPos.z);

            canvas = copy.gameObject.GetComponent<CanvasGroup>();

            var undo = UI.transform.Find("ClothPanel/CharacterCard/CardImageBG/BeforeCoode");
            if (undo == null)
                return;
            
            Object.Destroy(undo.gameObject);

            var content = UI.transform.Find("ClothPanel/CharacterCard/CoodenatePanel/Scroll View/Viewport/Content");
            if (content == null)
                return;

            var children = (from Transform child in content where child != null select child.gameObject).ToList();
            children.ForEach(Object.Destroy);
            
            var image = UI.transform.Find("ClothPanel/CharacterCard/CoodenatePanel/Image");
            if (image == null)
                return;

            var imgcopy = Object.Instantiate(image, image.transform.parent.parent);
            imgcopy.name = "Separator";
            imgcopy.localPosition = new Vector3(202, 5, 0);
            imgcopy.localScale = new Vector3(2, 1, 1);
            
            var apply = UI.transform.Find("ClothPanel/CharacterCard/CoodenatePanel/DecideCoode");
            if (apply == null)
                return;

            var coodPanel = apply.parent;
            
            var text = apply.gameObject.GetComponentInChildren<Text>();
            if (text == null)
                return;
            
            text.text = "Switch";
            
            applyBtn = apply.gameObject.GetComponent<Button>();
            if (applyBtn == null)
                return;
            
            PopulateList();
            SetupSwitch();

            {
                var btnCopy = Object.Instantiate(apply, coodPanel);
                btnCopy.name = "Swap Girls";

                var tr = btnCopy.GetComponent<RectTransform>();
                tr.offsetMin = new Vector2(-102.5f, 255);
                tr.offsetMax = new Vector2(-37.5f, 285);
                
                var btnText = btnCopy.GetComponentInChildren<Text>();
                btnText.text = "Swap Girls";
                btnText.resizeTextForBestFit = true;

                var btnComp = btnCopy.GetComponent<Button>();
                
                btnComp.onClick = new Button.ButtonClickedEvent();
                btnComp.onClick.AddListener(delegate
                {
                    HS2_HCharaSwitcher.instance.StartCoroutine(HS2_HCharaSwitcher.SwapCharacters(true));
                });
            }
            
            {
                var btnCopy = Object.Instantiate(apply, coodPanel);
                btnCopy.name = "Swap Boys";

                var tr = btnCopy.GetComponent<RectTransform>();
                tr.offsetMin = new Vector2(-32.5f, 255);
                tr.offsetMax = new Vector2(32.5f, 285);
                
                var btnText = btnCopy.GetComponentInChildren<Text>();
                btnText.text = "Swap Boys";
                btnText.resizeTextForBestFit = true;

                var btnComp = btnCopy.GetComponent<Button>();
                
                btnComp.onClick = new Button.ButtonClickedEvent();
                btnComp.onClick.AddListener(delegate
                {
                    HS2_HCharaSwitcher.instance.StartCoroutine(HS2_HCharaSwitcher.SwapCharacters(false));
                });
            }
            
            var checkBox = UI.transform.Find("ScreenEffect/Panel/DetailPanel/AmbientOcclusion/UI/Draw");
            
            var wakeCheckBox = Object.Instantiate(checkBox, coodPanel);
            wakeCheckBox.localPosition = new Vector3(225, -256, 0);
            wakeCheckBox.name = "Refreshen";

            var wakeText = wakeCheckBox.GetComponentInChildren<Text>();
            wakeText.text = "Refreshen";
            
            wakeToggle = wakeCheckBox.GetComponentInChildren<Toggle>();
            wakeToggle.onValueChanged.RemoveAllListeners();
            wakeToggle.onValueChanged = new Toggle.ToggleEvent();
            
            wakeToggle.isOn = true;

            SetupSortButtons();
            
            HS2_HCharaSwitcher.canSwitch = true;
        }
        
        public static void PopulateList()
        {
            var lstCoordinates = ctrav.Field("lstCoordinates").GetValue<List<HSceneSpriteCoordinatesNode>>();

            foreach (var t in lstCoordinates)
                Object.Destroy(t.gameObject);
            
            lstCoordinates.Clear();

            var cards = CustomCharaFileInfoAssist.CreateCharaFileInfoList(!isSelectedFemale, isSelectedFemale);
            var newBase = cards.Where(card => card != null).Select(t => new CustomClothesFileInfo {FullPath = t.FullPath, FileName = t.FileName, name = t.name}).ToList();

            for (var i = 0; i < cards.Count; i++)
            {
                var no = i;

                var hsceneSpriteCoordinatesNode = Object.Instantiate(ctrav.Field("CoordinatesNode").GetValue<HSceneSpriteCoordinatesNode>(), ctrav.Field("Content").GetValue<Transform>());
                hsceneSpriteCoordinatesNode.gameObject.SetActive(true);
                
                lstCoordinates.Add(hsceneSpriteCoordinatesNode);
                lstCoordinates[no].id = no;
                lstCoordinates[no].coodeName.text = cards[no].name;
                lstCoordinates[no].coodeName.color = Game.defaultFontColor;
                lstCoordinates[no].CreateCoodeTime = cards[no].time;
                
                lstCoordinates[no].GetComponent<Toggle>().onValueChanged.AddListener(delegate(bool val)
                {
                    if (val)
                    {
                        ctrav.Property("_SelectedID").SetValue(no);
                        lstCoordinates[no].coodeName.color = Game.defaultFontColor;
                        
                        return;
                    }
                    
                    lstCoordinates[no].coodeName.color = Game.selectFontColor;
                });
                
                lstCoordinates[no].image = lstCoordinates[no].GetComponent<Image>();
                lstCoordinates[no].fileName = cards[no].FullPath;
            }

            comp.ListSort(1);
            comp.ListSortUpDown(1);

            ctrav.Field("lstCoordinatesBase").SetValue(newBase);
            ctrav.Field("lstCoordinates").SetValue(lstCoordinates);
            
            var pActions = comp.gameObject.GetComponentsInChildren<SceneAssist.PointerDownAction>();
            foreach (var action in pActions)
                action.listAction.Add(HS2_HCharaSwitcher.hSprite.OnClickSliderSelect);
        }

        public static void SetupChaChoice(HSceneSpriteChaChoice choice)
        {
            var dropdown = Traverse.Create(choice).Field("dropdown").GetValue<Dropdown>();
            if (dropdown == null)
                return;

            var list = new List<string>();
            list.AddRange(from chara in HS2_HCharaSwitcher.chaFemales where chara != null && chara.fileParam != null select chara.fileParam.fullname);
            list.AddRange(from chara in HS2_HCharaSwitcher.chaMales where chara != null && chara.fileParam != null select chara.fileParam.fullname);

            for (var i = 0; i < dropdown.options.Count; i++)
                dropdown.options[i].text = list[i];
        }
        
        public static void SetupSwitch()
        {
            var nodes = ctrav.Field("lstCoordinates").GetValue<List<HSceneSpriteCoordinatesNode>>();
            
            applyBtn.onClick = new Button.ButtonClickedEvent();
            applyBtn.onClick.AddListener(delegate
            {
                var selected = ctrav.Property("_SelectedID").GetValue<int>();

                foreach (var t in nodes.Where(t => t.id == selected))
                    HS2_HCharaSwitcher.ChangeCharacter(t.fileName, HS2_HCharaSwitcher.hSceneManager.numFemaleClothCustom);
            });
        }
        
        private static void SetupSortButtons()
        {
            var sort = ctrav.Field("Sort").GetValue<Button[]>();
            var sortUpDown = ctrav.Field("SortUpDown").GetValue<Button[]>();
            var sortKind = ctrav.Field("sortKind").GetValue<int>();
            
            sort[0].onClick.AddListener(delegate
            {
                sort[0].gameObject.SetActive(false);
                sort[1].gameObject.SetActive(true);
                sortKind = 0;
                comp.ListSort(sortKind);
            });
            
            sort[1].onClick.AddListener(delegate
            {
                sort[0].gameObject.SetActive(true);
                sort[1].gameObject.SetActive(false);
                sortKind = 1;
                comp.ListSort(sortKind);
            });
            
            sortUpDown[0].onClick.AddListener(delegate
            {
                sortUpDown[0].gameObject.SetActive(false);
                sortUpDown[1].gameObject.SetActive(true);
                comp.ListSortUpDown(1);
            });
            
            sortUpDown[1].onClick.AddListener(delegate
            {
                sortUpDown[0].gameObject.SetActive(true);
                sortUpDown[1].gameObject.SetActive(false);
                comp.ListSortUpDown(0);
            });
        }
        
        public static void TogglePanel(bool state)
        {
            if (canvas == null)
                return;
            
            canvas.alpha = state ? 1f : 0f;
            canvas.blocksRaycasts = state;
        }
        
        public static HScene.AnimationListInfo CopyAnimationInfo(HScene.AnimationListInfo original)
        {
            var newInfo = new HScene.AnimationListInfo
            {
                id = original.id,
                nameAnimation = original.nameAnimation,
                assetpathBaseM = original.assetpathBaseM,
                assetBaseM = original.assetBaseM,
                assetpathMale = original.assetpathMale,
                fileMale = original.fileMale,
                isMaleHitObject = original.isMaleHitObject,
                fileMotionNeckMale = original.fileMotionNeckMale,
                assetpathBaseM2 = original.assetpathBaseM2,
                assetBaseM2 = original.assetBaseM2,
                assetpathMale2 = original.assetpathMale2,
                fileMale2 = original.fileMale2,
                isMaleHitObject2 = original.isMaleHitObject2,
                fileMotionNeckMale2 = original.fileMotionNeckMale2,
                assetpathBaseF = original.assetpathBaseF,
                assetBaseF = original.assetBaseF,
                assetpathFemale = original.assetpathFemale,
                fileFemale = original.fileFemale,
                isFemaleHitObject = original.isFemaleHitObject,
                fileMotionNeckFemale = original.fileMotionNeckFemale,
                fileMotionNeckFemalePlayer = original.fileMotionNeckFemalePlayer,
                assetpathBaseF2 = original.assetpathBaseF2,
                assetBaseF2 = original.assetBaseF2,
                assetpathFemale2 = original.assetpathFemale2,
                fileFemale2 = original.fileFemale2,
                isFemaleHitObject2 = original.isFemaleHitObject2,
                fileMotionNeckFemale2 = original.fileMotionNeckFemale2,
                ActionCtrl = original.ActionCtrl,
                nPositons = original.nPositons,
                lstOffset = original.lstOffset,
                isNeedItem = original.isNeedItem,
                nDownPtn = original.nDownPtn,
                nStatePtns = original.nStatePtns,
                nFaintnessLimit = original.nFaintnessLimit,
                nIyaAction = original.nIyaAction,
                Achievments = original.Achievments,
                nInitiativeFemale = original.nInitiativeFemale,
                nBackInitiativeID = original.nBackInitiativeID,
                lstSystem = original.lstSystem,
                nMaleSon = original.nMaleSon,
                nFemaleUpperCloths = original.nFemaleUpperCloths,
                nFemaleLowerCloths = original.nFemaleLowerCloths,
                nFeelHit = original.nFeelHit,
                nameCamera = original.nameCamera,
                fileSiruPaste = original.fileSiruPaste,
                fileSiruPasteSecond = original.fileSiruPasteSecond,
                fileSe = original.fileSe,
                nShortBreahtPlay = original.nShortBreahtPlay,
                hasVoiceCategory = original.hasVoiceCategory,
                nPromiscuity = original.nPromiscuity,
                reverseTaii = original.reverseTaii,
                Event = original.Event,
                ParmID = original.ParmID
            };

            return newInfo;
        }
    }
}