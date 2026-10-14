/// <summary>송수신 코드가 함께 사용하는 플레이어 전송 등급과 생성 슬롯 규칙입니다.</summary>
internal static class PlayerNetworkConstants
{
    public const int c_highSendRate = 30; // 근거리·초기 상태의 스냅샷 전송 등급(Hz)
    public const int c_middleSendRate = 15; // 중거리 전송 등급(Hz)
    public const int c_lowSendRate = 10; // 원거리 전송 등급(Hz)
    public const int c_spawnSlotCount = 4; // 현재 스폰 배치가 지원하는 시작 슬롯 수
    public const int c_unassignedSpawnSlot = -1; // 아직 슬롯을 배정하지 않았거나 빈 슬롯이 없음
}
