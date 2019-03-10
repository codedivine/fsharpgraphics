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
open SharpDX.Desktop
open SharpDX.Windows
open System.Drawing
open SharpDX.Mathematics.Interop
open System
open SharpDX.Direct3D

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
    member this.GetView() = vertexBufferView
    interface IDisposable with
        member this.Dispose() =
            vertexBuffer.Dispose()

type DeviceWithAdapterIndex = {device:Direct3D12.Device ;  adapterIndex:int}

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

let createRtvHeap (device:Direct3D12.Device) adapterIndex numBuffers = 
     let rtvHeapDesc = new DescriptorHeapDescription(
                            DescriptorCount = numBuffers,
                            Flags = DescriptorHeapFlags.None,
                            NodeMask = adapterIndex,
                            Type = DescriptorHeapType.RenderTargetView
                        )
     device.CreateDescriptorHeap(rtvHeapDesc)

let createRenderTargets (device:Direct3D12.Device) numBuffers (swapChain:SwapChain) (rtvHeap:DescriptorHeap) = 
    let rtvDescSize = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView)
    let rtvStart = rtvHeap.CPUDescriptorHandleForHeapStart
    let renderTargets = Array.init numBuffers (fun i -> 
            let target = swapChain.GetBackBuffer<SharpDX.Direct3D12.Resource>(i)
            let rtvHandle = rtvStart + i*rtvDescSize
            device.CreateRenderTargetView(target,System.Nullable(),rtvHandle)
            target
    )
    renderTargets
   
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
    use rtvHeap = createRtvHeap device adapterIndex numBuffers
    let renderTargets = createRenderTargets device numBuffers swapChain rtvHeap
    let rootSigDesc = new RootSignatureDescription(RootSignatureFlags.AllowInputAssemblerInputLayout)
    let rootSigBlob = rootSigDesc.Serialize()
    let dataPtr = Blob.op_Implicit(rootSigBlob)
    let rootSig = device.CreateRootSignature(dataPtr)
    form.Show()
    0 // return an integer exit code
