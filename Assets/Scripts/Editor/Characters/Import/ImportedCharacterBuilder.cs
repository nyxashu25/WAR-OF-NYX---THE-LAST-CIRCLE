using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarOfNyx.Characters;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters.Import
{
    /// <summary>How an imported model gets its skeleton.</summary>
    public enum RigSource
    {
        /// <summary>No skeleton in the file: the model must stand in a T-pose; joints are measured and weights computed.</summary>
        AutoTPose,
        /// <summary>The file has a skinned skeleton (Character Creator, Mixamo, VRoid...): its joints and weights are kept.</summary>
        EmbeddedSkin,
    }

    /// <summary>One third-party model and the roster look it replaces.</summary>
    public sealed class ImportedCharacterSpec
    {
        /// <summary>Roster name whose look the model replaces (KAI, AKIRA, YUNA, REN). Gameplay is unchanged.</summary>
        public string Slot;
        /// <summary>File name in ArtSource/Characters (kept out of source control).</summary>
        public string File;
        /// <summary>Height from the soles to the crown (metres).</summary>
        public float Height = 1.7f;
        public RigSource Rig;
        public Func<MotionStyle> Style = () => MotionStyle.Kai;
        public string[] Gear = new string[0];
        /// <summary>Triangle targets for the three detail levels (the nearest generated level at or under each is used).</summary>
        public int Lod0 = 38000, Lod1 = 12000, Lod2 = 5200;
        public int StandaloneTexture = 4096, AndroidTexture = 2048;
    }

    /// <summary>
    /// The models that replace roster looks. Files live in ArtSource/Characters (git-ignored: third-party art is never
    /// committed); a missing file leaves that character's previous look in place. Licences and attribution are read
    /// from each file and shown in game.
    /// </summary>
    public static class ImportedCharacterCatalog
    {
        /// <summary>
        /// Empty: the two supplied looks (YUNA from an Android 18 model, REN from a Tripo astronaut, imported 2026-10-17)
        /// were removed at the owner's request. Add an entry to import a model, e.g.
        /// <c>new ImportedCharacterSpec { Slot = "YUNA", File = "yuna.glb", Height = 1.66f, Rig = RigSource.EmbeddedSkin, Style = () => MotionStyle.Agile }</c>.
        /// </summary>
        public static readonly ImportedCharacterSpec[] All = new ImportedCharacterSpec[0];
    }

    /// <summary>
    /// Imports third-party character models (.glb) into the game (section 40 integration of supplied art): reads the
    /// file (geometry, materials, textures, skeleton, credit), normalises size and orientation, simplifies the dense
    /// source into three detail levels with Unity's Mesh LOD generator, rigs it on the shared humanoid skeleton (the
    /// file's own skeleton and weights, or measured joints and computed weights for a T-pose model), converts the
    /// materials to URP Lit with mobile texture settings, builds a validated Humanoid Avatar, authors the character's
    /// own movement style and writes the prefab and profiles the game loads for that roster slot. Every step and every
    /// measurement goes into an import report next to the generated assets.
    /// </summary>
    public static class ImportedCharacterBuilder
    {
        public const string SourceFolder = "ArtSource/Characters";

        [MenuItem("War of Nyx/Characters/Import supplied character models (ArtSource)", priority = 32)]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        /// <summary>Imports every catalogued model whose file is present. Returns the combined report.</summary>
        public static string BuildAll()
        {
            var report = new StringBuilder();
            RemoveStaleLooks(report);
            string only = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-importOnly")
                    only = args[i + 1];
            foreach (var spec in ImportedCharacterCatalog.All)
            {
                if (only != null && spec.Slot != only)
                    continue;
                string path = Path.Combine(SourceFolder, spec.File);
                if (!System.IO.File.Exists(path))
                {
                    report.AppendLine($"{spec.Slot}: skipped ({path} not present; the previous look stays).");
                    continue;
                }
                report.AppendLine(Build(spec, path));
            }
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        /// <summary>
        /// Removes imported looks that are no longer catalogued or whose file is gone, so the roster slot falls back to
        /// its previous look instead of keeping a stale model.
        /// </summary>
        public static void RemoveStaleLooks(StringBuilder report)
        {
            foreach (var look in CharacterRoster.All)
            {
                string profilePath = $"{CharacterAssetBuilder.ResourcesRoot}/{look.Name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>(profilePath);
                if (profile == null || profile.artSource == null || !profile.artSource.StartsWith("Imported"))
                    continue;
                var spec = Array.Find(ImportedCharacterCatalog.All, s => s.Slot == look.Name);
                if (spec != null && System.IO.File.Exists(Path.Combine(SourceFolder, spec.File)))
                    continue;
                AssetDatabase.DeleteAsset(profilePath);
                AssetDatabase.DeleteAsset($"{CharacterAssetBuilder.ResourcesRoot}/{look.Name}_Model.prefab");
                AssetDatabase.DeleteAsset($"{CharacterAssetBuilder.GeneratedRoot}/{look.Name}");
                report.AppendLine($"{look.Name}: imported look removed (no longer catalogued); the previous look is back.");
            }
        }

        /// <summary>Batch: import, then render studio previews of each imported character (-previewOut folder).</summary>
        public static void BuildAllAndRender()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (Array.IndexOf(args, "-noImport") < 0)
                    Debug.Log("[WarOfNyx][Import]\n" + BuildAll());
                string folder = "Temp/CharacterPreviews";
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-previewOut")
                        folder = args[i + 1];
                foreach (var spec in ImportedCharacterCatalog.All)
                    if (Array.IndexOf(args, "-noRender") < 0 && AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>($"{CharacterAssetBuilder.ResourcesRoot}/{spec.Slot}.asset") != null)
                        CharacterPreviewRender.Render(spec.Slot, folder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Batch QA of the VRM path without third-party files: writes synthetic VRM 1.0 and 0.x files from KAI's rigged
        /// body (<see cref="SyntheticVrm"/>), imports each into a temporary slot, renders studio previews (-previewOut),
        /// logs the import reports, then deletes the temporary assets.
        /// </summary>
        public static void SyntheticVrmSmoke()
        {
            try
            {
                string folder = "Temp/CharacterPreviews";
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-previewOut")
                        folder = args[i + 1];
                foreach (int version in new[] { 1, 0 })
                {
                    string slot = "VRMTEST" + version;
                    string path = $"Temp/SyntheticKai_vrm{version}.vrm";
                    SyntheticVrm.Write(path, version);
                    var spec = new ImportedCharacterSpec { Slot = slot, File = path, Height = 1.8f, Rig = RigSource.EmbeddedSkin, Style = () => MotionStyle.Kai };
                    Build(spec, path);
                    CharacterPreviewRender.Render(slot, folder);
                    AssetDatabase.DeleteAsset($"{CharacterAssetBuilder.ResourcesRoot}/{slot}.asset");
                    AssetDatabase.DeleteAsset($"{CharacterAssetBuilder.ResourcesRoot}/{slot}_Model.prefab");
                    AssetDatabase.DeleteAsset($"{CharacterAssetBuilder.GeneratedRoot}/{slot}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static string Build(ImportedCharacterSpec spec, string path)
        {
            var log = new StringBuilder();
            try
            {
                return Build(spec, path, log);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"{spec.Slot}: import of {Path.GetFileName(path)} failed: {e.Message}\nReport so far:\n{log}", e);
            }
        }

        private static string Build(ImportedCharacterSpec spec, string path, StringBuilder log)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            void Note(string line)
            {
                log.AppendLine(line);
            }
            string slot = spec.Slot;
            string dir = $"{CharacterAssetBuilder.GeneratedRoot}/{slot}";
            CharacterAssetBuilder.EnsureFolder(dir);
            CharacterAssetBuilder.EnsureFolder(CharacterAssetBuilder.ResourcesRoot);

            // 1. Read.
            var model = SourceModel.Load(path);
            Note($"Source: {Path.GetFileName(path)} ({new FileInfo(path).Length / 1e6:0.0} MB), {model.Credit}");
            Note($"  {model.VertexCount:N0} vertices, {model.SourceTriangles:N0} triangles, {model.Materials.Length} material(s), " +
                 $"{model.File.Array("images").Count} image(s), skin joints {model.SkinJoints.Count}, animations {model.File.Array("animations").Count}" +
                 $" (not used: the game's own clips drive the humanoid skeleton)");

            // 2. Size and orientation, skeleton conversion (A-pose to T-pose for rigged files), weld.
            float scale = model.Normalize(spec.Height);
            Note($"  Scaled ×{scale:0.###} to {spec.Height:0.00} m, feet on the ground, hips over the origin, facing +Z.");
            SkinConverter.Result converted = null;
            if (spec.Rig == RigSource.EmbeddedSkin)
            {
                if (!model.Skinned)
                    throw new InvalidOperationException($"{slot}: {path} has no skin; use RigSource.AutoTPose.");
                converted = SkinConverter.Convert(model);
                foreach (var line in converted.Notes)
                    Note("  " + line);
            }
            bool faceted = model.VertexCount >= model.SourceTriangles * 3 - 3;
            int removed = model.Weld(positionOnly: faceted);
            Note($"  Welded {removed:N0} duplicated vertices (exporter splits){(faceted ? ", faceted file: smooth normals rebuilt" : string.Empty)}.");

            // 3. Detail levels.
            var levels = LodBuilder.Generate(model);
            var lod = new[] { LodBuilder.Pick(levels, spec.Lod0), LodBuilder.Pick(levels, spec.Lod1), LodBuilder.Pick(levels, spec.Lod2) };
            Note($"  Mesh LOD generator: {levels.Count} levels ({string.Join(" / ", levels.ConvertAll(l => l.TriangleCount.ToString("N0")))} triangles)");
            Note($"  Chosen detail levels: {lod[0].TriangleCount:N0} / {lod[1].TriangleCount:N0} / {lod[2].TriangleCount:N0} triangles" +
                 (model.ShapeNames.Count > 0 ? $"; {model.ShapeNames.Count} blend shapes kept on the close-up level" : string.Empty));

            // 4. Joints measured on a dense level.
            LodLevel dense = levels[0];
            foreach (var l in levels)
            {
                if (Used(l).Length <= 160000)
                {
                    dense = l;
                    break;
                }
            }
            var densePoints = Array.ConvertAll(Used(dense), i => model.Positions[i]);
            var denseTriangles = new List<int>();
            foreach (var list in dense.Triangles)
                denseTriangles.AddRange(list);
            var fit = HumanoidFitter.FitTPose(model.Positions, denseTriangles);
            var heads = new Dictionary<string, Vector3>(fit.Heads);
            if (converted != null)
                foreach (var pair in converted.Heads)
                    heads[pair.Key] = pair.Value; // the file's own joints win
            if (converted != null && !heads.ContainsKey("LeftShoulder"))
                Note("  No clavicles in the file: placed by measurement.");
            var skeleton = NyxSkeleton.Fitted(heads, fit.Tails, converted?.Extras);
            Note($"  Skeleton: {skeleton.Bones.Count} bones (shared humanoid layout). Measurements on {densePoints.Length:N0} vertices (plus surface samples):");
            foreach (var line in fit.Notes)
                Note("    " + line);

            // 5. Skin weights on the finest level (the coarser levels use a subset of its vertices).
            var lod0Vertices = Used(lod[0]);
            BoneWeight[] weights;
            if (spec.Rig == RigSource.AutoTPose)
            {
                weights = AutoSkinner.Skin(skeleton, fit, model.Positions, lod0Vertices, lod[0].Triangles, smoothing: 4, fingers: fit.FingersMeasured);
                Note($"  Skin weights computed for {lod0Vertices.Length:N0} vertices (distance to bone over body-part radius, region rules, 4 smoothing passes)" +
                     (fit.FingersMeasured ? "; fingers measured and weighted." : "; fingers not separable: hands stay rigid."));
            }
            else
            {
                weights = ConvertWeights(model, converted, skeleton, lod0Vertices);
                int handVertices = 0;
                // Files without finger joints get measured fingers (the automatic weights handle up to 64 bones).
                if (fit.FingersMeasured && !converted.HasFingers && skeleton.Bones.Count <= 64)
                {
                    var auto = AutoSkinner.Skin(skeleton, fit, model.Positions, lod0Vertices, lod[0].Triangles, smoothing: 3, fingers: true);
                    for (int i = 0; i < lod0Vertices.Length; i++)
                    {
                        var p = model.Positions[lod0Vertices[i]];
                        int side = p.x < 0f ? -1 : 1;
                        string hand = side < 0 ? "LeftHand" : "RightHand";
                        if (side * p.x > fit.WristX[HumanoidFit.SideIndex(side)] + 0.01f && Share(weights[i], skeleton.IndexOf(hand)) > 0.6f)
                        {
                            weights[i] = auto[i];
                            handVertices++;
                        }
                    }
                }
                Note($"  Skin weights from the file ({model.SkinJoints.Count} joints merged onto the shared bones)" +
                     (converted.HasFingers ? "; the file's finger joints are kept." :
                         handVertices > 0 ? $"; {handVertices:N0} hand vertices re-weighted to measured fingers." : "; no finger joints: hands stay rigid."));
            }
            var weightOf = new Dictionary<int, BoneWeight>(lod0Vertices.Length);
            for (int i = 0; i < lod0Vertices.Length; i++)
                weightOf[lod0Vertices[i]] = weights[i];

            // 6. Textures and materials (URP Lit).
            var materials = BuildMaterials(model, spec, dir, slot, Note);

            // 7. Meshes.
            var bindposes = skeleton.BindPoses();
            var meshes = new Mesh[3];
            for (int l = 0; l < 3; l++)
            {
                // Facial blend shapes on the close-up level only (expressions are not read at a distance).
                meshes[l] = BuildMesh(model, lod[l], weightOf, bindposes, $"{slot}_LOD{l}", withShapes: l == 0);
                CharacterAssetBuilder.Save(meshes[l], $"{dir}/{slot}_LOD{l}.asset");
                meshes[l] = AssetDatabase.LoadAssetAtPath<Mesh>($"{dir}/{slot}_LOD{l}.asset");
            }

            // 8. Prefab: skeleton, three skinned renderers, LOD group, Avatar, Animator.
            var go = new GameObject(slot);
            try
            {
                var armature = new GameObject("Armature").transform;
                armature.SetParent(go.transform, false);
                var bones = skeleton.Instantiate(armature);
                var hips = bones[skeleton.IndexOf("Hips")];
                var renderers = new Renderer[3];
                for (int l = 0; l < 3; l++)
                {
                    var r = new GameObject($"{slot}_LOD{l}").AddComponent<SkinnedMeshRenderer>();
                    r.transform.SetParent(go.transform, false);
                    r.sharedMesh = meshes[l];
                    r.bones = bones;
                    r.rootBone = hips;
                    r.sharedMaterials = materials;
                    r.localBounds = new Bounds(new Vector3(0f, -0.05f * spec.Height, 0f), new Vector3(2.2f, 1.35f * spec.Height, 1.6f));
                    r.quality = l == 0 ? SkinQuality.Bone4 : SkinQuality.Bone2;
                    r.updateWhenOffscreen = false;
                    r.shadowCastingMode = l == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderers[l] = r;
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.16f, new[] { renderers[0] }), new LOD(0.05f, new[] { renderers[1] }), new LOD(0.0015f, new[] { renderers[2] }),
                });
                group.RecalculateBounds();
                var avatar = CharacterAssetBuilder.BuildAvatar(go, skeleton, slot);
                CharacterAssetBuilder.Save(avatar, $"{dir}/{slot}_Avatar.asset");
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>($"{dir}/{slot}_Avatar.asset");
                var animator = go.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // 9. The character's own movement style, authored and retargeted through the humanoid Avatar.
                var style = spec.Style();
                var controller = CharacterAssetBuilder.BakeStyle(style, dir, slot, out int clipCount);
                animator.runtimeAnimatorController = controller;
                Note($"  Avatar: humanoid={avatar.isHuman} valid={avatar.isValid}. Movement style '{style.Name}': {clipCount} humanoid clips + controller.");

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{CharacterAssetBuilder.ResourcesRoot}/{slot}_Model.prefab");

                // 10. Profiles the game loads for this roster slot.
                var animationProfile = ScriptableObject.CreateInstance<CharacterAnimationProfile>();
                animationProfile.controller = controller;
                float kaiShoulder = NyxSkeleton.Build(RigProportions.Lean)["LeftUpperArm"].Head.y;
                animationProfile.bodyScale = fit.ShoulderY / kaiShoulder;
                animationProfile.spineAimShare = 0.35f;
                CharacterAssetBuilder.Save(animationProfile, $"{dir}/{slot}_Animation.asset");
                animationProfile = AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>($"{dir}/{slot}_Animation.asset");
                var look = Array.Find(CharacterRoster.All, c => c.Name == slot);
                var profile = ScriptableObject.CreateInstance<CharacterVisualProfile>();
                profile.characterName = slot;
                profile.prefab = prefab;
                profile.animation = animationProfile;
                profile.energyColor = look != null ? look.Accent : Color.white;
                profile.headHeight = Mathf.Lerp(skeleton["Head"].Head.y, spec.Height, 0.4f);
                profile.faceSubmesh = -1;
                profile.artSource = $"Imported ({Path.GetFileName(path)}, ImportedCharacterBuilder)";
                profile.credit = $"Model: {model.Credit.Title} by {Author(model.Credit.Author)} ({LicenceName(model.Credit.License)})";
                profile.gearOverride = spec.Gear;
                if (converted != null)
                {
                    profile.springChains = converted.Springs.ToArray();
                    profile.springColliders = converted.Colliders.ToArray();
                }
                profile.faceShapes = FaceShapes(model);
                if (profile.faceShapes.Length > 0 || profile.springChains.Length > 0)
                    Note($"  Face: {profile.faceShapes.Length} expressions ({string.Join(", ", Array.ConvertAll(profile.faceShapes, f => f.preset))}); " +
                         $"secondary motion: {profile.springChains.Length} chains, {profile.springColliders.Length} colliders.");
                CharacterAssetBuilder.Save(profile, $"{CharacterAssetBuilder.ResourcesRoot}/{slot}.asset");
                AssetDatabase.SaveAssets();
                Note($"  Body scale (weapon carry) ×{animationProfile.bodyScale:0.00}; head height {profile.headHeight:0.00} m; built in {clock.Elapsed.TotalSeconds:0.0} s.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            string text = $"{slot} <- {Path.GetFileName(path)}\n{log}";
            System.IO.File.WriteAllText(Path.Combine(dir, $"{slot}_ImportReport.txt"), text);
            Debug.Log("[WarOfNyx][Import] " + text);
            return text;
        }

        // ------------------------------------------------------------------ helpers

        private static int[] Used(LodLevel level)
        {
            var seen = new HashSet<int>();
            var order = new List<int>();
            foreach (var list in level.Triangles)
                foreach (int v in list)
                    if (seen.Add(v))
                        order.Add(v);
            return order.ToArray();
        }

        private static float Share(BoneWeight w, int bone)
        {
            float s = 0f;
            if (w.boneIndex0 == bone) s += w.weight0;
            if (w.boneIndex1 == bone) s += w.weight1;
            if (w.boneIndex2 == bone) s += w.weight2;
            if (w.boneIndex3 == bone) s += w.weight3;
            return s;
        }

        private static BoneWeight[] ConvertWeights(SourceModel model, SkinConverter.Result converted, NyxSkeleton skeleton, int[] vertices)
        {
            var result = new BoneWeight[vertices.Length];
            var row = new float[skeleton.Bones.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                Array.Clear(row, 0, row.Length);
                foreach (var pair in SkinConverter.VertexBones(model, converted, vertices[i]))
                {
                    int b = skeleton.IndexOf(pair.Key);
                    if (b >= 0)
                        row[b] += pair.Value;
                }
                result[i] = AutoSkinner.Top4(row, 0, row.Length);
            }
            return result;
        }

        /// <summary>VRM expressions as blend-shape presets for <see cref="CharacterFacialAnimator"/>.</summary>
        private static FaceShapeDef[] FaceShapes(SourceModel model)
        {
            var result = new List<FaceShapeDef>();
            if (model.Vrm == null)
                return result.ToArray();
            foreach (var pair in model.Vrm.Expressions)
            {
                var shapes = new List<string>();
                var weights = new List<float>();
                foreach (var (mesh, target, weight) in pair.Value)
                {
                    if (!model.ShapeOf.TryGetValue((mesh, target), out var shape))
                        continue;
                    shapes.Add(shape);
                    weights.Add(weight);
                }
                if (shapes.Count > 0)
                    result.Add(new FaceShapeDef { preset = pair.Key, shapes = shapes.ToArray(), weights = weights.ToArray() });
            }
            return result.ToArray();
        }

        private static Mesh BuildMesh(SourceModel model, LodLevel level, Dictionary<int, BoneWeight> weightOf, Matrix4x4[] bindposes, string name, bool withShapes)
        {
            var order = Used(level);
            var local = new Dictionary<int, int>(order.Length);
            for (int i = 0; i < order.Length; i++)
                local[order[i]] = i;
            var mesh = new Mesh { name = name, indexFormat = order.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(Array.ConvertAll(order, i => model.Positions[i]));
            mesh.SetNormals(Array.ConvertAll(order, i => model.Normals[i]));
            if (model.Uv != null)
                mesh.SetUVs(0, Array.ConvertAll(order, i => model.Uv[i]));
            if (model.Colors != null)
                mesh.SetColors(Array.ConvertAll(order, i => model.Colors[i]));
            mesh.boneWeights = Array.ConvertAll(order, i => weightOf.TryGetValue(i, out var w) ? w : new BoneWeight { weight0 = 1f });
            mesh.bindposes = bindposes;
            mesh.subMeshCount = level.Triangles.Length;
            for (int s = 0; s < level.Triangles.Length; s++)
                mesh.SetTriangles(level.Triangles[s].ConvertAll(v => local[v]), s, false);
            if (withShapes)
            {
                foreach (var shape in model.ShapeNames)
                {
                    var deltas = model.ShapeDeltas[shape];
                    var normals = model.ShapeNormalDeltas[shape];
                    bool any = false;
                    foreach (int i in order)
                        if (deltas[i].sqrMagnitude > 1e-12f)
                        {
                            any = true;
                            break;
                        }
                    if (any)
                        mesh.AddBlendShapeFrame(shape, 100f, Array.ConvertAll(order, i => deltas[i]), Array.ConvertAll(order, i => normals[i]), null);
                }
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static string Author(string author)
        {
            int paren = author.IndexOf(" (", StringComparison.Ordinal);
            return paren > 0 ? author.Substring(0, paren) : author;
        }

        private static string LicenceName(string licence)
        {
            int paren = licence.IndexOf(" (", StringComparison.Ordinal);
            return paren > 0 ? licence.Substring(0, paren) : licence;
        }

        // ------------------------------------------------------------------ materials

        private static Material[] BuildMaterials(SourceModel model, ImportedCharacterSpec spec, string dir, string slot, Action<string> note)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                throw new InvalidOperationException("URP Lit shader not found.");
            CharacterAssetBuilder.EnsureFolder(dir + "/Textures");
            var result = new Material[model.Materials.Length];
            var written = new Dictionary<string, Texture2D>();
            for (int m = 0; m < model.Materials.Length; m++)
            {
                var src = model.Materials[m];
                if (model.Vrm != null && model.Vrm.Materials.TryGetValue(m, out var mtoon))
                {
                    result[m] = ToonMaterial(model, src, mtoon, m, spec, dir, slot, written, note);
                    continue;
                }
                var mat = new Material(lit) { name = $"{slot}_{m}_{Sanitize(src.Name)}" };
                mat.SetColor("_BaseColor", src.BaseColor);
                if (src.BaseColorImage >= 0)
                {
                    var tex = ImportImage(model, src.BaseColorImage, dir, slot, "BaseColor", srgb: true, normal: false, spec, written, note);
                    mat.SetTexture("_BaseMap", tex);
                }
                if (src.MetallicRoughnessImage >= 0)
                {
                    var tex = MetallicSmoothness(model, src, dir, slot, written, note);
                    mat.SetTexture("_MetallicGlossMap", tex);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", 1f);
                    mat.SetFloat("_Smoothness", 1f);
                }
                else
                {
                    mat.SetFloat("_Metallic", src.Metallic * 0.5f);
                    mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - src.Roughness) * 0.6f);
                }
                if (src.NormalImage >= 0)
                {
                    var tex = ImportImage(model, src.NormalImage, dir, slot, "Normal", srgb: false, normal: true, spec, written, note);
                    mat.SetTexture("_BumpMap", tex);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetFloat("_Cull", src.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);
                mat.doubleSidedGI = src.DoubleSided;
                if (src.AlphaMode != "OPAQUE")
                {
                    // Characters render opaque; transparency in the file becomes a cut-out (no sorting problems).
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", src.AlphaMode == "MASK" ? src.AlphaCutoff : 0.5f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                CharacterAssetBuilder.Save(mat, $"{dir}/{mat.name}.mat");
                result[m] = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{mat.name}.mat");
                note($"  Material {m} '{src.Name}': URP Lit, base colour {(src.BaseColorImage >= 0 ? "map" : "factor")}, metallic/smoothness " +
                     $"{(src.MetallicRoughnessImage >= 0 ? "map (converted)" : "factors")}, {(src.DoubleSided ? "two-sided" : "one-sided")}, {src.AlphaMode.ToLowerInvariant()}");
            }
            return result;
        }

        /// <summary>
        /// MToon (VRM) → WarOfNyx/NyxCharacterToon: base and shade maps, the shade step from MToon's shift / toony,
        /// outline width and colour, emission, and the render state for opaque, cut-out (hair, lashes) and blended
        /// (eye highlights) parts.
        /// </summary>
        private static Material ToonMaterial(SourceModel model, SourceMaterial src, VrmMaterial mtoon, int m, ImportedCharacterSpec spec, string dir,
            string slot, Dictionary<string, Texture2D> written, Action<string> note)
        {
            var shader = Shader.Find("WarOfNyx/NyxCharacterToon");
            if (shader == null)
                throw new InvalidOperationException("WarOfNyx/NyxCharacterToon shader missing.");
            var mat = new Material(shader) { name = $"{slot}_{m}_{Sanitize(src.Name)}" };
            mat.SetColor("_BaseColor", src.BaseColor);
            Texture2D baseMap = null;
            if (src.BaseColorImage >= 0)
            {
                baseMap = ImportImage(model, src.BaseColorImage, dir, slot, "BaseColor", srgb: true, normal: false, spec, written, note);
                mat.SetTexture("_BaseMap", baseMap);
            }
            var shadeMap = mtoon.ShadeImage >= 0 && mtoon.ShadeImage != src.BaseColorImage
                ? ImportImage(model, mtoon.ShadeImage, dir, slot, "Shade", srgb: true, normal: false, spec, written, note)
                : baseMap;
            if (shadeMap != null)
                mat.SetTexture("_ShadeMap", shadeMap);
            mat.SetColor("_ShadeColor", mtoon.ShadeColor);
            mat.SetFloat("_ShadeThreshold", Mathf.Clamp(-mtoon.ShadingShift, -1f, 1f));
            mat.SetFloat("_ShadeSoftness", Mathf.Clamp((1f - mtoon.ShadingToony) * 0.5f + 0.01f, 0.01f, 0.5f));
            mat.SetColor("_OutlineColor", mtoon.OutlineColor);
            mat.SetFloat("_OutlineWidth", mtoon.OutlineWidth);
            if (src.EmissiveImage >= 0)
                mat.SetTexture("_EmissionMap", ImportImage(model, src.EmissiveImage, dir, slot, "Emission", srgb: true, normal: false, spec, written, note));
            mat.SetColor("_EmissionColor", src.EmissiveFactor);
            string mode = mtoon.BlendMode ?? src.AlphaMode;
            float cull = model.Vrm.Version == 0 ? mtoon.Cull : src.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back;
            mat.SetFloat("_Cull", cull);
            mat.SetFloat("_Cutoff", mode == "MASK" ? (mtoon.BlendMode != null ? mtoon.Cutoff : src.AlphaCutoff) : 0.5f);
            if (mode == "BLEND")
            {
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent + m; // file order: highlights after the eyes
                mat.SetShaderPassEnabled("SRPDefaultUnlit", false);   // no outline on overlays
                mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                if (mode == "MASK")
                {
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (mtoon.OutlineWidth <= 0f)
                    mat.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
            CharacterAssetBuilder.Save(mat, $"{dir}/{mat.name}.mat");
            note($"  Material {m} '{src.Name}': MToon -> NyxCharacterToon, {mode.ToLowerInvariant()}, " +
                 $"shade step {-mtoon.ShadingShift:0.00}, outline {(mtoon.OutlineWidth > 0f ? (mtoon.OutlineWidth * 1000f).ToString("0.0") + " mm" : "off")}");
            return AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{mat.name}.mat");
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "material";
        }

        private static Texture2D ImportImage(SourceModel model, int image, string dir, string slot, string kind, bool srgb, bool normal,
            ImportedCharacterSpec spec, Dictionary<string, Texture2D> written, Action<string> note)
        {
            string key = kind + image;
            if (written.TryGetValue(key, out var existing))
                return existing;
            var bytes = model.File.ImageBytes(image, out string mime);
            string ext = mime == "image/jpeg" ? "jpg" : "png";
            string path = $"{dir}/Textures/{slot}_{kind}_{image}.{ext}";
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.maxTextureSize = spec.StandaloneTexture;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = spec.AndroidTexture, format = TextureImporterFormat.ASTC_6x6,
            });
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            note($"  Texture {kind} #{image}: {w}×{h} {ext.ToUpperInvariant()} source -> {Mathf.Min(spec.StandaloneTexture, w)} px on PC, " +
                 $"{Mathf.Min(spec.AndroidTexture, w)} px ASTC 6×6 on Android");
            written[key] = tex;
            return tex;
        }

        /// <summary>glTF packs roughness in G and metalness in B; URP Lit wants metalness in R and smoothness in A.</summary>
        private static Texture2D MetallicSmoothness(SourceModel model, SourceMaterial src, string dir, string slot, Dictionary<string, Texture2D> written,
            Action<string> note)
        {
            string key = "MS" + src.MetallicRoughnessImage;
            if (written.TryGetValue(key, out var existing))
                return existing;
            var bytes = model.File.ImageBytes(src.MetallicRoughnessImage, out _);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            source.LoadImage(bytes, false);
            int factor = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(source.width, source.height) / 2048f));
            int w = source.width / factor, h = source.height / factor;
            var input = source.GetPixels32();
            var output = new Color32[w * h];
            double metalSum = 0, roughSum = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int g = 0, b = 0, n = 0;
                    for (int dy = 0; dy < factor; dy++)
                        for (int dx = 0; dx < factor; dx++)
                        {
                            var c = input[(y * factor + dy) * source.width + x * factor + dx];
                            g += c.g;
                            b += c.b;
                            n++;
                        }
                    float rough = Mathf.Clamp01(g / (255f * n) * src.Roughness);
                    float metal = Mathf.Clamp01(b / (255f * n) * src.Metallic);
                    metalSum += metal;
                    roughSum += rough;
                    output[y * w + x] = new Color32((byte)(metal * 255f), 0, 0, (byte)((1f - rough) * 255f));
                }
            UnityEngine.Object.DestroyImmediate(source);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(output);
            string path = $"{dir}/Textures/{slot}_MetallicSmoothness_{src.MetallicRoughnessImage}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6,
            });
            importer.SaveAndReimport();
            note($"  Metallic/roughness map converted to URP metallic/smoothness ({w}×{h}); mean metalness {metalSum / (w * h):0.00}, mean roughness {roughSum / (w * h):0.00}");
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            written[key] = result;
            return result;
        }
    }
}
