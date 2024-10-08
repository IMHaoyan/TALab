using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class myRayMarching : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        public Material passMat = null;
        public FilterMode passfiltMode { get; set; }
        RenderTargetIdentifier passSource { get; set; }//源图像，目标图像
        int passTempTexID, passTempTexID1;//临时计算图像
                                          //RenderTargetIdentifier、RenderTargetHandle都可以理解为RT
                                          //Identifier为camera提供的需要被应用的texture，Handle为被shader处理渲染过的RT

        Matrix4x4 frustumCorners;

        GameObject passGo;
        ProfilingSampler passProfilingSampler = new ProfilingSampler("myRaymarchingProfiling");
        public CustomRenderPass(RenderPassEvent passEvent, Material material, GameObject go)
        {
            this.renderPassEvent = passEvent;
            this.passMat = material;
            this.passGo = go;
        }

        public void setup(RenderTargetIdentifier source)
        {
            this.passSource = source;
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {   //类似于OnRenderImage
            if (passMat == null)
            {
                Debug.LogError("材质球丢失！请设置材质球");
                return;
            }
            CommandBuffer cmd = CommandBufferPool.Get("my cloud");
            using (new ProfilingScope(cmd, passProfilingSampler))
            {
                Camera camera = renderingData.cameraData.camera;
                Matrix4x4 currentViewProjectionMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
                Matrix4x4 currentViewProjectionInverseMatrix = currentViewProjectionMatrix.inverse;
                passMat.SetMatrix("_CurrentViewProjectionInverseMatrix", currentViewProjectionInverseMatrix);

                Transform cameraTransform = camera.transform;
                float fov = camera.fieldOfView;
                float near = camera.nearClipPlane;
                float far = camera.farClipPlane;
                float aspect = camera.aspect;

                float halfHeight = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                Vector3 toRight = cameraTransform.right * halfHeight * aspect;
                Vector3 toTop = cameraTransform.up * halfHeight;
                Vector3 topLeft = cameraTransform.forward * near + toTop - toRight;
                float scale = topLeft.magnitude / near;
                topLeft.Normalize();
                topLeft *= scale;
                Vector3 topRight = cameraTransform.forward * near + toRight + toTop;
                topRight.Normalize();
                topRight *= scale;
                Vector3 bottomLeft = cameraTransform.forward * near - toTop - toRight;
                bottomLeft.Normalize();
                bottomLeft *= scale;
                Vector3 bottomRight = cameraTransform.forward * near + toRight - toTop;
                bottomRight.Normalize();
                bottomRight *= scale;

                frustumCorners.SetRow(0, bottomLeft);
                frustumCorners.SetRow(1, bottomRight);
                frustumCorners.SetRow(2, topRight);
                frustumCorners.SetRow(3, topLeft);
                passMat.SetMatrix("_FrustumCornersRay", frustumCorners);
                passMat.SetFloat("_ZFar", camera.farClipPlane);

                Vector4 position = new Vector4(0, 2, 0, 0);
                float radius = 2f;
                if (passGo != null)
                {
                    position = passGo.GetComponent<Transform>().position;
                    radius = passGo.GetComponent<Transform>().lossyScale.x / 2.0f;
                }
                passMat.SetVector("_position", position);
                passMat.SetFloat("_radius", radius);
                RenderTextureDescriptor CameraTexDesc = renderingData.cameraData.cameraTargetDescriptor;
                //CameraTexDesc.depthBufferBits = 0;
                CameraTexDesc.msaaSamples = 1;
                //cmd.GetTemporaryRT(passTempTexID, CameraTexDesc, passfiltMode);//申请一个临时图像
                int _Scale = 1;
                //depth???
                cmd.GetTemporaryRT(passTempTexID, CameraTexDesc.width / _Scale, CameraTexDesc.height / _Scale, 0, filter: FilterMode.Bilinear);
                cmd.GetTemporaryRT(passTempTexID1, CameraTexDesc.width / _Scale, CameraTexDesc.height / _Scale, 0, filter: FilterMode.Bilinear);
                
                passSource = renderingData.cameraData.renderer.cameraColorTarget;
                cmd.Blit(passSource, passTempTexID);
                cmd.Blit(passTempTexID, passTempTexID1, passMat);
                cmd.Blit(passTempTexID1, passSource);
            }
            context.ExecuteCommandBuffer(cmd);//执行命令
            CommandBufferPool.Release(cmd);//释放回收
        }
        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
            cmd.ReleaseTemporaryRT(passTempTexID);
        }
    }

    [System.Serializable]
    public class mySetting
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material myMat;
    }

    public mySetting setting = new mySetting();
    CustomRenderPass myPass;
    GameObject GO;
    public override void Create()
    {//进行初始化,这里最先开始

        GO = GameObject.Find("Sphere");
        if (GO == null)
        {
            //Debug.Log("No Sphere!");
        }
        myPass = new CustomRenderPass(setting.passEvent, setting.myMat, GO);//实例化一下并传参数,name就是tag
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

        if (setting.myMat == null)
        {
            Debug.LogError("材质球丢失！请设置材质球");
            return;
        }
        renderer.EnqueuePass(myPass);
    }
    // public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    // {
    //     myPass.setup(renderer.cameraColorTarget);
    // }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

}
