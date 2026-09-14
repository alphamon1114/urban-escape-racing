# 도심 외곽 주행 맵

기준: `30e55e482ffe97be45755f8c29d1a2851324f2fe` (`drift`).

현재 로컬 차량 설정은 2026-09-14 주행감 피드백에 따라 변경했다. 아래의 원본 유지 기록은 최초 맵 도입 시점의 기록이다. 지금은 고속 일반 조향에서 언더스티어, Space로 후륜 슬라이드 진입, 반대키로 회복하도록 `SedanController`를 조정했다. 맵 씬·건물 배치·주차장·카메라는 그대로다. 최신 수치와 재검사 방법은 `../VALIDATION.md`의 고속 언더스티어 항목을 참고한다.

## 실행

1. Unity 6000.6.0f1에서 이 프로젝트를 연다.
2. `Assets/Scenes/CityOutskirts.unity`를 열고 Play를 누른다.
3. W/S, A/D, Space, R은 기존과 같다. 출발 시 +Z 방향 직선을 따라가면 순환 코스로 연결된다.

씬은 이미 생성되어 저장되어 있다. Python이나 생성 메뉴 실행은 필요 없다. 새 씬이 빌드 목록 첫 번째이며 기존 DrivingTest도 남아 있다.

## 구성

- 약 941m 순환 코스, 폭 24m, 코너 중심선 반경 40m.
- 200m 출발 직선, 넓은 코너, 동쪽의 완만한 S자 연결, 복귀 직선.
- 64×64m 연습 주차장과 24m 폭 진입로.
- 상가·창고·사무실을 표현하는 기본 메시 건물 11개, 창문·입구·지붕·가로등.
- Roads, Markings, Buildings, Street furniture로 편집 그룹 분리.
- 차량, 휠, 카메라, HUD 및 주행 스크립트 원본 유지. R 복귀 지점은 기존 `(0, 1, -80)`.
- 주행면은 기존의 하나로 연결된 600×600m 바닥 콜라이더다. 도로와 차선은 콜라이더 없는 얇은 시각 표시이며, 건물·건물 주변 보도·가로등 기둥은 충돌한다.
- 도로 바깥 지면도 주행 가능하다. 이번 버전은 도로 형태와 건물 배치를 평가하는 초기 맵이다.

## 이 환경에서 확인한 내용

- Unity YAML 전체 파싱, 중복 fileID 및 끊어진 내부 참조 없음.
- 원본 Rigidbody, WheelCollider, MonoBehaviour, Camera 문서 보존.
- 새 고체 장애물은 샘플링한 도로 중심선에서 15m 넘게 떨어져 있음 (도로 반폭 12m).
- 출발점은 기존 직선 위이며 별도 리셋 로직 변경 없음.
- 생성 스크립트 재실행 결과의 결정성 확인.

최초 제작 환경에는 Unity Editor가 없어 실행 검증을 하지 못했다. 이후 로컬 Unity 검증 결과는 아래에 기록한다. `city-outskirts-layout.png`는 배치 데이터로 그린 평면도이며 게임 화면 캡처가 아니다.

## 로컬 Unity 검증 (2026-09-14)

- Windows, Unity 6000.6.0f1. `codex/city-outskirts`의 `47cfaff9fd4ed3d83567d8df22a6aa29f20d3299`를 가져온 뒤 실행했다.
- 기존 로컬 변경 `ProjectSettings/ProjectAuditorSettings.asset`, `.vsconfig`는 프로젝트 옆 `race-backup-20260914-city-outskirts` 폴더에 원본 그대로 백업했으며 작업 폴더에서도 유지했다.
- 새 씬 임포트와 스크립트 컴파일, 객체 1,908개의 누락 스크립트 검사 및 차량 1대·휠 4개·카메라·HUD 참조 검사를 통과했다.
- 새 씬에서 기존 drift 물리 검사 전체 통과: 지면 안착, 12초 가속 140.3km/h, 4초 제동 후 50.7km/h, 후진 -8.6m/s, 첫 조향 1.70도, 저속·고속 최대 조향 32도, 핸드브레이크 및 복귀.
- 후륜 최대 slip 0.278, 카운터스티어 보조 OFF/ON 회전 1.358/0.764rad/s, 핸드브레이크 조향/반대 조향 1.448/0.626rad/s로 기존 drift 검증 수치와 일치했다.
- 위 물리 검사는 Editor에서 입력 필드를 설정하고 `Physics.Simulate`를 호출한다. 실제 키보드 주행이나 순환 코스 완주 검사가 아니다. 검사 후 씬을 다시 로드하며 시뮬레이션 상태는 저장하지 않는다.
- Unity GUI에서 `CityOutskirts`를 열고 Play 모드 진입 및 Game 뷰 포커스를 확인했다. 관찰 구간의 실행 로그에 스크립트 컴파일 오류나 런타임 예외는 없었다.
- Windows 화면 캡처가 `SetIsBorderRequired ... 0x80004002` 오류로 실패하여 화면을 보면서 키보드로 주행하는 검증은 완료하지 못했다. 실제 주행감, 한 바퀴, 주차장 진입, 건물 충돌, 렌더링 및 FPS는 아래 체크리스트에 남겨 둔다.
- 차량 스크립트, 기존 DrivingTest 씬과 drift 주행 설정은 변경하지 않았다.

재검사: Unity 메뉴 `Urban Escape > Validate City Outskirts`, 또는 Unity에 `-batchmode -nographics -quit -projectPath <프로젝트 경로> -executeMethod CityOutskirtsValidation.Run -logFile <로그 경로>`를 전달한다. 메뉴 실행 시 현재 씬의 변경 사항 저장 여부를 먼저 확인한다.

로컬 로그: `Logs/city-outskirts-validation.log` (물리 검사 성공 및 종료 코드 0), `Logs/city-outskirts-play.log` (GUI 실행). 로그는 Git에서 제외된다.

## Unity에서 확인할 항목

- 씬 임포트 오류가 없고 차량과 카메라가 정상 동작하는지.
- R로 복귀한 직후 지면 안착과 전방 주행이 가능한지.
- 한 바퀴와 주차장 진입에 끊김이나 걸림이 없는지.
- 곡선 도로 조각의 이음새와 차선 표시가 실제 카메라에서 자연스러운지.
- 건물 충돌, 드리프트 탈출 여유, 프레임 속도.

## 재생성

`python Tools/build_city_outskirts.py`는 표준 라이브러리만 사용한다. 새 맵 씬과 City 머티리얼, 배치 JSON을 다시 쓰므로 Unity에서 직접 수정한 새 맵을 먼저 커밋한 뒤 사용한다. 기존 DrivingTest 및 차량 코드는 수정하지 않는다. 빌드 목록은 별도로 관리한다.

`python Tools/preview_city.py`는 선택 사항이며 Pillow가 필요하다.
