(* Copyright 2019 Rahul Garg 
This file is distributed as part of fsharpgraphics project under a BSD 3-clause license
License terms can be found in LICENSE file distributed with the project
*)

(*
This is a simple example of using Direct3D 12 from F#.  This is meant to show a simple triangle.
WIP.  Not finished and still buggy. 
*)

open SharpDX
open SharpDX.Direct3D
open SharpDX.Direct3D12
open SharpDX.DXGI
open SharpDX.Windows
open SharpDX.Mathematics.Interop
open SharpDX.D3DCompiler
open System

type FsgVertexResource(size:int,stride:int,device:Direct3D12.Device) =  
    let vertexBufferDesc = ResourceDescription.Buffer(int64 size)
    let vertexBuffer = device.CreateCommittedResource(
                        HeapProperties(HeapType.Upload),
                        HeapFlags.None,
                        vertexBufferDesc,
                        ResourceStates.GenericRead)
    let vertexBufferView = new VertexBufferView(
                            BufferLocation = vertexBuffer.GPUVirtualAddress,
                            StrideInBytes = stride,
                            SizeInBytes = size
                            )
    member this.SetData(data:float[]) = 
        let ptr = vertexBuffer.Map(0)
        Utilities.Write(ptr,data,0,data.Length) |> ignore
        vertexBuffer.Unmap(0)
    member this.View = vertexBufferView
    interface IDisposable with
        member this.Dispose() =
            vertexBuffer.Dispose()

 type FsgRenderResources(device:Direct3D12.Device, adapterIndex:int, numBuffers:int, swapChain:SwapChain) = 
    let rtvHeapDesc = new DescriptorHeapDescription(
                            DescriptorCount = numBuffers,
                            Flags = DescriptorHeapFlags.None,
                            NodeMask = adapterIndex,
                            Type = DescriptorHeapType.RenderTargetView
                        )
    let rtvHeap = device.CreateDescriptorHeap rtvHeapDesc
    let rtvDescSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView)
    let rtvStart = rtvHeap.CPUDescriptorHandleForHeapStart
    let renderTargets = Array.init numBuffers (fun i -> 
            let target = swapChain.GetBackBuffer<SharpDX.Direct3D12.Resource>(i)
            let rtvHandle = rtvStart + i*rtvDescSize
            device.CreateRenderTargetView(target,System.Nullable(),rtvHandle)
            target
    )
    interface IDisposable with 
        member this.Dispose() = 
            for target in renderTargets do
                target.Dispose()
            rtvHeap.Dispose()



let createSwapChain factory queue numBuffers mode isWindowed handle = 
    let mutable swapChainDesc = new SwapChainDescription()
    swapChainDesc.BufferCount <- numBuffers
    swapChainDesc.Usage <- Usage.RenderTargetOutput
    swapChainDesc.SampleDescription.Count <- 1 
    swapChainDesc.ModeDescription <- mode
    swapChainDesc.IsWindowed <- new RawBool(isWindowed)
    swapChainDesc.SwapEffect <- DXGI.SwapEffect.FlipDiscard
    swapChainDesc.OutputHandle <- handle
    new SwapChain(factory,queue,swapChainDesc)

let createPSO vertexShader pixelShader rootSig inputLayout (device:Direct3D12.Device) = 
    let mutable psoDesc = new GraphicsPipelineStateDescription()
    psoDesc.Flags <- PipelineStateFlags.None
    psoDesc.DepthStencilState <- new DepthStencilStateDescription(
                                    IsDepthEnabled = new RawBool(false),
                                    IsStencilEnabled = new RawBool(false)
                                    )
    psoDesc.RootSignature <- rootSig
    psoDesc.InputLayout <- inputLayout
    psoDesc.VertexShader <- vertexShader
    psoDesc.PixelShader <- pixelShader
    psoDesc.PrimitiveTopologyType <- PrimitiveTopologyType.Triangle
    psoDesc.SampleMask <- Int32.MaxValue
    psoDesc.SampleDescription <- new SharpDX.DXGI.SampleDescription(1,0)
    psoDesc.StreamOutput <- new StreamOutputDescription()
    psoDesc.BlendState <- BlendStateDescription.Default()
    psoDesc.RasterizerState <- RasterizerStateDescription.Default()
    psoDesc.RenderTargetCount <- 1
    psoDesc.RenderTargetFormats.[0] <- SharpDX.DXGI.Format.R8G8B8A8_UNorm
    device.CreateGraphicsPipelineState(psoDesc)


let createShaderFromFile fname entryPoint profile (flags:ShaderFlags) =  
    let byteCode:D3DCompiler.ShaderBytecode = D3DCompiler.ShaderBytecode.CompileFromFile(fname,entryPoint,profile,flags) 
                                            |> CompilationResult.op_Implicit
    let shaderD3D12 = new Direct3D12.ShaderBytecode(byteCode.Data)
    shaderD3D12

   
[<EntryPoint>]
let main argv =
    let form = new RenderForm("FirstTriangle")
    use factory = new Factory2(true)
    let adapterIndex = 0
    let numBuffers = 2
    use adapter = factory.GetAdapter1(adapterIndex)
    use device = new Direct3D12.Device(adapter,Direct3D.FeatureLevel.Level_11_0)
    let queue = device.CreateCommandQueue(CommandListType.Direct)
    let mode =  new ModeDescription(1024,768,new Rational(60,1),DXGI.Format.R8G8B8A8_UNorm)
    use swapChain = createSwapChain factory queue numBuffers mode true form.Handle
    use renderTargetResource = new FsgRenderResources(device,adapterIndex,numBuffers,swapChain)
    let rootSigDesc = new RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout)
    let rootSigDataPtr:DataPointer = rootSigDesc.Serialize() |> Blob.op_Implicit
    use rootSig = device.CreateRootSignature(rootSigDataPtr)

    //For each vertex, we store a 3D position and a color (RGBA)
    let inputElems = [| new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0);
                        new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)|]
    
    let shaderFlags = ShaderFlags.Debug
    let VS = createShaderFromFile "shaders.hlsl" "VSMain" "vs_5_0" shaderFlags
    let PS = createShaderFromFile "shaders.hlsl" "PSMain" "ps_5_0" shaderFlags
    let inputLayout = new InputLayoutDescription(inputElems)
    let pso = createPSO VS PS rootSig inputLayout device
    use fence = device.CreateFence((int64)0,FenceFlags.None)
    form.Show()
    0 // return an integer exit code
