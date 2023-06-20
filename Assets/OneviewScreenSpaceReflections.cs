using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.VisualScripting.Member;

public class OneviewScreenSpaceReflections : ScriptableRendererFeature
{
    private class OneviewScreenSpaceReflectionsPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier source;
        private RenderTargetHandle destination;
        private Material ssrMaterial;
        private Material pixelWorldPosMaterial;
        private Material blurMaterial;
        private Material combineMaterial;
        private int downSample;
        private int blurAmount;
        private bool showReflectionsOnly;

        public OneviewScreenSpaceReflectionsPass(Material ssrMaterial, Material pixelWorldPosMaterial, Material blurMaterial, Material combineMaterial, int downSample, int blurAmount, bool showReflectionsOnly)
        {
            this.ssrMaterial = ssrMaterial;
            this.pixelWorldPosMaterial = pixelWorldPosMaterial;
            this.blurMaterial = blurMaterial;
            this.combineMaterial = combineMaterial;
            this.downSample = downSample;
            this.blurAmount = blurAmount;
            this.showReflectionsOnly = showReflectionsOnly;
        }

        public void Setup(RenderTargetIdentifier source)
        {
            //this.source = source;
            //this.destination = destination;
            Debug.Log("순서1");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            Debug.Log("순서2");
            destination = RenderTargetHandle.CameraTarget;
            cmd.GetTemporaryRT(destination.id, cameraTextureDescriptor, FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("SSR");

            Camera camera = renderingData.cameraData.camera;
            this.source = renderingData.cameraData.renderer.cameraColorTarget;
            RenderTextureFormat format = camera.allowHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;

            // Create temporary buffers
            int screenWidth = camera.pixelWidth;
            int screenHeight = camera.pixelHeight;
            int downSampledWidth = screenWidth / downSample;
            int downSampledHeight = screenHeight / downSample;

            RenderTexture tempWorldPos = RenderTexture.GetTemporary(screenWidth, screenHeight, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture tempA = RenderTexture.GetTemporary(downSampledWidth, downSampledHeight, 0, format);
            RenderTexture tempB = RenderTexture.GetTemporary(downSampledWidth, downSampledHeight, 0, format);

            // Calculate per-pixel world position
            pixelWorldPosMaterial.SetMatrix("_CAMERA_XYZMATRIX", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse);
            cmd.Blit(null, tempWorldPos, pixelWorldPosMaterial);

            // Set textures and matrices for SSR
            ssrMaterial.SetTexture("_WPOS", tempWorldPos);
            Shader.SetGlobalMatrix("_View2Screen", (camera.cameraToWorldMatrix * camera.projectionMatrix.inverse).inverse);
            ssrMaterial.SetInt("_Downscale", downSample);

            // Raytrace
            cmd.Blit(source, tempA, ssrMaterial);

            // Blur SSR result
            for (int i = 0; i < blurAmount; ++i)
            {
                blurMaterial.SetVector("_Direction", new Vector4(1, 0, 0, 0));
                cmd.Blit(tempA, tempB, blurMaterial);
                blurMaterial.SetVector("_Direction", new Vector4(0, 1, 0, 0));
                cmd.Blit(tempB, tempA, blurMaterial);
            }

            // Set SSR texture for combining
            combineMaterial.SetTexture("_SSR", tempA);

            // Show reflections or perform final combine
            if (showReflectionsOnly)
            {
                cmd.Blit(tempA, destination.Identifier());
            }
            else
            {
                cmd.Blit(source, destination.Identifier(), combineMaterial);
            }

            // Release temporary buffers
            RenderTexture.ReleaseTemporary(tempA);
            RenderTexture.ReleaseTemporary(tempB);
            RenderTexture.ReleaseTemporary(tempWorldPos);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(destination.id);
        }
    }

    public Material ssrMaterial;
    public Material pixelWorldPosMaterial;
    public Material blurMaterial;
    public Material combineMaterial;
    public int downSample;
    public int blurAmount;
    public bool showReflectionsOnly;

    private OneviewScreenSpaceReflectionsPass ssrPass;

    public override void Create()
    {
        ssrPass = new OneviewScreenSpaceReflectionsPass(ssrMaterial, pixelWorldPosMaterial, blurMaterial, combineMaterial, downSample, blurAmount, showReflectionsOnly);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //ssrPass.Setup();
        renderer.EnqueuePass(ssrPass);
    }
}
