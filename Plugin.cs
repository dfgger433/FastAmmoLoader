using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.FastAmmoLoader
{
    [BepInPlugin("com.fastammoloader.casualtiesunknown", "快速装弹", "1.1.0")]
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

            Logger.LogInfo("快速装弹 Mod v1.1.0 已加载 (兼容多人联机)");
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
                LoadRoundsIntoDirectFeedGun(body, gun, gunItem, container);
            }
            else
            {
                LoadMagazineIntoGun(body, gun, gunItem, container);
            }
        }

        private void LoadRoundsIntoDirectFeedGun(Body body, GunScript gun, Item gunItem, Container container)
        {
            if (gun.roundsInMag >= gun.magCapacity) return;

            List<Item> ammoItems = new List<Item>();

            foreach (Transform child in container.transform)
            {
                if (ammoItems.Count >= gun.magCapacity - gun.roundsInMag) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != gun.ammoType) continue;

                Item ammoItem = child.GetComponent<Item>();
                if (ammoItem != null)
                {
                    ammoItems.Add(ammoItem);
                }
            }

            foreach (Item ammoItem in ammoItems)
            {
                if (ammoItem == null) continue;
                if (gun.roundsInMag >= gun.magCapacity) break;

                body.CombineItems(gunItem, ammoItem);
            }
        }

        private void LoadMagazineIntoGun(Body body, GunScript gun, Item gunItem, Container container)
        {
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
                if (gun.hasMag)
                {
                    gun.UnloadMag();
                }
                body.CombineItems(gunItem, bestMagItem);
            }
            else
            {
                LoadRoundsIntoGunMagazine(body, gun, gunItem, container);
            }
        }

        private void LoadRoundsIntoGunMagazine(Body body, GunScript gun, Item gunItem, Container container)
        {
            AmmoScript gunMag = null;
            Item gunMagItem = null;

            foreach (Transform child in gun.transform)
            {
                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo != null && ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                {
                    gunMag = ammo;
                    gunMagItem = child.GetComponent<Item>();
                    break;
                }
            }

            if (gunMag == null || gunMag.rounds >= gunMag.maxRounds) return;

            List<Item> ammoItems = new List<Item>();

            foreach (Transform child in container.transform)
            {
                if (ammoItems.Count >= gunMag.maxRounds - gunMag.rounds) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != gun.ammoType) continue;

                Item ammoItem = child.GetComponent<Item>();
                if (ammoItem != null)
                {
                    ammoItems.Add(ammoItem);
                }
            }

            foreach (Item ammoItem in ammoItems)
            {
                if (ammoItem == null) continue;
                if (gunMag.rounds >= gunMag.maxRounds) break;

                body.CombineItems(gunMagItem, ammoItem);
            }
        }

        private void LoadRoundsIntoMagazine(Body body, AmmoScript magazine, Item magItem, Container container)
        {
            if (magazine.rounds >= magazine.maxRounds) return;

            List<Item> ammoItems = new List<Item>();

            foreach (Transform child in container.transform)
            {
                if (ammoItems.Count >= magazine.maxRounds - magazine.rounds) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != magazine.ammoType) continue;

                Item ammoItem = child.GetComponent<Item>();
                if (ammoItem != null)
                {
                    ammoItems.Add(ammoItem);
                }
            }

            foreach (Item ammoItem in ammoItems)
            {
                if (ammoItem == null) continue;
                if (magazine.rounds >= magazine.maxRounds) break;

                body.CombineItems(magItem, ammoItem);
            }
        }
    }
}
