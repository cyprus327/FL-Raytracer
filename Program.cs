using Fraglib;
using System.Numerics;

internal sealed class Raytracer {
    private static Vector3 camPos = new(0f, 0f, 1.5f);
    private static Matrix4x4 yawPitchMatrix = Matrix4x4.Identity;
    private static float pitch = 0f, yaw = 0f;
    private const float MAX_PITCH = MathF.PI * 0.49f;

    private static bool renderNormals = true;

    private static readonly Sphere[] _spheres = {
        new() {
            Pos = new(-0.8f, -0.2f, 0.4f),
            Radius = 0.8f,
            MatInd = 7
        },
        // new() {
        //     Pos = new(0.3f, -0.8f, 0.2f),
        //     Radius = 0.2f,
        //     MatInd = 4
        // },
        // new() {
        //     Pos = new(-1.5f, -0.3f, 0.6f),
        //     Radius = 1.2f,
        //     MatInd = 1
        // },

        new() {
            Pos = new(0f, 1000f, 0f),
            Radius = 1000f,
            MatInd = 3
        },
        new() {
            Pos = new(1001.5f, 0f, 0f),
            Radius = 1000f,
            MatInd = 4
        },
        new() {
            Pos = new(0f, 0f, 1003f),
            Radius = 1000f,
            MatInd = 3
        },
        new() {
            Pos = new(-1003f, 0f, 0f),
            Radius = 1000f,
            MatInd = 5
        },
        new() {
            Pos = new(0f, 0f, -1003f),
            Radius = 1000f,
            MatInd = 3
        },
        new() {
            Pos = new(0f, -804f, 0f),
            Radius = 800.0035f,
            MatInd = 3
        }
    };

    private static readonly AABB[] _aabbs = {
        new() {
            Min = new(0.9f, 0.1f, -0.6f),
            Max = new(1.1f, -1.5f, 0.4f),
            MatInd = 1
        },
    };

    private static readonly Material[] _materials = {
        new() {
            Albedo = new(0.95f, 0.71f, 0.76f),
            Metallic = 0.9f,
            Roughness = 0.1f,
        },
        new() {
            Albedo = new(0.62f, 0.87f, 0.64f),
            Metallic = 0.5f,
            Roughness = 0.5f,
        },
        new() {
            Albedo = new(0.95f, 0.91f, 0.55f),
            Metallic = 0.1f,
            Roughness = 0.9f,
        },
        new() {
            Albedo = new(0.9f, 0.9f, 0.9f),
            Metallic = 0.05f,
            Roughness = 0.95f,
        },
        new() {
            Albedo = new(1f, 0.7f, 0.2f),
            Metallic = 0.2f,
            Roughness = 0.8f,
        },
        new() {
            Albedo = new(0.2f, 0.4f, 0.9f),
            Metallic = 0.2f,
            Roughness = 0.8f,
        },

        new() {
            Albedo = Vector3.One,
            EmissionStrength = 0.1f
        },
        new() {
            Albedo = Vector3.One,
            EmissionStrength = 1f
        }
    };

    public static void Main() {
        // Vector3 centerPoint = Vector3.Zero;
        // Vector3 referencePoint = new(0f, -1.25f, 0f);
        // Vector3 rotationAxis = Vector3.Normalize(referencePoint - centerPoint);
        // Matrix4x4 rotMat = Matrix4x4.CreateFromAxisAngle(rotationAxis, MathF.PI / 4f);
        // _spheres[2].Pos = Vector3.Transform(_spheres[2].Pos - centerPoint, rotMat) + centerPoint;
        // _spheres[3].Pos = Vector3.Transform(_spheres[3].Pos - centerPoint, rotMat) + centerPoint;
        // _spheres[4].Pos = Vector3.Transform(_spheres[4].Pos - centerPoint, rotMat) + centerPoint;
        // _spheres[5].Pos = Vector3.Transform(_spheres[5].Pos - centerPoint, rotMat) + centerPoint;

        FL.Settings.VSync = false;
        FL.Settings.TargetFramerate = 144;
        FL.Init(800, 450, "Raytracer", PerPixel, PerFrame);
        FL.Run();
    }

    public static void PerFrame() {
        if (!FL.RMBDown()) {
            FL.Settings.Accumulate = true;
            return;
        }

        yaw -= FL.MouseDelta.X * 0.005f;
        pitch += FL.MouseDelta.Y * 0.005f;
        pitch = Math.Clamp(pitch, -MAX_PITCH, MAX_PITCH);
        FL.Settings.Accumulate = false;

        const int STR_SIZE = 4;
        const string STR = "WASD to move,\nR to switch modes.";
        FL.DrawString(STR, 12, FL.Height - 32, STR_SIZE, FL.Black);
        FL.DrawString(STR, 10, FL.Height - 30, STR_SIZE, FL.White);

        const float SPEED = 2f;
        yawPitchMatrix = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, 0f);
        if (FL.GetKeyDown('W')) {
            camPos += SPEED * Vector3.Transform(-Vector3.UnitZ, yawPitchMatrix) * FL.DeltaTime;
        } else if (FL.GetKeyDown('S')) {
            camPos += SPEED * Vector3.Transform(Vector3.UnitZ, yawPitchMatrix) * FL.DeltaTime;
        }
        if (FL.GetKeyDown('A')) {
            camPos += SPEED * Vector3.Transform(-Vector3.UnitX, yawPitchMatrix) * FL.DeltaTime;
        } else if (FL.GetKeyDown('D')) {
            camPos += SPEED * Vector3.Transform(Vector3.UnitX, yawPitchMatrix) * FL.DeltaTime;
        }
        if (FL.GetKeyDown('Q')) {
            camPos.Y += SPEED * FL.DeltaTime;
        } else if (FL.GetKeyDown('E')) {
            camPos.Y -= SPEED * FL.DeltaTime;
        }

