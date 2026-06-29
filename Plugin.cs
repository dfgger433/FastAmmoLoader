using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.FastAmmoLoader
{
    [BepInPlugin("com.fastammoloader.casualtiesunknown", "快速装弹", "1.2.0")]
    public class FastAmmoLoaderPlugin : BaseUnityPlugin
    {
        public static FastAmmoLoaderPlugin Instance;

        private ConfigEntry<KeyboardShortcut> loadAmmoKey;
        private Harmony harmony;
        private static int handledFrame = -1;

        private void Awake()
        {
            Instance = this;

            loadAmmoKey = Config.Bind("Keys", "LoadAmmo",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "按下此键自动从打开的容器中装填弹药到手持的枪械或弹夹");

            harmony = new Harmony("com.fastammoloader.casualtiesunknown");
            harmony.PatchAll();

            Logger.LogInfo("快速装弹 Mod v1.2.0 已加载 (兼容多人联机)");
        }

        private void Update()
        {
            ProcessLoadAmmo(PlayerCamera.main);
        }

        private void ProcessLoadAmmo(PlayerCamera cam)
        {
            if (cam == null || cam.body == null) return;
            if (Time.timeScale <= 0f) return;
            if (Time.frameCount == handledFrame) return;

            if (!loadAmmoKey.Value.IsDown()) return;

            handledFrame = Time.frameCount;

            Body body = cam.body;
            Container openContainer = cam.currentContainer;
            if (openContainer == null) return;

            int handSlot = body.handSlot;
            Item heldItem = body.GetItem(handSlot);
            if (heldItem == null) return;

            GunScript gun = heldItem.GetComponent<GunScript>();
            if (gun == null) gun = heldItem.GetComponentInChildren<GunScript>(true);

            if (gun != null)
            {
                LoadAmmoIntoGun(body, gun, heldItem, openContainer);
                cam.RepopulateContainer();
                return;
            }

            AmmoScript heldAmmo = heldItem.GetComponent<AmmoScript>();
            if (heldAmmo != null && heldAmmo.itemType == AmmoScript.AmmoItemType.Magazine)
            {
                LoadRoundsIntoMagazine(body, heldAmmo, heldItem, openContainer);
                cam.RepopulateContainer();
            }
        }

        private void LoadAmmoIntoGun(Body body, GunScript gun, Item gunItem, Container container)
        {
            if (gun.feedType == GunScript.FeedType.Direct)
            {
                LoadRoundsFromContainer(body, gunItem, gun.ammoType, container, gun.magCapacity - gun.roundsInMag);
                return;
            }

            if (gun.hasMag)
            {
                LoadRoundsFromContainer(body, gunItem, gun.ammoType, container, gun.magCapacity - gun.roundsInMag);
                return;
            }

            AmmoScript bestMag = null;
            Item bestMagItem = null;
            int bestRounds = -1;

            foreach (Transform child in container.transform)
            {
                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Magazine) continue;
                if (ammo.ammoType != gun.ammoType) continue;
                if (ammo.rounds > bestRounds)
                {
                    bestRounds = ammo.rounds;
                    bestMag = ammo;
                    bestMagItem = child.GetComponent<Item>();
                }
            }

            if (bestMag != null && bestMagItem != null)
            {
                body.CombineItems(gunItem, bestMagItem);
            }
        }

        private void LoadRoundsIntoMagazine(Body body, AmmoScript magazine, Item magItem, Container container)
        {
            if (magazine.rounds >= magazine.maxRounds) return;
            LoadRoundsFromContainer(body, magItem, magazine.ammoType, container, magazine.maxRounds - magazine.rounds);
        }

        private static void LoadRoundsFromContainer(Body body, Item targetItem, GunScript.AmmoType ammoType, Container container, int maxToLoad)
        {
            if (maxToLoad <= 0) return;

            List<Item> ammoItems = new List<Item>();

            foreach (Transform child in container.transform)
            {
                if (ammoItems.Count >= maxToLoad) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != ammoType) continue;

                Item item = child.GetComponent<Item>();
                if (item != null) ammoItems.Add(item);
            }

            foreach (Item ammoItem in ammoItems)
            {
                if (ammoItem == null) continue;
                container.UnloadItem(ammoItem, null);
                body.CombineItems(targetItem, ammoItem);
            }
        }
    }

    [HarmonyPatch(typeof(GunScript), "LoadMag")]
    public static class GunScript_LoadMag_Patch
    {
        static bool Prefix(GunScript __instance, AmmoScript ammo)
        {
            if (__instance.feedType == GunScript.FeedType.Mag &&
                __instance.hasMag &&
                ammo.ammoType == __instance.ammoType &&
                ammo.itemType == AmmoScript.AmmoItemType.Round &&
                __instance.roundsInMag < __instance.magCapacity)
            {
                __instance.roundsInMag++;
                Object.Destroy(((Component)ammo).gameObject);
                Sound.Play("gunloadshell", ((Component)__instance).transform.position);
                return false;
            }
            return true;
        }
    }
}
