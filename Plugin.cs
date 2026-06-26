using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknown.FastAmmoLoader
{
    [BepInPlugin("com.fastammoloader.casualtiesunknown", "快速装弹", "1.0.0")]
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

            Logger.LogInfo("快速装弹 Mod v1.0.0 已加载");
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
                LoadAmmoIntoGun(body, gun, openContainer);
                cam.RepopulateContainer();
                return;
            }

            AmmoScript heldAmmo = heldItem.GetComponent<AmmoScript>();
            if (heldAmmo != null && heldAmmo.itemType == AmmoScript.AmmoItemType.Magazine)
            {
                LoadRoundsIntoMagazine(body, heldAmmo, openContainer);
                cam.RepopulateContainer();
            }
        }

        private void LoadAmmoIntoGun(Body body, GunScript gun, Container container)
        {
            if (gun.feedType == GunScript.FeedType.Direct)
            {
                LoadRoundsIntoDirectFeedGun(body, gun, container);
            }
            else
            {
                LoadMagazineIntoGun(body, gun, container);
            }
        }

        private void LoadRoundsIntoDirectFeedGun(Body body, GunScript gun, Container container)
        {
            if (gun.roundsInMag >= gun.magCapacity) return;

            bool loaded = false;
            List<Transform> toDestroy = new List<Transform>();

            foreach (Transform child in container.transform)
            {
                if (gun.roundsInMag >= gun.magCapacity) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != gun.ammoType) continue;

                if (gun.racked && gun.roundInChamber == GunScript.RoundInChamber.None)
                {
                    gun.roundInChamber = GunScript.RoundInChamber.Round;
                }
                else
                {
                    gun.roundsInMag++;
                }
                toDestroy.Add(child);
                loaded = true;
            }

            foreach (Transform t in toDestroy)
            {
                Object.Destroy(t.gameObject);
            }

            if (loaded)
            {
                Sound.Play("gunloadshell", gun.transform.position, false, true, null, 1f, 1f, false, false);
            }
        }

        private void LoadMagazineIntoGun(Body body, GunScript gun, Container container)
        {
            AmmoScript bestMag = null;
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
                }
            }

            if (bestMag != null)
            {
                if (gun.hasMag)
                {
                    gun.UnloadMag();
                }
                Item magItem = bestMag.GetComponent<Item>();
                container.UnloadItem(magItem, null);
                gun.LoadMag(bestMag);
                Sound.Play("gunloadshell", gun.transform.position, false, true, null, 1f, 1f, false, false);
            }
            else
            {
                LoadRoundsIntoGunMagazine(body, gun, container);
            }
        }

        private void LoadRoundsIntoGunMagazine(Body body, GunScript gun, Container container)
        {
            AmmoScript gunMag = null;

            foreach (Transform child in gun.transform)
            {
                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo != null && ammo.itemType == AmmoScript.AmmoItemType.Magazine)
                {
                    gunMag = ammo;
                    break;
                }
            }

            if (gunMag == null || gunMag.rounds >= gunMag.maxRounds) return;

            List<Transform> toDestroy = new List<Transform>();

            foreach (Transform child in container.transform)
            {
                if (gunMag.rounds >= gunMag.maxRounds) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != gun.ammoType) continue;

                gunMag.rounds++;
                toDestroy.Add(child);
            }

            foreach (Transform t in toDestroy)
            {
                Object.Destroy(t.gameObject);
            }

            if (toDestroy.Count > 0)
            {
                Sound.Play("gunloadshell", gun.transform.position, false, true, null, 1f, 1f, false, false);
            }
        }

        private void LoadRoundsIntoMagazine(Body body, AmmoScript magazine, Container container)
        {
            if (magazine.rounds >= magazine.maxRounds) return;

            List<Transform> toDestroy = new List<Transform>();

            foreach (Transform child in container.transform)
            {
                if (magazine.rounds >= magazine.maxRounds) break;

                AmmoScript ammo = child.GetComponent<AmmoScript>();
                if (ammo == null || ammo.itemType != AmmoScript.AmmoItemType.Round) continue;
                if (ammo.ammoType != magazine.ammoType) continue;

                magazine.rounds++;
                toDestroy.Add(child);
            }

            foreach (Transform t in toDestroy)
            {
                Object.Destroy(t.gameObject);
            }

            if (toDestroy.Count > 0)
            {
                Sound.Play("gunloadshell", magazine.transform.position, false, true, null, 1f, 1f, false, false);
            }
        }
    }
}
