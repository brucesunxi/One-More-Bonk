using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreBonk.Rebuild.Visuals
{
    /// <summary>
    /// Redwarm / 红温暴走, the first complete procedural 3D weapon family.
    /// Every piece is a closed solid (including the trumpet's inner wall).
    /// X goes from the grip to the tip; XY is the silhouette; Z is thickness.
    /// This is an art-directed game sample, not a photorealistic sculpt of the reference.
    /// </summary>
    public static class RageWeaponGeometry
    {
        public static readonly int[] NominalLevels = { 0, 2, 4, 9, 15, 22, 29, 45, 60, 75, 90, 99 };
        public static readonly string[] StageNames = { "断钝短剑", "细剑", "宽剑", "愤怒面罩", "膨胀红腮", "尖帽长啸", "蒸汽怒球", "双撑活塞", "单侧号角", "三重号角", "炉柱顶叉", "暴走锯轮" };
        private const string Prefix = "OMB_Redwarm_";
        private const float Epsilon = 0.0000001f;
        private static Material steel, darkSteel, red, brass, grip, teeth, mouth, ember;
        private static readonly Material[] RouteMetals = new Material[3];
        private static readonly Material[] RouteCores = new Material[3];

        private sealed class Builder
        {
            public readonly GameObject Root;
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public readonly List<GameObject> Parts = new List<GameObject>();
            private readonly List<SurfaceSample> Surfaces = new List<SurfaceSample>();
            public readonly Material Metal, Core;
            private sealed class SurfaceSample
            {
                public Vector3[] Vertices;
                public int[] Triangles;
                public Vector3 Position;
            }
            public Builder(GameObject root, int route, bool awakened)
            { Root = root; Metal = awakened ? RouteMetals[route] : steel; Core = RouteCores[route]; }
            public void Add(string name, Mesh mesh, Material material, Vector3 position)
            {
                mesh.name = Prefix + name;
                Meshes.Add(mesh);
                var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(Root.transform, false);
                part.transform.localPosition = position;
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
                Parts.Add(part);
                Surfaces.Add(new SurfaceSample { Vertices = mesh.vertices, Triangles = mesh.triangles, Position = position });
            }
            public void Plate(string name, Vector2[] polygon, float thickness, Material material, float z = 0f)
            { Add(name, Extrude(polygon, thickness, true), material, new Vector3(0f, 0f, z)); }
            public void Box(string name, Vector3 center, Vector3 size, Material material)
            { Add(name, Extrude(P(-size.x / 2,-size.y / 2, size.x / 2,-size.y / 2, size.x / 2,size.y / 2, -size.x / 2,size.y / 2), size.z, true), material, center); }
            public void Cylinder(string name, Vector3 center, Vector3 direction, float length, float radius, Material material, int sides = 20)
            { Add(name, Lathe(direction.normalized, new[] { new Vector2(-length / 2,0), new Vector2(-length / 2,radius), new Vector2(length / 2,radius), new Vector2(length / 2,0) }, sides), material, center); }
            public void Ball(string name, Vector3 center, Vector3 radius, Material material)
            { Add(name, Ellipsoid(radius, 20, 10), material, center); }
            public void Rod(string name, Vector3 a, Vector3 b, float radius, Material material)
            { Cylinder(name, (a + b) / 2, b - a, Vector3.Distance(a, b), radius, material, 12); }
            public void Stud(string name, float x, float y, float z, float radius = .045f)
            {
                int face=z<0?-1:1;
                // Fasten to the actual existing casting, rather than a fixed
                // front-plane estimate. A .06-deep head penetrates its support
                // by .01. Unsupported perimeter candidates are omitted.
                if(!SurfaceDepth(x,y,face,out float surface))return;
                Cylinder(name, new Vector3(x,y,surface+face*.020f), Vector3.forward, .060f, radius, brass, 8);
            }
            private bool SurfaceDepth(float x,float y,int face,out float surface)
            {
                surface=face<0?float.PositiveInfinity:float.NegativeInfinity;bool found=false;
                foreach(var sample in Surfaces)
                {
                    var vertices=sample.Vertices;var triangles=sample.Triangles;Vector3 offset=sample.Position;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        Vector3 a=vertices[triangles[i]]+offset,b=vertices[triangles[i+1]]+offset,c=vertices[triangles[i+2]]+offset;
                        if(x<Mathf.Min(a.x,Mathf.Min(b.x,c.x))-.00001f||x>Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.00001f||y<Mathf.Min(a.y,Mathf.Min(b.y,c.y))-.00001f||y>Mathf.Max(a.y,Mathf.Max(b.y,c.y))+.00001f)continue;
                        float abX=b.x-a.x,abY=b.y-a.y,acX=c.x-a.x,acY=c.y-a.y;
                        float determinant=abX*acY-abY*acX;if(Mathf.Abs(determinant)<Epsilon)continue;
                        float u=((x-a.x)*acY-(y-a.y)*acX)/determinant,v=(abX*(y-a.y)-abY*(x-a.x))/determinant;
                        if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                        float hit=a.z+u*(b.z-a.z)+v*(c.z-a.z);
                        surface=face<0?Mathf.Min(surface,hit):Mathf.Max(surface,hit);found=true;
                    }
                }
                return found;
            }
            public void Both(string name, Vector2[] polygon, float thickness, float depth, Material material)
            { Plate(name + " Front", polygon, thickness, material, -depth); Plate(name + " Back", polygon, thickness, material, depth); }
            public void Finish()
            {
                var groups = new Dictionary<Material, List<CombineInstance>>();
                foreach (var part in Parts)
                {
                    var material = part.GetComponent<MeshRenderer>().sharedMaterial;
                    if (!groups.TryGetValue(material, out var instances)) { instances = new List<CombineInstance>(); groups.Add(material, instances); }
                    instances.Add(new CombineInstance { mesh = part.GetComponent<MeshFilter>().sharedMesh,
                        transform = Root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix });
                }
                foreach (var group in groups)
                {
                    var mesh = new Mesh { name = Prefix + group.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(group.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var part = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(Root.transform, false);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    part.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
                }
                foreach (var part in Parts) { part.SetActive(false); Dispose(part); }
                foreach (var mesh in Meshes) Dispose(mesh);
            }
        }

        /// <param name="stage">Index into NominalLevels, from 0 to 11.</param>
        /// <param name="awakened">At +80 and above, enables metal/core route variants. Red face armour is retained.</param>
        public static GameObject Create(int stage, int materialRoute = 0, bool awakened = false)
        {
            stage = Mathf.Clamp(stage, 0, NominalLevels.Length - 1);
            materialRoute = Mathf.Clamp(materialRoute, 0, 2);
            EnsureMaterials();
            var root = new GameObject("红温暴走 +" + NominalLevels[stage] + " — " + StageNames[stage]);
            // A +75 form persists through +80..+89, so its explicit awakening
            // flag can switch route metals before the +90 silhouette arrives.
            // Earlier silhouettes keep their initial red/iron palette.
            var b = new Builder(root, materialRoute, awakened && stage >= 9);
            BuildGrip(b, stage);
            switch (stage)
            {
                case 0: Broken(b); break;
                case 1: Rapier(b); break;
                case 2: Broad(b); break;
                case 3: MaskBlade(b); break;
                case 4: CheekBlade(b); break;
                case 5: ShoutingBlade(b); break;
                case 6: SteamBall(b); break;
                case 7: PistonFrame(b); break;
                case 8: OneHorn(b); break;
                case 9: ThreeHorns(b); break;
                case 10: FurnaceColumn(b); break;
                default: SawWheel(b); break;
            }
            // A real closed metal landing at the common strike position, on
            // both faces. All forms therefore preserve exactly .68 thickness
            // here during a contact-time form swap, including the open mouths.
            // Late mouths integrate it as a central furnace tongue/bridge.
            b.Box("Solid forge bridge", new Vector3(.6f,0,0), new Vector3(.22f,.15f,.68f), stage < 3 ? b.Metal : darkSteel);
            b.Both("Forge bridge cap", P(.51f,-.06f, .69f,-.06f, .69f,.06f, .51f,.06f), .014f, .333f, stage < 3 ? b.Metal : brass);
            // A small core crystal on the grip collar hints at the selected
            // route before awakening without repainting the red face armour.
            b.Both("Latent route pin", P(-.99f,-.036f, -.89f,-.036f, -.85f,0, -.89f,.036f, -.99f,.036f), .015f, .175f, b.Core);
            b.Finish();
            Socket(root, "GripSocket", new Vector3(-1.75f,0,0));
            Socket(root, "DisplayPivot", Vector3.zero);
            Socket(root, "ForgeContact", new Vector3(.6f,0,-.34f));
            return root;
        }

        private static void BuildGrip(Builder b, int stage)
        {
            b.Cylinder("Black wrapped grip", new Vector3(-1.73f,0,0), Vector3.right, 1.24f, .12f, grip, 18);
            for (int i = 0; i < 9; i++)
            {
                float x = -2.27f + i * .133f;
                b.Cylinder("Grip binding", new Vector3(x,0,0), new Vector3(1,.09f,.04f), .04f, .133f, darkSteel, 14);
            }
            b.Cylinder("Pommel polygon", new Vector3(-2.46f,0,0), Vector3.right, .20f, stage == 0 ? .17f : .20f, stage < 3 ? steel : brass, stage < 2 ? 8 : 6);
            b.Cylinder("Pommel inset", new Vector3(-2.565f,0,0), Vector3.right, .022f, .085f, darkSteel, 8);
            b.Cylinder("Grip shoulder", new Vector3(-1.075f,0,0), Vector3.right, .19f, .18f, stage < 3 ? steel : brass, 12);
            b.Cylinder("Grip neck", new Vector3(-.94f,0,0), Vector3.right, .17f, .14f, darkSteel, 16);
            b.Box("Guard crosspiece", new Vector3(-.89f,0,0), new Vector3(.14f, stage == 0 ? .56f : stage < 3 ? .82f : .57f,.30f), stage < 3 ? steel : brass);
            for (int face = -1; face <= 1; face += 2) b.Stud("Guard pin", -.89f, 0, face * .17f, .06f);
        }

        private static void Broken(Builder b)
        {
            b.Plate("Chipped unfinished steel", P(-.85f,-.19f, .96f,-.19f, 1.13f,-.06f, 1.06f,.12f, .90f,.09f, .95f,.20f, .76f,.11f, .71f,.23f, .51f,.17f, -.85f,.18f), .22f, steel);
            b.Both("Broken forging scar", P(-.65f,-.018f, .34f,-.024f, .76f,.015f, .28f,.028f, -.65f,.018f), .012f, .113f, darkSteel);
            b.Both("Exposed rough chip", P(.55f,.14f, .73f,.15f, .71f,.23f), .016f, .115f, brass);
        }
        private static void Rapier(Builder b)
        {
            b.Plate("Slender steel point", P(-.86f,-.105f, 2.13f,-.12f, 2.67f,0, 2.13f,.12f, -.86f,.105f), .20f, steel);
            b.Both("Raised sword ridge", P(-.65f,-.026f, 2.05f,-.032f, 2.43f,0, 2.05f,.032f, -.65f,.026f), .05f, .10f, darkSteel);
        }
        private static void Broad(Builder b)
        {
            b.Plate("Forged broad blade", P(-.85f,-.23f, -.15f,-.26f, 1.91f,-.32f, 2.67f,0, 1.91f,.32f, .40f,.27f, -.85f,.23f), .24f, steel);
            b.Both("Broad faceted spine", P(-.58f,-.045f, 1.93f,-.07f, 2.40f,0, 1.93f,.07f, -.58f,.045f), .06f, .125f, darkSteel);
            b.Both("Broad guard flare", P(-.96f,-.46f, -.82f,-.43f, -.66f,-.26f, -.67f,.26f, -.82f,.43f, -.96f,.46f), .04f, .16f, steel);
        }
        private static void MaskBlade(Builder b)
        {
            b.Plate("Short angled cleaver", P(-.80f,-.39f, 1.86f,-.37f, 2.13f,.31f, 1.58f,.23f, 1.51f,.38f, -.80f,.39f), .30f, b.Metal);
            b.Both("Upper red cleaver panel", P(.90f,.00f, 1.80f,-.22f, 1.96f,.20f, 1.57f,.17f, 1.38f,.28f, .95f,.28f), .04f, .16f, red);
            Face(b, 1.10f, -.66f, .43f, false, .245f);
            b.Both("Blade vent slot", P(1.00f,-.15f, 1.56f,-.17f, 1.61f,-.09f, 1.08f,-.08f), .028f, .183f, brass);
        }
        private static void CheekBlade(Builder b)
        {
            b.Plate("Cheek sword steel", P(-.8f,-.27f, 1.98f,-.23f, 2.65f,0, 1.98f,.23f, -.8f,.27f), .28f, b.Metal);
            b.Ball("Left swollen red cheek", new Vector3(.18f,-.62f,0), new Vector3(.61f,.45f,.41f), red);
            b.Ball("Right swollen red cheek", new Vector3(.18f,.62f,0), new Vector3(.61f,.45f,.41f), red);
            Face(b, 1.10f, -.68f, .47f, false, .265f);
            b.Cylinder("Blade crown collar", new Vector3(1.17f,0,0), Vector3.right, .11f, .30f, brass, 10);
            CheekRivets(b, .18f, .62f);
        }
        private static void ShoutingBlade(Builder b)
        {
            b.Plate("Long mouth iron backing", P(-.76f,-.41f, 1.45f,-.43f, 1.93f,0, 1.45f,.43f, -.76f,.41f), .50f, darkSteel);
            b.Plate("Left tall iron fork", P(1.12f,-.44f, 2.28f,-.43f, 2.72f,-.19f, 2.20f,-.24f, 1.45f,-.20f), .29f, b.Metal);
            b.Plate("Right tall iron fork", P(1.45f,.20f, 2.20f,.24f, 2.72f,.19f, 2.28f,.43f, 1.12f,.44f), .29f, b.Metal);
            b.Both("Pointed red helmet", P(1.13f,-.44f, 2.08f,0, 1.13f,.44f, .87f,.33f, .94f,0, .87f,-.33f), .10f, .28f, red);
            Face(b, 1.50f, -.66f, .42f, true, .265f);
            FrameMouth(b, -.57f, .73f, .33f, .30f);
            b.Both("Helmet center crest", P(1.52f,-.04f, 1.98f,0, 1.52f,.04f), .02f, .341f, brass);
        }
        private static void SteamBall(Builder b)
        {
            b.Ball("Round red pressure vessel", new Vector3(.36f,0,0), new Vector3(1.05f,1.00f,.255f), red);
            b.Cylinder("Pressure iron equator", new Vector3(.37f,0,0), Vector3.forward, .32f, .92f, darkSteel, 32);
            b.Ball("Front red pressure dome", new Vector3(.37f,0,-.17f), new Vector3(.88f,.92f,.09f), red);
            b.Ball("Back red pressure dome", new Vector3(.37f,0,.17f), new Vector3(.88f,.92f,.09f), red);
            Face(b, 1.05f, -.64f, .60f, false, .269f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 origin = new Vector3(1.02f, side * .62f, 0);
                b.Cylinder("Steam exhaust copper", origin, new Vector3(.85f,side * .40f,0), .59f, .145f, brass, 12);
                for (int i = 0; i < 3; i++)
                    for (int face = -1; face <= 1; face += 2) b.Stud("Exhaust perforation", 1.02f + i * .13f, side * (.60f + i * .055f), face * .135f, .031f);
            }
            b.Cylinder("Steam cap", new Vector3(1.40f,0,0), Vector3.right, .12f, .40f, brass);
            b.Cylinder("Cap iron band", new Vector3(1.44f,0,0), Vector3.right, .06f, .41f, darkSteel);
            b.Add("Lifting ring", Torus(.13f,.033f,20,8), brass, new Vector3(1.66f,0,0));
            PerimeterRivets(b, new Vector2(.36f,0), .91f, .89f, .286f, 12);
        }
        private static void PistonFrame(Builder b)
        {
            b.Plate("Piston lower iron chassis", P(-.78f,-.60f, .89f,-.57f, 1.10f,-.32f, 1.10f,.32f, .89f,.57f, -.78f,.60f), .48f, darkSteel);
            Face(b, .94f, -.68f, .55f, false, .250f);
            b.Cylinder("Central brass piston", new Vector3(1.35f,0,0), Vector3.right, 1.49f, .22f, brass);
            b.Cylinder("Piston iron upper collar", new Vector3(1.92f,0,0), Vector3.right, .28f, .27f, darkSteel);
            b.Cylinder("Piston upper rod", new Vector3(2.28f,0,0), Vector3.right, .50f, .12f, red);
            b.Cylinder("Piston crown", new Vector3(2.57f,0,0), Vector3.right, .13f, .21f, brass);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 bottom = new Vector3(.08f,side * .68f,0), top = new Vector3(1.97f,side * .67f,0);
                b.Rod("Outer diagonal steel brace", bottom, top, .095f, b.Metal);
                b.Rod("Inner piston rod", new Vector3(.27f,side * .43f,0), new Vector3(1.83f,side * .58f,0), .048f, brass);
                b.Cylinder("Lower rotating hinge", bottom, Vector3.forward, .61f, .21f, darkSteel, 12);
                b.Cylinder("Lower brass pivot", bottom, Vector3.forward, .66f, .10f, brass, 10);
                b.Both("Red top brace fin", P(1.67f,side * .51f, 1.89f,side * .81f, 2.21f,side * .90f, 2.15f,side * .51f), .09f, .12f, red);
                b.Cylinder("Upper joint", top, Vector3.forward, .40f, .15f, darkSteel, 10);
            }
            for (int i = 0; i < 5; i++)
                for (int face = -1; face <= 1; face += 2) b.Stud("Piston rivet", 1.10f + i * .15f, 0, face * .221f, .024f);
        }
        private static void OneHorn(Builder b)
        {
            b.Plate("Horn mask body", P(-.77f,-.47f, .82f,-.49f, 1.17f,-.26f, 1.29f,.12f, .91f,.50f, -.77f,.48f), .50f, darkSteel);
            Face(b, 1.12f, -.69f, .48f, false, .269f);
            b.Ball("Horn red pressure neck", new Vector3(.44f,.56f,0), new Vector3(.50f,.36f,.34f), red);
            Horn(b, "Large right horn", new Vector3(.50f,.49f,-.03f), new Vector3(0, .83f,-.55f), .68f,.64f);
            b.Cylinder("Mask valve crown", new Vector3(1.25f,-.18f,0), Vector3.right, .21f, .23f, brass, 12);
            PerimeterRivets(b, new Vector2(.15f,-.15f), .75f,.47f,.29f,8);
        }
        private static void ThreeHorns(Builder b)
        {
            b.Plate("Three horn iron mask", P(-.76f,-.43f, 1.04f,-.44f, 1.34f,0, 1.04f,.44f, -.76f,.43f), .50f, darkSteel);
            Face(b, 1.09f, -.67f, .47f, false, .269f);
            Horn(b, "Left horn", new Vector3(.45f,-.42f,0), new Vector3(0,-.84f,-.54f),.67f,.56f);
            Horn(b, "Right horn", new Vector3(.45f,.42f,0), new Vector3(0,.84f,-.54f),.67f,.56f);
            Horn(b, "Crown horn", new Vector3(1.13f,0,0), new Vector3(.89f,0,-.46f),.91f,.60f);
            b.Cylinder("Bottom horn clamp", new Vector3(-.54f,0,0), Vector3.right,.14f,.50f,brass,12);
        }
        private static void FurnaceColumn(Builder b)
        {
            b.Cylinder("Furnace column core", new Vector3(.19f,0,0), Vector3.right, 1.83f,.265f,red);
            for (int i = 0; i < 6; i++)
            {
                float x = -.68f + i * .29f;
                b.Cylinder("Layered furnace iron flange", new Vector3(x,0,0),Vector3.right,.075f,.52f-i * .033f,darkSteel,16);
                b.Cylinder("Layered furnace brass lip", new Vector3(x-.05f,0,0),Vector3.right,.035f,.54f-i * .033f,brass,16);
                for (int face = -1; face <= 1; face += 2)
                    b.Box("Column glowing slot",new Vector3(x+.095f,0,face*.269f),new Vector3(.10f,.13f,.022f),b.Core);
            }
            b.Plate("Upper furnace mask backing",P(.79f,-.44f, 1.98f,-.39f, 2.15f,0, 1.98f,.39f, .79f,.44f),.50f,darkSteel);
            Face(b,2.03f,.79f,.42f,true,.269f);
            b.Plate("Left upper fork",P(1.86f,-.42f,2.32f,-.45f,2.85f,-.15f,2.28f,-.24f,1.88f,-.21f),.29f,b.Metal);
            b.Plate("Right upper fork",P(1.88f,.21f,2.28f,.24f,2.85f,.15f,2.32f,.45f,1.86f,.42f),.29f,b.Metal);
            b.Both("Fork red inset",P(2.00f,-.32f,2.27f,-.34f,2.54f,-.23f,2.26f,-.26f,2.02f,-.24f),.04f,.16f,red);
            b.Both("Fork red inset mirrored",P(2.02f,.24f,2.26f,.26f,2.54f,.23f,2.27f,.34f,2.00f,.32f),.04f,.16f,red);
            b.Box("Forge column face landing",new Vector3(.60f,0,0),new Vector3(.23f,.15f,.58f),darkSteel);
            b.Cylinder("Bottom column flange",new Vector3(-.79f,0,0),Vector3.right,.11f,.58f,brass,12);
        }
        private static void SawWheel(Builder b)
        {
            b.Cylinder("Heavy closed iron saw hub",new Vector3(.62f,0,0),Vector3.forward,.41f,1.27f,darkSteel,48);
            b.Add("Outer red saw gear",Extrude(Gear(new Vector2(.62f,0),1.26f,1.43f,24),.25f,true),red,Vector3.zero);
            b.Cylinder("Inner metal wheel face",new Vector3(.62f,0,0),Vector3.forward,.45f,1.17f,b.Metal,48);
            b.Cylinder("Red inner face dome",new Vector3(.62f,0,0),Vector3.forward,.49f,.98f,red,40);
            for (int face = -1; face <= 1; face += 2)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2 / 12;
                    var radial = new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    var tangent = new Vector2(-radial.y,radial.x);
                    var center = new Vector2(.62f,0)+radial * 1.03f;
                    b.Plate("Radial saw armour",new[] {center-radial*.13f-tangent*.065f,center+radial*.22f-tangent*.065f,center+radial*.20f+tangent*.085f,center-radial*.14f+tangent*.085f},.045f,red,face*.252f);
                    b.Stud("Saw rim bolt",center.x,center.y,face*.296f,.041f);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    var center = new Vector3(.60f,side*.75f,face*.31f);
                    b.Cylinder("Brass cheek gear",center,Vector3.forward,.24f,.28f,brass,24);
                    b.Cylinder("Cheek steel axle",new Vector3(center.x,center.y,face*.455f),Vector3.forward,.08f,.12f,darkSteel,12);
                    b.Cylinder("Cheek axle cap",new Vector3(center.x,center.y,face*.50f),Vector3.forward,.045f,.075f,brass,10);
                }
            }
            Face(b,1.48f,-.52f,.59f,true,.272f);
            b.Both("Saw nose crest",P(1.19f,-.20f,1.66f,0,1.19f,.20f,1.05f,0),.075f,.283f,red);
        }

        // Masks have real eye sockets, raised brows, cast nose, tooth solids,
        // red rims, and a closed iron back. The face is on both sides so the
        // collectible remains expressive through a full 360-degree rotation.
        private static void Face(Builder b,float top,float bottom,float width,bool open,float depth)
        {
            float h=top-bottom, eye=top-h*.27f, mouthTop=top-h*.49f, mouthBottom=bottom+h*.13f;
            b.Plate("Closed mask iron casting",P(bottom,-width*.78f, bottom+.10f,-width, top-.24f,-width, top,0, top-.24f,width, bottom+.10f,width, bottom,width*.78f),depth*2,darkSteel);
            for (int side=-1;side<=1;side+=2)
            {
                float s=side;
                b.Both("Angular red cheek",P(bottom+.08f,s*width*.78f, mouthTop-.08f,s*width*.58f, eye+.09f,s*width*.87f, top-.21f,s*width, top-.30f,s*width*.45f, mouthTop,s*width*.33f, bottom+.20f,s*width*.46f),.06f,depth+.018f,red);
                b.Both("Deep black eye socket",P(eye-.10f,s*width*.15f, eye-.05f,s*width*.72f, eye+.17f,s*width*.90f, eye+.03f,s*width*.29f),.034f,depth+.030f,mouth);
                b.Both("Gold angry eye",P(eye-.074f,s*width*.21f, eye-.025f,s*width*.68f, eye+.071f,s*width*.72f, eye-.018f,s*width*.30f),.024f,depth+.054f,teeth);
                b.Both("Sharp red angry eyebrow",P(eye+.015f,s*width*.04f, eye+.23f,s*width*.87f, eye+.38f,s*width*.97f, eye+.19f,s*width*.19f),.065f,depth+.072f,red);
                b.Both("Steel eyebrow edge",P(eye+.00f,s*width*.11f, eye+.215f,s*width*.86f, eye+.255f,s*width*.90f, eye+.048f,s*width*.14f),.023f,depth+.111f,b.Metal);
                for(int face=-1;face<=1;face+=2) { b.Stud("Mask outer rivet",bottom+.13f,s*width*.79f,face*(depth+.084f)); b.Stud("Brow outer rivet",eye+.22f,s*width*.89f,face*(depth+.12f),.034f); }
            }
            var opening=P(mouthBottom,-width*.46f,mouthTop,-width*.50f,mouthTop+.06f,0,mouthTop,width*.50f,mouthBottom,width*.46f,mouthBottom-.055f,0);
            b.Both("Dark closed mouth recess",opening,.025f,depth+.017f,mouth);
            FrameMouth(b,mouthBottom-.03f,mouthTop+.025f,width*.52f,depth+.057f);
            if(open)
            {
                for(int i=0;i<5;i++)
                {
                    float y=(i-2)*width*.185f;
                    b.Both("Upper fang",P(mouthTop+.035f,y-width*.076f,mouthTop+.035f,y+width*.076f,mouthTop-.22f-(i==2?.035f:0),y),.08f,depth+.072f,teeth);
                    b.Both("Lower fang",P(mouthBottom-.015f,y-width*.067f,mouthBottom+.18f,y,mouthBottom-.015f,y+width*.067f),.07f,depth+.070f,teeth);
                }
                for(int side=-1;side<=1;side+=2)
                    b.Both("Molten mouth side",P(mouthBottom+.10f,side*width*.36f,mouthTop-.18f,side*width*.38f,mouthTop-.15f,side*width*.40f,mouthBottom+.09f,side*width*.40f),.020f,depth+.04f,b.Core);
            }
            else
            {
                float row=(mouthTop-mouthBottom)/2;
                for(int i=0;i<5;i++) for(int r=0;r<2;r++)
                {
                    float y=(i-2)*width*.178f, x=mouthBottom+row*(r+.5f);
                    b.Both("Clenched tooth",P(x-row*.40f,y-width*.072f,x+row*.40f,y-width*.072f,x+row*.40f,y+width*.072f,x-row*.40f,y+width*.072f),.068f,depth+.077f,teeth);
                }
            }
            b.Both("Cast red nose",P(mouthTop+.045f,-width*.15f,eye-.035f,0,mouthTop+.045f,width*.15f,mouthTop-.015f,0),.054f,Mathf.Min(depth+.068f,.308f),red);
            b.Both("Helmet upper red plate",P(eye+.23f,-width*.71f,top-.08f,0,eye+.23f,width*.71f,eye+.095f,0),.046f,depth+.052f,red);
            b.Both("Helmet brass median",P(eye+.29f,-.025f,top-.14f,0,eye+.29f,.025f),.026f,depth+.092f,brass);
            // Small exposed metal paint chips are geometry, not opaque decals.
            for(int side=-1;side<=1;side+=2)
                b.Both("Red armour paint chip",P(eye+.30f,side*width*.72f,eye+.37f,side*width*.76f,eye+.33f,side*width*.80f),.012f,depth+.111f,b.Metal);
        }
        private static void FrameMouth(Builder b,float bottom,float top,float halfWidth,float z)
        {
            float rim=.070f;
            b.Both("Mouth lower red jaw",P(bottom-rim,-halfWidth,bottom,-halfWidth,bottom,halfWidth,bottom-rim,halfWidth),.054f,z,red);
            for(int side=-1;side<=1;side+=2)
                b.Both("Long red mouth rim",P(bottom,side*halfWidth,top,side*halfWidth,top+.012f,side*(halfWidth+rim),bottom-.035f,side*(halfWidth+rim)),.055f,z,red);
        }
        private static void CheekRivets(Builder b,float x,float y)
        {
            for(int side=-1;side<=1;side+=2) for(int face=-1;face<=1;face+=2)
                for(int i=0;i<5;i++) {float a=i*Mathf.PI*2/5; b.Stud("Cheek rivet",x+Mathf.Cos(a)*.39f,side*y+Mathf.Sin(a)*.27f,face*.336f,.032f);}
        }
        private static void PerimeterRivets(Builder b,Vector2 center,float rx,float ry,float z,int count)
        {
            for(int i=0;i<count;i++) {float a=i*Mathf.PI*2/count;for(int face=-1;face<=1;face+=2)b.Stud("Pressure perimeter rivet",center.x+Mathf.Cos(a)*rx,center.y+Mathf.Sin(a)*ry,face*z,.043f);}
        }
        private static void Horn(Builder b,string name,Vector3 origin,Vector3 direction,float length,float radius)
        {
            direction.Normalize();
            // A continuous closed lathed cross-section: throat -> outer wall ->
            // lip -> concave interior -> recessed solid throat. No flat black disc.
            var profile=new[] {new Vector2(0,0),new Vector2(0,.14f),new Vector2(length*.18f,.18f),new Vector2(length*.55f,radius*.50f),new Vector2(length,radius),new Vector2(length+.023f,radius),new Vector2(length+.023f,radius-.045f),new Vector2(length*.55f,radius*.50f-.037f),new Vector2(length*.17f,.12f),new Vector2(length*.12f,0)};
            b.Add(name+" hollow brass bell",Lathe(direction,profile,32),brass,origin);
            b.Cylinder(name+" red neck",origin+direction*.07f,direction,.20f,.192f,red,16);
            b.Cylinder(name+" dark throat",origin+direction*.087f,direction,.07f,.11f,mouth,20);
            b.Add(name+" rolled brass rim",OrientMesh(Torus(radius-.013f,.031f,32,8),Quaternion.FromToRotation(Vector3.forward,direction)),brass,origin+direction*(length+.015f));
            Vector3 tangent=Vector3.Cross(direction,Vector3.forward).normalized;
            if(tangent.sqrMagnitude<.1f)tangent=Vector3.right;
            b.Rod(name+" neck brace",origin-tangent*.19f,origin+direction*.25f-tangent*.23f,.034f,darkSteel);
        }

        private static Vector2[] Gear(Vector2 center,float inner,float outer,int count)
        {
            var result=new Vector2[count*4];
            for(int i=0;i<result.Length;i++){float a=i*Mathf.PI*2/result.Length;float r=(i%4==1||i%4==2)?outer:inner;result[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}
            return result;
        }
        private static Vector2[] P(params float[] coordinates)
        {var result=new Vector2[coordinates.Length/2];for(int i=0;i<result.Length;i++)result[i]=new Vector2(coordinates[i*2],coordinates[i*2+1]);return result;}

        private static Mesh Extrude(Vector2[] source,float thickness,bool bevel)
        {
            var points=new List<Vector2>(source);float area=0;Vector2 center=Vector2.zero;
            for(int i=0;i<points.Count;i++){area+=Cross(points[i],points[(i+1)%points.Count]);center+=points[i];}
            if(Mathf.Abs(area)<Epsilon)throw new ArgumentException("Zero-area Redwarm polygon");
            if(area<0)points.Reverse();center/=points.Count;
            int n=points.Count;var caps=Triangulate(points);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            float half=thickness/2,wall=half-Mathf.Min(.018f,thickness*.18f);float inset=bevel?.955f:1f;
            var front=new Vector3[n];var back=new Vector3[n];
            for(int i=0;i<n;i++){var p=center+(points[i]-center)*inset;front[i]=new Vector3(p.x,p.y,half);back[i]=new Vector3(p.x,p.y,-half);}
            for(int i=0;i<caps.Count;i+=3){Tri(vertices,triangles,front[caps[i]],front[caps[i+1]],front[caps[i+2]]);Tri(vertices,triangles,back[caps[i+2]],back[caps[i+1]],back[caps[i]]);}
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;var af=new Vector3(points[i].x,points[i].y,wall);var bf=new Vector3(points[j].x,points[j].y,wall);var ab=new Vector3(points[i].x,points[i].y,-wall);var bb=new Vector3(points[j].x,points[j].y,-wall);
                Quad(vertices,triangles,front[i],af,bf,front[j]);Quad(vertices,triangles,ab,bb,bf,af);Quad(vertices,triangles,back[j],bb,ab,back[i]);
            }
            return MakeMesh(vertices,triangles);
        }
        private static List<int> Triangulate(List<Vector2> p)
        {
            var remaining=new List<int>();var result=new List<int>();for(int i=0;i<p.Count;i++)remaining.Add(i);
            while(remaining.Count>3)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=Epsilon)continue;bool occupied=false;
                    foreach(int k in remaining){if(k==a||k==b||k==c)continue;if(Cross(p[b]-p[a],p[k]-p[a])>=-Epsilon&&Cross(p[c]-p[b],p[k]-p[b])>=-Epsilon&&Cross(p[a]-p[c],p[k]-p[c])>=-Epsilon){occupied=true;break;}}
                    if(occupied)continue;result.Add(a);result.Add(b);result.Add(c);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new ArgumentException("Self-intersecting Redwarm polygon");
            }
            result.AddRange(remaining);return result;
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static Mesh Lathe(Vector3 axis,Vector2[] profile,int sides)
        {
            axis.Normalize();Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.z)<.9f?Vector3.forward:Vector3.up).normalized,v=Vector3.Cross(axis,u);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,c=(i+1)*Mathf.PI*2/sides;
                var r0=u*Mathf.Cos(a)+v*Mathf.Sin(a);var r1=u*Mathf.Cos(c)+v*Mathf.Sin(c);
                Vector3 p0=axis*profile[j].x+r0*profile[j].y,p1=axis*profile[j].x+r1*profile[j].y,p2=axis*profile[j+1].x+r1*profile[j+1].y,p3=axis*profile[j+1].x+r0*profile[j+1].y;
                if(profile[j].y<=Epsilon)Tri(vertices,triangles,p0,p2,p3);
                else if(profile[j+1].y<=Epsilon)Tri(vertices,triangles,p0,p1,p2);
                else Quad(vertices,triangles,p0,p1,p2,p3);
            }
            return MakeMesh(vertices,triangles);
        }
        private static Mesh Ellipsoid(Vector3 radius,int sides,int rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {
                float t0=j*Mathf.PI/rings,t1=(j+1)*Mathf.PI/rings,a0=i*Mathf.PI*2/sides,a1=(i+1)*Mathf.PI*2/sides;
                Vector3 p0=SpherePoint(radius,t0,a0),p1=SpherePoint(radius,t0,a1),p2=SpherePoint(radius,t1,a1),p3=SpherePoint(radius,t1,a0);
                if(j==0)Tri(vertices,triangles,p0,p3,p2);else if(j==rings-1)Tri(vertices,triangles,p0,p2,p1);else Quad(vertices,triangles,p0,p3,p2,p1);
            }
            return MakeMesh(vertices,triangles);
        }
        private static Vector3 SpherePoint(Vector3 r,float t,float a)=>new Vector3(r.x*Mathf.Cos(t),r.y*Mathf.Sin(t)*Mathf.Cos(a),r.z*Mathf.Sin(t)*Mathf.Sin(a));
        private static Mesh Torus(float radius,float tube,int sides,int rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<sides;i++)for(int j=0;j<rings;j++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides,c=j*Mathf.PI*2/rings,d=(j+1)*Mathf.PI*2/rings;
                Quad(vertices,triangles,TorusPoint(radius,tube,a,c),TorusPoint(radius,tube,b,c),TorusPoint(radius,tube,b,d),TorusPoint(radius,tube,a,d));
            }
            return MakeMesh(vertices,triangles);
        }
        private static Vector3 TorusPoint(float r,float tube,float a,float b)=>new Vector3((r+tube*Mathf.Cos(b))*Mathf.Cos(a),(r+tube*Mathf.Cos(b))*Mathf.Sin(a),tube*Mathf.Sin(b));
        private static Mesh OrientMesh(Mesh mesh,Quaternion rotation)
        {var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=rotation*vertices[i];mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        private static void Tri(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
        {int index=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(index);t.Add(index+1);t.Add(index+2);}
        private static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {Tri(v,t,a,b,c);Tri(v,t,a,c,d);}
        private static Mesh MakeMesh(List<Vector3> vertices,List<int> triangles)
        {
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x*.2f+.5f,vertices[i].y*.2f+.5f);
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32,vertices=vertices.ToArray(),uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Socket(GameObject root,string name,Vector3 position)
        {var socket=new GameObject(name);socket.transform.SetParent(root.transform,false);socket.transform.localPosition=position;}

        /// <summary>Destroys this sample's owned generated meshes. Cached palette materials are shared.</summary>
        public static void Release(GameObject weapon)
        {
            if(weapon==null)return;weapon.SetActive(false);var released=new HashSet<Mesh>();
            foreach(var filter in weapon.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null&&filter.sharedMesh.name.StartsWith(Prefix,StringComparison.Ordinal)&&released.Add(filter.sharedMesh))Dispose(filter.sharedMesh);
            Dispose(weapon);
        }
        private static void EnsureMaterials()
        {
            if(steel!=null&&red!=null&&RouteCores[2]!=null)return;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Redwarm requires a Lit shader.");
            steel=MakeMaterial(shader,"Worn iron",new Color(.48f,.50f,.49f),.78f,.37f);
            darkSteel=MakeMaterial(shader,"Blackened iron",new Color(.115f,.12f,.115f),.72f,.34f);
            red=MakeMaterial(shader,"Chipped vermilion armour",new Color(.63f,.052f,.025f),.42f,.31f);
            brass=MakeMaterial(shader,"Aged brass",new Color(.57f,.335f,.105f),.78f,.42f);
            grip=MakeMaterial(shader,"Black leather wrap",new Color(.035f,.029f,.021f),.04f,.25f);
            teeth=MakeMaterial(shader,"Old ivory steel teeth",new Color(.81f,.69f,.39f),.48f,.42f);
            mouth=MakeMaterial(shader,"Furnace cavity",new Color(.017f,.009f,.007f),.12f,.10f);
            ember=MakeMaterial(shader,"Furnace red glow",new Color(.85f,.10f,.025f),.2f,.45f);
            Emission(ember,new Color(.50f,.018f,.004f));
            RouteMetals[0]=MakeMaterial(shader,"Awakened sunfire bronze",new Color(.66f,.36f,.10f),.83f,.51f);
            RouteMetals[1]=MakeMaterial(shader,"Awakened moonsteel",new Color(.43f,.59f,.66f),.88f,.57f);
            RouteMetals[2]=MakeMaterial(shader,"Awakened voidglass iron",new Color(.15f,.095f,.21f),.63f,.61f);
            RouteCores[0]=ember;
            RouteCores[1]=MakeMaterial(shader,"Moonsteel latent core",new Color(.28f,.75f,.94f),.25f,.52f);Emission(RouteCores[1],new Color(.025f,.15f,.26f));
            RouteCores[2]=MakeMaterial(shader,"Voidglass latent core",new Color(.55f,.15f,.79f),.3f,.51f);Emission(RouteCores[2],new Color(.10f,.013f,.23f));
        }
        private static Material MakeMaterial(Shader shader,string name,Color color,float metallic,float smoothness)
        {
            var m=new Material(shader){name="Redwarm "+name};m.color=color;
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
            // Shared, deterministic 128px surfaces keep the family inexpensive
            // on mobile. Colour is baked into the linear albedo so exposed iron
            // paint chips can be grey rather than merely a darker shade of red.
            if(name.Contains("armour"))WeatheredSurface(m,color,metallic,smoothness,1);
            else if(name.Contains("brass")||name.Contains("bronze"))WeatheredSurface(m,color,metallic,smoothness,2);
            else if(name.Contains("iron")||name.Contains("moonsteel")||name.Contains("teeth"))WeatheredSurface(m,color,metallic,smoothness,3);
            else if(name.Contains("leather"))WeatheredSurface(m,color,metallic,smoothness,4);
            return m;
        }
        private static void WeatheredSurface(Material material,Color baseColor,float metallic,float smoothness,int kind)
        {
            const int size=128;
            var albedo=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name=Prefix+"Surface colour "+material.name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};
            var response=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name=Prefix+"Surface metal smoothness "+material.name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var colours=new Color[size*size];var metalGloss=new Color[size*size];
            float seed=kind*17.319f;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float broad=Mathf.PerlinNoise(x*.036f+seed,y*.036f+seed*.67f);
                float grain=Mathf.PerlinNoise(x*.37f+seed*.23f,y*.37f+seed*.91f);
                float pits=Mathf.PerlinNoise(x*.119f+seed*.83f,y*.119f+seed*.39f);
                float fleck=SurfaceHash(x,y,kind);
                float shade=.57f+broad*.55f+grain*.18f;
                Color colour=baseColor*shade;colour.a=1;
                float metal=metallic,gloss=smoothness*(.65f+grain*.70f);
                bool scratch=Mathf.Abs(Mathf.Sin(x*.43f+y*.077f+broad*4f))>.997f&&grain>.54f;
                if(kind==1)
                {
                    // Irregular islands of missing red paint and fine pitting;
                    // these are albedo + metal/roughness changes, not geometry.
                    bool chip=(pits>.69f&&grain>.57f)||(fleck>.979f&&grain>.61f)||scratch;
                    if(chip){colour=new Color(.20f,.22f,.215f)*(.78f+grain*.50f);metal=.84f;gloss=.38f+grain*.18f;}
                    else if(fleck<.032f){colour*=.42f;gloss*=.53f;}
                    else if(pits<.30f){colour*=.72f;gloss*=.65f;}
                }
                else if(kind==2)
                {
                    // Uneven dark patina, green-brown oxidation and worn bright
                    // copper ridges prevent uniformly orange plastic fittings.
                    if(pits>.65f){colour=Color.Lerp(colour,new Color(.075f,.115f,.076f),.42f);metal*=.57f;gloss*=.47f;}
                    if(fleck<.075f){colour*=.56f;gloss*=.60f;}
                    if(scratch){colour=Color.Lerp(colour,new Color(.69f,.47f,.20f),.42f);gloss=.61f;}
                }
                else if(kind==3)
                {
                    if(pits>.62f){colour=Color.Lerp(colour,new Color(.13f,.105f,.079f),.18f);gloss*=.61f;}
                    if(fleck<.048f){colour*=.61f;gloss*=.46f;}
                    if(scratch){colour*=1.20f;gloss=Mathf.Min(.65f,gloss+.18f);}
                }
                else
                {
                    float seam=Mathf.Abs(Mathf.Sin(x*.50f+y*.17f));colour*=.73f+seam*.22f;metal=.025f;gloss=.12f+grain*.12f;
                }
                colours[y*size+x]=colour;
                metalGloss[y*size+x]=new Color(Mathf.Clamp01(metal),0,0,Mathf.Clamp01(gloss));
            }
            albedo.SetPixels(colours);albedo.Apply(true,true);response.SetPixels(metalGloss);response.Apply(true,true);
            material.color=Color.white;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",albedo);
            if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",albedo);
            if(material.HasProperty("_MetallicGlossMap"))material.SetTexture("_MetallicGlossMap",response);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",1f);
            if(material.HasProperty("_GlossMapScale"))material.SetFloat("_GlossMapScale",1f);
            if(material.HasProperty("_SmoothnessTextureChannel"))material.SetFloat("_SmoothnessTextureChannel",0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");material.EnableKeyword("_METALLICGLOSSMAP");
        }
        private static float SurfaceHash(int x,int y,int seed)
        {
            // Independent visual noise, with no System.Random or gameplay RNG.
            unchecked{uint value=(uint)(x*374761393+y*668265263+seed*1442695041);value=(value^(value>>13))*1274126177u;return(value^(value>>16))/4294967295f;}
        }
        private static void Emission(Material m,Color color){m.EnableKeyword("_EMISSION");if(m.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",color);}
        private static void Dispose(UnityEngine.Object item){if(item==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
    }
}
