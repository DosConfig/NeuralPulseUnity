using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Code-only 게임 렌더링 패턴 모음.
/// Prefab 없이 SpriteRenderer, ParticleSystem 등을 C#으로 생성.
/// </summary>
public static class GameRenderer
{
    // ====== Sprite 생성 ======

    /// <summary>색상 원형 스프라이트 오브젝트 생성.</summary>
    public static GameObject CreateSprite(Transform parent, string name,
                                           Sprite sprite, Color color,
                                           Vector3 position, float scale = 1f,
                                           int sortingOrder = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        return go;
    }

    /// <summary>프로시저럴 원형 스프라이트 생성 (런타임).</summary>
    public static Sprite CreateCircleSprite(int resolution = 64)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        float center = resolution / 2f;
        float radius = center - 1;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) * 2f); // anti-alias edge
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                             new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>프로시저럴 사각형 스프라이트 생성.</summary>
    public static Sprite CreateSquareSprite(int resolution = 4)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
                             new Vector2(0.5f, 0.5f), resolution);
    }

    // ====== Object Pool ======

    /// <summary>간단한 오브젝트 풀. 게임 오브젝트 재사용.</summary>
    public class Pool
    {
        private readonly Stack<GameObject> _inactive = new Stack<GameObject>();
        private readonly System.Func<GameObject> _factory;
        private readonly Transform _parent;

        public Pool(System.Func<GameObject> factory, Transform parent, int preWarm = 0)
        {
            _factory = factory;
            _parent = parent;

            for (int i = 0; i < preWarm; i++)
            {
                var go = _factory();
                go.transform.SetParent(_parent, false);
                go.SetActive(false);
                _inactive.Push(go);
            }
        }

        public GameObject Get()
        {
            GameObject go;
            if (_inactive.Count > 0)
            {
                go = _inactive.Pop();
                go.SetActive(true);
            }
            else
            {
                go = _factory();
                go.transform.SetParent(_parent, false);
            }
            return go;
        }

        public void Return(GameObject go)
        {
            go.SetActive(false);
            _inactive.Push(go);
        }

        public void ReturnAll(List<GameObject> active)
        {
            foreach (var go in active)
            {
                if (go != null) Return(go);
            }
            active.Clear();
        }
    }

    // ====== Particle Helper ======

    /// <summary>Code-only ParticleSystem 생성.</summary>
    public static ParticleSystem CreateParticleSystem(Transform parent, string name,
                                                       Color startColor,
                                                       float startSize = 0.3f,
                                                       float startLifetime = 0.8f,
                                                       int maxParticles = 50,
                                                       float gravityModifier = 0.5f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();

        // Main module
        var main = ps.main;
        main.startColor = startColor;
        main.startSize = startSize;
        main.startLifetime = startLifetime;
        main.maxParticles = maxParticles;
        main.gravityModifier = gravityModifier;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        // Emission (burst mode)
        var emission = ps.emission;
        emission.rateOverTime = 0;

        // Shape (point)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        // Renderer
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = startColor;

        return ps;
    }

    /// <summary>파티클 버스트 발사. position에서 count개 파티클.</summary>
    public static void EmitBurst(ParticleSystem ps, Vector3 position, int count,
                                  Color? color = null, float? speed = null)
    {
        if (ps == null) return;

        ps.transform.position = position;

        var emitParams = new ParticleSystem.EmitParams();
        if (color.HasValue) emitParams.startColor = color.Value;
        if (speed.HasValue)
        {
            var main = ps.main;
            main.startSpeed = speed.Value;
        }

        ps.Emit(emitParams, count);
    }

    // ====== Camera Setup ======

    /// <summary>2D 게임용 카메라 설정.</summary>
    public static Camera Setup2DCamera(float orthoSize = 5f, Color? bgColor = null)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("GameCamera");
            cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bgColor ?? new Color(0.04f, 0.04f, 0.06f);
        cam.nearClipPlane = -10f;
        cam.farClipPlane = 100f;

        return cam;
    }

    // ====== Line Renderer ======

    /// <summary>Code-only LineRenderer. 스와이프 라인, 체인 하이라이트 등.</summary>
    public static LineRenderer CreateLine(Transform parent, string name,
                                           Color color, float width = 0.05f,
                                           int sortingOrder = 10)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;

        return lr;
    }
}
