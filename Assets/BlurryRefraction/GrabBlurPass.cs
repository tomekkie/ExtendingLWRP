using System;
using UnityEngine;
using UnityEngine.Rendering;



public class GrabBlurPass : UnityEngine.Rendering.Universal.ScriptableRendererFeature
{
    private GrabBlurPassImpl m_grabBlurPass;

    public override void AddRenderPasses(UnityEngine.Rendering.Universal.ScriptableRenderer renderer, ref UnityEngine.Rendering.Universal.RenderingData renderingData)
    {
        m_grabBlurPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(m_grabBlurPass);
    }

    public override void Create()
    {
        m_grabBlurPass = new GrabBlurPassImpl(UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget);
        m_grabBlurPass.renderPassEvent = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingOpaques;
    }
}


public class GrabBlurPassImpl : UnityEngine.Rendering.Universal.ScriptableRenderPass
{
    const string k_RenderGrabPassTag = "BlurredGrabPass";

    public Shader m_BlurShader;
    public RenderTargetIdentifier m_ColorSource;
   
    UnityEngine.Rendering.Universal.RenderTargetHandle m_BlurTemp1;
    UnityEngine.Rendering.Universal.RenderTargetHandle m_BlurTemp2;
    UnityEngine.Rendering.Universal.RenderTargetHandle m_ScreenCopyId;
    CommandBufferBlur m_Blur;

    public GrabBlurPassImpl(UnityEngine.Rendering.Universal.RenderTargetHandle colorHandle)
    {
        m_Blur = new CommandBufferBlur();
        m_BlurTemp1.Init("_Temp1");
        m_BlurTemp2 .Init("_Temp2");
        m_ScreenCopyId.Init("_ScreenCopyTexture");
    }

    public void Setup(RenderTargetIdentifier colorSource)
    {
        m_ColorSource = colorSource;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // get two smaller RTs
        RenderTextureDescriptor opaqueDesc = cameraTextureDescriptor;
        opaqueDesc.width /= 2;
        opaqueDesc.height /= 2;
        cmd.GetTemporaryRT(m_ScreenCopyId.id, opaqueDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(m_BlurTemp1.id, opaqueDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(m_BlurTemp2.id, opaqueDesc, FilterMode.Bilinear);

        //ConfigureTarget(m_ColorHandle.Identifier());
    }

    public override void Execute(ScriptableRenderContext context, ref UnityEngine.Rendering.Universal.RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(k_RenderGrabPassTag);

        using (new ProfilingSample(cmd, k_RenderGrabPassTag))
        {
            // copy screen into temporary RT
            Blit(cmd, m_ColorSource, m_ScreenCopyId.Identifier());

            // downsample screen copy into smaller RTs
            Blit(cmd, m_ScreenCopyId.Identifier(), m_BlurTemp1.Identifier());

            // Setup blur commands
            m_Blur.SetupCommandBuffer(cmd, m_BlurTemp1.id, m_BlurTemp2.id);

            // Set texture id so we can use it later
            cmd.SetGlobalTexture("_GrabBlurTexture", m_BlurTemp1.id);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");

        cmd.ReleaseTemporaryRT(m_BlurTemp1.id);
        cmd.ReleaseTemporaryRT(m_BlurTemp2.id);
        cmd.ReleaseTemporaryRT(m_ScreenCopyId.id);
    }
}
