using UnityEngine;
using UnityEditor;

/// <summary>
/// 아이템 Variant의 Model 기준 콜라이더 생성 도구
/// Box: Model 아래 렌더러 범위로 Model에 BoxCollider 하나 (단순한 모양)
/// Convex: Model 아래 메시마다 Col_ 자식을 만들고 Convex MeshCollider (불규칙한 모양)
/// 두 방식은 한 아이템에 섞어 쓰지 않음
/// </summary>
public static class ItemColliderFitter
{
    private const string MODEL_NAME = "Model";
    private const string COLLIDER_PREFIX = "Col_";
    private const string MENU_PATH = "Tools/Item/Model 콜라이더 생성";
    private const string MENU_PATH_CONVEX = "Tools/Item/Model Convex 콜라이더 생성";
    private const string UNDO_NAME = "Fit Item Collider";

    // =============================================================
    // Box

    /// <summary>
    /// 콜라이더 생성
    /// </summary>
    [MenuItem(MENU_PATH)]
    private static void FitBoxCollider()
    {
        if (!TryGetModel(out GameObject selected, out Transform model))
            return;

        // Model에 다른 콜라이더가 이미 있으면 건드리지 않음
        foreach (Collider existing in model.GetComponents<Collider>())
        {
            if (existing is not BoxCollider)
            {
                Debug.LogWarning($"[ItemColliderFitter] {selected.name}: Model에 {existing.GetType().Name}가 이미 있음. 중단", model);
                return;
            }
        }

        // Convex 방식(Col_ 자식)과 섞이지 않게 중단
        if (HasColliderChild(model))
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: {COLLIDER_PREFIX} 자식이 이미 있음 (Convex 방식). 지우고 다시 실행", model);
            return;
        }

        // 규칙 검사: 메시 자식에 콜라이더가 있으면 알림
        WarnMeshChildColliders(model);

