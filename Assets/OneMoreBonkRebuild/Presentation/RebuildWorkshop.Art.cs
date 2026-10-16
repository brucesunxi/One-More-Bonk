using System;
using System.Collections.Generic;
using OneMoreBonk.Rebuild.Visuals;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed partial class RebuildWorkshop
    {
        [Serializable] sealed class LayoutFile { public LayoutAsset[] assets; }
        [Serializable] sealed class LayoutAsset
        {
            public string id;
            public float x, y, w, h, sw, sh, left, top, right, bottom, dl, dt, dr, db;
        }
        [Serializable] sealed class HoleFile { public ChargeHole[] slots; }
        [Serializable] sealed class ChargeHole { public float cx, cy, w, h, angle; }
        readonly Dictionary<string, LayoutAsset> layouts = new Dictionary<string, LayoutAsset>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
        Font font;
        Texture2D[] chargeLights;
        ChargeHole[] holes;
        bool artLoaded;
        GameObject previewRoot, previewWeapon;
        Camera previewCamera, clearCamera;
        RenderTexture previewTexture;
        string previewKey, viewerError;
        bool previewMerchant;

        void LoadArt()
        {
            if (artLoaded) return; artLoaded = true;
            font = Resources.Load<Font>("RebuildArt/Fonts/NotoSansSC-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TextAsset layout = Resources.Load<TextAsset>("RebuildArt/MintUI/asset-layout");
            if (layout != null)
            {
                var data = JsonUtility.FromJson<LayoutFile>(layout.text);
                if (data?.assets != null) foreach (var asset in data.assets)
                {
                    layouts[asset.id] = asset;
                    textures[asset.id] = Resources.Load<Texture2D>("RebuildArt/MintUI/" + asset.id);
                }
            }
            var holeJson = Resources.Load<TextAsset>("RebuildArt/MintUI/dock-slots");
            if (holeJson != null) holes = JsonUtility.FromJson<HoleFile>(holeJson.text)?.slots;
            if (holes != null)
            {
                chargeLights = new Texture2D[holes.Length];
                for (int i = 0; i < holes.Length; i++) chargeLights[i] = ChargeLight(holes[i]);
            }
        }

        // Bake the small oval's angle into its pixels. Rotating GUI.matrix inside a scroll
        // group also transforms the clip origin and can move the light away from its socket.
        Texture2D ChargeLight(ChargeHole hole)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Charge socket light", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            float cosine = Mathf.Cos(hole.angle * Mathf.Deg2Rad), sine = Mathf.Sin(hole.angle * Mathf.Deg2Rad);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x + .5f - size / 2f, dy = size / 2f - y - .5f;
                float u = (dx * cosine + dy * sine) / (hole.w * .39f);
                float v = (-dx * sine + dy * cosine) / (hole.h * .35f);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - Mathf.Sqrt(u * u + v * v)) * 10));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        void InitStyles() { }
        GUIStyle TextStyle(int size, TextAnchor anchor)
        {
            int key = size * 16 + (int)anchor;
            if (!styles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = anchor,
                    wordWrap = true, richText = false, padding = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Clip };
                styles.Add(key, style);
            }
            return style;
        }
        void Label(Rect rect, string text, int size = 24, Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            GUIStyle style = TextStyle(size, anchor); style.normal.textColor = color ?? ink;
            GUI.Label(rect, text ?? "", style);
        }
        void Solid(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        bool Button(Rect rect, string text, bool enabled = true, bool primary = false)
        {
            bool old = GUI.enabled;
            bool allowed = old && enabled;
            GUI.enabled = true;
            Color oldColor = GUI.color;
            if (!allowed) GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * .72f);
            Slice(rect, primary ? "button-gold" : "button-ivory");
            GUI.color = oldColor;
            Label(new Rect(rect.x + 9, rect.y + 7, rect.width - 18, rect.height - 14), text, rect.width < 150 ? 21 : 24,
                allowed ? ink : muted, TextAnchor.MiddleCenter);
            GUI.enabled = allowed;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = old; return clicked;
        }
        void Panel(Rect rect)
        {
            if (!textures.ContainsKey("panel-frosted")) Solid(rect, new Color(.97f, .99f, .97f, .96f));
            else Slice(rect, "panel-frosted");
        }
        void Patch(Rect target, Texture2D texture, LayoutAsset a, float x, float y, float w, float h)
        {
            if (texture == null || w <= 0 || h <= 0 || target.width <= 0 || target.height <= 0) return;
            GUI.DrawTextureWithTexCoords(target, texture, new Rect(x / a.sw, 1 - (y + h) / a.sh, w / a.sw, h / a.sh), true);
        }
        void Sprite(Rect target, string id)
        {
            if (!layouts.TryGetValue(id, out var a) || !textures.TryGetValue(id, out var texture)) return;
            float ratio = Mathf.Min(target.width / a.w, target.height / a.h);
            Rect fit = new Rect(target.center.x - a.w * ratio / 2, target.center.y - a.h * ratio / 2, a.w * ratio, a.h * ratio);
            Patch(fit, texture, a, a.x, a.y, a.w, a.h);
        }
        void Slice(Rect rect, string id)
        {
            if (!layouts.TryGetValue(id, out var a) || !textures.TryGetValue(id, out var texture) || texture == null)
            { Solid(rect, cream); return; }
            float l = Mathf.Min(a.dl, rect.width / 3), r = Mathf.Min(a.dr, rect.width / 3);
            float t = Mathf.Min(a.dt, rect.height / 3), b = Mathf.Min(a.db, rect.height / 3);
            float[] dx = { rect.x, rect.x + l, rect.xMax - r, rect.xMax };
            float[] dy = { rect.y, rect.y + t, rect.yMax - b, rect.yMax };
            float[] sx = { a.x, a.x + a.left, a.x + a.w - a.right, a.x + a.w };
            float[] sy = { a.y, a.y + a.top, a.y + a.h - a.bottom, a.y + a.h };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                Patch(new Rect(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]), texture, a,
                    sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
        }
        void DrawBackground()
        {
            if (!textures.TryGetValue("courtyard-background", out var texture) || texture == null) return;
            float sourceAspect = 853f / 1844, targetAspect = Width / height;
            Rect uv = new Rect(0, 0, 1, 1);
            if (sourceAspect > targetAspect) { uv.width = targetAspect / sourceAspect; uv.x = (1 - uv.width) / 2; }
            else { uv.height = sourceAspect / targetAspect; uv.y = (1 - uv.height) / 2; }
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Width, height), texture, uv, false);
        }
        void AtlasIcon(Rect target, int tile)
        {
            if (!textures.TryGetValue("items-atlas-v1", out var texture) || texture == null) return;
            GUI.DrawTextureWithTexCoords(target, texture, new Rect((tile % 4) * .25f, 1 - (tile / 4 + 1) * .25f, .25f, .25f), true);
        }
        void DrawCharge(Rect target, int charge, bool frenzy)
        {
            if (holes == null || !layouts.TryGetValue("dock-mint", out var a)) return;
            float scale = Mathf.Min(target.width / a.w, target.height / a.h);
            Vector2 start = target.center - new Vector2(a.w, a.h) * scale / 2;
            Color old = GUI.color;
            for (int i = 0; i < holes.Length && i < 10; i++)
            {
                if (i >= charge && !frenzy) continue;
                var hole = holes[i]; Vector2 center = start + new Vector2(hole.cx - a.x, hole.cy - a.y) * scale;
                GUI.color = frenzy ? new Color(1f, .62f, .10f) : new Color(.95f, .83f, .25f);
                GUI.DrawTexture(new Rect(center.x - 64 * scale, center.y - 64 * scale, 128 * scale, 128 * scale), chargeLights[i]);
            }
            GUI.color = old;
        }

        void CreateViewer()
        {
            var screenObject = new GameObject("Rebuild screen camera"); screenObject.transform.SetParent(transform, false);
            clearCamera = screenObject.AddComponent<Camera>(); clearCamera.depth = -100;
            clearCamera.clearFlags = CameraClearFlags.SolidColor; clearCamera.backgroundColor = cream; clearCamera.cullingMask = 0;
            previewRoot = new GameObject("Rebuild weapon preview"); previewRoot.transform.SetParent(transform, false);
            previewRoot.transform.position = new Vector3(100, 0, 0);
            var cameraObject = new GameObject("Rebuild preview camera"); cameraObject.transform.SetParent(previewRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -12);
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.orthographic = true;
            previewCamera.orthographicSize = 2; previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 30;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(0, 0, 0, 0);
            previewCamera.cullingMask = 1 << 30; previewCamera.allowHDR = false;
            previewTexture = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32) { name = "Rebuild sword preview" };
            previewTexture.Create(); previewCamera.targetTexture = previewTexture;
            MakeLight("Preview key", new Vector3(30, -35, 0), 1.5f, new Color(1, .91f, .78f));
            MakeLight("Preview fill", new Vector3(-25, 145, 0), .85f, new Color(.75f, .87f, 1));
        }
        void MakeLight(string name, Vector3 rotation, float intensity, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(previewRoot.transform, false);
            go.transform.localRotation = Quaternion.Euler(rotation);
            Light light = go.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = intensity; light.color = color; light.cullingMask = 1 << 30;
        }
        void UpdateViewer()
        {
            if (previewCamera == null) return;
            previewCamera.enabled = page == Page.Forge && view?.Run != null && Selected != null;
            var sword = Selected;
            if (sword == null) { ReleasePreview(); previewKey = null; return; }
            int stage = RiskForgeRules.Stage(sword), route = (int)sword.MaterialRoute;
            bool awakened = RiskForgeRules.MaterialAwakened(sword);
            bool merchant = Array.IndexOf(MerchantWeaponGeometry.Families, sword.SourceFamily) >= 0;
            string key = (merchant ? sword.SourceFamily : "ordinary") + ":" + stage + ":" + route + ":" + awakened;
            if (previewKey == key) return;
            ReleasePreview(); previewKey = key; viewerError = null; previewMerchant = merchant;
            try
            {
                previewWeapon = merchant ? MerchantWeaponGeometry.Create(sword.SourceFamily, stage, route, awakened) : RageWeaponGeometry.Create(stage, route, awakened);
                previewWeapon.transform.SetParent(previewRoot.transform, false);
                previewWeapon.transform.localPosition = Vector3.zero;
                previewWeapon.transform.localRotation = Quaternion.Euler(-8, -12, 9);
                foreach (Transform child in previewWeapon.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                var renderers = previewWeapon.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    previewCamera.transform.position = bounds.center + Vector3.back * 12;
                    previewCamera.orthographicSize = Mathf.Max(.75f, Mathf.Max(bounds.extents.y, bounds.extents.x / 2) * 1.20f);
                }
            }
            catch (Exception error) { viewerError = "剑预览暂不可用：" + error.Message; }
        }
        void ReleasePreview()
        {
            if (previewWeapon == null) return;
            if (previewMerchant) MerchantWeaponGeometry.Release(previewWeapon); else RageWeaponGeometry.Release(previewWeapon);
            previewWeapon = null;
        }
        void DisposeViewer()
        {
            ReleasePreview();
            if (previewTexture != null) { previewTexture.Release(); Destroy(previewTexture); }
            if (chargeLights != null) foreach (var light in chargeLights) if (light != null) Destroy(light);
            if (previewRoot != null) Destroy(previewRoot);
            if (clearCamera != null) Destroy(clearCamera.gameObject);
        }
    }
}
