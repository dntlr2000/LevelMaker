# R5 수동 검증 환경

이 폴더는 R5·R5.1의 Blueprint 저장·덮어쓰기·비교·미리보기, 저장 레시피 복원과 SavedBlueprint StageDefinition 생성 흐름을 Unity Editor에서 직접 확인하기 위한 독립 검증 자료입니다.

## 준비

1. Unity 메뉴 `Tools > Rogue Dungeon Lab > R5 수동 검증 환경 생성`을 실행합니다.
2. 자동으로 열린 `R5_AuthoringVerification` 장면에서 `Rogue Dungeon Generator`가 선택됐는지 확인합니다.
3. `Tools > Rogue Dungeon Lab > 실험실 열기`를 열고 `스테이지 자산` 탭으로 이동합니다.
4. Console의 Error가 0개인지 확인합니다.

생성 도구는 `Blueprints/Reference`, `Stages/Reference`, `Settings`, `Scenes`만 기준값으로 갱신합니다. 세 기준 Blueprint에는 R5.1 정확 재생성에 필요한 제작 레시피 snapshot도 함께 저장됩니다. 사용자가 만든 자산은 반드시 `Blueprints/Output`과 `Stages/Output`에 저장하세요. 생성 도구를 다시 실행해도 두 Output 폴더의 자산은 삭제하거나 덮어쓰지 않습니다.

## 파일 역할

| 경로 | 확인 목적 |
|---|---|
| `Scenes/R5_AuthoringVerification.unity` | StableV2 seed 12345 절차 원본에서 저장 UI를 검증 |
| `Scenes/R5_SavedRuntimeVerification.unity` | 기준 SavedBlueprint StageDefinition의 Play 자동 로드 검증 |
| `Blueprints/Reference/R5_Reference_Seed12345.asset` | 현재 절차 결과와 `Identical` 비교 기준 |
| `Blueprints/Reference/R5_Reference_Seed12346.asset` | 같은 provenance의 `DifferentSeed` 비교 기준 |
| `Blueprints/Reference/R5_Reference_ChangedRecipe.asset` | 다른 recipe hash의 `StaleInputs` 비교 기준 |
| `Stages/Reference/R5_Procedural_Seed12345.asset` | 제작 장면의 기본 절차 원본 |
| `Stages/Reference/R5_Procedural_Seed12346.asset` | 덮어쓰기 검증용 같은 입력·다른 시드 원본 |
| `Stages/Reference/R5_Saved_Seed12345.asset` | 저장본 Play 자동 로드 기준 |
| `Blueprints/Output` | 수동 검증 중 새로 만드는 Blueprint 전용 |
| `Stages/Output` | 수동 검증 중 새로 만드는 StageDefinition 전용 |

## 1. 기준 결과와 비교 상태

`R5_AuthoringVerification` 장면의 `스테이지 자산` 탭에서 `선택 Blueprint`를 다음 순서로 교체합니다.

1. `R5_Reference_Seed12345`: `절차 원본과 저장본의 논리 결과가 같습니다.`가 표시돼야 합니다.
2. `R5_Reference_Seed12346`: `생성 입력은 같지만 시드가 다른 별도 결과입니다.`가 표시돼야 합니다.
3. `R5_Reference_ChangedRecipe`: stale 경고와 서로 다른 Recipe hash가 표시돼야 합니다.
4. 세 자산 모두 `선택 저장본` 검증 리포트에 오류가 없어야 합니다.

## 2. 새 Blueprint 저장과 재임포트

1. `선택 Blueprint`를 비운 뒤 제작 메모에 `R5 manual save`를 입력합니다.
2. `현재 결과를 새 Blueprint로 저장`을 누릅니다.
3. 저장 경로를 `Assets/R5ManualVerification/Blueprints/Output/R5_UserSaved_Seed12345.asset`로 지정합니다.
4. 생성된 자산이 자동 선택되고 비교 상태가 동일 결과인지 확인합니다.
5. `저장 레시피 설정 복원` 영역에 저장 레시피가 유효하다는 안내와 두 복원 버튼이 표시되는지 확인합니다.
6. Project 창에서 자산을 우클릭해 `Reimport`한 뒤 메모, seed `12345`, generatorVersion `2`, Recipe/Catalog/Blueprint hash와 레시피 snapshot이 유지되는지 확인합니다.

## 3. 덮어쓰기와 Undo

1. Hierarchy에서 `Rogue Dungeon Generator`를 선택합니다.
2. Inspector의 `Stage Definition`을 `Stages/Reference/R5_Procedural_Seed12346.asset`로 교체합니다.
3. 컴포넌트 컨텍스트 메뉴에서 `Load Stage Definition`을 실행합니다.
4. `스테이지 자산` 탭에서 `R5_UserSaved_Seed12345`를 선택하면 다른 시드 상태여야 합니다.
5. `선택 Blueprint 덮어쓰기`를 누르고 확인 창에서 실제로 덮어씁니다.
6. 저장 자산의 seed가 `12346`으로 바뀌고 비교 상태가 동일 결과가 되는지 확인합니다.
7. `Ctrl+Z`를 한 번 실행해 seed와 Blueprint hash가 다시 `12345`로 돌아오는지 확인합니다.
8. `File > Save Project`를 실행하고 해당 Blueprint를 우클릭해 `Reimport`한 뒤에도 seed·Blueprint hash·저장 레시피가 `12345` 기준으로 유지되는지 확인합니다.
9. 검증을 계속하기 전에 Generator의 Stage Definition을 `R5_Procedural_Seed12345`로 되돌리고 `Load Stage Definition`을 실행합니다.

