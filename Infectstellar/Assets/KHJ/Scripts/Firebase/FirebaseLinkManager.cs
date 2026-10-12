using UnityEngine;
using Firebase;
using Firebase.Auth;
using Cysharp.Threading.Tasks;
using Firebase.Database;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;


namespace ENZEUN.Runtime
{

    /// <summary>
    /// 현재 플레이어의 데이터를 저장하는 클래스
    /// </summary>
    public class PlayerData
    {
        public string Nickname { get; set; }
        //public string UID { get; set; }
    }

    /// <summary>
    /// Firebase 랭킹에 저장할 데이터 목록
    /// </summary>
    [System.Serializable]
    public class RankingData
    {
        public string nickname;
        public int score;

        public RankingData(string nickname, int score)
        {
            this.nickname = nickname;
            this.score = score;
        }
    }

    /// <summary>
    /// Firebase 와 통신하는 메니저 클래스
    /// </summary>
    public class FirebaseLinkManager : MonoBehaviour
    {
        public static FirebaseLinkManager Instance { get; private set; }

        public static FirebaseAuth Auth { get; private set; }

        public static PlayerData CurrentPlayerData { get; private set; } = new();

        public static bool playerHasNickname { get; private set; }

        private DatabaseReference databaseReference;




        /// <summary>
        /// Instance 는 하나만 존재하도록 (싱글턴)
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// 활성화됨과 동시에 firebase 와 연결 / 플레이어의 정보 최신화
        /// </summary>
        public async UniTask ConnectToFirebaseAsync()
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();

            if (status == DependencyStatus.Available)
            {
                Auth = FirebaseAuth.DefaultInstance;

                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;

#if UNITY_EDITOR
                Debug.Log("Firebase initialized successfully.");
#endif

            }

#if UNITY_EDITOR
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {status}");
            }
#endif
            if (Auth.CurrentUser == null)
            {
                try
                {
                    var result = await Auth.SignInAnonymouslyAsync();

#if UNITY_EDITOR
                    Debug.Log($"Anonymous login successful. User ID: {result.User.UserId}");
#endif
                }

                catch (System.Exception ex)
                {
#if UNITY_EDITOR
                    Debug.LogError($"Anonymous login failed: {ex.Message}");
#endif
                }
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"login successful. User ID: {Auth.CurrentUser.UserId}");
#endif
            }

            if (Auth.CurrentUser != null)
            {
                playerHasNickname = await LoadUserNicknameAsync();
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning("No user is currently signed in.");
            }
