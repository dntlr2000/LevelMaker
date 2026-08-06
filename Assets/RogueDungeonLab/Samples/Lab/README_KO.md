# Rogue Dungeon Lab Lab Sample

실험실용 `RuntimeLabHUD`, `LabOrbitCamera`, `RogueDungeonClickInteractor`와
`PrototypePlayerController`를 제공하는 선택 패키지입니다.

먼저 Runtime Core를 설치하고 Package Manager에서
`com.unity.inputsystem`을 설치한 다음 Sample 패키지를 가져옵니다.
제품 장면에 이 Sample 컴포넌트를 넣지 않으면 빌드에서 실험실 HUD가 나타나지
않습니다.

Sample을 설치하는 것만으로 장면에 HUD가 자동 생성되지는 않습니다.
`RuntimeLabHUD`, `LabOrbitCamera`와 `RogueDungeonClickInteractor`를 검증 장면에
명시적으로 배치하거나 원본 LevelMaker의 장면 자동 구성 도구를 사용합니다.