## 4. 저장본 미리보기와 현재 절차 설정 재생성

1. `선택 Blueprint`에 `R5_UserSaved_Seed12345` 또는 기준 `R5_Reference_Seed12345`를 지정합니다.
2. `저장본 미리보기`를 누릅니다.
3. Hierarchy의 `__RogueDungeonLab_Generated`가 교체되고 현재 StageInstance 출처가 SavedBlueprint가 되어야 합니다.
4. seed와 Blueprint hash가 선택 저장본과 같은지 확인합니다.
5. `현재 절차 설정으로 재생성`을 눌러 미리보기 전 seed로 돌아오는지 확인합니다. 이 버튼은 현재 설정값 자체를 저장 당시 값으로 복원하지 않습니다.
6. 전환을 3회 반복해도 generated root가 하나뿐이고 Console Error가 없어야 합니다.

## 5. 저장 레시피 설정 복원과 정확 재생성

1. Generator의 Stage Definition이 `R5_Procedural_Seed12345`이고 `선택 Blueprint`가 `R5_Reference_Seed12345`인지 확인합니다.
2. 현재 설정의 스테이지 너비 또는 방 개수를 변경하고 다른 시드로 생성합니다.
3. `레시피 설정만 불러오기 (현재 시드 유지)`를 누른 뒤 생성 필드와 밀도 곡선은 저장 당시 값으로 돌아오고 현재 시드는 유지되는지 확인합니다.
4. `Ctrl+Z`를 실행해 설정값이 적용 전 상태로 복구되는지 확인합니다.
5. `레시피 + 저장 시드 적용 후 절차 생성`을 누르고 확인 창에서 실행합니다.
6. 설정과 시드가 저장 당시 값으로 바뀌며 StableV2 결과의 Blueprint hash가 `R5_Reference_Seed12345`와 정확히 일치하는지 확인합니다.
7. 기존 snapshot 없는 R5 Blueprint를 선택하면 저장본 미리보기·StageDefinition 생성은 가능하지만 두 레시피 복원 버튼만 비활성화되는지 확인합니다.

## 6. SavedBlueprint StageDefinition 생성

1. `선택 Blueprint`에 `R5_UserSaved_Seed12345`를 지정합니다.
2. `생성 후 현재 Generator에 연결`과 `새 StageDefinition의 Play 진입 자동 로드`를 켭니다.
3. `SavedBlueprint StageDefinition 생성`을 누릅니다.
4. 저장 경로를 `Assets/R5ManualVerification/Stages/Output/R5_UserSaved_Seed12345_Stage.asset`로 지정합니다.
5. 생성 자산의 Source Mode가 SavedBlueprint, Build Mode가 RuntimeBuild이고 저장 Blueprint 참조가 유지되는지 확인합니다.
6. 현재 Generator의 Stage Definition에 새 자산이 연결됐는지 확인합니다.
7. 장면을 저장하고 Play에 진입했을 때 저장본 seed와 hash가 유지되는지 확인합니다.

## 7. 독립 저장형 장면과 Unity 재시작

1. `Scenes/R5_SavedRuntimeVerification.unity`를 열고 Play를 누릅니다.
2. 던전이 자동 로드되고 임시 플레이어 생성, 이동, 적·파괴물 클릭이 정상인지 확인합니다.
3. Play를 종료하고 Console Error가 없는지 확인합니다.
4. Unity를 완전히 종료했다가 프로젝트를 다시 엽니다.
5. `R5_AuthoringVerification`, `R5_SavedRuntimeVerification`, `R5_UserSaved_Seed12345`, 생성한 StageDefinition을 다시 열어 참조와 hash가 유지되는지 확인합니다.

## 통과 기준

- 새 Blueprint 저장과 Reimport 후 중첩 데이터·메모·hash가 유지됩니다.
- 덮어쓰기 확인과 `Ctrl+Z` 복구가 저장·강제 재임포트 뒤에도 유지됩니다.
- 동일·다른 시드·stale 비교 상태가 기준 자산에 맞게 구분됩니다.
- 저장본 미리보기는 저장 seed/hash를 유지하며 현재 절차 설정으로 다시 생성할 수 있습니다.
- 저장 레시피만 적용하면 현재 시드는 유지되고, 레시피와 저장 시드를 함께 적용하면 원 Blueprint hash가 정확히 재현됩니다.
- snapshot 없는 기존 R5 자산은 로드 호환성을 유지하고 설정 복원만 차단됩니다.
- SavedBlueprint StageDefinition을 만든 뒤 Play와 Unity 재시작 후에도 참조가 유지됩니다.
- 장면마다 `__RogueDungeonLab_Generated`는 하나뿐이고 Console Error가 0개입니다.
