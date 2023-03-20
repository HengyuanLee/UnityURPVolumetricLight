using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ApplyPass : ScriptableRenderPass
{
    private readonly string k_BlitAddTag = "VolumetricLight Blit Add RenderPass";

    public VolumtericResolution m_VolumtericResolution;
    public VolumetricLightFeature m_Feature;

    private Material m_ApplyMaterial;
    private CommandBuffer m_ApplyCommand;
    private RenderTargetHandle m_BlurTempTex;
    private RenderTargetHandle m_FullRayMarchTex;
    RenderTargetHandle m_TemporaryColorTexture;
    public ApplyPass()
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        m_ApplyMaterial = CoreUtils.CreateEngineMaterial("Hidden/Apply");
        int downSampleCout = m_VolumtericResolution == VolumtericResolution.Full ? 1 : m_VolumtericResolution == VolumtericResolution.Half ? 2 : 4;
        m_ApplyMaterial.SetInt("_DownSampleCount", downSampleCout);
        m_BlurTempTex.Init("BlurTempTex");
        m_FullRayMarchTex.Init("FullRayMarchTex");
        m_TemporaryColorTexture.Init("m_TemporaryColorTexture");
        m_TempColorTexHandle = RTHandles.Alloc("m_TempColorTexHandle");
    }
    ScriptableRenderer m_Renderer;
    public void Setup(ScriptableRenderer renderer)
    {
        m_Renderer = renderer;
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
        cmd.GetTemporaryRT(m_FullRayMarchTex.id, cameraTextureDescriptor, FilterMode.Bilinear);

        RenderTextureDescriptor descriptor = cameraTextureDescriptor;
        if (m_VolumtericResolution == VolumtericResolution.Half)
        {
            descriptor.width /= 2;
            descriptor.height /= 2;
        }
        else if (m_VolumtericResolution == VolumtericResolution.Quarter) {
            descriptor.width /= 4;
            descriptor.height /= 4;
        }
        cmd.GetTemporaryRT(m_BlurTempTex.id, descriptor, FilterMode.Bilinear);
        cmd.GetTemporaryRT(m_TemporaryColorTexture.id, descriptor, FilterMode.Bilinear);

    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_ApplyMaterial == null) return;
        m_ApplyCommand = CommandBufferPool.Get(k_BlitAddTag);
        context.ExecuteCommandBuffer(m_ApplyCommand);
        m_ApplyCommand.Clear();
        Render(m_ApplyCommand, ref renderingData);
        context.ExecuteCommandBuffer(m_ApplyCommand);
        CommandBufferPool.Release(m_ApplyCommand);
    }
    RTHandle m_TempColorTexHandle;
    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera) return;
        cmd.SetGlobalTexture("_SourceTex", m_Renderer.cameraColorTargetHandle);
        Blit(cmd, m_TempColorTexHandle, m_TemporaryColorTexture.Identifier(), m_ApplyMaterial);
        Blit(cmd, m_TemporaryColorTexture.Identifier(), m_Renderer.cameraColorTargetHandle);

    }
    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(m_BlurTempTex.id);
        cmd.ReleaseTemporaryRT(m_FullRayMarchTex.id);
        cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);
        m_TempColorTexHandle.Release();
    }
}
