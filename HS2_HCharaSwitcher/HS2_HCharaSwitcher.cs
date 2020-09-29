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
        public const string VERSION = "1.2.0";

        public static HS2_HCharaSwitcher instance;

        public static HScene hScene;
        public static HSceneSprite hSprite;
        public static HSceneFlagCtrl hFlagCtrl;
        public static HSceneManager hSceneManager;

        public static ChaControl[] chaMales;
        public static ChaControl[] chaFemales;
        
        public static Traverse htrav;
        
        public static bool canSwitch;
        
        private void Awake()
        {
            instance = this;
            
            Tools.isSelectedFemale = true;

            var harmony = Harmony.CreateAndPatchAll(typeof(Hooks));
            
            var iteratorMethod = AccessTools.Method(typeof(HSceneSpriteChaChoice), "<Init>b__17_0");
            var postfix = new HarmonyMethod(typeof(Hooks), nameof(Hooks.HSceneSpriteChaChoice_Init_ChangeSelection));
            harmony.Patch(iteratorMethod, null, postfix);
        }

        public static void ChangeCharacter(string card, int id)
        {
            if (!canSwitch || !ProcBase.endInit || htrav.Field("nowChangeAnim").GetValue<bool>() || hFlagCtrl.nowOrgasm)
                return;

            switch (id)
            {
                case 0:
                case 1:
                    var charaF = chaFemales[id];
                    if (charaF == null)
                        return;
            
                    instance.StartCoroutine(ChangeCharacterF(charaF, card, id));
                    break;
                case 2:
                case 3:
                    var charaM = chaMales[id - 2];
                    if (charaM == null)
                        return;
            
                    instance.StartCoroutine(ChangeCharacterM(charaM, card, id - 2));
                    break;
            }
        }

        public static IEnumerator SwapCharacters(bool swapFemales)
        {
            if (!canSwitch || !ProcBase.endInit || htrav.Field("nowChangeAnim").GetValue<bool>() || hFlagCtrl.nowOrgasm)
                yield break;
            
            if (swapFemales && chaFemales[0] != null && chaFemales[1] != null)
            {
                var firstCard = chaFemales[0].chaFile.charaFileName;
                var secondCard = chaFemales[1].chaFile.charaFileName;
                    
                yield return ChangeCharacterF(chaFemales[0], secondCard, 0);
                yield return ChangeCharacterF(chaFemales[1], firstCard, 1);
            }
            else if (!swapFemales && chaMales[0] != null && chaMales[1] != null)
            {
                var firstCard = chaMales[0].chaFile.charaFileName;
                var secondCard = chaMales[1].chaFile.charaFileName;
                    
                yield return ChangeCharacterM(chaMales[0], secondCard, 0);
                yield return ChangeCharacterM(chaMales[1], firstCard, 1);
            }
        }
        
        private static IEnumerator ChangeCharacterF(ChaControl chara, string card, int id)
        {
            canSwitch = false;

            var visible = chara.visibleAll;
            
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
            
            chara.visibleAll = visible;

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
            Tools.SetupChaChoice(hSprite.charaChoice);

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

        private static IEnumerator ChangeCharacterM(ChaControl chara, string card, int id)
        {
            canSwitch = false;

            var visible = chara.visibleAll;
            
            // card, outfit, reload
            if (!chara.chaFile.LoadCharaFile(card, chara.sex))
            {
                canSwitch = true;
                yield break;
            }

            chara.ChangeNowCoordinate();
            chara.Reload();

            if (id == 0)
            {
                chara.isPlayer = true;
                hSceneManager.player = chara;
            }

            chara.visibleAll = visible;
            
            chara.LoadHitObject();
            hScene.ctrlMaleCollisionCtrls[id].Init(chaFemales[0], chaMales[id].objHitHead, chaMales[id].objHitBody);
            
            yield return hScene.ctrlHitObjectMales[id].HitObjInit(0, chaMales[id].objBodyBone, chaMales[id]);
            
            hScene.ctrlLookAts[id].DankonInit(chaMales[id], chaFemales);

            var yure = htrav.Field("ctrlYureMale").GetValue<YureCtrlMale[]>()[id];
            
            yure.Init();
            yure.chaMale = chaMales[id];
            yure.MaleID = id;

            if (chaMales[id] != null && chaMales[id].objBodyBone != null)
            {
                hScene.ctrlEyeNeckMale[id].Init(chaMales[id], id);
                hScene.ctrlEyeNeckMale[id].SetPartner(chaFemales[id].objBodyBone, (chaFemales[1] != null) ? chaFemales[1].objBodyBone : null, (chaMales[id == 0 ? 1 : 0] != null) ? chaMales[id == 0 ? 1 : 0].objBodyBone : null);
            }  
            
            hSprite.Setting(chaFemales, chaMales);
            Tools.SetupChaChoice(hSprite.charaChoice);
            
            yield return 0;
            
            // Reload animation. The copy is unavoidable because there's an equals check for new animation
            hSprite.ChangeStart = true;
            hFlagCtrl.selectAnimationListInfo = Tools.CopyAnimationInfo(hFlagCtrl.nowAnimationInfo);

            yield return 0;
            
            canSwitch = true;
        }
    }
}