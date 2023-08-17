﻿using Fraglib;
using System.Numerics;

internal sealed class Raytracer {
    private static Vector3 camPos = new(0f, 0f, 1.5f);
    private static Matrix4x4 yawPitchMatrix = Matrix4x4.Identity;
    private static float pitch = 0f, yaw = 0f;
    private const float MAX_PITCH = MathF.PI * 0.49f;

    private static readonly Sphere[] _spheres = {
        new Sphere() {
            Pos = new(0.4f, -0.35f, -0.35f),
            Radius = 0.35f,
            MatInd = 0
        },
        new Sphere() {
            Pos = new(-0.4f, -0.35f, -0.35f),
            Radius = 0.35f,
            MatInd = 1
        },
        new Sphere() {
            Pos = new(0f, -0.35f, 0.35f),
            Radius = 0.35f,
            MatInd = 2
        },
        new Sphere() {
            Pos = new(0f, 1000f, 0f),
            Radius = 1000f,
            MatInd = 3
        },
        new Sphere() {
            Pos = new(1001.5f, 0f, 0f),
            Radius = 1000f,
            MatInd = 4
        },
        new Sphere() {
            Pos = new(0f, 0f, 1001.5f),
            Radius = 1000f,
            MatInd = 3
        },
        new Sphere() {
            Pos = new(-1001.5f, 0f, 0f),
            Radius = 1000f,
            MatInd = 5
        },
        new Sphere() {
            Pos = new(0f, 0f, -1001.5f),
            Radius = 1000f,
            MatInd = 3
        },
        new Sphere() {
            Pos = new(0f, -802.5f, 0f),
            Radius = 800.0035f,
            MatInd = 6
        }
    };

    private static readonly Material[] _materials = {
        new Material() {
            Albedo = new(0.62f, 0.87f, 0.64f),
            Metallic = 0.9f,
            Roughness = 0.1f,
        },
        new Material() {
            Albedo = new(0.95f, 0.91f, 0.55f),
            Metallic = 0.5f,
            Roughness = 0.5f,
        },
        new Material() {
            Albedo = new(0.95f, 0.71f, 0.76f),
            Metallic = 0.1f,
            Roughness = 0.9f,
        },
        new Material() {
            Albedo = new(0.9f, 0.9f, 0.9f),
            Metallic = 0.4f,
            Roughness = 0.4f,
        },
        new Material() {
            Albedo = new(1f, 0.7f, 0.2f),
            Metallic = 0.6f,
            Roughness = 0.4f,
        },
        new Material() {
            Albedo = new(0.2f, 0.4f, 0.9f),
            Metallic = 0.6f,
            Roughness = 0.4f,
        },
        new Material() {
            Albedo = Vector3.One,
            EmissionStrength = 1f
        },
    };

    public static void Main() {
        FL.Init(800, 450, "raytracer", PerPixel, PerFrame);
        FL.Run();
    }

