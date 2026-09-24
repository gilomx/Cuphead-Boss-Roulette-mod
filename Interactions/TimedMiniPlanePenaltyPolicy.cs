using System.Collections.Generic;

namespace Gilomx.CupheadBossRoulette
{
    // DamageDealer can report several enemy colliders for one projectile in
    // the same frame. Admit one native player hit per shooter at a time; the
    // game's own hit invulnerability then prevents a held shot from draining
    // every life immediately.
    internal sealed class TimedMiniPlanePenaltyPolicy
    {
        private readonly HashSet<int> pendingPlayers = new HashSet<int>();

        internal static bool IsViolation(
            bool challengeActive,
            float appliedDamage,
            bool enemyTarget,
            bool validPlayer,
            bool smallPlaneDamage,
            bool superDamage)
        {
            return challengeActive && appliedDamage > 0f && enemyTarget &&
                validPlayer && !smallPlaneDamage && !superDamage;
        }

        internal bool TryQueue(int playerId)
        {
            return pendingPlayers.Add(playerId);
        }

        internal void Complete(int playerId)
        {
            pendingPlayers.Remove(playerId);
        }

        internal void Clear()
        {
            pendingPlayers.Clear();
        }
    }
}
