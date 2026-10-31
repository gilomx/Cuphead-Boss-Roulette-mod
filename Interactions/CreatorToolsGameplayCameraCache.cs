using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gilomx.CupheadBossRoulette
{
    // Labels and active actors can query the view several times in one frame.
    // Share discovery, while reading live camera zoom/transform at each use.
    internal static class CreatorToolsGameplayCameraCache
    {
        private static int frame = -1;
        private static Scene scene;
        private static Camera camera;

        internal static Camera Get()
        {
            var currentScene = SceneManager.GetActiveScene();
            if (frame == Time.frameCount && scene == currentScene &&
                (ReferenceEquals(camera, null) ||
                 (camera != null && camera.enabled && camera.orthographic &&
                  camera.gameObject.activeInHierarchy)))
                return camera;

            frame = Time.frameCount;
            scene = currentScene;
            camera = Camera.main;
            if (camera != null && camera.enabled && camera.orthographic &&
                camera.gameObject.activeInHierarchy)
                return camera;

            camera = null;
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            for (var i = 0; i < cameras.Length; i++)
                if (cameras[i] != null && cameras[i].enabled &&
                    cameras[i].orthographic)
                {
                    camera = cameras[i];
                    break;
                }
            return camera;
        }
    }
}
