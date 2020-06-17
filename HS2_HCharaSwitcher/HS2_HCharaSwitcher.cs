using HarmonyLib;

using BepInEx;

using Illusion.Game;
using AIChara;
using Manager;

namespace HS2_HCharaSwitcher
{
    [BepInProcess("HoneySelect2")]
    [BepInPlugin(nameof(HS2_HCharaSwitcher), nameof(HS2_HCharaSwitcher), VERSION)]
    public class HS2_HCharaSwitcher : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";

        private static HS2_HCharaSwitcher instance;
        
        private static HScene hScene;
        private static HSceneFlagCtrl hFlagCtrl;
        private static HSceneManager hSceneManager;

        private static ChaControl[] chaMales;
        private static ChaControl[] chaFemales;

        private void Awake()
        {
            instance = this;
            
            Harmony.CreateAndPatchAll(typeof(HS2_HCharaSwitcher));
        }

        //-- Start of HScene --//
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartAnimationInfo")]
        public static void HScene_SetStartAnimationInfo_Patch(HScene __instance, HSceneManager ___hSceneManager, ChaControl[] ___chaMales, ChaControl[] ___chaFemales)
        {
            hScene = __instance;
            hFlagCtrl = hScene.ctrlFlag;
            hSceneManager = ___hSceneManager;

            chaMales = ___chaMales;
            chaFemales = ___chaFemales;
        }

        public static void ChangeCharacter(ChaControl chara, string card, bool is2nd)
        {
            if (string.IsNullOrEmpty(card))
                return;
            
            if (chara.sex == 1 && !hFlagCtrl.bFutanari)
            {
                if ((!is2nd && chaFemales[0] == null) || (is2nd && chaFemales[1] == null))
                    return;
                
                ChangeCharacterF(chara, card, is2nd);
            }
            else if (chara.sex == 0 || (chara.sex == 1 && hFlagCtrl.bFutanari))
            {
                if ((!is2nd && chaMales[0] == null) || (is2nd && chaMales[1] == null))
                    return;
                
                ChangeCharacterM(chara, card, is2nd);
            }
        }
        
        private static void ChangeCharacterF(ChaControl chara, string card, bool is2nd)
        {
            if (!chara.chaFile.LoadCharaFile(card, chara.sex))
                return;

            var id = is2nd ? 1 : 0;
            
            chara.ChangeNowCoordinate();
            chara.Reload();

            hSceneManager.females[id] = chara;
            hSceneManager.Personality[id] = chara.chaFile.parameter2.personality;
            
            chara.visibleAll = true;

            var mtrav = Traverse.Create(hSceneManager);
            
            var FemaleState = mtrav.Field("femaleState").GetValue<ChaFileDefine.State[]>();
            FemaleState[id] = chara.fileGameInfo2.nowDrawState;

            mtrav.Field("femaleState").SetValue(FemaleState);
            hSceneManager.FemaleStateNum[id].Clear();
            hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Favor, chara.fileGameInfo2.Favor);
            hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Enjoyment, chara.fileGameInfo2.Enjoyment);
            hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Slavery, chara.fileGameInfo2.Slavery);
            hSceneManager.FemaleStateNum[id].Add(ChaFileDefine.State.Aversion, chara.fileGameInfo2.Aversion);

            if (!is2nd)
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

            if (chara.objTop != null)
            {
                chara.LoadHitObject();
                hScene.ctrlFemaleCollisionCtrls[id].Init(chara, chara.objHitHead, chara.objHitBody);
            }

            if (!is2nd)
            {
                ChaFileGameInfo2 fileGameInfo = hSceneManager.females[0].fileGameInfo2;
                
                if (hSceneManager.females[0].chaID != -1 && fileGameInfo != null)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.state_normal + (int)fileGameInfo.nowDrawState));
                else if (hSceneManager.females[0].chaID == -1)
                    Utils.Sound.Play(new Utils.Sound.SettingBGM(BGM.fur));
            }
            
            // shapes stuff L428 - L441

            hScene.ctrlHitObjectFemales[id] = new HitObjectCtrl();
            if (chaFemales[id] != null && chaFemales[id].objBodyBone != null)
            {
                hScene.ctrlHitObjectFemales[id].id = id;
                instance.StartCoroutine(hScene.ctrlHitObjectFemales[id].HitObjInit(1, chaFemales[id].objBodyBone, chaFemales[id]));
            }

            // shapes stuff L467 - L476
            
            var htrav = Traverse.Create(hScene);
            
            if (!is2nd)
            {
                htrav.Field("ctrlFeelHit").Method("FeelHitInit", hSceneManager.Personality[0]).GetValue();
                htrav.Field("ctrlFeelHit").Method("SetFeelCha", chaFemales[0]).GetValue();
            }

            var yures = htrav.Field("ctrlYures").GetValue<YureCtrl[]>();
            if (yures != null)
            {
                yures[id].Init();
                yures[id].SetChaControl(chaFemales[id]);
                yures[id].femaleID = id;
            }
            
            if(!is2nd)
                hScene.ctrlAuto.Load(hSceneManager.strAssetLeaveItToYouFolder, hSceneManager.Personality[0]);

            var dynamics = htrav.Field("ctrlDynamics").GetValue<DynamicBoneReferenceCtrl[]>();
            dynamics?[id].Init(chaFemales[id]);

            instance.StartCoroutine(is2nd
            ? hScene.ctrlVoice.Init(hSceneManager.females[0].fileParam2.personality,
                hSceneManager.females[0].fileParam2.voicePitch, hSceneManager.females[0],
                hSceneManager.females[1].fileParam2.personality, hSceneManager.females[1].fileParam2.voicePitch,
                hSceneManager.females[1])
            : hScene.ctrlVoice.Init(hSceneManager.females[0].fileParam2.personality,
                hSceneManager.females[0].fileParam2.voicePitch, hSceneManager.females[0]));

            if (!is2nd)
            {
                var animatorStateInfo = chaFemales[0].getAnimatorStateInfo(0);
                hScene.ctrlVoice.BreathProc(animatorStateInfo, chaFemales[0], 0, hFlagCtrl.voice.sleep);
            }

            if (Voice.IsPlay(hFlagCtrl.voice.voiceTrs[id]))
                Voice.Stop(hFlagCtrl.voice.voiceTrs[id]);

            // better current animation not starting
            instance.StartCoroutine(hScene.ChangeAnimation(hScene.StartAnimInfo, true, false, false));
        }
        
        private static void ChangeCharacterM(ChaControl chara, string card, bool is2nd)
        {

        }
    }
}
