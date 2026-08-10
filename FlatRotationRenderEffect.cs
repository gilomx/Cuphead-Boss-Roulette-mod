using System;
using UnityEngine;

namespace Gilomx.CupheadBossRoulette
{
    // Final-frame image effect used by the upside-down challenge. Rotating the
    // rendered quad leaves gameplay coordinates, camera logic and hitboxes
    // untouched. The component is added last to Cuphead's battle camera so it
    // receives the image after the game's native postprocessing.
    internal sealed class FlatRotationRenderEffect : MonoBehaviour,
        IDisposable
    {
        private BlurGamma sourceBlur;
        private Camera targetCamera;
        private Material rotationMaterial;
        private float angleDegrees;
        private float rotationProgress;
        private float horizontalMirrorScale = 1f;
        private bool disposed;

        internal static bool TryCreate(
            BlurGamma blur, Shader shader,
            out FlatRotationRenderEffect effect,
            out string error)
        {
            effect = null;
            error = null;
            if (blur == null)
            {
                error = "BlurGamma ya no está disponible.";
                return false;
            }
            if (shader == null)
            {
                error = "el shader de imagen final no está cargado.";
                return false;
            }
            if (!shader.isSupported)
            {
                error = "la GPU no admite el shader de imagen final.";
                return false;
            }
            try
            {
                var camera = blur.GetComponent<Camera>();
                if (camera == null)
                {
                    error = "la cámara de combate no está disponible.";
                    return false;
                }

                effect = camera.gameObject.AddComponent<
                    FlatRotationRenderEffect>();
                effect.sourceBlur = blur;
                effect.targetCamera = camera;
                effect.rotationMaterial = new Material(shader);
                effect.rotationMaterial.name =
                    "Gilomx flat 180 rotation";
                effect.rotationMaterial.hideFlags =
                    HideFlags.HideAndDontSave;
                effect.rotationMaterial.SetFloat("_Saturation", 1f);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (effect != null)
                    effect.Dispose();
                effect = null;
                return false;
            }
        }

        internal bool IsValid
        {
            get
            {
                return !disposed && sourceBlur != null &&
                       targetCamera != null && rotationMaterial != null;
            }
        }

        internal bool Matches(BlurGamma blur)
        {
            return !disposed && sourceBlur == blur;
        }

        internal void SetAngle(float value)
        {
            angleDegrees = Mathf.Clamp(value, 0f, 180f);
            rotationProgress = angleDegrees / 180f;
            // Keep the known-good single opaque pass. A transparent two-pass
            // mirror crossfade blacked out Cuphead's final render target on
            // this Unity build and is intentionally not used.
            horizontalMirrorScale = rotationProgress < 0.5f ? 1f : -1f;
        }

        private void OnRenderImage(
            RenderTexture source, RenderTexture destination)
        {
            if (disposed || rotationMaterial == null || source == null ||
                angleDegrees <= 0.001f)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = destination;
                GL.Clear(false, true, Color.black);

                var aspect = source.height <= 0
                    ? 1f
                    : (float)source.width / source.height;
                var radians = angleDegrees * Mathf.Deg2Rad;
                var cosine = Mathf.Cos(radians);
                var sine = Mathf.Sin(radians);
                var previousWrapMode = source.wrapMode;
                source.wrapMode = TextureWrapMode.Clamp;

                GL.PushMatrix();
                GL.LoadOrtho();
                rotationMaterial.SetTexture("_MainTex", source);
                rotationMaterial.SetFloat("_Saturation", 1f);
                rotationMaterial.SetFloat("_FlipY", 0f);
                if (!rotationMaterial.SetPass(0))
                {
                    GL.PopMatrix();
                    source.wrapMode = previousWrapMode;
                    Graphics.Blit(source, destination);
                    return;
                }
                DrawQuad(
                    aspect, cosine, sine, horizontalMirrorScale);
                GL.PopMatrix();
                source.wrapMode = previousWrapMode;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static void DrawQuad(
            float aspect, float cosine, float sine, float mirrorScaleX)
        {
            // Draw a full-screen destination quad and rotate its UVs in the
            // opposite direction. Samples that land outside the captured
            // frame use Clamp, extending only its edge pixels into the empty
            // corners. The complete central frame stays at 1:1 scale: no zoom
            // and no black wedges.
            GL.Begin(GL.QUADS);
            DrawVertex(
                0f, 0f, aspect, cosine, sine, mirrorScaleX);
            DrawVertex(
                1f, 0f, aspect, cosine, sine, mirrorScaleX);
            DrawVertex(
                1f, 1f, aspect, cosine, sine, mirrorScaleX);
            DrawVertex(
                0f, 1f, aspect, cosine, sine, mirrorScaleX);
            GL.End();
        }

        private static void DrawVertex(
            float x, float y, float aspect, float cosine, float sine,
            float mirrorScaleX)
        {
            var destinationX = (x - 0.5f) * aspect;
            var destinationY = y - 0.5f;
            var sourceX = (destinationX * cosine +
                           destinationY * sine) * mirrorScaleX;
            var sourceY = -destinationX * sine +
                          destinationY * cosine;
            var sourceU = sourceX /
                          Mathf.Max(0.001f, aspect) + 0.5f;
            var sourceV = sourceY + 0.5f;

            GL.TexCoord2(sourceU, sourceV);
            GL.Vertex3(x, y, 0f);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            enabled = false;
            if (rotationMaterial != null)
            {
                UnityEngine.Object.Destroy(rotationMaterial);
                rotationMaterial = null;
            }
            UnityEngine.Object.Destroy(this);
        }

        private void OnDestroy()
        {
            disposed = true;
            if (rotationMaterial != null)
            {
                UnityEngine.Object.Destroy(rotationMaterial);
                rotationMaterial = null;
            }
        }
    }
}