    public static void PerFrame() {
        if (!FL.RMBDown()) {
            FL.Accumulate = true;
            return;
        }

        yaw -= FL.MouseDelta.X * 0.005f;
        pitch += FL.MouseDelta.Y * 0.005f;
        pitch = Math.Clamp(pitch, -MAX_PITCH, MAX_PITCH);
        FL.Accumulate = false;

        float moveSpeed = FL.GetKeyDown(' ') ? 5f : 1f;

        yawPitchMatrix = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, 0f);
        if (FL.GetKeyDown('W')) {
            camPos += moveSpeed * Vector3.Transform(-Vector3.UnitZ, yawPitchMatrix) * FL.DeltaTime;
        } else if (FL.GetKeyDown('S')) {
            camPos += moveSpeed * Vector3.Transform(Vector3.UnitZ, yawPitchMatrix) * FL.DeltaTime;
        }
        if (FL.GetKeyDown('A')) {
            camPos += moveSpeed * Vector3.Transform(-Vector3.UnitX, yawPitchMatrix) * FL.DeltaTime;
        } else if (FL.GetKeyDown('D')) {
            camPos += moveSpeed * Vector3.Transform(Vector3.UnitX, yawPitchMatrix) * FL.DeltaTime;
        }
        if (FL.GetKeyDown('Q')) {
            camPos.Y += moveSpeed * FL.DeltaTime;
        } else if (FL.GetKeyDown('E')) {
            camPos.Y -= moveSpeed * FL.DeltaTime;
        }
    }

    public static uint PerPixel(int x, int y, Uniforms u) {
        float aspectRatio = (float)u.Width / u.Height;
        float uvx = (2f * (x + 0.5f) / u.Width - 1f) * aspectRatio;
        float uvy = 1f - 2f * (y + 0.5f) / u.Height;
        
        Vector3 rayDir = Vector3.Normalize(Vector3.Transform(new(uvx, uvy, -1f), yawPitchMatrix));
        Vector3 rayOrigin = camPos;
        
        if (_spheres.Length == 0) {
            return FL.Black;
        }

        Vector3 light = Vector3.Zero;
        Vector3 contribution = Vector3.One;

        const int BOUNCES = 12;
        for (int i = 0; i < BOUNCES; i++) {
            HitPayload payload = TraceRay(rayOrigin, rayDir);
            if (payload.HitDist == -1f) {
                light += new Vector3(0.2f, 0.4f, rayDir.Y * 0.5f + 0.5f) * contribution;
                break;
            }

            Sphere sphere = _spheres[payload.ObjectInd];
            Material mat = _materials[sphere.MatInd];
            
            light += mat.Emission * contribution;
            contribution *= mat.Albedo;

            rayDir = Vector3.Lerp(
                Vector3.Normalize(payload.WorldNormal + FL.RandInUnitSphere()) * mat.Roughness, 
                Vector3.Reflect(rayDir, payload.WorldNormal), 
                mat.Metallic);
            rayOrigin = payload.WorldPos + payload.WorldNormal * 0.0001f;
        }

        return FL.NewColor(Vector3.Clamp(light, Vector3.Zero, Vector3.One));
    }

    private static HitPayload TraceRay(Vector3 rayOrigin, Vector3 rayDir) {
        int closestSphereInd = -1;
        float hitDist = float.MaxValue;
        for (int i = 0; i < _spheres.Length; i++) {
            Sphere sphere = _spheres[i];

            Vector3 origin = rayOrigin - sphere.Pos;

            float a = Vector3.Dot(rayDir, rayDir);
            float b = 2f * Vector3.Dot(origin, rayDir);
            float c = Vector3.Dot(origin, origin) - sphere.Radius * sphere.Radius;

            float disc = b * b - 4f * a * c;

            if (disc < 0) {
                continue;
            }

            //float fartherT = (-b + MathF.Sqrt(disc)) / (2f * a);
            float closestT = (-b - MathF.Sqrt(disc)) / (2f * a);
            if (closestT < hitDist && closestT > 0) {
                hitDist = closestT;
                closestSphereInd = i;
            }
        }

        if (closestSphereInd == -1) {
            return HitPayload.MissPayload;
        }

        return ClosestHit(rayOrigin, rayDir, hitDist, closestSphereInd);
    }

    private static HitPayload ClosestHit(Vector3 rayOrigin, Vector3 rayDir, float hitDist, int objectInd) {
        HitPayload payload = new() {
            HitDist = hitDist,
            ObjectInd = objectInd
        };

        ref Sphere sphere = ref _spheres[objectInd];

        payload.WorldPos = rayOrigin - sphere.Pos + rayDir * hitDist;
        payload.WorldNormal = Vector3.Normalize(payload.WorldPos);

        payload.WorldPos += sphere.Pos;

        return payload;
    }

    private struct Sphere {
        public Vector3 Pos;
        public float Radius;
        
        public int MatInd;
    }

    private struct Material {
        public Vector3 Albedo;
        public float Roughness;
        public float Metallic;
        public float EmissionStrength;

        public readonly Vector3 Emission => Albedo * EmissionStrength;
    }

    private struct HitPayload {
        public float HitDist;
        public Vector3 WorldPos, WorldNormal;

        public int ObjectInd;

        public static readonly HitPayload MissPayload = new() {
            HitDist = -1f,
        };
    }
}