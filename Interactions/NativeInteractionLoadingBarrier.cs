using System;
using System.Collections;

namespace Gilomx.CupheadBossRoulette
{
    // Runs after the native fade covers the scene, before the native loader
    // can enqueue a Single scene load. Never wait behind a held Unity load.
    internal static class NativeInteractionLoadingBarrier
    {
        internal const float PreparationBudgetSeconds = 30f;

        internal static IEnumerator BeforeSceneLoad(
            IEnumerator nativeLoad,
            bool prepareCatalog,
            Func<bool> settled,
            Func<bool> busy,
            Action<bool> setPreloadWindow,
            Func<float> realtime,
            Action onTimeout,
            Action<bool> setPreparing = null)
        {
            try
            {
                if (prepareCatalog && !settled())
                {
                    if (setPreparing != null) setPreparing(true);
                    var startedAt = realtime();
                    setPreloadWindow(true);
                    while (!settled() &&
                        realtime() - startedAt < PreparationBudgetSeconds)
                        yield return null;
                    setPreloadWindow(false);
                    if (!settled())
                        onTimeout();
                }

                // Drain any in-flight work when returning to a menu or map.
                // On timeout stop admitting new work, then let the current
                // Unity operation unload safely; Unity cannot cancel it.
                // Busy also includes native atlas requests, even if the
                // template is ready and the source preload has released.
                while (busy())
                {
                    if (setPreparing != null) setPreparing(true);
                    yield return null;
                }

                if (setPreparing != null) setPreparing(false);
                while (nativeLoad.MoveNext())
                    yield return nativeLoad.Current;
            }
            finally
            {
                if (setPreparing != null) setPreparing(false);
                setPreloadWindow(false);
                var disposable = nativeLoad as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }
    }
}
