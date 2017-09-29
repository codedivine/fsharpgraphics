(* 
Copyright 2017 Rahul Garg
This file is part of fsharpgraphics project. 
It is subject to the license terms in the LICENSE file found in the top-level directory of the project. 
No part of fsharpgraphics project, including this file, may be copied, modified, propagated, or distributed except according to the terms contained in the LICENSE file.
*)

open SharpDX
open SharpDX.Direct3D
open SharpDX.Direct3D11
open SharpDX.DXGI
open SharpDX.Windows
open SharpDX.D3DCompiler
open SharpDX.Mathematics.Interop
open System

let createSwapChain factory device handle = 
    let mutable swapChainDesc = SwapChainDescription()
    swapChainDesc.BufferCount <- 1
    swapChainDesc.ModeDescription <- ModeDescription(600,600,Rational(60,1),Format.R8G8B8A8_UNorm)
    swapChainDesc.IsWindowed <- RawBool(true)
    swapChainDesc.SampleDescription <- SampleDescription(1,0)
    swapChainDesc.SwapEffect <- SwapEffect.Discard
    swapChainDesc.OutputHandle <- handle
    swapChainDesc.Usage <- Usage.RenderTargetOutput
    let swapChain = new SwapChain(factory,device,swapChainDesc)
    swapChain

let getRenderView device (swapChain:SwapChain) = 
    use backBuffer = swapChain.GetBackBuffer<Texture2D>(0)
    let renderView = new RenderTargetView(device,backBuffer)
    renderView

let createShader (shader:string) (profile:string) (entryPoint:string) =
    let result = ShaderBytecode.Compile(shader,entryPoint,profile)
    if result.HasErrors then
        printfn "%s" result.Message
    result.Bytecode

type MyRender(vshaderBC: ShaderBytecode, fshaderBC: ShaderBytecode) =
    let renderForm = new RenderForm()
    let mutable factory = new DXGI.Factory1()
    let adapter = factory.GetAdapter1(0)
    let device = new Direct3D11.Device(adapter,DeviceCreationFlags.Debug)
    let swapChain = createSwapChain factory device renderForm.Handle
    let deviceCon = device.ImmediateContext
    let renderView = getRenderView device swapChain
    let color = RawColor4(0.0f, 0.0f, 0.0f, 0.0f)
    let vertexData =  [| 0.0f; 0.5f; 0.5f; 0.5f; -0.5f; 0.5f; -0.5f; -0.5f; 0.5f |]
    let vertexBuf = Direct3D11.Buffer.Create<float32>(device,Direct3D11.BindFlags.VertexBuffer,vertexData)
    let vshader = new Direct3D11.VertexShader(device,vshaderBC.Data)
    let fshader = new Direct3D11.PixelShader(device,fshaderBC.Data)
    let vbufBinding = VertexBufferBinding(vertexBuf,12,0)
    let elements = [| InputElement("position",0,DXGI.Format.R32G32B32_Float,0) |]
    let inputLayout = new Direct3D11.InputLayout(device,vshaderBC.Data,elements)
    let drawScene () = 
        deviceCon.ClearRenderTargetView(renderView,color)
        deviceCon.InputAssembler.InputLayout <- inputLayout
        deviceCon.InputAssembler.SetVertexBuffers(0,vbufBinding)
        deviceCon.InputAssembler.PrimitiveTopology <- PrimitiveTopology.TriangleList
        deviceCon.Rasterizer.SetViewport(0.0f,0.0f,float32(renderForm.Height),float32(renderForm.Width))
        deviceCon.VertexShader.Set(vshader)
        deviceCon.PixelShader.Set(fshader)
        deviceCon.OutputMerger.SetTargets(renderView)
        deviceCon.Draw(3,0)
        swapChain.Present(0,PresentFlags.None) |> ignore
    member this.Form = renderForm
    member this.RenderCallback = drawScene
    //member this.RenderCallback = new RenderLoop.RenderCallback(drawScene)
    interface IDisposable with 
        member this.Dispose() =
            vertexBuf.Dispose()
            vshader.Dispose()
            fshader.Dispose()
            swapChain.Dispose()
            device.Dispose()
            adapter.Dispose()
            factory.Dispose()
            renderForm.Dispose()

[<EntryPoint>]
let main argv =
    let vshaderText = "void VS(float3 pos:POSITION,  out float4 opos: SV_POSITION){\n 
      opos = float4(pos,1.0);\n
    }"
    let fshaderText = "float4 PS(float4 opos: SV_POSITION): SV_TARGET{\n
     return float4(0.0,1,0,1);
    }"
    use vshaderBC = createShader vshaderText "vs_5_0" "VS"
    use fshaderBC = createShader fshaderText "ps_5_0" "PS"
    use renderer = new MyRender(vshaderBC,fshaderBC)
    RenderLoop.Run(renderer.Form,renderer.RenderCallback)
    0

