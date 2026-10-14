using System.Collections.Generic;
using UnityEngine;
using WarOfNyx.Core.Vehicles;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>The meshes of one vehicle: body, one wheel (used for all four), and the steering wheel / handlebars.</summary>
    public sealed class VehicleModel
    {
        public Mesh Body, Wheel, Steering;
        /// <summary>Wheel centres at rest (front left, front right, rear left, rear right), vehicle space.</summary>
        public Vector3[] Wheels;
        /// <summary>Seat positions (hip height), driver first.</summary>
        public Vector3[] Seats;
        public Vector3 SteeringPosition;
        public Vector3 SteeringEuler;
        /// <summary>Collision boxes (centre, size) in vehicle space: the lower body, plus a cab for the truck.</summary>
        public Bounds[] Colliders;
    }

    /// <summary>
    /// Detailed procedural PLACEHOLDER vehicles (WarOfNyx/NyxToonVC materials): the Nyx Buggy (tubular roll cage, bucket
    /// seats with harnesses, rear engine with fins and exhaust, light bar, spare tyre), the Raider Truck (armoured cab
    /// with glossy windows, bull bar, grille, bed with bench seats, roll bar, exhaust stack) and the Scout Quad (fairing,
    /// fenders, racks, handlebars). Treaded tyres on machined rims; three paint schemes per type. Vehicle space: origin
    /// on the ground between the wheels, +Z forward.
    /// </summary>
    public static class VehicleModels
    {
        public const int PaintCount = 3;

        private static readonly Dictionary<int, VehicleModel> Cache = new Dictionary<int, VehicleModel>();

        private static readonly Color[][] Paints =
        {
            new[] { new Color(0.35f, 0.12f, 0.55f), new Color(0.62f, 0.08f, 0.12f), new Color(0.75f, 0.6f, 0.35f) },
            new[] { new Color(0.3f, 0.33f, 0.22f), new Color(0.22f, 0.24f, 0.28f), new Color(0.85f, 0.86f, 0.88f) },
            new[] { new Color(0.05f, 0.45f, 0.5f), new Color(0.85f, 0.4f, 0.08f), new Color(0.1f, 0.1f, 0.12f) },
        };

        private static readonly Color Steel = new Color(0.2f, 0.21f, 0.24f);
        private static readonly Color Chrome = new Color(0.7f, 0.72f, 0.78f);
        private static readonly Color Black = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color Glass = new Color(0.06f, 0.09f, 0.12f);
        private static readonly Color SeatColor = new Color(0.12f, 0.1f, 0.1f);
        private static readonly Color Brass = new Color(0.85f, 0.65f, 0.3f);
        private static readonly Color Head = new Color(1f, 0.95f, 0.85f);
        private static readonly Color Tail = new Color(1f, 0.1f, 0.08f);
        private static readonly Color Neon = new Color(0f, 0.9f, 1f);

        public static VehicleModel Get(int type, int paint)
        {
            var stats = VehicleCatalog.Get(type) ?? VehicleCatalog.All[0];
            type = VehicleCatalog.IndexOf(stats.Id);
            paint = ((paint % PaintCount) + PaintCount) % PaintCount;
            int key = type * PaintCount + paint;
            if (Cache.TryGetValue(key, out var model) && model.Body != null)
                return model;
            model = Build(type, stats, Paints[type][paint]);
            Cache[key] = model;
            return model;
        }

        private static Color S(Color c) => MeshKit.Solid(c);
        private static Color G(Color c, float glow) => MeshKit.Glow(c, glow);

        private static VehicleModel Build(int type, VehicleStats v, Color paint)
        {
            float r = v.WheelRadius, hx = v.TrackWidth * 0.5f, hz = v.Wheelbase * 0.5f;
            var model = new VehicleModel
            {
                Wheels = new[] { new Vector3(-hx, r, hz), new Vector3(hx, r, hz), new Vector3(-hx, r, -hz), new Vector3(hx, r, -hz) },
                Wheel = WheelMesh(v, type),
            };
            var k = new MeshKit();
            var s = new MeshKit();
            switch (type)
            {
                case 1: Truck(k, s, model, v, paint); break;
                case 2: Quad(k, s, model, v, paint); break;
                default: Buggy(k, s, model, v, paint); break;
            }
            model.Body = k.ToMesh(v.Id + "_body");
            model.Steering = s.ToMesh(v.Id + "_steering");
            return model;
        }

        // ------------------------------------------------------------------ shared parts

        private static Ring[] Tube(float y0, float y1, float radius) => new[] { new Ring(y0, radius, radius), new Ring(y1, radius, radius) };

        private static Mesh WheelMesh(VehicleStats v, int type)
        {
            var k = new MeshKit();
            Tire(k, Matrix4x4.identity, v.WheelRadius, type == 2 ? 0.24f : 0.32f);
            return k.ToMesh(v.Id + "_wheel");
        }

        /// <summary>A treaded tyre on a machined rim, axle along X, centred at the origin of <paramref name="m"/>.</summary>
        private static void Tire(MeshKit k, Matrix4x4 m, float radius, float width)
        {
            float h = width * 0.5f;
            var axle = m * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)); // rings along Y become rings along X
            var tire = new[]
            {
                new Ring(-h, radius * 0.66f, radius * 0.66f), new Ring(-h, radius * 0.86f, radius * 0.86f), new Ring(-h + 0.025f, radius * 0.97f, radius * 0.97f),
                new Ring(-h + 0.06f, radius, radius), new Ring(h - 0.06f, radius, radius), new Ring(h - 0.025f, radius * 0.97f, radius * 0.97f),
                new Ring(h, radius * 0.86f, radius * 0.86f), new Ring(h, radius * 0.66f, radius * 0.66f),
            };
            // Chunky off-road tread: alternating blocks across the face, grooves on the shoulders.
            k.Use(Surf.Rubber).Surface(tire, 40, S(Black), axle, 0f,
                (y, a) => Mathf.Abs(y) < h - 0.05f && Mathf.Repeat(a + (y > 0f ? 4.5f : 0f), 9f) < 4f ? -0.014f : Mathf.Abs(y) < h - 0.02f && Mathf.Repeat(a, 9f) < 2f ? -0.006f : 0f);
            // Rim: dished face, spokes, hub, brass lug nuts, a neon ring.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (h - 0.01f);
                var face = m * Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, side * 90f, 0f), Vector3.one);
                k.Use(Surf.Metal).Disc(radius * 0.66f, radius * 0.66f, 24, S(Steel), face);
                k.Use(Surf.Metal, Weave.Plain, 0f);
                for (int i = 0; i < 6; i++)
                    k.RoundBox(new Vector3(0.05f, radius * 0.5f, 0.02f), 0.008f, S(Chrome),
                        face * Matrix4x4.TRS(Vector3.forward * 0.012f, Quaternion.Euler(0f, 0f, i * 60f), Vector3.one) * MeshKit.At(new Vector3(0f, radius * 0.3f, 0f)));
                k.Use(Surf.Metal).Surface(Tube(0f, 0.04f, radius * 0.16f), 14, S(Chrome), face * Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)), 0f, null, false, true);
                k.Use(Surf.Gold, Weave.Plain, 0f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    k.Ellipsoid(new Vector3(0.012f, 0.012f, 0.012f), 3, 6, S(Brass), face * MeshKit.At(new Vector3(Mathf.Sin(a) * radius * 0.23f, Mathf.Cos(a) * radius * 0.23f, 0.03f)));
                }
                k.Use(Surf.Circuit, Weave.Plain, 0f).Torus(radius * 0.64f, radius * 0.64f, 0.006f, 28, 3, G(Neon, 0.7f), face * Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)));
                k.Outline = 1f;
            }
        }

        /// <summary>A wheel arch: a curved plate over the top of a wheel, from just ahead of it to just behind.</summary>
        private static void Arch(MeshKit k, Vector3 centre, float radius, float width, Color color)
        {
            var path = new List<Vector3>();
            var widths = new List<float>();
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.Lerp(-25f, 205f, i / 12f) * Mathf.Deg2Rad;
                path.Add(centre + new Vector3(0f, Mathf.Sin(a) * radius, Mathf.Cos(a) * radius));
                widths.Add(width);
            }
            k.Band(path, widths, 0.035f, Vector3.right, _ => color, Matrix4x4.identity);
        }

        private static void Pipe(MeshKit k, Color color, float radius, params Vector3[] points) => k.Tube(points, radius, S(color), Matrix4x4.identity, 10);

        private static void Lamp(MeshKit k, Vector3 at, float radius, Color color, Vector3 facing)
        {
            var m = Matrix4x4.TRS(at, Quaternion.LookRotation(facing), Vector3.one);
            k.Use(Surf.Metal).Surface(Tube(-0.05f, 0f, radius * 1.15f), 14, S(Black), m * Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)), 0f, null, true, false);
            k.Use(Surf.Circuit, Weave.Plain, 0f).Disc(radius, radius, 16, G(color, 0.95f), m * MeshKit.At(new Vector3(0f, 0f, 0.002f)));
            k.Outline = 1f;
        }

        private static void Seat(MeshKit k, Vector3 hip, float width, bool harness, Color stitch)
        {
            k.Use(Surf.Leather).RoundBox(new Vector3(width, 0.12f, 0.46f), 0.05f, S(SeatColor), MeshKit.At(hip + new Vector3(0f, -0.1f, 0.06f)));
            k.RoundBox(new Vector3(width, 0.62f, 0.12f), 0.05f, S(SeatColor), MeshKit.At(hip + new Vector3(0f, 0.2f, -0.2f), new Vector3(-10f, 0f, 0f)));
            k.Use(Surf.Fabric, Weave.Quilt).RoundBox(new Vector3(width * 0.7f, 0.5f, 0.02f), 0.01f, S(Color.Lerp(SeatColor, stitch, 0.35f)), MeshKit.At(hip + new Vector3(0f, 0.22f, -0.135f), new Vector3(-10f, 0f, 0f)));
            if (!harness)
                return;
            k.Use(Surf.Fabric, Weave.Rib);
            for (int side = -1; side <= 1; side += 2)
                k.Band(new[] { hip + new Vector3(side * 0.09f, 0.52f, -0.24f), hip + new Vector3(side * 0.08f, 0.3f, -0.08f), hip + new Vector3(side * 0.05f, 0.05f, 0.0f) },
                    new[] { 0.045f, 0.045f, 0.045f }, 0.006f, Vector3.right, _ => S(stitch), Matrix4x4.identity);
        }

        // ------------------------------------------------------------------ Nyx Buggy

        private static void Buggy(MeshKit k, MeshKit steering, VehicleModel model, VehicleStats v, Color paint)
        {
            var accent = Color.Lerp(paint, Color.white, 0.25f);
            float r = v.WheelRadius;
            model.Seats = new[] { new Vector3(-0.32f, 0.78f, 0.0f), new Vector3(0.32f, 0.78f, 0.0f) };
            model.SteeringPosition = new Vector3(-0.32f, 1.12f, 0.5f);
            model.SteeringEuler = new Vector3(-62f, 0f, 0f);
            model.Colliders = new[] { new Bounds(new Vector3(0f, 0.72f, 0.05f), new Vector3(1.65f, 0.55f, 3.5f)) };

            // Floor pan, sills with glowing strips, nose, rear deck.
            k.Use(Surf.Polymer).RoundBox(new Vector3(1.35f, 0.12f, 3.0f), 0.05f, S(Color.Lerp(paint, Black, 0.6f)), MeshKit.At(new Vector3(0f, 0.55f, 0.05f)));
            for (int s = -1; s <= 1; s += 2)
            {
                k.Use(Surf.Lacquer).RoundBox(new Vector3(0.22f, 0.26f, 1.5f), 0.07f, S(paint), MeshKit.At(new Vector3(s * 0.66f, 0.66f, 0.05f)));
                k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.006f, 0.03f, 1.3f), G(accent, 0.85f), MeshKit.At(new Vector3(s * 0.774f, 0.66f, 0.05f)));
                k.Outline = 1f;
                // Fenders over the wheels.
                foreach (float z in new[] { model.Wheels[0].z, model.Wheels[2].z })
                    Arch(k.Use(Surf.Lacquer), new Vector3(s * v.TrackWidth * 0.5f, r, z), r + 0.1f, 0.42f, S(paint));
            }
            k.Use(Surf.Lacquer).RoundBox(new Vector3(1.25f, 0.32f, 0.75f), 0.1f, S(paint), MeshKit.At(new Vector3(0f, 0.78f, 1.42f), new Vector3(-12f, 0f, 0f)));
            k.Use(Surf.Carbon).RoundBox(new Vector3(0.9f, 0.05f, 0.5f), 0.02f, S(new Color(0.1f, 0.1f, 0.12f)), MeshKit.At(new Vector3(0f, 0.95f, 1.4f), new Vector3(-12f, 0f, 0f)));
            k.Use(Surf.Metal);
            Pipe(k, Steel, 0.035f, new Vector3(-0.75f, 0.62f, 1.9f), new Vector3(-0.7f, 0.75f, 1.98f), new Vector3(0.7f, 0.75f, 1.98f), new Vector3(0.75f, 0.62f, 1.9f));
            Pipe(k, Steel, 0.03f, new Vector3(-0.5f, 0.75f, 1.98f), new Vector3(-0.5f, 0.5f, 1.95f));
            Pipe(k, Steel, 0.03f, new Vector3(0.5f, 0.75f, 1.98f), new Vector3(0.5f, 0.5f, 1.95f));
            for (int s = -1; s <= 1; s += 2)
            {
                Lamp(k, new Vector3(s * 0.42f, 0.86f, 1.8f), 0.08f, Head, new Vector3(0f, -0.1f, 1f));
                Lamp(k, new Vector3(s * 0.6f, 0.82f, -1.75f), 0.05f, Tail, Vector3.back);
            }

            // Tubular roll cage: two hoops, roof rails, diagonals, a roof light bar.
            var cage = Black;
            k.Use(Surf.Metal);
            foreach (float z in new[] { -0.32f, 0.62f })
                Pipe(k, cage, 0.032f, new Vector3(-0.7f, 0.6f, z), new Vector3(-0.68f, 1.45f, z - 0.05f), new Vector3(-0.55f, 1.6f, z - 0.08f),
                    new Vector3(0.55f, 1.6f, z - 0.08f), new Vector3(0.68f, 1.45f, z - 0.05f), new Vector3(0.7f, 0.6f, z));
            for (int s = -1; s <= 1; s += 2)
            {
                Pipe(k, cage, 0.03f, new Vector3(s * 0.55f, 1.6f, -0.4f), new Vector3(s * 0.55f, 1.6f, 0.54f), new Vector3(s * 0.6f, 1.2f, 1.1f), new Vector3(s * 0.62f, 0.85f, 1.3f));
                Pipe(k, cage, 0.028f, new Vector3(s * 0.68f, 1.45f, -0.37f), new Vector3(s * 0.7f, 0.65f, -1.2f));
            }
            Pipe(k, cage, 0.028f, new Vector3(-0.55f, 1.6f, -0.4f), new Vector3(0.55f, 1.6f, 0.54f));
            k.Use(Surf.Metal).RoundBox(new Vector3(1.0f, 0.08f, 0.12f), 0.02f, S(Black), MeshKit.At(new Vector3(0f, 1.66f, 0.56f)));
            for (int i = 0; i < 4; i++)
                Lamp(k, new Vector3(-0.36f + i * 0.24f, 1.66f, 0.62f), 0.035f, Head, Vector3.forward);

            // Cockpit: bucket seats with harnesses, dashboard with glowing gauges, steering column.
            foreach (var seat in model.Seats)
                Seat(k, seat, 0.42f, true, accent);
            k.Use(Surf.Polymer).RoundBox(new Vector3(1.15f, 0.22f, 0.24f), 0.06f, S(Black), MeshKit.At(new Vector3(0f, 0.98f, 0.78f), new Vector3(-20f, 0f, 0f)));
            k.Use(Surf.Circuit, Weave.Plain, 0f);
            for (int i = 0; i < 3; i++)
                k.Disc(0.035f, 0.035f, 14, G(i == 1 ? accent : Neon, 0.8f), MeshKit.TRS(new Vector3(-0.42f + i * 0.1f, 1.04f, 0.67f), new Vector3(-20f, 180f, 0f), Vector3.one));
            k.Outline = 1f;
            Pipe(k, Steel, 0.022f, new Vector3(-0.32f, 0.98f, 0.72f), model.SteeringPosition + new Vector3(0f, -0.02f, 0.03f));

            // Rear engine: block with cooling fins, air filters, exhaust headers with brass tips, a spare tyre.
            k.Use(Surf.Metal).RoundBox(new Vector3(0.9f, 0.42f, 0.62f), 0.06f, S(Steel), MeshKit.At(new Vector3(0f, 0.85f, -1.3f)));
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (int i = 0; i < 7; i++)
                k.Box(new Vector3(0.92f, 0.02f, 0.5f), S(Chrome), MeshKit.At(new Vector3(0f, 0.72f + i * 0.04f, -1.3f)));
            k.Outline = 1f;
            for (int s = -1; s <= 1; s += 2)
            {
                k.Use(Surf.Metal).Surface(Tube(0f, 0.12f, 0.08f), 16, S(Chrome), MeshKit.At(new Vector3(s * 0.22f, 1.08f, -1.25f)), 0f,
                    (y, a) => Mathf.Repeat(y * 60f, 1f) < 0.3f ? -0.006f : 0f, false, true);
                k.Use(Surf.Metal);
                Pipe(k, Chrome, 0.035f, new Vector3(s * 0.4f, 0.8f, -1.55f), new Vector3(s * 0.5f, 0.75f, -1.75f), new Vector3(s * 0.52f, 0.85f, -1.95f));
                k.Use(Surf.Gold).Surface(Tube(0f, 0.1f, 0.045f), 12, S(Brass), Matrix4x4.TRS(new Vector3(s * 0.52f, 0.85f, -1.95f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one), 0f, null, false, false);
            }
            Tire(k, Matrix4x4.TRS(new Vector3(0f, 1.25f, -1.72f), Quaternion.Euler(0f, 90f, -15f), Vector3.one), r * 0.95f, 0.28f);

            // Steering wheel: leather rim, three spokes, glowing hub.
            steering.Use(Surf.Leather).Torus(0.17f, 0.17f, 0.022f, 28, 8, S(Black), Matrix4x4.identity);
            steering.Use(Surf.Metal);
            for (int i = 0; i < 3; i++)
                steering.RoundBox(new Vector3(0.025f, 0.012f, 0.16f), 0.005f, S(Chrome), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, i * 120f + 90f, 0f), Vector3.one) * MeshKit.At(new Vector3(0f, 0f, 0.08f)));
            steering.Use(Surf.Circuit, Weave.Plain, 0f).Disc(0.04f, 0.04f, 14, G(accent, 0.9f), MeshKit.TRS(new Vector3(0f, 0.012f, 0f), new Vector3(-90f, 0f, 0f), Vector3.one));
        }

        // ------------------------------------------------------------------ Raider Truck

        private static void Truck(MeshKit k, MeshKit steering, VehicleModel model, VehicleStats v, Color paint)
        {
            float r = v.WheelRadius;
            model.Seats = new[] { new Vector3(-0.45f, 1.18f, 0.75f), new Vector3(0.45f, 1.18f, 0.75f), new Vector3(-0.55f, 1.42f, -1.0f), new Vector3(0.55f, 1.42f, -1.55f) };
            model.SteeringPosition = new Vector3(-0.45f, 1.55f, 1.25f);
            model.SteeringEuler = new Vector3(-58f, 0f, 0f);
            model.Colliders = new[]
            {
                new Bounds(new Vector3(0f, 1.0f, 0.0f), new Vector3(2.05f, 0.7f, 4.9f)),
                new Bounds(new Vector3(0f, 1.75f, 0.85f), new Vector3(1.95f, 0.8f, 1.8f)),
            };
            var dark = Color.Lerp(paint, Black, 0.55f);

            // Frame rails and flared rubber arches.
            k.Use(Surf.Metal);
            for (int s = -1; s <= 1; s += 2)
                k.RoundBox(new Vector3(0.14f, 0.18f, 4.6f), 0.03f, S(Black), MeshKit.At(new Vector3(s * 0.55f, 0.62f, 0f)));
            for (int s = -1; s <= 1; s += 2)
                foreach (float z in new[] { model.Wheels[0].z, model.Wheels[2].z })
                    Arch(k.Use(Surf.Rubber), new Vector3(s * (v.TrackWidth * 0.5f + 0.02f), r, z), r + 0.12f, 0.44f, S(Black));
            // Body sides and the cab with armour plates, glossy windows, mirrors.
            k.Use(Surf.Lacquer).RoundBox(new Vector3(2.0f, 0.7f, 4.7f), 0.08f, S(paint), MeshKit.At(new Vector3(0f, 1.02f, 0.0f)));
            k.RoundBox(new Vector3(1.95f, 0.85f, 1.75f), 0.1f, S(paint), MeshKit.At(new Vector3(0f, 1.72f, 0.82f)));
            k.Use(Surf.Eye, Weave.Plain, 0f);
            k.RoundBox(new Vector3(1.75f, 0.55f, 0.04f), 0.03f, S(Glass), MeshKit.At(new Vector3(0f, 1.82f, 1.72f), new Vector3(-22f, 0f, 0f)));
            for (int s = -1; s <= 1; s += 2)
                k.RoundBox(new Vector3(0.04f, 0.42f, 1.2f), 0.03f, S(Glass), MeshKit.At(new Vector3(s * 0.98f, 1.85f, 0.78f)));
            k.RoundBox(new Vector3(1.6f, 0.38f, 0.04f), 0.03f, S(Glass), MeshKit.At(new Vector3(0f, 1.86f, -0.06f)));
            k.Outline = 1f;
            k.Use(Surf.Metal);
            for (int s = -1; s <= 1; s += 2)
            {
                k.RoundBox(new Vector3(0.05f, 0.45f, 1.3f), 0.03f, S(Steel), MeshKit.At(new Vector3(s * 1.0f, 1.28f, 0.82f)));
                k.Use(Surf.Metal, Weave.Plain, 0f);
                for (int i = 0; i < 6; i++)
                    k.Ellipsoid(new Vector3(0.016f, 0.016f, 0.016f), 3, 6, S(Chrome), MeshKit.At(new Vector3(s * 1.026f, 1.08f + (i % 2) * 0.4f, 0.3f + (i / 2) * 0.5f)));
                k.Outline = 1f;
                k.Use(Surf.Metal).RoundBox(new Vector3(0.06f, 0.16f, 0.12f), 0.02f, S(Black), MeshKit.At(new Vector3(s * 1.1f, 1.8f, 1.5f)));
                k.Use(Surf.Eye, Weave.Plain, 0f).Box(new Vector3(0.005f, 0.13f, 0.09f), S(Glass), MeshKit.At(new Vector3(s * 1.08f, 1.8f, 1.44f)));
                k.Outline = 1f;
            }
            k.Use(Surf.Lacquer).RoundBox(new Vector3(1.85f, 0.08f, 1.5f), 0.04f, S(dark), MeshKit.At(new Vector3(0f, 2.18f, 0.78f)));
            // Hood, grille with slats, headlights, bull bar.
            k.Use(Surf.Lacquer).RoundBox(new Vector3(1.9f, 0.45f, 1.0f), 0.1f, S(paint), MeshKit.At(new Vector3(0f, 1.45f, 2.05f), new Vector3(-4f, 0f, 0f)));
            k.Use(Surf.Metal).RoundBox(new Vector3(1.2f, 0.4f, 0.06f), 0.03f, S(Black), MeshKit.At(new Vector3(0f, 1.3f, 2.56f)));
            k.Use(Surf.Metal, Weave.Plain, 0f);
            for (int i = 0; i < 6; i++)
                k.Box(new Vector3(1.1f, 0.025f, 0.03f), S(Chrome), MeshKit.At(new Vector3(0f, 1.16f + i * 0.055f, 2.6f)));
            k.Outline = 1f;
            for (int s = -1; s <= 1; s += 2)
            {
                Lamp(k, new Vector3(s * 0.78f, 1.35f, 2.56f), 0.11f, Head, Vector3.forward);
                Lamp(k, new Vector3(s * 0.9f, 1.1f, -2.36f), 0.07f, Tail, Vector3.back);
            }
            k.Use(Surf.Metal);
            Pipe(k, Steel, 0.045f, new Vector3(-0.95f, 0.8f, 2.7f), new Vector3(-0.9f, 1.55f, 2.8f), new Vector3(0.9f, 1.55f, 2.8f), new Vector3(0.95f, 0.8f, 2.7f));
            Pipe(k, Steel, 0.04f, new Vector3(-0.95f, 1.0f, 2.75f), new Vector3(0.95f, 1.0f, 2.75f));
            Pipe(k, Steel, 0.035f, new Vector3(-0.4f, 1.55f, 2.8f), new Vector3(-0.4f, 0.8f, 2.72f));
            Pipe(k, Steel, 0.035f, new Vector3(0.4f, 1.55f, 2.8f), new Vector3(0.4f, 0.8f, 2.72f));
            // Bed: floor, sides, tailgate, roll bar with lights, bench seats for the passengers.
            k.Use(Surf.Metal).RoundBox(new Vector3(1.9f, 0.08f, 2.1f), 0.03f, S(Steel), MeshKit.At(new Vector3(0f, 1.38f, -1.25f)));
            for (int s = -1; s <= 1; s += 2)
                k.Use(Surf.Lacquer).RoundBox(new Vector3(0.1f, 0.45f, 2.2f), 0.04f, S(paint), MeshKit.At(new Vector3(s * 0.97f, 1.6f, -1.25f)));
            k.RoundBox(new Vector3(1.95f, 0.45f, 0.1f), 0.04f, S(paint), MeshKit.At(new Vector3(0f, 1.6f, -2.33f)));
            k.Use(Surf.Metal);
            Pipe(k, Black, 0.045f, new Vector3(-0.9f, 1.4f, -0.25f), new Vector3(-0.85f, 2.35f, -0.3f), new Vector3(0.85f, 2.35f, -0.3f), new Vector3(0.9f, 1.4f, -0.25f));
            for (int i = 0; i < 3; i++)
                Lamp(k, new Vector3(-0.4f + i * 0.4f, 2.42f, -0.26f), 0.06f, Head, Vector3.forward);
            Seat(k, model.Seats[0], 0.5f, false, paint);
            Seat(k, model.Seats[1], 0.5f, false, paint);
            for (int s = 2; s < 4; s++)
                k.Use(Surf.Leather).RoundBox(new Vector3(0.36f, 0.12f, 0.9f), 0.04f, S(SeatColor), MeshKit.At(model.Seats[s] + new Vector3(0f, -0.1f, 0.25f)));
            // Exhaust stack behind the cab.
            k.Use(Surf.Metal).Surface(Tube(1.2f, 2.45f, 0.055f), 14, S(Chrome), MeshKit.At(new Vector3(0.85f, 0f, -0.15f)), 0f, null, false, false);
            k.Use(Surf.Gold).Surface(Tube(2.45f, 2.55f, 0.065f), 14, S(Brass), MeshKit.At(new Vector3(0.85f, 0f, -0.15f)), 0f, null, false, false);
            k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(1.9f, 0.025f, 0.01f), G(Neon, 0.8f), MeshKit.At(new Vector3(0f, 1.28f, 2.36f)));
            k.Outline = 1f;

            steering.Use(Surf.Leather).Torus(0.2f, 0.2f, 0.024f, 28, 8, S(Black), Matrix4x4.identity);
            steering.Use(Surf.Metal);
            for (int i = 0; i < 4; i++)
                steering.RoundBox(new Vector3(0.025f, 0.012f, 0.19f), 0.005f, S(Steel), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, i * 90f + 45f, 0f), Vector3.one) * MeshKit.At(new Vector3(0f, 0f, 0.095f)));
            steering.Use(Surf.Polymer).Surface(Tube(-0.02f, 0.03f, 0.06f), 14, S(Black), Matrix4x4.identity, 0f, null, true, true);
        }

        // ------------------------------------------------------------------ Scout Quad

        private static void Quad(MeshKit k, MeshKit steering, VehicleModel model, VehicleStats v, Color paint)
        {
            float r = v.WheelRadius;
            model.Seats = new[] { new Vector3(0f, 0.95f, -0.2f) };
            model.SteeringPosition = new Vector3(0f, 1.1f, 0.42f);
            model.SteeringEuler = Vector3.zero;
            model.Colliders = new[] { new Bounds(new Vector3(0f, 0.62f, 0f), new Vector3(1.0f, 0.5f, 1.9f)) };

            k.Use(Surf.Metal).RoundBox(new Vector3(0.36f, 0.16f, 1.6f), 0.04f, S(Steel), MeshKit.At(new Vector3(0f, 0.45f, 0f)));
            k.Use(Surf.Lacquer).RoundBox(new Vector3(0.62f, 0.32f, 1.2f), 0.1f, S(paint), MeshKit.At(new Vector3(0f, 0.7f, 0.0f)));
            k.RoundBox(new Vector3(0.5f, 0.24f, 0.45f), 0.1f, S(paint), MeshKit.At(new Vector3(0f, 0.92f, 0.32f)));                   // tank
            k.RoundBox(new Vector3(0.8f, 0.18f, 0.5f), 0.08f, S(paint), MeshKit.At(new Vector3(0f, 0.82f, 0.82f), new Vector3(-15f, 0f, 0f))); // fairing
            for (int s = -1; s <= 1; s += 2)
                foreach (float z in new[] { model.Wheels[0].z, model.Wheels[2].z })
                    Arch(k.Use(Surf.Lacquer), new Vector3(s * v.TrackWidth * 0.5f, r, z), r + 0.08f, 0.32f, S(paint));
            k.Use(Surf.Leather).RoundBox(new Vector3(0.38f, 0.12f, 0.75f), 0.05f, S(SeatColor), MeshKit.At(model.Seats[0] + new Vector3(0f, -0.1f, 0.05f)));
            for (int s = -1; s <= 1; s += 2)
                Lamp(k, new Vector3(s * 0.2f, 0.88f, 1.06f), 0.06f, Head, new Vector3(0f, -0.15f, 1f));
            Lamp(k, new Vector3(0f, 0.8f, -0.95f), 0.05f, Tail, Vector3.back);
            k.Use(Surf.Metal);
            // Front and rear racks, exhaust.
            Pipe(k, Black, 0.02f, new Vector3(-0.35f, 0.9f, 0.7f), new Vector3(-0.35f, 0.98f, 0.95f), new Vector3(0.35f, 0.98f, 0.95f), new Vector3(0.35f, 0.9f, 0.7f));
            Pipe(k, Black, 0.02f, new Vector3(-0.38f, 0.92f, -0.55f), new Vector3(-0.38f, 0.95f, -0.95f), new Vector3(0.38f, 0.95f, -0.95f), new Vector3(0.38f, 0.92f, -0.55f));
            Pipe(k, Chrome, 0.03f, new Vector3(0.22f, 0.55f, -0.3f), new Vector3(0.3f, 0.6f, -0.8f), new Vector3(0.3f, 0.68f, -1.0f));
            k.Use(Surf.Circuit, Weave.Plain, 0f).Box(new Vector3(0.64f, 0.02f, 0.005f), G(Neon, 0.8f), MeshKit.At(new Vector3(0f, 0.62f, 0.605f)));
            k.Outline = 1f;
            Pipe(k, Steel, 0.025f, new Vector3(0f, 0.85f, 0.55f), model.SteeringPosition);

            // Handlebars with rubber grips and a glowing display.
            steering.Use(Surf.Metal).Tube(new[] { new Vector3(-0.38f, 0.04f, -0.06f), new Vector3(-0.2f, 0.02f, 0f), new Vector3(0.2f, 0.02f, 0f), new Vector3(0.38f, 0.04f, -0.06f) },
                0.016f, S(Chrome), Matrix4x4.identity, 10);
            for (int s = -1; s <= 1; s += 2)
                steering.Use(Surf.Rubber).Surface(Tube(0f, 0.12f, 0.022f), 12, S(Black), Matrix4x4.TRS(new Vector3(s * 0.32f, 0.03f, -0.04f), Quaternion.Euler(0f, 0f, s * -90f), Vector3.one), 0f, null, true, true);
            steering.Use(Surf.Circuit, Weave.Plain, 0f).RoundBox(new Vector3(0.12f, 0.06f, 0.01f), 0.005f, G(Neon, 0.8f), MeshKit.At(new Vector3(0f, 0.06f, 0.02f), new Vector3(-30f, 0f, 0f)));
        }
    }
}