        if (FL.GetKeyUp('R')) {
            renderNormals = !renderNormals;
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

        if (renderNormals) {
            Vector3 normalColor = Vector3.Zero;

            HitPayload payload = TraceRay(rayOrigin, rayDir);
            if (payload.HitDist != -1f) {
                normalColor = (Vector3.Normalize(payload.WorldNormal) + Vector3.One) * 0.5f;
            }

            return FL.NewColor(normalColor);
        }

        Vector3 light = Vector3.Zero;
        Vector3 contribution = Vector3.One;

        const int BOUNCES = 16;
        for (int i = 0; i < BOUNCES; i++) {
            HitPayload payload = TraceRay(rayOrigin, rayDir);
            if (payload.HitDist == -1f) {
                //light += new Vector3(0.2f, 0.4f, rayDir.Y * 0.5f + 0.5f) * contribution;
                break;
            }

            int ind = payload.ObjectInd;
            Material mat = _materials[ind < _spheres.Length ? _spheres[ind].MatInd : _aabbs[ind - _spheres.Length].MatInd];
            
            contribution *= mat.Albedo;
            light += mat.Emission * contribution;

            rayDir = Vector3.Lerp(
                Vector3.Normalize(payload.WorldNormal + FL.RandInUnitSphere()) * mat.Roughness, 
                Vector3.Reflect(rayDir, payload.WorldNormal), 
                mat.Metallic);
            rayOrigin = payload.WorldPos + payload.WorldNormal * 0.0001f;
        }

        return FL.NewColor(Vector3.Clamp(light, Vector3.Zero, Vector3.One));
    }

    private static HitPayload TraceRay(Vector3 rayOrigin, Vector3 rayDir) {
        int closestObjInd = -1;
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
                closestObjInd = i;
            }
        }

        for (int i = 0; i < _aabbs.Length; i++) {
            AABB aabb = _aabbs[i];

            Vector3 invRayDir = new(1f / rayDir.X, 1f / rayDir.Y, 1f / rayDir.Z);
            Vector3 tMin = (aabb.Min - rayOrigin) * invRayDir;
            Vector3 tMax = (aabb.Max - rayOrigin) * invRayDir;

            Vector3 t1 = Vector3.Min(tMin, tMax);
            Vector3 t2 = Vector3.Max(tMin, tMax);

            float tNear = MathF.Max(MathF.Max(t1.X, t1.Y), t1.Z);
            float tFar = MathF.Min(MathF.Min(t2.X, t2.Y), t2.Z);

            if (tNear > tFar || tFar < 0) {
                continue;
            }

            float t = tNear > 0 ? tNear : tFar;
            if (t < hitDist) {
                hitDist = t;
                closestObjInd = i + _spheres.Length;
            }
        }

        if (closestObjInd == -1) {
            return HitPayload.MissPayload;
        }

        return ClosestHit(rayOrigin, rayDir, hitDist, closestObjInd);
    }

    private static HitPayload ClosestHit(Vector3 rayOrigin, Vector3 rayDir, float hitDist, int objectInd) {
        HitPayload payload = new() {
            HitDist = hitDist,
            ObjectInd = objectInd
        };

        Vector3 hitPoint;
        Vector3 normal;

        if (objectInd < _spheres.Length) {
            hitPoint = rayOrigin + rayDir * hitDist;
            normal = Vector3.Normalize(hitPoint - _spheres[objectInd].Pos);
        } else {
            AABB aabb = _aabbs[objectInd - _spheres.Length];

            hitPoint = rayOrigin + rayDir * hitDist;

            Vector3 center = (aabb.Min + aabb.Max) * 0.5f;
            Vector3 extents = aabb.Max - center;
            Vector3 hitPointLocal = hitPoint - center;

            float maxAbs = MathF.Max(MathF.Abs(hitPointLocal.X / extents.X), MathF.Max(MathF.Abs(hitPointLocal.Y / extents.Y), MathF.Abs(hitPointLocal.Z / extents.Z)));

            if (MathF.Abs(hitPointLocal.X / extents.X) == maxAbs) {
                normal = new Vector3(MathF.Sign(hitPointLocal.X), 0, 0);
            } else if (MathF.Abs(hitPointLocal.Y / extents.Y) == maxAbs) {
                normal = new Vector3(0, MathF.Sign(hitPointLocal.Y), 0);
            } else {
                normal = new Vector3(0, 0, MathF.Sign(hitPointLocal.Z));
            }
        }

        payload.WorldPos = hitPoint;
        payload.WorldNormal = normal;

        return payload;
    }

    private struct Sphere {
        public Vector3 Pos;
        public float Radius;
        
        public int MatInd;
    }

    private struct AABB {
        public Vector3 Min;
        public Vector3 Max;
        
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
