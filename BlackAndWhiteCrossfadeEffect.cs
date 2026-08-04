using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gilomx.CupheadBossRoulette
{
    // Runs the shader compiled with Unity 2017.4.9f1 after Cuphead's own image
    // effects. Only the final visible frame is sampled, and only saturation is
    // changed, so no overlay can obscure the fight or survive a scene change.
    internal sealed class BlackAndWhiteSaturationEffect : IDisposable
    {
        private readonly BlurGamma sourceBlur;
        private readonly Camera camera;
        private readonly Material saturationMaterial;
        private readonly CommandBuffer commandBuffer;
        private readonly int visibleFrameId;
        private bool disposed;

        private BlackAndWhiteSaturationEffect(
            BlurGamma blur, Camera targetCamera, Shader shader)
        {
            sourceBlur = blur;
            camera = targetCamera;
            saturationMaterial = new Material(shader);
            saturationMaterial.name = "Gilomx smooth BW saturation";
            saturationMaterial.hideFlags = HideFlags.HideAndDontSave;
            saturationMaterial.SetFloat("_FlipY", 1f);
            var suffix = targetCamera.GetInstanceID().ToString();
            visibleFrameId = Shader.PropertyToID(
                "_GilomxBwVisible" + suffix);

            var cameraTarget = new RenderTargetIdentifier(
                BuiltinRenderTextureType.CameraTarget);
            var visibleFrame = new RenderTargetIdentifier(visibleFrameId);
            commandBuffer = new CommandBuffer();
            commandBuffer.name = "Gilomx bundled BW saturation";
            commandBuffer.GetTemporaryRT(
                visibleFrameId, -1, -1, 0, FilterMode.Bilinear);
            commandBuffer.Blit(cameraTarget, visibleFrame);
            commandBuffer.Blit(
                visibleFrame, cameraTarget, saturationMaterial, 0);
            commandBuffer.ReleaseTemporaryRT(visibleFrameId);
            targetCamera.AddCommandBuffer(
                CameraEvent.AfterImageEffects, commandBuffer);
        }

        internal static bool TryCreate(
            BlurGamma blur, Shader shader,
            out BlackAndWhiteSaturationEffect effect,
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
                error = "el shader compilado no está cargado.";
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

                if (!shader.isSupported)
                {
                    error = "la GPU no admite el shader de saturación.";
                    return false;
                }

                effect = new BlackAndWhiteSaturationEffect(
                    blur, camera, shader);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                effect = null;
                return false;
            }
        }

        internal bool IsValid
        {
            get
            {
                return !disposed && sourceBlur != null && camera != null &&
                       saturationMaterial != null;
            }
        }

        internal bool Matches(BlurGamma blur)
        {
            return !disposed && sourceBlur == blur;
        }

        internal void SetBlend(float blackAndWhiteBlend)
        {
            if (disposed || saturationMaterial == null)
                return;
            saturationMaterial.SetFloat(
                "_Saturation",
                1f - Mathf.Clamp01(blackAndWhiteBlend));
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            if (camera != null && commandBuffer != null)
                camera.RemoveCommandBuffer(
                    CameraEvent.AfterImageEffects, commandBuffer);
            if (commandBuffer != null)
                commandBuffer.Release();
            if (saturationMaterial != null)
                UnityEngine.Object.Destroy(saturationMaterial);
        }
    }
}