#endif
        }


        /// <summary>
        /// 버튼클릭 이벤트 -> 닉네임 저장하기
        /// </summary>
        /// <returns></returns>
        public async Task TrySaveNickname(string name)
        {
            await SaveUserNicknameAsync(name);
        }


        /// <summary>
        /// 내 닉네임을 서버에 저장 (유저당 1개 고정)
        /// </summary>
        public async UniTask<bool> SaveUserNicknameAsync(string newNickname)
        {
            if (Auth.CurrentUser == null)
            {
                Debug.Log("로그인된 유저가 없습니다.");
                return false;
            }

            string userId = Auth.CurrentUser.UserId;

            // "Users/{UID}/nickname" 경로에 단일 닉네임 저장
            await databaseReference.Child("Users").Child(userId).Child("nickname").SetValueAsync(newNickname);

            // 로컬 데이터도 동기화
            CurrentPlayerData.Nickname = newNickname;

#if UNITY_EDITOR
            Debug.Log($"닉네임 저장 완료: {newNickname}");
#endif
            playerHasNickname = true;

            return true;
        }

        /// <summary>
        /// 서버에서 내 닉네임 불러오기 (로그인 직후 호출용)
        /// </summary>
        public async UniTask<bool> LoadUserNicknameAsync()
        {
            if (Auth.CurrentUser == null) return false;

            string userId = Auth.CurrentUser.UserId;

            try
            {
                DataSnapshot snapshot = await databaseReference.Child("Users").Child(userId).Child("nickname").GetValueAsync();

                if (snapshot.Exists && snapshot.Value != null)
                {
                    string loadedNickname = snapshot.Value.ToString();

                    if (!string.IsNullOrEmpty(loadedNickname))
                    {
                        CurrentPlayerData.Nickname = loadedNickname;

                        Debug.Log($"닉네임 불러오기 성공: {loadedNickname}");

                        playerHasNickname = true;

                        return true;
                    }
                }

                Debug.Log("등록된 닉네임이 없거나 비어있습니다.");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"닉네임을 불러오는 중 에러 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 랭킹 점수 저장 메서드
        /// </summary>
        public async UniTaskVoid SaveRankingScoreAsync(RankingData rankingData)
        {
            if (Auth.CurrentUser == null)
            {
                Debug.Log("로그인 된 유저가 없습니다. 저장할 수 없습니다");
                return;
            }

            string json = JsonUtility.ToJson(rankingData);

            // "Rankings" 경로 아래에 유저 UID별로 저장
            await databaseReference.Child("Rankings").Push().SetRawJsonValueAsync(json);

            await TrimRankingsAsync(20);

            Debug.Log("랭킹 점수 등록 완료!");
        }

        List<RankingData> rankingList = new List<RankingData>();

        /// <summary>
        /// 랭킹 데이터를 점수 높은 순(내림차순)으로 불러오기
        /// </summary>
        /// <param name="limit">가져올 최대 순위 인원수 (예: 상위 10명)</param>
        public async UniTaskVoid GetTopRankingsAsync(int limit = 10)
        {
            rankingList.Clear();

            try
            {
                // "Rankings" 경로에서 "score" 필드를 기준으로 정렬하는 쿼리 생성
                DataSnapshot snapshot = await databaseReference.Child("Rankings")
                    .OrderByChild("score")
                    .LimitToLast(limit) // 점수가 높은 순으로 뒤에서부터 가져옴 (오름차순 정렬이 기본이므로 LimitToLast 사용)
                    .GetValueAsync();

                if (snapshot.Exists)
                {
                    foreach (var childSnapshot in snapshot.Children)
                    {
                        string json = childSnapshot.GetRawJsonValue();

                        RankingData data = JsonUtility.FromJson<RankingData>(json);

                        if (data != null)
                        {
                            rankingList.Add(data);
                        }
                    }

                    // Firebase의 기본 정렬은 오름차순(낮은 점수 -> 높은 점수)이므로,
                    // 내림차순(높은 점수 -> 낮은 점수)으로 최종 정렬합니다.
                    rankingList = rankingList.OrderByDescending(r => r.score).ToList();
                }

                else
                {
                    Debug.Log("등록된 랭킹 데이터가 없습니다.");
                }
            }

            catch (System.Exception ex)
            {
                Debug.LogError($"랭킹을 불러오는 중 에러 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 지정한 개수(maxCount)를 초과하는 하위 랭킹 데이터를 오래된/낮은 순으로 삭제
        /// </summary>
        private async UniTask TrimRankingsAsync(int maxCount)
        {
            try
            {
                // score 기준 오름차순(낮은 점수 -> 높은 점수)으로 전체 가져오기
                DataSnapshot snapshot = await databaseReference.Child("Rankings")
                    .OrderByChild("score")
                    .GetValueAsync();

                if (snapshot.Exists && snapshot.ChildrenCount > maxCount)
                {
                    // 초과된 개수 계산
                    long excessCount = snapshot.ChildrenCount - maxCount;
                    int deletedCount = 0;

                    foreach (var child in snapshot.Children)
                    {
                        // 초과된 개수만큼만 앞에서부터(점수가 가장 낮은 것부터) 삭제
                        if (deletedCount >= excessCount) break;

                        string key = child.Key;
                        await databaseReference.Child("Rankings").Child(key).RemoveValueAsync();
                        deletedCount++;
                    }

                    Debug.Log($"랭킹 정리 완료: 하위 데이터 {deletedCount}개 삭제됨");
                }
            }

            catch (System.Exception ex)
            {
                Debug.LogError($"랭킹 정리 중 에러 발생: {ex.Message}");
            }
        }

    }
}
