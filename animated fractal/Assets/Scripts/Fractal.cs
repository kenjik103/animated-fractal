using System;
using UnityEngine;
using UnityEngine.UIElements;

public class Fractal : MonoBehaviour
{
    [SerializeField, Range(1,8)]  int depth = 4;
    [SerializeField] Mesh mesh;
    [SerializeField] Material material;

    readonly Vector3[] directions = { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back};
    Quaternion[] rotations = { 
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)
    };

    struct FractalPart
    {
        public Vector3 direction;
        public Quaternion rotation;
        public Transform transform;
    }
    
    FractalPart[][] parts; //parts seperated by layer

    void Awake() {
        parts = new FractalPart[depth][];
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new FractalPart[length];
        }

        float scale = 1f;
        parts[0][0] = CreatePart(0, 0, scale);
        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            FractalPart[] levelPart = parts[li];
            for (int fpi = 0; fpi < levelPart.Length; fpi+=5) {
                for (int ci = 0; ci < 5; ci++) {
                    levelPart[fpi + ci] = CreatePart(li, ci, scale);
                }
            }
        }
    }

    FractalPart CreatePart(int levelIndex, int childIndex, float scale) {
        GameObject go = new GameObject("Fractal Part L" + levelIndex + " C" + childIndex);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;
        go.transform.localScale = Vector3.one * scale;

        return new FractalPart
        {
            direction = directions[childIndex],
            rotation = rotations[childIndex],
            transform = go.transform
                
        };
    }

    void Update() {
        Quaternion deltaRotation = Quaternion.Euler(0, 22.5f * Time.deltaTime, 0);
        FractalPart rootPart = parts[0][0];
        rootPart.rotation *= deltaRotation;
        rootPart.transform.localRotation = rootPart.rotation;
        parts[0][0] = rootPart;
        
        for (int li = 1; li < parts.Length; li++) {
            FractalPart[] parentParts = parts[li - 1];
            FractalPart[] levelParts = parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                Transform parentTransform = parentParts[fpi / 5].transform;
                FractalPart part = levelParts[fpi];
                part.rotation *= deltaRotation;
                part.transform.localRotation = parentTransform.localRotation * part.rotation;
                part.transform.localPosition = 
                    parentTransform.localPosition + 
                    parentTransform.localRotation * 
                    (1.5f * part.transform.localScale.x * part.direction);
                levelParts[fpi] = part;
            }
        }
    }
}