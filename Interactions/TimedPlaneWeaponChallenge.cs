using System.Collections.Generic;
using System.Reflection;

namespace Gilomx.CupheadBossRoulette
{
    // Store the full-size selection, not the temporary gun used while shrunk.
    // Native SwitchWeapon ends basic fire, clears unshrunkWeapon and restarts
    // the proper small-plane shot when necessary. Never interrupt an EX/super.
    internal sealed class TimedPlaneWeaponChallenge
    {
        private static readonly FieldInfo Current = typeof(PlanePlayerWeaponManager).GetField("currentWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Unshrunk = typeof(PlanePlayerWeaponManager).GetField("unshrunkWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Switch = typeof(PlanePlayerWeaponManager).GetMethod("SwitchWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Dictionary<PlanePlayerWeaponManager, Weapon> previous = new Dictionary<PlanePlayerWeaponManager, Weapon>();
        private readonly List<PlanePlayerWeaponManager> released = new List<PlanePlayerWeaponManager>();
        internal static bool Supported { get { return Current != null && Unshrunk != null && Switch != null; } }
        internal bool IsRestoring { get; private set; }

        internal void Update(CreatorToolsTimedChallenge timer)
        {
            var active = timer.Active && CreatorToolsTimedChallenge.RequiresPlane(timer.Item);
            if (!active)
            {
                released.Clear();
                foreach (var entry in previous)
                {
                    var manager = entry.Key;
                    if (manager == null || SceneLoader.CurrentlyLoading || manager.player == null ||
                        manager.player.IsDead || !manager.gameObject.activeInHierarchy)
                    {
                        released.Add(manager);
                        continue;
                    }
                    if (manager.player.WeaponBusy) continue;
                    SetNormalWeapon(manager, entry.Value);
                    released.Add(manager);
                }
                foreach (var manager in released) previous.Remove(manager);
                IsRestoring = previous.Count > 0;
                return;
            }
            IsRestoring = false;
            foreach (var actor in PlayerManager.GetAllPlayers())
            {
                var player = actor as PlanePlayerController;
                if (player == null || player.IsDead || !player.gameObject.activeInHierarchy || player.weaponManager == null) continue;
                var manager = player.weaponManager;
                if (!previous.ContainsKey(manager)) previous.Add(manager, NormalWeapon(manager));
                if (player.WeaponBusy) continue;
                var chalice = player.stats != null && player.stats.isChalice;
                var desired = timer.Item == CreatorToolsTimedChallenge.NoBombs
                    ? (chalice ? Weapon.plane_chalice_weapon_3way : Weapon.plane_weapon_peashot)
                    : (chalice ? Weapon.plane_chalice_weapon_bomb : Weapon.plane_weapon_bomb);
                SetNormalWeapon(manager, desired);
            }
        }

        private static Weapon NormalWeapon(PlanePlayerWeaponManager manager)
        {
            var unshrunk = (Weapon)Unshrunk.GetValue(manager);
            return unshrunk != Weapon.None ? unshrunk : (Weapon)Current.GetValue(manager);
        }

        private static void SetNormalWeapon(PlanePlayerWeaponManager manager, Weapon weapon)
        {
            if (weapon != Weapon.None && NormalWeapon(manager) != weapon)
                Switch.Invoke(manager, new object[] { weapon });
        }
    }
}
