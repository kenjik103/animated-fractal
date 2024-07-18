using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

public class Fractal : MonoBehaviour
{
    [SerializeField, Range(1,8)]  int depth = 4;
    [SerializeField] Mesh mesh;
    [SerializeField] Material material;

    struct UpdateFractalLevelJob : IJobFor
    {
        public float spinAngleDelta;
        public float scale;

        public NativeArray<FractalPart> parts;
        
        [ReadOnly]
        public NativeArray<FractalPart> parents;
        [WriteOnly]
        public NativeArray<Matrix4x4> matrices;
        public void Execute(int i) {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];
            part.spinAngle += spinAngleDelta;
            part.worldRotation = parent.worldRotation * part.rotation * Quaternion.Euler(0f, part.spinAngle, 0f);
            part.worldPosition = 
                parent.worldPosition + 
                parent.worldRotation * 
                (1.5f * scale * part.direction);
            parts[i] = part;
            matrices[i] = Matrix4x4.TRS(part.worldPosition, part.worldRotation, Vector3.one * scale);
        }
    }

    readonly Vector3[] directions = { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back};
    Quaternion[] rotations = { 
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)
    };

    struct FractalPart
    {
        public Vector3 direction, worldPosition;
        public Quaternion rotation, worldRotation;
        public float spinAngle;
    }
    
    NativeArray<FractalPart>[] parts; //parts seperated by layer
    NativeArray<Matrix4x4>[] matrices;

    ComputeBuffer[] matricesBuffer;

    static readonly int matriciesId = Shader.PropertyToID("_Matrices");

    static MaterialPropertyBlock propertyBlock;

    void OnEnable() {
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<Matrix4x4>[depth];
        matricesBuffer = new ComputeBuffer[depth];
        int stride = 16 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<Matrix4x4>(length, Allocator.Persistent);
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
        float spinDelta = 22.5f * Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spinAngle += spinDelta;
        rootPart.worldRotation = transform.rotation * (rootPart.rotation * Quaternion.Euler(0f, rootPart.spinAngle, 0f));
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        matrices[0][0] = Matrix4x4.TRS(rootPart.worldPosition, rootPart.worldRotation, objectScale* Vector3.one);
        
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