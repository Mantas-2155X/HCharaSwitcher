using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

using HarmonyLib;

using BepInEx;
using BepInEx.Configuration;

using AIChara;
using Manager;
using CharaCustom;
using Illusion.Game;

using UnityEngine;
using UnityEngine.UI;

namespace HS2_HCharaSwitcher
{
    [BepInProcess("HoneySelect2")]
    [BepInPlugin(nameof(HS2_HCharaSwitcher), nameof(HS2_HCharaSwitcher), VERSION)]
    public class HS2_HCharaSwitcher : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";

        private static HS2_HCharaSwitcher instance;
        
        private static HScene hScene;
        private static HSceneSprite hSprite;
        private static HSceneFlagCtrl hFlagCtrl;
        private static HSceneManager hSceneManager;

        private static ChaControl[] chaMales;
        private static ChaControl[] chaFemales;

        private static Traverse htrav;
        private static Traverse ftrav;

        private static CanvasGroup canvas;
        
        private static ConfigEntry<bool> saveStatsOnSwitch { get; set; }
        
        private void Awake()
        {
            instance = this;
            
            saveStatsOnSwitch = Config.Bind("General", "Save stats when switching", false, new ConfigDescription("Save stats for the character who is getting switched. Might be buggy so disabled by default"));
            
            Harmony.CreateAndPatchAll(typeof(HS2_HCharaSwitcher));
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartAnimationInfo")]
        public static void HScene_SetStartAnimationInfo_Patch(HScene __instance, HSceneManager ___hSceneManager, HSceneSprite ___sprite, ChaControl[] ___chaMales, ChaControl[] ___chaFemales)
        {
            hScene = __instance;
            hSprite = ___sprite;
            hFlagCtrl = hScene.ctrlFlag;
            hSceneManager = ___hSceneManager;

            chaMales = ___chaMales;
            chaFemales = ___chaFemales;
            
            htrav = Traverse.Create(hScene);
            ftrav = Traverse.Create(hFlagCtrl);

            var UI = GameObject.Find("UI");
            if (UI == null)
                return;
            
            var orig = UI.transform.Find("ClothPanel/CoordinatesCard");
            if (orig == null)
                return;
            
            var copy = Instantiate(orig, orig.transform.parent);
            
            var comp = copy.gameObject.GetComponent<HSceneSpriteCoordinatesCard>();
            if (comp == null)
                return;
            
            copy.name = "CharacterCard";
            
            var oldPos = copy.localPosition;
            copy.localPosition = new Vector3(oldPos.x, -350, oldPos.z);

            canvas = copy.gameObject.GetComponent<CanvasGroup>();

            var undo = UI.transform.Find("ClothPanel/CharacterCard/CardImageBG/BeforeCoode");
            if (undo == null)
                return;
            
            Destroy(undo.gameObject);

            var content = UI.transform.Find("ClothPanel/CharacterCard/CoodenatePanel/Scroll View/Viewport/Content");
            if (content == null)
                return;

            var children = (from Transform child in content where child != null select child.gameObject).ToList();
            children.ForEach(Destroy);
            
            var apply = UI.transform.Find("ClothPanel/CharacterCard/CoodenatePanel/DecideCoode");
            if (apply == null)
                return;
        
            var text = apply.gameObject.GetComponentInChildren<Text>();
            if (text == null)
                return;
            
            text.text = "Switch";
            
            var btn = apply.gameObject.GetComponent<Button>();
            if (btn == null)
                return;
            
            PopulateList(comp);
            
            var trav = Traverse.Create(comp);
            var nodes = trav.Field("lstCoordinates").GetValue<List<HSceneSpriteCoordinatesNode>>();
            
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(delegate
            {
                var selected = trav.Property("_SelectedID").GetValue<int>();

                foreach (var t in nodes.Where(t => t.id == selected))
                    ChangeCharacter(t.fileName, hSceneManager.numFemaleClothCustom);
            });

            var sort = trav.Field("Sort").GetValue<Button[]>();
            var sortUpDown = trav.Field("SortUpDown").GetValue<Button[]>();
            var sortKind = trav.Field("sortKind").GetValue<int>();
            
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
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "OnClickCloth")]
        public static void HSceneSprite_OnClickCloth_Patch(int mode)
        {
            if (canvas == null)
                return;
            
            var visible = hSprite.objClothPanel.alpha == 1f;

            if (visible)
            {
                if (mode != 2)
                {
                    canvas.alpha = 0f;
                    canvas.blocksRaycasts = false;
                }
                else
                {
                    canvas.alpha = 1f;
                    canvas.blocksRaycasts = true;
                }
            }
            else
            {
                canvas.alpha = 1f;
                canvas.blocksRaycasts = true;
            }
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "ClothPanelClose")]
        public static void HSceneSprite_ClothPanelClose_Patch()
        {
            if (canvas == null)
                return;

            canvas.alpha = 0f;
            canvas.blocksRaycasts = false;
        }
        
        private static void PopulateList(HSceneSpriteCoordinatesCard comp)
        {
            var trav = Traverse.Create(comp);
            var lstCoordinates = trav.Field("lstCoordinates").GetValue<List<HSceneSpriteCoordinatesNode>>();

            foreach (var t in lstCoordinates)
                Destroy(t.gameObject);
            
            lstCoordinates.Clear();

            var cards = CustomCharaFileInfoAssist.CreateCharaFileInfoList(false, true);
            var newBase = new List<CustomClothesFileInfo>();
            
            foreach (var t in cards.Where(card => card != null))
                newBase.Add(new CustomClothesFileInfo {FullPath = t.FullPath, FileName = t.FileName, name = t.name});

            for (var i = 0; i < cards.Count; i++)
            {
                var no = i;

                var hsceneSpriteCoordinatesNode = Instantiate(trav.Field("CoordinatesNode").GetValue<HSceneSpriteCoordinatesNode>(), trav.Field("Content").GetValue<Transform>());
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
                        trav.Property("_SelectedID").SetValue(no);
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

            trav.Field("lstCoordinatesBase").SetValue(newBase);
            trav.Field("lstCoordinates").SetValue(lstCoordinates);
            
            var pActions = comp.gameObject.GetComponentsInChildren<SceneAssist.PointerDownAction>();
            foreach (var action in pActions)
                action.listAction.Add(hSprite.OnClickSliderSelect);
        }
        
        private static void ChangeCharacter(string card, int id)
        {
            if (string.IsNullOrEmpty(card))
                return;

            if (!ProcBase.endInit || htrav.Field("nowChangeAnim").GetValue<bool>() || hFlagCtrl.nowOrgasm)
                return;

            if (id > 1)
                return;
            
            var chara = chaFemales[id];
            if (chara == null || chara.visibleAll == false)
                return;
            
            instance.StartCoroutine(ChangeCharacterF(chara, card, id));
        }
        
        // change character coroutine //
        private static IEnumerator ChangeCharacterF(ChaControl chara, string card, int id)
        {
            if(saveStatsOnSwitch.Value)
                SaveStatsF(id);
            
            // card, outfit, reload
            if (!chara.chaFile.LoadCharaFile(card, chara.sex))
                yield break;

            chara.ChangeNowCoordinate();
            chara.Reload();

            hSceneManager.females[id] = chara;
            hSceneManager.Personality[id] = chara.chaFile.parameter2.personality;
            
            chara.visibleAll = true;

            // States
            var mtrav = Traverse.Create(hSceneManager);
            var FemaleState = mtrav.Field("femaleState").GetValue<ChaFileDefine.State[]>();
            if (FemaleState != null)
            {
                FemaleState[id] = chara.fileGameInfo2.nowDrawState;

                mtrav.Field("femaleState").SetValue(FemaleState);
                hSceneManager.FemaleStateNum[id].Clear();
                hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Favor, chara.fileGameInfo2.Favor);
                hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Enjoyment, chara.fileGameInfo2.Enjoyment);
                hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Slavery, chara.fileGameInfo2.Slavery);
                hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Aversion, chara.fileGameInfo2.Aversion);
            }

            if (id == 0)
            {
                switch (hSceneManager.FemaleState[0])
                {
                    case ChaFileDefine.State.Broken:
                        hFlagCtrl.isFaintness = true;
                        hFlagCtrl.FaintnessType = 1;
                        hFlagCtrl.isFaintnessVoice = true;
                        break;
                    case ChaFileDefine.State.Aversion:
                        hSceneManager.isForce = true;
                        break;
                }
            }
            else
            {
                switch (hSceneManager.FemaleState[1])
                {
                    case ChaFileDefine.State.Broken:
                        hFlagCtrl.FaintnessType = hFlagCtrl.FaintnessType == 1 ? 0 : 2;
                        break;
                    case ChaFileDefine.State.Aversion:
                        hSceneManager.isForceSecond = true;
                        break;
                }
            }

            // Hitobjects, collisions
            if (chara.objTop != null)
            {
                chara.LoadHitObject();
                hScene.ctrlFemaleCollisionCtrls[id].Init(chara, chara.objHitHead, chara.objHitBody);
            }

            // BGM
            if (id == 0)
            {
                ChaFileGameInfo2 fileGameInfo = hSceneManager.females[0].fileGameInfo2;
                
                if (hSceneManager.females[0].chaID != -1 && fileGameInfo != null)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.state_normal + (int)fileGameInfo.nowDrawState));
                else if (hSceneManager.females[0].chaID == -1)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.fur));
            }
            
            // shapes stuff L428 - L441

            // More hitobjects
            hScene.ctrlHitObjectFemales[id] = new HitObjectCtrl();
            if (chaFemales[id].objBodyBone != null)
            {
                hScene.ctrlHitObjectFemales[id].id = id;
                instance.StartCoroutine(hScene.ctrlHitObjectFemales[id].HitObjInit(1, chaFemales[id].objBodyBone, chaFemales[id]));
            }

            // shapes stuff L467 - L476

            // FeelHit gauge
            if (id == 0)
            {
                htrav.Field("ctrlFeelHit").Method("FeelHitInit", hSceneManager.Personality[0]).GetValue();
                htrav.Field("ctrlFeelHit").Method("SetFeelCha", chaFemales[0]).GetValue();
            }

            // yures and dynamics
            var yures = htrav.Field("ctrlYures").GetValue<YureCtrl[]>();
            if (yures != null)
            {
                yures[id].Init();
                yures[id].SetChaControl(chaFemales[id]);
                yures[id].femaleID = id;
            }
            
            if(id == 0)
                hScene.ctrlAuto.Load(hSceneManager.strAssetLeaveItToYouFolder, hSceneManager.Personality[0]);

            var dynamics = htrav.Field("ctrlDynamics").GetValue<DynamicBoneReferenceCtrl[]>();
            dynamics?[id].Init(chaFemales[id]);

            // Voice stuff
            instance.StartCoroutine(id == 1
            ? hScene.ctrlVoice.Init(hSceneManager.females[0].fileParam2.personality,
                hSceneManager.females[0].fileParam2.voicePitch, hSceneManager.females[0],
                hSceneManager.females[1].fileParam2.personality, hSceneManager.females[1].fileParam2.voicePitch,
                hSceneManager.females[1])
            : hScene.ctrlVoice.Init(hSceneManager.females[0].fileParam2.personality,
                hSceneManager.females[0].fileParam2.voicePitch, hSceneManager.females[0]));
            
            // More voice stuff
            var animatorStateInfo = chaFemales[id].getAnimatorStateInfo(0);
            hScene.ctrlVoice.BreathProc(animatorStateInfo, chaFemales[id], id, id == 0 && hFlagCtrl.voice.sleep);
            
            // Reload UI stuff
            hSprite.Setting(chaFemales, chaMales);
            hSprite.charaChoice.Init();
            
            // Reload ProcBases
            var proc = htrav.Field("lstProc").GetValue<List<ProcBase>>();
            foreach (var procBase in proc)
            {
                var trav = Traverse.Create(procBase);
                trav.Field("feelHit").SetValue(htrav.Field("ctrlFeelHit").GetValue<FeelHit>());
                trav.Field("chaFemales").SetValue(chaFemales);
                trav.Field("ctrlYures").SetValue(yures);

                try
                {
                    procBase.setAnimationParamater();
                }
                catch
                {
                    // Error if ProcBase is Les and 2nd girl doesn't exist
                }
            }
            
            yield return 0;
            yield return 0;

            hFlagCtrl.click = HSceneFlagCtrl.ClickKind.RecoverFaintness;

            yield return 0;
            yield return 0;

            // Change animation
            hSprite.ChangeStart = true;
            hFlagCtrl.selectAnimationListInfo = CopyAnimationInfo(hFlagCtrl.nowAnimationInfo);
        }

        // probably very bad way of saving old chara stats before switch //
        private static void SaveStatsF(int id)
        {
            chaFemales[id].fileGameInfo2.hCount++;
            
            hSceneManager.maleFinish = hFlagCtrl.numInside + hFlagCtrl.numOutSide + hFlagCtrl.numDrink + hFlagCtrl.numVomit;
            hSceneManager.femaleFinish = hFlagCtrl.numOrgasmTotal;
            hSceneManager.endStatus = (byte)(hFlagCtrl.isFaintness ? 1 : 0);
            
            var oldstate = chaFemales[id].fileGameInfo2.nowDrawState;
            hFlagCtrl.EndSetAddMindParam(chaFemales[id].fileParam2.hAttribute);
            hFlagCtrl.EndSetAddTraitParam(chaFemales[id].fileParam2.trait);
            hFlagCtrl.EndSetAddTaiiParam(chaFemales[id].fileGameInfo2);
            hFlagCtrl.ParamCalc(chaFemales[id].fileGameInfo2, chaFemales[0].fileParam2.personality);
            var newstate = chaFemales[id].fileGameInfo2.nowDrawState;
            
            var oldresist = chaFemales[id].fileGameInfo2.resistH;
            hFlagCtrl.FinishResistParamCalc(chaFemales[id].fileGameInfo2, chaFemales[id].fileParam2.hAttribute, chaFemales[id].fileParam2.personality);
            var newresist = chaFemales[id].fileGameInfo2.resistH;
            
            var newStateNum = -1;
            var newResistNum = -1;
            
            var siruCount = Enum.GetValues(typeof(ChaFileDefine.SiruParts)).Cast<object>().Count(obj => chaFemales[id].GetSiruFlag((ChaFileDefine.SiruParts) obj) == 2);

            if (oldstate != newstate)
            {
                if (oldstate == ChaFileDefine.State.Broken)
                    newStateNum = 6;
                else
                    switch (newstate)
                    {
                        case ChaFileDefine.State.Blank:
                            newStateNum = -1;
                            break;
                        case ChaFileDefine.State.Favor:
                            newStateNum = 2;
                            break;
                        case ChaFileDefine.State.Enjoyment:
                            newStateNum = 3;
                            break;
                        case ChaFileDefine.State.Aversion:
                            newStateNum = 5;
                            break;
                        case ChaFileDefine.State.Slavery:
                            newStateNum = 4;
                            break;
                        case ChaFileDefine.State.Broken:
                            newStateNum = 0;
                            break;
                        case ChaFileDefine.State.Dependence:
                            newStateNum = 1;
                            break;
                    }
            }
            
            if (oldresist != newresist && newresist >= 100)
                newResistNum = 0;

            htrav.Method("AfterPtn", chaFemales[id].fileParam2.personality, chaFemales[id].fileGameInfo2, newStateNum, newResistNum, siruCount).GetValue();

            hFlagCtrl.rateTuya = 0f;
            hFlagCtrl.rateNip = 0f;
            hFlagCtrl.numOrgasm = 0;
            hFlagCtrl.numOrgasmTotal = 0;
            hFlagCtrl.numSameOrgasm = 0;
            hFlagCtrl.numInside = 0;
            hFlagCtrl.numOutSide = 0;
            hFlagCtrl.numDrink = 0;
            hFlagCtrl.numVomit = 0;
            hFlagCtrl.numOrgasmM2 = 0;
            hFlagCtrl.numShotM2 = 0;
            hFlagCtrl.numOrgasmF2 = 0;
            hFlagCtrl.numShotF2 = 0;
            hFlagCtrl.numAnalOrgasm = 0;
            hFlagCtrl.numAibu = 0;
            hFlagCtrl.numHoushi = 0;
            hFlagCtrl.numSonyu = 0;
            hFlagCtrl.numLes = 0;
            hFlagCtrl.numUrine = 0;
            hFlagCtrl.numFaintness = 0;
            hFlagCtrl.numKokan = 0;
            hFlagCtrl.numAnal = 0;
            hFlagCtrl.numLeadFemale = 0;
            
            ftrav.Field("EndAddTaiiParam").Method("Clear").GetValue();
            ftrav.Field("SendParam").Method("Clear").GetValue();
            ftrav.Field("FinishResistTaii").Method("Clear").GetValue();

            hFlagCtrl.feelPain = 0f;

            hSceneManager.maleFinish = 0;
            hSceneManager.femaleFinish = 0;
            hSceneManager.endStatus = 0;
            
            if (chaFemales[id].fileGameInfo2 == null) 
                return;
            
            if (chaFemales[id].chaID == -1)
                chaFemales[id].chaFile.SaveCharaFile(Singleton<Character>.Instance.conciergePath);
            else if (!chaFemales[id].chaFile.charaFileName.IsNullOrEmpty())
                chaFemales[id].chaFile.SaveCharaFile(chaFemales[id].chaFile.charaFileName);
        }

        private static HScene.AnimationListInfo CopyAnimationInfo(HScene.AnimationListInfo original)
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