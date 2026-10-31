using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    internal sealed class InkRainGroundProbe
    {
        private readonly RaycastHit2D[] buffer;
        private readonly Func<Vector2, Vector2, RaycastHit2D[], int> cast;
        private readonly Func<Vector2, Vector2, RaycastHit2D[]> castAll;

        internal InkRainGroundProbe()
            : this(Physics2D.LinecastNonAlloc, Physics2D.LinecastAll, 16) { }

        internal InkRainGroundProbe(
            Func<Vector2, Vector2, RaycastHit2D[], int> cast,
            Func<Vector2, Vector2, RaycastHit2D[]> castAll, int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            this.cast = cast;
            this.castAll = castAll;
            buffer = new RaycastHit2D[capacity];
        }

        internal bool TryFindGround(Vector2 from, Vector2 to,
            out RaycastHit2D ground, out RaycastHit2D[] hits, out int count)
        {
            count = cast(from, to, buffer);
            hits = buffer;
            // A full buffer can hide the ground behind other colliders. Keep
            // the complete native query/order in this uncommon crowded case.
            if (count >= buffer.Length)
            {
                hits = castAll(from, to);
                count = hits == null ? 0 : hits.Length;
            }
            ground = new RaycastHit2D();
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || !collider.enabled || collider.name != "Level_Ground")
                    continue;
                // LinecastAll orders by distance. Do not assume the reusable
                // API has the same ordering: select the nearest valid ground
                // without changing the hit array used by diagnostics.
                if (!found || hits[i].fraction < ground.fraction)
                {
                    ground = hits[i];
                    found = true;
                }
            }
            return found;
        }
    }
}
