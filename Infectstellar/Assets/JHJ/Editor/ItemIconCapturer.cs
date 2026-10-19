using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class ItemIconCapturer
{
    private const string ICON_FOLDER = "Assets/JHJ/Icons";
    private const int ICON_SIZE = 256;

    // 카메라 각도: 위에서 30도, 옆으로 45도 (테스트 후 조절)
    private static readonly Vector3 VIEW_ANGLE = new Vector3(30f, -45f, 0f);



    [MenuItem("Tools/Item/Capture Icons")]
    private static void CaptureSelected()
    {
        // 선택한 에셋 중 GameObject만
        GameObject[] prefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);

        if(prefabs.Length == 0 )
        {
            Debug.LogWarning($"[ItemIconCapturer] Project창에서 아이템 프리팹을 선택하세요");
            return;
        }

        foreach( GameObject prefab in prefabs )
        {
            if(!prefab.TryGetComponent(out Item item))
            {
                Debug.LogWarning($"[ItemIconCapturer] 아이템 프리팹 아님: {prefab.name}", prefab);
                continue;
            }

            if(TryCapture(prefab, out string path))
                Debug.Log($"[ItemIconCapturer] 저장: {path}", prefab);
        }
    }

    /// <summary>
    /// 프리팹을 미리보기 씬에서 찍어 PNG로 저장
    /// </summary>
    private static bool TryCapture(GameObject prefab, out string path)
    {
        path = $"{ICON_FOLDER}/Icon_{prefab.name}.png";

        // 미리보기 전용 씬 (열린 씬과 분리, 닫으면 안의 오브젝트도 같이 정리됨)
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture renderTexture = null;
        Texture2D texture = null;

        try
        {
            // 프리팹 복사본을 미리보기 씬에 생성 (에디터 모드라 Item.Awake 등은 실행되지 않음)
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);

            if (!TryGetRendererBounds(instance, out Bounds bounds))
            {
                Debug.LogWarning($"[ItemIconCapturer] 렌더러 없음: {prefab.name}", prefab);
                return false;
            }

            // 카메라: 미리보기 씬에 두고, 그 씬만 그리도록 지정
            GameObject cameraObject = new GameObject("IconCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;          // 자동으로 그리지 않고, 아래에서 한 번만
            camera.scene = previewScene;
            SetupCamera(camera, bounds);

            // 조명: 미리보기 씬에는 기본 조명이 없음
            GameObject lightObject = new GameObject("IconLight");
            SceneManager.MoveGameObjectToScene(lightObject, previewScene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            renderTexture = new RenderTexture(ICON_SIZE, ICON_SIZE, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            texture = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, ICON_SIZE, ICON_SIZE), 0, 0);
            texture.Apply();

            RenderTexture.active = previousActive;
            camera.targetTexture = null;

            Directory.CreateDirectory(ICON_FOLDER);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            return true;
        }
        finally
        {
            // 씬을 닫으면 복사본·카메라·조명이 함께 사라짐
            EditorSceneManager.ClosePreviewScene(previewScene);

            if (renderTexture != null)
                Object.DestroyImmediate(renderTexture);
            if (texture != null)
                Object.DestroyImmediate(texture);
        }
    }

    /// <summary>
    /// 자식 렌더러 전체 감싸는 범위
    /// </summary>
    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;

        for(int i = 0; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }


    private static void SetupCamera(Camera cam, Bounds bounds)
    {
        float radius = bounds.extents.magnitude;
        Quaternion rot = Quaternion.Euler(VIEW_ANGLE);

        cam.orthographic = true;
        cam.orthographicSize = radius;

        cam.transform.rotation = rot;
        cam.transform.position = bounds.center - rot * Vector3.forward * (radius * 2f);

        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = radius * 4f;

        cam.clearFlags = CameraClearFlags.SolidColor;
        // 투명 배경
        cam.backgroundColor = Color.clear;
    }
    
}
