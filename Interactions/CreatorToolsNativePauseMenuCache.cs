using System;

namespace Gilomx.CupheadBossRoulette
{
    // The game registers one LevelGUI owner. Resolve its serialized menu once
    // per owner/lifetime, then read live menu state on every query. No scene
    // discovery or reflection is needed for ordinary warm gameplay frames.
    internal sealed class CreatorToolsNativePauseMenuCache<TOwner, TMenu>
        where TOwner : class where TMenu : class
    {
        private readonly Func<TOwner, TMenu> resolve;
        private readonly Predicate<TOwner> ownerExists;
        private readonly Func<TOwner, TOwner, bool> sameOwner;
        private readonly Predicate<TMenu> menuExists;
        private readonly Predicate<TMenu> menuActive;
        private TOwner cachedOwner;
        private TMenu cachedMenu;

        internal CreatorToolsNativePauseMenuCache(Func<TOwner, TMenu> resolve,
            Predicate<TOwner> ownerExists, Func<TOwner, TOwner, bool> sameOwner,
            Predicate<TMenu> menuExists, Predicate<TMenu> menuActive)
        {
            this.resolve = resolve;
            this.ownerExists = ownerExists;
            this.sameOwner = sameOwner;
            this.menuExists = menuExists;
            this.menuActive = menuActive;
        }

        internal bool TryGetActive(TOwner owner, out TMenu value)
        {
            value = null;
            if (!ownerExists(owner))
            {
                Invalidate();
                return false;
            }
            if (!sameOwner(cachedOwner, owner) || !menuExists(cachedMenu))
            {
                cachedOwner = owner;
                cachedMenu = resolve(owner);
            }
            if (!menuExists(cachedMenu) || !menuActive(cachedMenu))
                return false;
            value = cachedMenu;
            return true;
        }

        internal void Invalidate()
        {
            cachedOwner = null;
            cachedMenu = null;
        }
    }
}
