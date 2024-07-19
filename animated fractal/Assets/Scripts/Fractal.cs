using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

using static Unity.Mathematics.math;
using float4x4 = Unity.Mathematics.float4x4;
using quaternion = Unity.Mathematics.quaternion;


public class Fractal : MonoBehaviour
{
    [SerializeField, Range(1, 8)] int depth = 4;
    [SerializeField] Mesh mesh;
    [SerializeField] Material material;
    
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor
    {
        public float spinAngleDelta;
        public float scale;

        public NativeArray<FractalPart> parts;
        
        [ReadOnly]
        public NativeArray<FractalPart> parents;
        [WriteOnly]
        public NativeArray<float4x4> matrices;
        public void Execute(int i) {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];
            part.spinAngle += spinAngleDelta;
            part.worldRotation = mul(parent.worldRotation , mul(part.rotation, quaternion.RotateY(part.spinAngle)));
            part.worldPosition = 
                parent.worldPosition + 
                mul(parent.worldRotation , 
                (1.5f * scale * part.direction));
            parts[i] = part;
            matrices[i] = float4x4.TRS(part.worldPosition, part.worldRotation, float3(scale));
        }
    }

    static float3[] directions = {
        up(), right(), left(), forward(), back()
    };
    
    static quaternion[] rotations = { 
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };
    
    struct FractalPart
    {
        public float3 direction, worldPosition;
        public quaternion rotation, worldRotation;
        public float spinAngle;
    }
    
    NativeArray<FractalPart>[] parts; //parts seperated by layer
    NativeArray<float4x4>[] matrices;

    ComputeBuffer[] matricesBuffer;

    static readonly int matriciesId = Shader.PropertyToID("_Matrices");

    static MaterialPropertyBlock propertyBlock;

    void OnEnable() {
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float4x4>[depth];
        matricesBuffer = new ComputeBuffer[depth];
        int stride = 16 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float4x4>(length, Allocator.Persistent);
            matricesBuffer[i] = new ComputeBuffer(length, stride);
        }

        parts[0][0] = CreatePart(0);
        for (int li = 1; li < parts.Length; li++) {
            NativeArray<FractalPart> levelPart = parts[li];
            for (int fpi = 0; fpi < levelPart.Length; fpi+=5) {
                for (int ci = 0; ci < 5; ci++) {
                    levelPart[fpi + ci] = CreatePart(ci);
                }
            }
        }

        propertyBlock ??= new MaterialPropertyBlock();
    }

    void OnDisable() {
        for (int i = 0; i < matricesBuffer.Length; i++) {
            matricesBuffer[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }
        matrices = null;
        parts = null;
        matricesBuffer = null;
    }

    FractalPart CreatePart(int childIndex) => new FractalPart {
        direction = directions[childIndex],
        rotation = rotations[childIndex],
    };
    
    void OnValidate() {
        if (parts != null && enabled) {
            OnEnable();
            OnDisable();
        }
    }

    void Update() {
        float spinDelta = 0.125f * PI * Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spinAngle += spinDelta;
        rootPart.worldRotation = mul( transform.rotation , mul(rootPart.rotation , quaternion.RotateY(rootPart.spinAngle)));
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        matrices[0][0] = float4x4.TRS(rootPart.worldPosition, rootPart.worldRotation, float3(objectScale));
        
        float scale = objectScale;
        JobHandle handle = default;
        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            handle = new UpdateFractalLevelJob
            {
                spinAngleDelta = spinDelta,
                scale = scale,
                parts = parts[li],
                parents = parts[li-1],
                matrices = matrices[li]
            }.Schedule(parts[li].Length, handle);
        }
        handle.Complete();

        Bounds bounds = new Bounds(Vector3.zero, 3f * Vector3.one);
        for (int i = 0; i < matricesBuffer.Length; i++) {
            ComputeBuffer buffer = matricesBuffer[i];
            buffer.SetData(matrices[i]);
            propertyBlock.SetBuffer(matriciesId, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }
}