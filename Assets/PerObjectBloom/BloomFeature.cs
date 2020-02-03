using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomFeature : ScriptableRendererFeature
{
    class BloomPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier source { get; set; }
        private RenderTargetHandle destination { get; set; }
        CommandBufferBlur m_Blur;
        int m_BlurTemp1;
        int m_BlurTemp2;
        Material m_MaskedBrightnessBlit;
        Material m_AdditiveBlit;
        BloomSettings settings;
        RenderTargetHandle temporaryColorTexture;

        public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination)
        {
            this.source = source;
            this.destination = destination;
            m_Blur = new CommandBufferBlur();

            m_BlurTemp1 = Shader.PropertyToID("_Temp1");
            m_BlurTemp2 = Shader.PropertyToID("_Temp2");
        }

        public BloomPass(Material m_Masked, Material m_Additive, BloomSettings bs)
        {
            this.m_MaskedBrightnessBlit = m_Masked;
            this.m_AdditiveBlit = m_Additive;
            this.settings = bs;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in an performance manner.
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {

        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Bloom Pass");

            RenderTextureDescriptor opaqueDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDescriptor.depthBufferBits = 0;
            opaqueDescriptor.height = opaqueDescriptor.height / 2;
            opaqueDescriptor.width = opaqueDescriptor.width / 2;

            // Create our temp working buffers, work at quarter size
            cmd.GetTemporaryRT(m_BlurTemp1, opaqueDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_BlurTemp2, opaqueDescriptor, FilterMode.Bilinear);

            // Copy all values about our brightness and inside our mask to a temp buffer
            m_MaskedBrightnessBlit.SetFloat("_BloomThreshold", settings.bloomThreshold);
            Blit(cmd, source, m_BlurTemp1, m_MaskedBrightnessBlit);

            // Setup command for blurring the buffer
            m_Blur.SetupCommandBuffer(cmd, m_BlurTemp1, m_BlurTemp2);

            // Blit the blurred brightness back into the color buffer, optionally increasing the brightness
            m_AdditiveBlit.SetFloat("_AdditiveAmount", settings.bloomAmount);
            Blit(cmd, m_BlurTemp1, source, m_AdditiveBlit);

            // Blit to the destination buffer
            //BlitFullscreenTriangle(context.source, context.destination);
            //Blit(cmd, source, destination.id);
            /*if (destination == RenderTargetHandle.CameraTarget)
            {
                cmd.GetTemporaryRT(temporaryColorTexture.id, opaqueDescriptor, FilterMode.Point);
                Blit(cmd, source, temporaryColorTexture.Identifier(), m_MaskedBrightnessBlit, 0);
                Blit(cmd, temporaryColorTexture.Identifier(), source);

            }
            else Blit(cmd, source, destination.Identifier(), m_MaskedBrightnessBlit, 0);*/


            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {

            if (destination == RenderTargetHandle.CameraTarget)
                cmd.ReleaseTemporaryRT(temporaryColorTexture.id);
        }
    }

    [System.Serializable]
    public class BloomSettings
    {
        [Range(0f, 1f), Tooltip("Bloom Threshold")]
        public float bloomThreshold = 0.5f;

        [Range(0f, 5f), Tooltip("Bloom Amount")]
        public float bloomAmount = 1.1f;
    }

    public BloomSettings settings = new BloomSettings();
    Material m_MaskedBrightnessBlit;
    Material m_AdditiveBlit;
    BloomPass bloomPass;
    RenderTargetHandle bloomTexture;

    public override void Create()
    {
        bloomPass = new BloomPass(m_MaskedBrightnessBlit, m_AdditiveBlit, settings);

        m_MaskedBrightnessBlit = CoreUtils.CreateEngineMaterial("Hidden/MaskedBrightnessBlit");
        m_AdditiveBlit = CoreUtils.CreateEngineMaterial("Hidden/AdditiveBlit");
        bloomPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        bloomPass.Setup(renderer.cameraColorTarget, RenderTargetHandle.CameraTarget);
        renderer.EnqueuePass(bloomPass);
    }
}


