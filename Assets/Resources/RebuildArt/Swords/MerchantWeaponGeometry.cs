using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreBonk.Rebuild.Visuals
{
    /// <summary>
    /// Closed procedural 3D reconstructions of the supplied merchant concept sheets.
    /// The eleven source drawings receive one explicitly added early intermediate form.
    /// XY defines the silhouette, X runs from grip to tip, and Z is real thickness.
    /// This is a playable concept reconstruction, not a finished sculpt or PNG billboard.
    /// </summary>
    public static class MerchantWeaponGeometry
    {
        public const string Prefix="OMB_Merchant_";
        public static readonly string[] Families={"world-tree","fishblade","funerary"};
        public static readonly string[][] StageNames={
            new[]{"种芽匕","枝芽小刃","单叶月刃","双枝剪","木核环锤","菌根伞刃","根系笼枪","莲形花刃","鸟巢枝钩","星果天仪","天幕枝门","星木宇宙冠"},
            new[]{"鱼骨匕","鱼脊小刃","白鳍月刃","龙虾钳刃","蓝鳍鱼刃","群鱼叠刃","橙鳍羽刃","河豚锤","珊瑚枝枪","旗鱼弯刃","章鱼缠杖","海祭巨鳍"},
            new[]{"墓纹匕","墓纹凿刃","掘墓短铲","掘墓重铲","墓石刃","棺甲剑","冥铃锤","灵晶笼灯","葬月镰","束碑剑","双铃天平","葬钥终仪"}
        };
        static Material bark,leaf,ivory,sea,coral,stone,grip,brass,dark,crystal;
        static readonly Material[] Metals=new Material[3],Cores=new Material[3];
        static readonly Dictionary<int,List<Mesh>> owned=new Dictionary<int,List<Mesh>>();
        const float Epsilon=1e-7f;

        sealed class Builder
        {
            public readonly GameObject Root;
            public readonly Material Metal,Core;
            readonly List<GameObject> parts=new List<GameObject>();
            readonly List<Mesh> meshes=new List<Mesh>();
            public Builder(GameObject root,int route,bool awake){Root=root;Metal=awake?Metals[route]:brass;Core=awake?Cores[route]:crystal;}
            public void Add(string name,Mesh mesh,Vector3 center,Material material)
            {
                mesh.name=Prefix+name;meshes.Add(mesh);
                var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(Root.transform,false);go.transform.localPosition=center;
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;parts.Add(go);
            }
            public void Plate(string name,Vector2[] polygon,float thickness,Material material,float z=0)
                =>Add(name,Extrude(polygon,thickness),new Vector3(0,0,z),material);
            public void Box(string name,Vector3 center,Vector3 size,Material material)
                =>Add(name,Extrude(P(-size.x/2,-size.y/2,size.x/2,-size.y/2,size.x/2,size.y/2,-size.x/2,size.y/2),size.z),center,material);
            public void Rod(string name,Vector3 a,Vector3 b,float radius,Material material,int sides=12)
                =>Add(name,MerchantWeaponGeometry.Cylinder(b-a,Vector3.Distance(a,b),radius,sides),(a+b)/2,material);
            public void Cylinder(string name,Vector3 center,Vector3 axis,float length,float radius,Material material,int sides=16)
                =>Add(name,MerchantWeaponGeometry.Cylinder(axis,length,radius,sides),center,material);
            public void Ball(string name,Vector3 center,Vector3 radius,Material material)
                =>Add(name,Ellipsoid(radius,16,10),center,material);
            public void Ring(string name,Vector3 center,float radius,float tube,Material material)
                =>Add(name,Torus(radius,tube,28,8),center,material);
            public void Leaf(string name,Vector2 root,Vector2 tip,float width,Material material,float depth=.12f,float z=0)
            {
                Vector2 dir=tip-root,normal=new Vector2(-dir.y,dir.x).normalized;
                Vector2 a=root+dir*.20f+normal*width*.55f,b=root+dir*.53f+normal*width,c=root+dir*.78f+normal*width*.55f;
                Vector2 d=root+dir*.72f-normal*width*.64f,e=root+dir*.39f-normal*width*.72f;
                Plate(name,new[]{root,e,d,tip,c,b,a},depth,material,z);
                Rod(name+" vein",new Vector3(root.x,root.y,z),new Vector3(tip.x,tip.y,z),.028f,Metal,10);
            }
            public void Crystal(string name,Vector3 center,float length,float width)
            {
                Plate(name,P(center.x-length/2,center.y,center.x-length*.20f,center.y-width/2,center.x+length*.28f,center.y-width*.40f,
                    center.x+length/2,center.y,center.x+length*.28f,center.y+width*.40f,center.x-length*.20f,center.y+width/2),width*.62f,Core,center.z);
            }
            public void Finish()
            {
                // Stable insertion order gives identical geometry batches for every material route.
                var materials=new List<Material>();var groups=new List<List<CombineInstance>>();
                foreach(var part in parts)
                {
                    Material material=part.GetComponent<MeshRenderer>().sharedMaterial;int group=materials.IndexOf(material);
                    if(group<0){group=materials.Count;materials.Add(material);groups.Add(new List<CombineInstance>());}
                    groups[group].Add(new CombineInstance{mesh=part.GetComponent<MeshFilter>().sharedMesh,transform=part.transform.localToWorldMatrix});
                }
                var final=new List<Mesh>();
                for(int i=0;i<groups.Count;i++)
                {
                    var mesh=new Mesh{name=Prefix+"Batch"+i,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(groups[i].ToArray(),true,true);mesh.RecalculateBounds();
                    var go=new GameObject("Merchant "+materials[i].name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(Root.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=materials[i];final.Add(mesh);
                }
                foreach(var part in parts){part.SetActive(false);Dispose(part);}foreach(var mesh in meshes)Dispose(mesh);
                owned.Add(Root.GetInstanceID(),final);
            }
        }

        public static GameObject Create(string family,int stage,int route=0,bool awakened=false)
        {
            int familyIndex=Array.IndexOf(Families,family);
            if(familyIndex<0)throw new ArgumentOutOfRangeException(nameof(family),"Choose world-tree, fishblade or funerary.");
            stage=Mathf.Clamp(stage,0,11);route=Mathf.Clamp(route,0,2);EnsureMaterials();
            var root=new GameObject("商人剑 "+family+" · "+StageNames[familyIndex][stage]+" · 形态"+stage);
            var b=new Builder(root,route,awakened);
            BuildGrip(b,familyIndex);
            if(familyIndex==0)BuildTree(b,stage);else if(familyIndex==1)BuildFish(b,stage);else BuildFunerary(b,stage);
            // A structural collar intersects the body and carries both actual hammer faces.
            b.Box("Closed forge landing",new Vector3(.6f,0,0),new Vector3(.28f,.18f,.68f),b.Metal);
            b.Box("Forge landing front inset",new Vector3(.6f,0,-.333f),new Vector3(.20f,.13f,.014f),dark);
            b.Box("Forge landing back inset",new Vector3(.6f,0,.333f),new Vector3(.20f,.13f,.014f),dark);
            b.Finish();Socket(root,"GripSocket",new Vector3(-1.75f,0,0));Socket(root,"DisplayPivot",Vector3.zero);Socket(root,"ForgeContact",new Vector3(.6f,0,-.34f));
            return root;
        }
        public static int OwnedMeshCount(GameObject weapon)=>weapon!=null&&owned.TryGetValue(weapon.GetInstanceID(),out var meshes)?meshes.Count:0;
        public static void Release(GameObject weapon)
        {
            if(weapon==null)return;weapon.SetActive(false);
            if(owned.TryGetValue(weapon.GetInstanceID(),out var meshes))
            {foreach(Mesh mesh in meshes)if(mesh!=null)Dispose(mesh);owned.Remove(weapon.GetInstanceID());}
            // A mesh supplied by another system is never destroyed merely because its name matches.
            Dispose(weapon);
        }
        static void BuildGrip(Builder b,int family)
        {
            Material handle=family==0?bark:family==1?ivory:grip;
            b.Cylinder("Themed rigid grip",new Vector3(-1.73f,0,0),Vector3.right,1.26f,.125f,handle);
            for(int i=0;i<8;i++)b.Cylinder("Grip binding "+i,new Vector3(-2.20f+i*.135f,0,0),Vector3.right,.035f,.137f,b.Metal,12);
            b.Cylinder("Grip pommel",new Vector3(-2.45f,0,0),Vector3.right,.22f,.19f,b.Metal,8);
            b.Cylinder("Grip neck",new Vector3(-1.035f,0,0),Vector3.right,.25f,.19f,b.Metal,12);
            b.Box("Themed guard",new Vector3(-.87f,0,0),new Vector3(.15f,.61f,.28f),b.Metal);
            b.Crystal("Pommel inset",new Vector3(-2.55f,0,-.12f),.15f,.12f);
            if(family==0){b.Leaf("Guard small leaf",new Vector2(-.88f,.18f),new Vector2(-.40f,.41f),.16f,leaf);}
            else if(family==1){b.Ball("Shell guard",new Vector3(-.87f,0,0),new Vector3(.22f,.36f,.19f),ivory);}
            else b.Cylinder("Guard funeral seal",new Vector3(-.87f,0,-.18f),Vector3.forward,.07f,.15f,dark,8);
        }
        static void TreeStem(Builder b,float end,float width=.15f)
        {
            b.Rod("Living wood spine",new Vector3(-.85f,0,0),new Vector3(end,0,0),width,bark);
            for(int i=0;i<5;i++)
            {
                float x=-.55f+(end+.25f)*i/5f;
                b.Rod("Wood vine binding "+i,new Vector3(x,-width*.95f,-width*.3f),new Vector3(x+.19f,width*.85f,width*.35f),.035f,b.Metal,10);
            }
        }
        static void BuildTree(Builder b,int stage)
        {
            switch(stage)
            {
                case 0:
                    TreeStem(b,1.35f,.13f);b.Leaf("Seed dagger leaf",new Vector2(-.72f,0),new Vector2(1.62f,.30f),.34f,leaf,.20f);
                    b.Ball("Seed heart",new Vector3(.16f,-.17f,0),new Vector3(.20f,.16f,.18f),bark);break;
                case 1:
                    TreeStem(b,1.78f);b.Leaf("Intermediate folded bud",new Vector2(-.65f,0),new Vector2(2.0f,.10f),.46f,leaf,.22f);
                    b.Leaf("Bud second leaf",new Vector2(.20f,-.05f),new Vector2(1.1f,-.58f),.23f,leaf);b.Crystal("Growing seed",new Vector3(.12f,0,-.15f),.29f,.18f);break;
                case 2:
                    TreeStem(b,2.25f);b.Leaf("Single moon leaf",new Vector2(-.67f,0),new Vector2(2.58f,.34f),.62f,leaf,.24f);
                    b.Leaf("Leaf heel",new Vector2(-.63f,-.10f),new Vector2(.16f,-.69f),.24f,ivory,.14f);break;
                case 3:
                    TreeStem(b,1.30f);b.Leaf("Scissor upper branch",new Vector2(.14f,.05f),new Vector2(2.62f,.80f),.32f,leaf,.18f);
                    b.Leaf("Scissor lower branch",new Vector2(.14f,-.05f),new Vector2(2.34f,-.82f),.31f,leaf,.18f);
                    b.Ring("Split root binding",new Vector3(.28f,0,0),.29f,.09f,b.Metal);break;
                case 4:
                    TreeStem(b,.93f,.19f);b.Ring("Root core ring",new Vector3(1.11f,0,0),.78f,.20f,bark);
                    b.Ring("Root bronze band",new Vector3(1.11f,0,-.06f),.68f,.055f,b.Metal);b.Ball("Wood core",new Vector3(1.11f,0,0),new Vector3(.34f,.41f,.28f),bark);
                    for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;b.Leaf("Core leaf "+i,new Vector2(1.11f+Mathf.Cos(a)*.65f,Mathf.Sin(a)*.65f),new Vector2(1.11f+Mathf.Cos(a)*1.05f,Mathf.Sin(a)*1.05f),.15f,leaf);}
                    break;
                case 5:
                    TreeStem(b,1.4f,.18f);b.Add("Closed mushroom canopy",Lathe(Vector3.right,new[]{new Vector2(-.18f,0),new Vector2(-.12f,.80f),new Vector2(.04f,.89f),new Vector2(.27f,.62f),new Vector2(.40f,.16f),new Vector2(.42f,0)},24),new Vector3(1.40f,0,0),ivory);
                    b.Ring("Canopy bronze rim",new Vector3(1.25f,0,0),.76f,.055f,b.Metal);b.Leaf("Mushroom root leaf",new Vector2(.05f,0),new Vector2(.65f,-.76f),.25f,leaf);break;
                case 6:
                    TreeStem(b,2.64f,.11f);TreeCage(b,new Vector2(1.28f,0),1.36f,.65f,false);b.Crystal("Root cage white crystal",new Vector3(1.45f,0,0),1.20f,.34f);
                    b.Leaf("Root spear crown",new Vector2(2.10f,0),new Vector2(2.93f,.10f),.30f,leaf);break;
                case 7:
                    TreeStem(b,1.8f);b.Ball("Lotus wood heart",new Vector3(1.56f,0,0),new Vector3(.31f,.30f,.24f),bark);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI*2/8;b.Leaf("Lotus thick petal "+i,new Vector2(1.56f+Mathf.Cos(a)*.12f,Mathf.Sin(a)*.12f),new Vector2(1.56f+Mathf.Cos(a)*1.04f,Mathf.Sin(a)*1.04f),.33f,ivory,.14f);}
                    b.Crystal("Lotus core",new Vector3(1.56f,0,-.20f),.42f,.37f);break;
                case 8:
                    TreeStem(b,1.10f,.18f);b.Ring("Nest branch ring",new Vector3(1.50f,.24f,0),.75f,.12f,bark);b.Ring("Nest vine rim",new Vector3(1.50f,.24f,.11f),.59f,.065f,b.Metal);
                    b.Leaf("Nest hooked leaf",new Vector2(.89f,-.18f),new Vector2(2.90f,.94f),.34f,leaf,.20f);
                    for(int i=0;i<3;i++)b.Ball("Nest glowing egg "+i,new Vector3(1.28f+i*.21f,.10f,-.10f),new Vector3(.16f,.20f,.17f),b.Core);break;
                case 9:
                    TreeStem(b,1.54f,.16f);b.Ball("Starfruit crystal orb",new Vector3(1.53f,0,0),new Vector3(.65f,.65f,.55f),b.Core);
                    TreeCage(b,new Vector2(1.53f,0),.80f,.80f,true);b.Leaf("Starfruit top leaf",new Vector2(1.90f,.30f),new Vector2(2.59f,.80f),.24f,leaf);break;
                case 10:
                    TreeStem(b,2.30f,.12f);TreeCage(b,new Vector2(1.33f,0),1.57f,.64f,false);b.Crystal("Gate long crystal",new Vector3(1.47f,0,0),1.55f,.28f);
                    for(int f=-1;f<=1;f+=2){b.Leaf("Gate open leaf "+f,new Vector2(.11f,f*.36f),new Vector2(2.64f,f*.94f),.35f,leaf,.17f);b.Leaf("Gate white flower "+f,new Vector2(.26f,f*.36f),new Vector2(.48f,f*.90f),.25f,ivory);}
                    break;
                default:
                    TreeStem(b,1.70f,.21f);b.Ring("Cosmic wood crown",new Vector3(1.50f,0,0),1.10f,.13f,bark);b.Ring("Cosmic bronze cage",new Vector3(1.50f,0,.11f),.95f,.05f,b.Metal);
                    b.Crystal("Cosmic crystal heart",new Vector3(1.50f,0,0),1.50f,.67f);
                    for(int i=0;i<8;i++){float a=i*Mathf.PI*2/8;var point=new Vector3(1.50f+Mathf.Cos(a)*1.04f,Mathf.Sin(a)*1.04f,0);b.Ball("Star crown orb "+i,point,new Vector3(.22f,.22f,.20f),b.Core);b.Leaf("Crown leaf "+i,new Vector2(point.x,point.y),new Vector2(1.50f+Mathf.Cos(a)*1.47f,Mathf.Sin(a)*1.47f),.23f,leaf);}
                    b.Leaf("Crown tall sprout",new Vector2(2.19f,.11f),new Vector2(3.20f,.10f),.28f,leaf);break;
            }
        }
        static void TreeCage(Builder b,Vector2 center,float halfLength,float halfWidth,bool round)
        {
            var a=new Vector3(center.x-halfLength,center.y,0);var c=new Vector3(center.x+halfLength,center.y,0);
            b.Ball("Root cage heel knot",a,new Vector3(.16f,.16f,.16f),bark);
            b.Ball("Root cage crown knot",c,new Vector3(.16f,.16f,.16f),bark);
            for(int f=-1;f<=1;f+=2)
            {
                var m=new Vector3(center.x,center.y+f*halfWidth,.11f);
                b.Ball("Root cage side knot "+f,m,new Vector3(.16f,.16f,.16f),bark);
                // Conceal distinct capped rod endpoints inside real knots instead of
                // welding several cap diameters into one nonmanifold boundary edge.
                JointRod(b,"Cage lower root "+f,a,m,.085f,bark);
                JointRod(b,"Cage upper root "+f,m,c,.085f,bark);
                JointRod(b,"Cage metal rail "+f,a+new Vector3(0,0,-.12f),m+new Vector3(0,0,-.12f),.035f,b.Metal);
            }
            if(round){b.Ring("Starfruit transverse band",new Vector3(center.x,center.y,0),halfWidth,.06f,b.Metal);}
            else b.Ball("Root cage tip",new Vector3(center.x+halfLength,center.y,0),new Vector3(.15f,.18f,.14f),b.Metal);
        }
        static void JointRod(Builder b,string name,Vector3 a,Vector3 c,float radius,Material material)
        {
            Vector3 axis=(c-a).normalized;
            b.Rod(name,a+axis*.05f,c-axis*.05f,radius,material);
        }
        static void FishSpine(Builder b,float end)
        {b.Rod("Rigid fish bone spine",new Vector3(-.85f,0,0),new Vector3(end,0,0),.13f,ivory);}
        static void FishBody(Builder b,Vector2 center,float length,float width,Material skin,bool fin=true,float z=0)
        {
            b.Ball("Thick fish body",new Vector3(center.x,center.y,z),new Vector3(length*.48f,width,.22f),skin);
            b.Plate("Fish tail",P(center.x-length*.37f,center.y,center.x-length*.74f,center.y-width*.85f,center.x-length*.60f,center.y,center.x-length*.74f,center.y+width*.85f),.15f,skin,z);
            for(int face=-1;face<=1;face+=2){b.Ball("Fish raised eye "+face,new Vector3(center.x+length*.26f,center.y+width*.34f,z+face*.205f),new Vector3(.09f,.09f,.055f),b.Metal);b.Ball("Fish eye pupil "+face,new Vector3(center.x+length*.26f,center.y+width*.34f,z+face*.248f),new Vector3(.037f,.037f,.025f),dark);}
            if(fin)
            {b.Plate("Fish dorsal fin",P(center.x-length*.21f,center.y+width*.76f,center.x+length*.04f,center.y+width*1.63f,center.x+length*.32f,center.y+width*.70f),.10f,ivory,z);
             b.Plate("Fish ventral fin",P(center.x-length*.08f,center.y-width*.74f,center.x+length*.14f,center.y-width*1.36f,center.x+length*.29f,center.y-width*.61f),.10f,b.Metal,z);}
        }
        static void BuildFish(Builder b,int stage)
        {
            switch(stage)
            {
                case 0:
                    FishSpine(b,1.15f);b.Plate("Fishbone dagger",P(-.80f,-.15f,.65f,-.20f,1.54f,.10f,1.22f,.41f,.25f,.22f,-.80f,.18f),.20f,sea);
                    for(int i=0;i<4;i++)b.Rod("Dagger bone rib "+i,new Vector3(-.22f+i*.28f,-.12f,0),new Vector3(-.12f+i*.28f,.25f,0),.042f,ivory,10);break;
                case 1:
                    FishSpine(b,1.60f);b.Plate("Intermediate fish spine blade",P(-.80f,-.15f,.94f,-.29f,1.92f,0,1.38f,.31f,.54f,.22f,-.80f,.18f),.22f,sea);
                    for(int i=0;i<5;i++)b.Plate("Spine tooth "+i,P(.20f+i*.26f,.15f,.31f+i*.26f,.43f,.46f+i*.26f,.18f),.11f,ivory);break;
                case 2:
                    FishSpine(b,2.24f);b.Plate("White fin curved blade",P(-.80f,-.14f,1.23f,-.24f,2.59f,.24f,2.02f,.73f,1.82f,.58f,1.58f,.61f,1.50f,.41f,1.11f,.44f,.42f,.19f,-.80f,.18f),.23f,ivory);
                    b.Plate("Moon fin turquoise inset",P(-.54f,-.05f,1.25f,-.10f,2.27f,.27f,1.41f,.29f,.35f,.10f,-.54f,.06f),.035f,sea,-.13f);break;
                case 3:
                    FishSpine(b,1.48f);b.Plate("Lobster upper pincer",P(-.68f,-.06f,1.33f,-.01f,2.05f,.57f,1.78f,.90f,.85f,.55f,.55f,.18f,-.68f,.11f),.26f,coral);
                    b.Plate("Lobster lower pincer",P(-.65f,-.13f,.45f,-.15f,1.55f,-.76f,2.20f,-.38f,1.41f,-.39f,.45f,-.41f,-.65f,-.27f),.26f,coral);
                    b.Ball("Pincer joint",new Vector3(.54f,0,0),new Vector3(.31f,.31f,.25f),b.Metal);
                    for(int i=0;i<5;i++)b.Plate("Lobster tooth "+i,P(.72f+i*.23f,.10f,.83f+i*.23f,-.06f,.91f+i*.23f,.16f),.10f,ivory);break;
                case 4:
                    FishSpine(b,2.20f);FishBody(b,new Vector2(1.08f,0),2.50f,.41f,sea);
                    b.Plate("Fish sharp nose",P(2.06f,-.12f,2.85f,0,2.06f,.17f),.17f,ivory);break;
                case 5:
                    FishSpine(b,2.17f);
                    for(int i=0;i<3;i++)FishBody(b,new Vector2(.30f+i*.79f,(i%2==0?.20f:-.18f)),1.28f,.29f,sea,true,(i-1)*.018f);
                    b.Rod("Fish stack bronze tie",new Vector3(-.15f,-.35f,.02f),new Vector3(2.17f,.34f,.02f),.045f,b.Metal);break;
                case 6:
                    FishSpine(b,2.35f);FishBody(b,new Vector2(1.33f,0),2.22f,.31f,coral,false);
                    for(int f=-1;f<=1;f+=2)for(int i=0;i<6;i++)
                        b.Plate("Long feather fin "+f+" "+i,P(.29f+i*.30f,f*.19f,.45f+i*.30f,f*(.65f+.05f*(i%3)),.67f+i*.30f,f*.21f),.13f,i%2==0?coral:ivory);
                    break;
                case 7:
                    FishSpine(b,1.40f);b.Ball("Solid pufferfish body",new Vector3(1.28f,0,0),new Vector3(.78f,.76f,.55f),ivory);
                    for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;Vector3 at=new Vector3(1.28f+Mathf.Cos(a)*.71f,Mathf.Sin(a)*.71f,0);b.Rod("Puffer spike "+i,at,at+new Vector3(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f,0),.055f,b.Metal,8);}
                    for(int face=-1;face<=1;face+=2){b.Ball("Puffer face eye "+face,new Vector3(1.46f,.27f,face*.51f),new Vector3(.11f,.11f,.065f),dark);b.Ring("Puffer mouth "+face,new Vector3(1.50f,-.22f,face*.54f),.12f,.035f,b.Metal);}
                    break;
                case 8:
                    FishSpine(b,2.85f);
                    for(int i=0;i<4;i++)b.Ball("Coral root joint "+i,new Vector3(.05f+i*.61f,0,0),new Vector3(.14f,.14f,.14f),coral);
                    for(int f=-1;f<=1;f+=2)for(int i=0;i<4;i++)
                    {
                        float x=.05f+i*.61f;Vector3 a=new Vector3(x,0,0),m=new Vector3(x+.39f,f*.51f,0),end=new Vector3(x+.29f,f*(.93f+(i%2)*.18f),.03f);
                        b.Rod("Coral branch trunk "+f+" "+i,a+(m-a).normalized*.05f,m,.09f,coral);b.Rod("Coral branch tip "+f+" "+i,m,end,.075f,coral);
                        b.Rod("Coral branch fork "+f+" "+i,m,new Vector3(x+.70f,f*.78f,0),.055f,coral);
                    }
                    b.Ball("Coral pearl",new Vector3(.70f,0,-.06f),new Vector3(.31f,.31f,.26f),b.Core);b.Ring("Pearl bezel",new Vector3(.70f,0,-.06f),.34f,.07f,b.Metal);break;
                case 9:
                    FishSpine(b,2.45f);b.Plate("Swordfish crescent",P(-.72f,-.18f,.70f,-.26f,2.58f,.14f,3.06f,.63f,2.05f,.43f,1.68f,.71f,1.64f,.46f,1.18f,.52f,.91f,.25f,-.72f,.16f),.25f,sea);
                    for(int i=0;i<6;i++)b.Plate("Crescent ivory tooth "+i,P(.43f+i*.32f,.23f,.58f+i*.32f,.57f,.74f+i*.32f,.29f),.14f,ivory);
                    b.Leaf("Swordfish tail guard",new Vector2(-.71f,-.04f),new Vector2(-.02f,-.84f),.24f,sea);break;
                case 10:
                    FishSpine(b,2.63f);b.Ball("Octopus crystal head",new Vector3(2.16f,.06f,0),new Vector3(.46f,.43f,.35f),sea);b.Crystal("Octopus staff heart",new Vector3(.53f,0,0),.67f,.44f);
                    for(int f=-1;f<=1;f+=2)
                    {
                        Vector3 old=new Vector3(2.16f,f*.30f,0);
                        for(int i=1;i<=10;i++){float x=2.16f-i*.27f,y=f*(.33f+.28f*Mathf.Sin(i*.90f));Vector3 next=new Vector3(x,y,.06f*Mathf.Sin(i));b.Rod("Octopus solid arm "+f+" "+i,old,next,.12f,sea);b.Ball("Arm suction cup "+f+" "+i,new Vector3(x,y,-.12f),new Vector3(.060f,.060f,.039f),coral);old=next;}
                    }
                    b.Ring("Octopus oracle bezel",new Vector3(.53f,0,-.04f),.41f,.06f,b.Metal);break;
                default:
                    FishSpine(b,2.95f);b.Plate("Leviathan left fin",P(-.76f,-.10f,.31f,-.24f,1.11f,-.73f,1.79f,-.91f,2.51f,-.72f,3.11f,-.04f,2.41f,-.29f,2.19f,-.09f,1.52f,-.42f,.63f,-.13f,-.76f,.14f),.26f,sea);
                    b.Plate("Leviathan right fin",P(-.71f,.10f,.19f,.31f,.72f,.87f,1.70f,1.10f,2.55f,.86f,3.27f,.17f,2.19f,.54f,1.77f,.37f,.85f,.47f,.21f,.18f),.26f,sea);
                    for(int i=0;i<6;i++)b.Plate("Leviathan bone blade "+i,P(.21f+i*.41f,.27f,.45f+i*.41f,.78f,.62f+i*.41f,.31f),.16f,ivory);
                    b.Ring("Sea oracle ring",new Vector3(.65f,0,0),.61f,.105f,b.Metal);b.Crystal("Sea oracle crystal",new Vector3(.65f,0,-.16f),.65f,.47f);
                    b.Ball("Leviathan tail joint",new Vector3(-.63f,0,0),new Vector3(.12f,.12f,.12f),b.Metal);
                    for(int f=-1;f<=1;f+=2)
                    {
                        var a=new Vector3(-.63f,0,0);var tip=new Vector3(-.22f,f*.96f,0);
                        b.Rod("Leviathan tail tassel "+f,a+(tip-a).normalized*.05f,tip,.052f,b.Metal);
                    }
                    break;
            }
        }
        static void FuneralSpine(Builder b,float end)
        {b.Box("Funeral structural spine",new Vector3((end-.84f)/2,0,0),new Vector3(end+.84f,.22f,.26f),dark);}
        static void Coffin(Builder b,float start,float end,float width)
        {
            b.Plate("Closed coffin armour",P(start,-width*.64f,start+.35f,-width,end-.31f,-width,end,0,end-.31f,width,start+.35f,width,start,width*.64f),.37f,stone);
            b.Plate("Coffin bronze outline",P(start+.10f,-width*.54f,start+.38f,-width*.79f,end-.34f,-width*.79f,end-.16f,0,end-.34f,width*.79f,start+.38f,width*.79f,start+.10f,width*.54f),.045f,b.Metal,-.21f);
            b.Plate("Coffin inner panel",P(start+.20f,-width*.40f,start+.42f,-width*.62f,end-.42f,-width*.62f,end-.30f,0,end-.42f,width*.62f,start+.42f,width*.62f,start+.20f,width*.40f),.048f,dark,-.238f);
            b.Box("Coffin cross stem",new Vector3((start+end)/2,0,-.28f),new Vector3((end-start)*.45f,.06f,.045f),b.Metal);
            b.Box("Coffin crossbar",new Vector3((start+end)/2+.15f,0,-.285f),new Vector3(.06f,width*.67f,.045f),b.Metal);
        }
        static void FuneralBell(Builder b,string name,Vector3 center,float length,float radius)
        {
            // Outer and inner bell walls plus the rolled annular rim form one closed hollow solid.
            b.Add(name,Lathe(Vector3.right,new[]{new Vector2(-length/2,0),new Vector2(-length/2,radius*.43f),new Vector2(-length*.30f,radius*.61f),new Vector2(length*.39f,radius*.92f),new Vector2(length/2,radius),new Vector2(length/2,radius-.09f),new Vector2(length*.33f,radius*.79f),new Vector2(-length*.27f,radius*.48f),new Vector2(-length*.35f,0)},20),center,stone);
            b.Add(name+" hollow rolled lip",Lathe(Vector3.right,new[]{new Vector2(-.03f,radius*.90f),new Vector2(-.03f,radius*1.01f),new Vector2(.03f,radius*1.01f),new Vector2(.03f,radius*.90f),new Vector2(-.03f,radius*.90f)},20),center+new Vector3(length*.49f,0,0),b.Metal);
            b.Rod(name+" clapper stem",center+new Vector3(-length*.26f,0,0),center+new Vector3(length*.52f,0,0),.06f,dark);
            b.Ball(name+" clapper",center+new Vector3(length*.52f,0,0),new Vector3(.14f,.14f,.14f),b.Metal);
        }
        static void BuildFunerary(Builder b,int stage)
        {
            switch(stage)
            {
                case 0:
                    FuneralSpine(b,1.16f);b.Plate("Grave dagger",P(-.83f,-.15f,.70f,-.19f,1.60f,0,.70f,.19f,-.83f,.15f),.23f,stone);
                    b.Box("Dagger cemetery rune",new Vector3(.13f,0,-.13f),new Vector3(.40f,.04f,.03f),b.Metal);break;
                case 1:
                    FuneralSpine(b,1.59f);b.Plate("Intermediate grave chisel",P(-.80f,-.21f,1.61f,-.27f,1.90f,-.16f,1.90f,.16f,1.61f,.27f,-.80f,.21f),.27f,stone);
                    for(int i=0;i<3;i++)b.Box("Chisel funeral mark "+i,new Vector3(.30f+i*.37f,0,-.15f),new Vector3(.09f,.20f,.026f),b.Metal);break;
                case 2:
                    FuneralSpine(b,1.81f);b.Plate("Grave short spade",P(-.77f,-.11f,.47f,-.12f,.64f,-.34f,1.58f,-.34f,2.02f,0,1.58f,.34f,.64f,.34f,.47f,.12f,-.77f,.11f),.25f,stone);
                    b.Box("Spade central ridge",new Vector3(1.13f,0,-.16f),new Vector3(1.03f,.08f,.045f),b.Metal);break;
                case 3:
                    FuneralSpine(b,1.69f);b.Plate("Heavy grave shovel",P(-.81f,-.10f,.13f,-.12f,.29f,-.56f,1.67f,-.62f,2.15f,-.25f,2.20f,0,2.15f,.25f,1.67f,.62f,.29f,.56f,.13f,.12f,-.81f,.10f),.29f,stone);
                    b.Box("Shovel face reinforcement",new Vector3(1.04f,0,-.18f),new Vector3(1.51f,.09f,.05f),b.Metal);break;
                case 4:
                    FuneralSpine(b,2.38f);b.Plate("Faceted cemetery stoneblade",P(-.81f,-.22f,1.96f,-.34f,2.60f,0,1.96f,.34f,-.81f,.22f),.36f,stone);
                    b.Plate("Stoneblade face facet",P(-.61f,-.035f,1.99f,-.09f,2.40f,0,1.99f,.09f,-.61f,.035f),.08f,dark,-.22f);
                    for(int i=0;i<3;i++)b.Rod("Stoneblade bronze binding "+i,new Vector3(.10f+i*.63f,-.28f,-.20f),new Vector3(.36f+i*.63f,.28f,-.20f),.045f,b.Metal);break;
                case 5:
                    FuneralSpine(b,2.41f);Coffin(b,-.38f,2.55f,.47f);break;
                case 6:
                    FuneralSpine(b,1.51f);FuneralBell(b,"Great mourning bell",new Vector3(1.48f,0,0),1.39f,.70f);
                    b.Ring("Bell hanger band",new Vector3(.74f,0,0),.34f,.06f,b.Metal);b.Crystal("Bell spirit inset",new Vector3(1.33f,0,-.62f),.49f,.35f);break;
                case 7:
                    FuneralSpine(b,2.08f);b.Crystal("Spirit crystal lantern",new Vector3(1.47f,0,0),1.21f,.70f);
                    var heel=new Vector3(.54f,0,0);var crown=new Vector3(2.36f,0,0);
                    b.Ball("Lantern heel joint",heel,new Vector3(.12f,.12f,.12f),b.Metal);
                    b.Ball("Lantern crown joint",crown,new Vector3(.12f,.12f,.12f),b.Metal);
                    for(int i=0;i<4;i++)
                    {
                        float a=i*Mathf.PI/2;Vector3 p=new Vector3(1.47f,Mathf.Cos(a)*.50f,Mathf.Sin(a)*.40f);
                        b.Ball("Lantern cage knuckle "+i,p,new Vector3(.10f,.10f,.10f),b.Metal);
                        JointRod(b,"Lantern lower cage "+i,heel,p,.06f,b.Metal);
                        JointRod(b,"Lantern upper cage "+i,p,crown,.06f,b.Metal);
                    }
                    b.Cylinder("Lantern cap",new Vector3(2.38f,0,0),Vector3.right,.15f,.24f,b.Metal);break;
                case 8:
                    FuneralSpine(b,2.63f);b.Plate("Funeral curved scythe",P(1.84f,-.18f,2.16f,-.16f,2.63f,.19f,2.78f,.75f,2.43f,1.38f,1.60f,1.89f,.69f,2.08f,1.61f,1.40f,2.01f,.88f,2.04f,.32f),.25f,stone);
                    b.Plate("Scythe bronze edge",P(2.09f,.07f,2.54f,.31f,2.61f,.76f,2.30f,1.31f,1.62f,1.75f,1.15f,1.91f,1.78f,1.29f,2.15f,.80f),.035f,b.Metal,-.15f);
                    b.Cylinder("Scythe hinge",new Vector3(2.00f,0,0),Vector3.forward,.38f,.27f,b.Metal);break;
                case 9:
                    FuneralSpine(b,2.82f);b.Plate("Bound cemetery obelisk",P(-.78f,-.32f,2.39f,-.37f,2.91f,0,2.39f,.37f,-.78f,.32f),.40f,stone);
                    for(int i=0;i<4;i++)b.Rod("Obelisk locking band "+i,new Vector3(-.43f+i*.74f,-.33f,-.25f),new Vector3(-.11f+i*.74f,.33f,-.25f),.055f,b.Metal);
                    for(int i=0;i<5;i++)b.Crystal("Spirit obelisk rune "+i,new Vector3(-.03f+i*.40f,0,-.23f),.24f,.13f);break;
                case 10:
                    FuneralSpine(b,2.76f);b.Ring("Mourning balance head",new Vector3(2.28f,0,0),.49f,.10f,b.Metal);b.Crystal("Balance spirit core",new Vector3(2.28f,0,-.08f),.50f,.38f);
                    b.Rod("Balance beam",new Vector3(1.92f,-1.00f,0),new Vector3(1.92f,1.00f,0),.073f,b.Metal);
                    for(int f=-1;f<=1;f+=2){b.Rod("Balance bell link "+f,new Vector3(1.92f,f*.91f,0),new Vector3(1.43f,f*.91f,0),.043f,dark);FuneralBell(b,"Balance bell "+f,new Vector3(1.13f,f*.91f,0),.65f,.30f);}
                    break;
                default:
                    FuneralSpine(b,3.04f);b.Ring("Great burial key bow",new Vector3(2.43f,0,0),.70f,.14f,b.Metal);b.Ring("Burial key stone rim",new Vector3(2.43f,0,-.06f),.53f,.065f,stone);
                    b.Crystal("Great burial crystal",new Vector3(2.43f,0,0),.90f,.74f);
                    for(int i=0;i<3;i++){float x=.02f+i*.45f;b.Box("Key solid tooth "+i,new Vector3(x,-.46f,0),new Vector3(.24f,.78f,.28f),stone);b.Box("Key bronze cap "+i,new Vector3(x,-.79f,-.16f),new Vector3(.24f,.085f,.04f),b.Metal);}
                    b.Plate("Key crown point",P(2.95f,-.15f,3.31f,-.15f,3.63f,0,3.31f,.15f,2.95f,.15f),.25f,b.Metal);
                    break;
            }
        }
        static Vector2[] P(params float[] values)
        {var points=new Vector2[values.Length/2];for(int i=0;i<points.Length;i++)points[i]=new Vector2(values[i*2],values[i*2+1]);return points;}
        static Mesh Extrude(Vector2[] polygon,float thickness)
        {
            if(polygon==null||polygon.Length<3||thickness<=0)throw new ArgumentException("Closed extrusion needs a polygon and positive thickness.");
            var p=(Vector2[])polygon.Clone();float area=0;for(int i=0;i<p.Length;i++){Vector2 a=p[i],b=p[(i+1)%p.Length];area+=a.x*b.y-b.x*a.y;}
            if(area<0)Array.Reverse(p);
            List<int> caps=Triangulate(p);var vertices=new List<Vector3>();var triangles=new List<int>();float z=thickness/2;
            for(int i=0;i<caps.Count;i+=3)
            {
                Vector2 a=p[caps[i]],b=p[caps[i+1]],c=p[caps[i+2]];
                Tri(vertices,triangles,new Vector3(a.x,a.y,z),new Vector3(b.x,b.y,z),new Vector3(c.x,c.y,z));
                Tri(vertices,triangles,new Vector3(c.x,c.y,-z),new Vector3(b.x,b.y,-z),new Vector3(a.x,a.y,-z));
            }
            for(int i=0;i<p.Length;i++)
            {Vector2 a=p[i],b=p[(i+1)%p.Length];Quad(vertices,triangles,new Vector3(a.x,a.y,-z),new Vector3(b.x,b.y,-z),new Vector3(b.x,b.y,z),new Vector3(a.x,a.y,z));}
            return MakeMesh(vertices,triangles);
        }
        static List<int> Triangulate(Vector2[] p)
        {
            var remaining=new List<int>();for(int i=0;i<p.Length;i++)remaining.Add(i);var result=new List<int>();int guard=p.Length*p.Length;
            while(remaining.Count>3&&guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=Epsilon)continue;
                    bool contains=false;
                    foreach(int other in remaining)if(other!=a&&other!=b&&other!=c&&InTriangle(p[other],p[a],p[b],p[c])){contains=true;break;}
                    if(contains)continue;result.Add(a);result.Add(b);result.Add(c);remaining.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)throw new InvalidOperationException("A merchant outline must be a simple nondegenerate polygon.");
            }
            if(remaining.Count!=3)throw new InvalidOperationException("Merchant cap triangulation failed.");
            result.AddRange(remaining);return result;
        }
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
            =>Cross(b-a,p-a)>=-Epsilon&&Cross(c-b,p-b)>=-Epsilon&&Cross(a-c,p-c)>=-Epsilon;
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static Mesh Cylinder(Vector3 axis,float length,float radius,int sides)
        {
            if(length<=0||radius<=0)throw new ArgumentException("Closed cylinder dimensions must be positive.");
            return Lathe(axis,new[]{new Vector2(-length/2,0),new Vector2(-length/2,radius),new Vector2(length/2,radius),new Vector2(length/2,0)},Mathf.Max(6,sides));
        }
        static Mesh Lathe(Vector3 axis,Vector2[] profile,int sides)
        {
            if(axis.sqrMagnitude<=Epsilon)throw new ArgumentException("Revolution needs a nonzero axis.");
            axis.Normalize();Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.z)<.9f?Vector3.forward:Vector3.up).normalized,v=Vector3.Cross(axis,u);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 r0=u*Mathf.Cos(a)+v*Mathf.Sin(a),r1=u*Mathf.Cos(b)+v*Mathf.Sin(b);
                Vector3 p0=axis*profile[j].x+r0*profile[j].y,p1=axis*profile[j].x+r1*profile[j].y,p2=axis*profile[j+1].x+r1*profile[j+1].y,p3=axis*profile[j+1].x+r0*profile[j+1].y;
                if(profile[j].y<=Epsilon)Tri(vertices,triangles,p0,p2,p3);else if(profile[j+1].y<=Epsilon)Tri(vertices,triangles,p0,p1,p2);else Quad(vertices,triangles,p0,p1,p2,p3);
            }
            return MakeMesh(vertices,triangles);
        }
        static Mesh Ellipsoid(Vector3 radius,int sides,int rings)
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
        static Vector3 SpherePoint(Vector3 r,float t,float a)=>new Vector3(r.x*Mathf.Cos(t),r.y*Mathf.Sin(t)*Mathf.Cos(a),r.z*Mathf.Sin(t)*Mathf.Sin(a));
        static Mesh Torus(float radius,float tube,int sides,int rings)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<sides;i++)for(int j=0;j<rings;j++)
            {float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides,c=j*Mathf.PI*2/rings,d=(j+1)*Mathf.PI*2/rings;Quad(vertices,triangles,TorusPoint(radius,tube,a,c),TorusPoint(radius,tube,b,c),TorusPoint(radius,tube,b,d),TorusPoint(radius,tube,a,d));}
            return MakeMesh(vertices,triangles);
        }
        static Vector3 TorusPoint(float r,float tube,float a,float b)=>new Vector3((r+tube*Mathf.Cos(b))*Mathf.Cos(a),(r+tube*Mathf.Cos(b))*Mathf.Sin(a),tube*Mathf.Sin(b));
        static void Tri(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
        {int index=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(index);t.Add(index+1);t.Add(index+2);}
        static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {Tri(v,t,a,b,c);Tri(v,t,a,c,d);}
        static Mesh MakeMesh(List<Vector3> vertices,List<int> triangles)
        {
            var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x*.2f+.5f,vertices[i].y*.2f+.5f);
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32,vertices=vertices.ToArray(),triangles=triangles.ToArray(),uv=uv};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void Socket(GameObject root,string name,Vector3 at)
        {var socket=new GameObject(name);socket.transform.SetParent(root.transform,false);socket.transform.localPosition=at;}
        static void EnsureMaterials()
        {
            if(bark!=null&&Metals[2]!=null)return;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("Merchant concept models need a Lit shader.");
            bark=Mat(shader,"Warm carved root",new Color(.25f,.12f,.050f),.10f,.32f);leaf=Mat(shader,"Emerald leaf",new Color(.058f,.29f,.12f),.13f,.38f);
            ivory=Mat(shader,"Pearl ivory",new Color(.85f,.79f,.62f),.17f,.41f);sea=Mat(shader,"Deep turquoise",new Color(.065f,.30f,.39f),.47f,.42f);
            coral=Mat(shader,"Living orange coral",new Color(.68f,.21f,.095f),.12f,.34f);stone=Mat(shader,"Dark carved grave stone",new Color(.26f,.28f,.29f),.48f,.34f);
            grip=Mat(shader,"Dark funeral leather",new Color(.065f,.058f,.050f),.025f,.20f);brass=Mat(shader,"Aged gold bronze",new Color(.59f,.38f,.14f),.76f,.42f);
            dark=Mat(shader,"Obsidian bindings",new Color(.075f,.083f,.078f),.61f,.31f);crystal=Mat(shader,"Blue spirit crystal",new Color(.28f,.69f,.68f),.38f,.59f);
            Metals[0]=Mat(shader,"Sunfire bronze",new Color(.72f,.38f,.12f),.85f,.52f);Metals[1]=Mat(shader,"Moonsteel silver",new Color(.50f,.67f,.73f),.88f,.58f);Metals[2]=Mat(shader,"Voidglass alloy",new Color(.21f,.12f,.30f),.73f,.60f);
            Cores[0]=Mat(shader,"Sunfire core",new Color(.95f,.28f,.045f),.25f,.51f);Cores[1]=Mat(shader,"Moonsteel core",new Color(.31f,.78f,.95f),.30f,.57f);Cores[2]=Mat(shader,"Voidglass core",new Color(.57f,.22f,.80f),.30f,.57f);
        }
        static Material Mat(Shader shader,string name,Color color,float metal,float smooth)
        {var material=new Material(shader){name=Prefix+name,color=color};if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",metal);if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smooth);return material;}
        static void Dispose(UnityEngine.Object item){if(item==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
    }
}