        // 렌더러 범위 계산
        if (!TryGetRendererBounds(model, out Bounds bounds))
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: Model 아래 켜진 Renderer 없음", model);
            return;
        }

        // 이미 있으면 재사용
        BoxCollider box = model.GetComponent<BoxCollider>();
        if(box == null)
        {
            // ctrl + z로 되돌릴 수 있도록
            box = Undo.AddComponent<BoxCollider>(model.gameObject);
        }
        else
        {
            Undo.RecordObject(box, UNDO_NAME);
        }

        // bounds.center = 월드 좌표, Model기준 로컬좌표로 변환
        box.center = model.InverseTransformPoint(bounds.center);
        box.size = bounds.size;

        Debug.Log($"[ItemColliderFitter] {selected.name}: Box center {box.center:F3}, size {box.size:F3}", model);
    }

    // =============================================================
    // Convex

    /// <summary>
    /// convex meshCollider 생성
    /// </summary>
    [MenuItem(MENU_PATH_CONVEX)]
    private static void FitConvexColliders()
    {
        if (!TryGetModel(out GameObject selected, out Transform model))
            return;

        // Box 방식(Model에 직접 붙인 콜라이더)과 섞이지 않게 중단
        if (model.GetComponent<Collider>() != null)
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: Model에 콜라이더가 이미 있음 (Box 방식). 지우고 다시 실행", model);
            return;
        }

        // 메시 목록을 먼저 모음 (아래에서 Col_ 자식을 지우고 만들기 때문에, 순회 중에 자식 목록이 바뀌지 않도록)
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>();   // 메시 데이터를 가진 컴포넌트. 기본값: 꺼진 오브젝트 제외
        if (filters.Length == 0)
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: Model 아래 MeshFilter 없음", model);
            return;
        }

        WarnMeshChildColliders(model);
        RemoveColliderChildren(model);

        // 실제로 만든 콜라이더 수 (로그용)
        int created = 0;                 
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;                         // 에셋 원본 메시 참조 (mesh 프로퍼티는 복사본을 만들어 에셋 연결이 끊기므로 사용 안 함)
            if (mesh == null)
                continue;

            Renderer renderer = filter.GetComponent<Renderer>();   // 같은 오브젝트의 렌더러
            // 화면에 안 보이는 메시는 제외
            if (renderer != null && !renderer.enabled)              
                continue;

            GameObject colObj = new GameObject(COLLIDER_PREFIX + filter.name);   // 콜라이더 전용 오브젝트
            colObj.transform.SetParent(model, false);                             
            // 새로 만든 오브젝트를 되돌리기 대상으로 기록 (Ctrl+Z 시 삭제됨)
            Undo.RegisterCreatedObjectUndo(colObj, UNDO_NAME);

            // 메시 자식과 같은 월드 자세로 맞춤 → 외형과 콜라이더가 정확히 겹침
            colObj.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            colObj.transform.localScale = filter.transform.lossyScale;            // Model 스케일이 1이라 월드 스케일 = Model 기준 로컬 스케일

            MeshCollider meshCol = colObj.AddComponent<MeshCollider>();           
            meshCol.sharedMesh = mesh;                                            
            meshCol.convex = true;                                                

            if (!mesh.isReadable)   // 임포트 설정 Read/Write 꺼짐 → 스케일 조건에 따라 빌드에서 문제될 수 있음
            {
                Debug.LogWarning($"[ItemColliderFitter] {filter.name}: 메시 Read/Write 꺼짐. 빌드에서 콜라이더 확인 필요", filter);
            }

            created++;
        }

        Debug.Log($"[ItemColliderFitter] {selected.name}: Convex 콜라이더 {created}개 생성", model);
    }

    // =============================================================
    // 메뉴 활성화

    /// <summary>
    /// 메뉴 항목 활성화 여부 확인
    /// </summary>
    [MenuItem(MENU_PATH, true)] [MenuItem(MENU_PATH_CONVEX, true)]
    private static bool ValidateSelection()
    {
        return Selection.activeGameObject != null;
    }


    // =============================================================
    // 공통

    /// <summary>
    /// 선택 확인 → Model 찾기 → Model 월드 회전 0 / 스케일 1 확인
    /// </summary>
    private static bool TryGetModel(out GameObject selected, out Transform model)
    {
        selected = Selection.activeGameObject;
        model = null;                               

        if (selected == null)
        {
            Debug.LogWarning("[ItemColliderFitter] 선택된 오브젝트 없음");
            return false;
        }

        model = FindModel(selected.transform);
        if (model == null)
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: '{MODEL_NAME}' 자식을 찾을 수 없음", selected);
            return false;
        }

        if (model.rotation != Quaternion.identity || model.lossyScale != Vector3.one)
        {
            Debug.LogWarning($"[ItemColliderFitter] {selected.name}: Model(또는 루트)의 월드 회전 0 / 스케일 1이 아님. 보정은 메시 자식에서만", model);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 선택이 Model이면 그대로, 아니면 자식 중 Model
    /// </summary>
    private static Transform FindModel(Transform selected)
    {
        if (selected.name == MODEL_NAME)
            return selected;

        return selected.Find(MODEL_NAME);
    }

    /// <summary>
    /// Model의 직계 자식 중 Col_ 오브젝트가 있는지
    /// </summary>
    private static bool HasColliderChild(Transform model)
    {
        foreach (Transform child in model)
        {
            if (child.name.StartsWith(COLLIDER_PREFIX))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Model의 직계 자식 중 Col_ 오브젝트 전부 삭제 (되돌리기 가능)
    /// </summary>
    private static void RemoveColliderChildren(Transform model)
    {
        // 뒤에서부터 지움: 앞에서부터 지우면 인덱스가 당겨져 하나씩 건너뜀
        for (int i = model.childCount - 1; i >= 0; i--)
        {
            Transform child = model.GetChild(i);
            if (child.name.StartsWith(COLLIDER_PREFIX))
            {
                Undo.DestroyObjectImmediate(child.gameObject);   // 에디터에서 즉시 삭제 + 되돌리기 기록 (Destroy는 플레이 중에만 동작)
            }
        }
    }

    /// <summary>
    /// 규칙 검사: 콜라이더는 Model 자신 또는 Col_ 자식에만. 메시 자식 쪽에 있으면 알림
    /// </summary>
    private static void WarnMeshChildColliders(Transform model)
    {
        foreach (Collider col in model.GetComponentsInChildren<Collider>(true))   // 꺼진 오브젝트까지 포함
        {
            // Model 자신 (Box 방식)
            if (col.transform == model)                              
                continue;

            // Model 직계 Col_ 자식 (Convex 방식)
            if (col.transform.parent == model && col.name.StartsWith(COLLIDER_PREFIX))
                continue;

            Debug.LogWarning($"[ItemColliderFitter] 규칙 위반: Model 아래 '{col.name}'에 {col.GetType().Name} 있음, 제거 필요", col);
        }
    }

    /// <summary>
    /// Model 아래 켜진 렌더러들 범위를 하나로 합침
    /// </summary>
    private static bool TryGetRendererBounds(Transform model, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) //꺼진 렌더러 제외
                continue;

            if(!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                // 기존 범위 확장
                bounds.Encapsulate(r.bounds);
            }
        }

        return found;
    }
}
