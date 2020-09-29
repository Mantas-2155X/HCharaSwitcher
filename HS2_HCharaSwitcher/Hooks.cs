using System.Collections.Generic;
using System.Linq;
using AIChara;
using HarmonyLib;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace HS2_HCharaSwitcher
{
    public static class Hooks
    {
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartAnimationInfo")]
        public static void HScene_SetStartAnimationInfo_Patch(HScene __instance, HSceneManager ___hSceneManager, HSceneSprite ___sprite, ChaControl[] ___chaMales, ChaControl[] ___chaFemales)
        {
            HS2_HCharaSwitcher.hScene = __instance;
            HS2_HCharaSwitcher.hSprite = ___sprite;
            HS2_HCharaSwitcher.hFlagCtrl = HS2_HCharaSwitcher.hScene.ctrlFlag;
            HS2_HCharaSwitcher.hSceneManager = ___hSceneManager;

            HS2_HCharaSwitcher.chaMales = ___chaMales;
            HS2_HCharaSwitcher.chaFemales = ___chaFemales;
            
            HS2_HCharaSwitcher.htrav = Traverse.Create(HS2_HCharaSwitcher.hScene);

            Tools.isSelectedFemale = true;
            
            Tools.CreateUI();
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "OnClickCloth")]
        public static void HSceneSprite_OnClickCloth_Patch(int mode)
        {
            if (HS2_HCharaSwitcher.hSprite.objClothPanel.alpha > 0.99f)
                Tools.TogglePanel(mode == 2);
            else
                Tools.TogglePanel(true);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneSprite), "ClothPanelClose")]
        public static void HSceneSprite_ClothPanelClose_Patch() => Tools.TogglePanel(false);
        
        public static void HSceneSpriteChaChoice_Init_ChangeSelection(HSceneSpriteChaChoice __instance, int val)
        {
            var oldIsSelectedFemale = Tools.isSelectedFemale;

            var list = new List<ChaControl>();
            list.AddRange(from chaControl in __instance.Females where chaControl != null && chaControl.fileParam != null select chaControl);
            list.AddRange(from chaControl in __instance.Males where chaControl != null && chaControl.fileParam != null select chaControl);

            Tools.isSelectedFemale = list[val].sex == 1;
            
            if(oldIsSelectedFemale != Tools.isSelectedFemale)
            {
                foreach (var comp in Object.FindObjectsOfType<HSceneSpriteCoordinatesCard>())
                {
                    if(comp == null)
                        continue;

                    var trav = Traverse.Create(comp);
                    
                    trav.Property("_SelectedID").SetValue(-1);
                    trav.Field("SelectedLabel").GetValue<Text>().text = "";
                    trav.Field("filename").SetValue("");
                    trav.Field("CardImage").GetValue<RawImage>().texture = trav.Field("CardImageDef").GetValue<Texture>();
                }
            }

            Tools.PopulateList();
            Tools.SetupSwitch();
        }
    }
}