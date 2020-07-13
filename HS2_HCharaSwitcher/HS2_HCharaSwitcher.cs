using System.Collections;
using System.Collections.Generic;

using HarmonyLib;

using BepInEx;

using AIChara;
using Manager;
using Illusion.Game;

namespace HS2_HCharaSwitcher
{
    [BepInProcess("HoneySelect2")]
    [BepInPlugin(nameof(HS2_HCharaSwitcher), nameof(HS2_HCharaSwitcher), VERSION)]
    public class HS2_HCharaSwitcher : BaseUnityPlugin
    {
        public const string VERSION = "1.1.0";

        private static HS2_HCharaSwitcher instance;

        private static HScene hScene;
        public static HSceneSprite hSprite;
        private static HSceneFlagCtrl hFlagCtrl;
        public static HSceneManager hSceneManager;

        private static ChaControl[] chaMales;
        private static ChaControl[] chaFemales;
        
        private static Traverse htrav;
        
        public static bool canSwitch;
        
        private void Awake()
        {
            instance = this;
            
            Harmony.CreateAndPatchAll(typeof(HS2_HCharaSwitcher));
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), nameof(HScene.SetStartAnimationInfo))]
        public static void HScene_SetStartAnimationInfo_Patch(HScene __instance, HSceneManager ___hSceneManager, HSceneSprite ___sprite, ChaControl[] ___chaMales, ChaControl[] ___chaFemales)
        {
            hScene = __instance;
            hSprite = ___sprite;
            hFlagCtrl = hScene.ctrlFlag;
            hSceneManager = ___hSceneManager;

            chaMales = ___chaMales;
            chaFemales = ___chaFemales;
            
            htrav = Traverse.Create(hScene);

            Tools.CreateUI();
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "OnClickCloth")]
        public static void HSceneSprite_OnClickCloth_Patch(int mode)
        {
            if (hSprite.objClothPanel.alpha > 0.99f)
                Tools.TogglePanel(mode == 2);
            else
                Tools.TogglePanel(true);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "ClothPanelClose")]
        public static void HSceneSprite_ClothPanelClose_Patch() => Tools.TogglePanel(false);

        public static void ChangeCharacter(string card, int id)
        {
            if (string.IsNullOrEmpty(card))
                return;

            if (!canSwitch || !ProcBase.endInit || htrav.Field("nowChangeAnim").GetValue<bool>() || hFlagCtrl.nowOrgasm || id > 1)
                return;

            var chara = chaFemales[id];
            if (chara == null || chara.visibleAll == false)
                return;
            
            instance.StartCoroutine(ChangeCharacterF(chara, card, id));
        }
        
        private static IEnumerator ChangeCharacterF(ChaControl chara, string card, int id)
        {
            canSwitch = false;

            // card, outfit, reload
            if (!chara.chaFile.LoadCharaFile(card, chara.sex))
            {
                canSwitch = true;
                yield break;
            }

            chara.ChangeNowCoordinate();
            chara.Reload();

            hSceneManager.females[id] = chara;
            hSceneManager.Personality[id] = chara.chaFile.parameter2.personality;
            
            chara.visibleAll = true;

            // States & Rootmotion
            hSceneManager.SetFemaleState(id == 0 ? new[] {chaFemales[0], null} : new[] {null, chaFemales[1]});

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
                
                hScene.RootmotionOffsetF = new []
                {
                    new RootmotionOffset(),
                    hScene.RootmotionOffsetF[1]
                };
                
                hScene.RootmotionOffsetF[0].Chara = chaFemales[0];
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
                
                hScene.RootmotionOffsetF = new []
                {
                    hScene.RootmotionOffsetF[0],
                    new RootmotionOffset()
                };
                
                hScene.RootmotionOffsetF[1].Chara = chaFemales[1];
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
                var fileGameInfo = hSceneManager.females[0].fileGameInfo2;
                
                if (hSceneManager.females[0].chaID != -1 && fileGameInfo != null)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.state_normal + (int)fileGameInfo.nowDrawState));
                else if (hSceneManager.females[0].chaID == -1)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.fur));
            }
            
            // missing shapes stuff L428 - L441

            // More hitobjects
            hScene.ctrlHitObjectFemales[id] = new HitObjectCtrl();
            if (chaFemales[id].objBodyBone != null)
            {
                hScene.ctrlHitObjectFemales[id].id = id;
                instance.StartCoroutine(hScene.ctrlHitObjectFemales[id].HitObjInit(1, chaFemales[id].objBodyBone, chaFemales[id]));
            }

            // missing shapes stuff L467 - L476

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
            
            // ProcBases setparams
            var proc = htrav.Field("lstProc").GetValue<List<ProcBase>>();
            var mode = htrav.Field("mode").GetValue<int>();
            var modeCtrl = htrav.Field("modeCtrl").GetValue<int>();
            
            var trav = Traverse.Create(proc[mode]);
            trav.Field("feelHit").SetValue(htrav.Field("ctrlFeelHit").GetValue<FeelHit>());
            trav.Field("chaFemales").SetValue(chaFemales);
            trav.Field("ctrlYures").SetValue(yures);
            
            if (mode != -1 && modeCtrl != -1 && ProcBase.endInit)
                proc[mode].setAnimationParamater();

            yield return 0;
            yield return 0;

            // Refreshen from weakness
            if (Tools.wakeToggle.isOn && hFlagCtrl.isFaintness)
            {
                hFlagCtrl.click = HSceneFlagCtrl.ClickKind.RecoverFaintness;

                yield return 0;
                yield return 0;
            }
            
            // Reload animation. The copy is unavoidable because there's an equals check for new animation
            hSprite.ChangeStart = true;
            hFlagCtrl.selectAnimationListInfo = Tools.CopyAnimationInfo(hFlagCtrl.nowAnimationInfo);

            yield return 0;
            
            canSwitch = true;
        }
    }
}