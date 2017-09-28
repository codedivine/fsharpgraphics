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
    let modeDesc = ModeDescription(600,600,Rational(60,1),Format.R8G8B8A8_UNorm)
    let mutable swapChainDesc = SwapChainDescription(BufferCount = 1,ModeDescription = modeDesc, IsWindowed = RawBool(true))
    swapChainDesc.SampleDescription <- SampleDescription(1,0)
    swapChainDesc.OutputHandle <- handle
    swapChainDesc.Usage <- Usage.RenderTargetOutput
    let swapChain = new SwapChain(factory,device,swapChainDesc)
    swapChain

let getRenderView device (swapChain:SwapChain) = 
    use backBuffer = swapChain.GetBackBuffer<Texture2D>(0)
    let renderView = new RenderTargetView(device,backBuffer)
    renderView

let createVertexBuf (device:Direct3D11.Device) =
    let data = [| 0.0f; 0.0f; 0.0f; 100.f; 0.0f; 0.0f; 0.0f; 100.0f; 0.0f |]
    let mutable bufferDesc = Direct3D11.BufferDescription()
    bufferDesc.BindFlags <- Direct3D11.BindFlags.VertexBuffer
    bufferDesc.CpuAccessFlags <- Direct3D11.CpuAccessFlags.None
    bufferDesc.OptionFlags <-  Direct3D11.ResourceOptionFlags.None
    bufferDesc.SizeInBytes <- data.Length * sizeof<float32>
    let buffer = Direct3D11.Buffer.Create<float32>(device,data,bufferDesc)
    buffer

let createVertexSRV (device:Direct3D11.Device) vertexBuf = 
    let mutable vdesc: Direct3D11.ShaderResourceViewDescription = ShaderResourceViewDescription()
    vdesc.Dimension <- ShaderResourceViewDimension.Buffer
    vdesc.Format <- DXGI.Format.R32_Float
    let srv = new ShaderResourceView(device,vertexBuf,vdesc)
    srv

let createShader (shader:string) (profile:string) =
    let result = ShaderBytecode.Compile(shader,profile,ShaderFlags.None,EffectFlags.None)
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
    let color = RawColor4(1.0f, 0.0f, 0.0f, 0.0f)
    let vertexBuf = createVertexBuf device
    let vshader = new Direct3D11.VertexShader(device,vshaderBC.Data)
    let fshader = new Direct3D11.PixelShader(device,fshaderBC.Data)
    let vshaderView = createVertexSRV device vertexBuf
    let elements = Array.create 10 (Direct3D11.InputElement())
    let inputLayout = new Direct3D11.InputLayout(device,vshaderBC.Data,elements)
    let drawScene () = 
        deviceCon.ClearRenderTargetView(renderView,color)
        deviceCon.InputAssembler.InputLayout <- inputLayout
        deviceCon.VertexShader.Set(vshader)
        deviceCon.PixelShader.Set(fshader)
        deviceCon.VertexShader.SetShaderResource(0,vshaderView)
        deviceCon.Draw(3,0)
        swapChain.Present(1,PresentFlags.None) |> ignore
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
    //currently reading shader from file, will be replaced with hardcoded shader in this file
    let vshaderFname = argv.[0]
    let fshaderFname = argv.[1]
    let vshaderText = System.IO.File.ReadAllText(vshaderFname)
    let fshaderText = System.IO.File.ReadAllText(fshaderFname)
    use vshaderBC = createShader vshaderText "vs_5_0"
    use fshaderBC = createShader fshaderText "ps_5_0"
    use renderer = new MyRender(vshaderBC,fshaderBC)
    RenderLoop.Run(renderer.Form,renderer.RenderCallback)
    0

