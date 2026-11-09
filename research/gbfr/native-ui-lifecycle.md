# 네이티브 UI: 소유권, 작업 문맥과 정리 순서

[English](native-ui-lifecycle.en.md)

대상 빌드와 근거 분류는 [연구 개요](README.md)를 따른다. 이 노트는 기존 UI 엔진의 effect 없는 Image 경로와 이를 사용하는 모드 구현을 설명한다. 모든 UI component나 렌더링 경로를 다루는 일반 생성 API는 아니다.

## Object, component와 관리자 소유권

**정적 확인:** Object factory와 미부착 Image factory는 owning wrapper를 사용한다. Image를 Object에 attach하면 component 배열로 소유권이 이동하고 기존 wrapper의 포인터가 비워지는 경로가 있다. 부착된 Canvas/Image의 수명은 Object의 해제 경로와 연결된다. 별도로 보관한 component 주소는 그 자체로 추가 소유권을 뜻하지 않는다.

UI owner의 `+0x220` staging map과 `+0x1E0` live map은 모두 소유권을 보유하는 구조다. 관찰한 native handoff는 owner의 `+0x260` 잠금 아래 같은 node를 옮긴다. Object 또는 owning wrapper를 복제하는 것으로 해석하면 안 된다. 중복 key가 남긴 staging node는 wrapper 정리를 거칠 수 있어, 중복 등록을 무해한 재시도로 가정할 수 없다.

부모-자식 계층 연결, 관리자 소유권 등록, 활성 Canvas 목록 편입은 별도 관계다. 어느 하나가 다른 둘까지 보장하지 않는다. Image의 `+0x108` 조상 CanvasGroup 참조도 실제 렌더 Canvas와 구별해야 한다.

**구현 정책:** 모드 소유 Object만 live map에 등록하고, 자신의 component와 명령만 추적한다. 생성자 내부에서 잠시 설정되는 기저 클래스 vtable이나 destructor 중의 vtable만으로 최종 파생 객체의 멤버 배치를 결정하지 않는다.

## 고정 UI 스레드나 전역 정지점으로 단순화할 수 없는 이유

**실행 관찰:** UI 생성·컨트롤러 갱신·파괴가 하나의 고정된 스레드에서만 실행된다고 볼 수 없는 표본이 있다. UI 작업 join 이후의 지점에서도 리소스 작업 및 리소스 mutex 소유가 겹쳤다. native 파괴와 다른 렌더 작업의 실행이 겹친 표본도 있다.

따라서 UI 갱신 뒤라는 이유만으로 다른 모든 UI·리소스·렌더 작업이 멈췄다고 해석하면 안 된다. 재귀 mutex의 중간 unlock도 최종 소유 해제나 전체 작업 완료의 증거가 아니다.

**구현 정책:** 서비스 콜백은 다음 조건을 조합한다.

1. 재진입과 추적 중인 UI·render·resource·Object destructor 구간을 피한다.
2. 리소스 manager의 `+0x8E8` mutex에서 타입 `0x102`를 확인하고 native try-lock을 한 번 시도한다. 관찰한 경로에서 반환값 `3`은 보류, `0`은 성공으로 취급한다. 성공한 경우에만 짝지어 unlock한다.
3. 그다음 UI owner의 `+0x260`을 CAS로 한 번 시도한다. 실패하면 해당 실행의 작업을 미룬다.
4. component 생성·리소스 bind 중에는 리소스 잠금을 유지하면서 owner 잠금을 놓을 수 있고, publication 전에 owner 잠금을 다시 확인한다.

이 잠금 순서와 활성 구간 추적은 이 구현의 계약이다. owner 잠금 하나가 모든 native hierarchy 작업을 보호한다는 뜻이 아니다.

## 읽기·후킹·호출 지점

아래 값은 모두 대상 실행 파일의 **RVA**다. 역할 이름은 공개 C# 바인딩의 이름이며 공식 심볼명이 아니다.

서비스 bridge는 `0x1CBD6F`의 **함수 내부 명령 위치**에 연결된다. 일반 함수 진입점 hook과 구별해야 한다. bridge는 정수 레지스터·FLAGS·legacy FX 상태와 Win64 stack/shadow space를 보존한다. upper YMM lane이 살아 있는 임의의 위치로 재사용할 수 있다는 근거는 없다.

실행 구간 및 수명 관찰에 사용하는 entry hook은 다음과 같다.

- UI 갱신 `0x2136FA0`, 렌더 경로 `0x1A97D0`, 리소스 작업 `0x8D78D0`.
- Object destructor `0x2135180`, Canvas 해제 `0xBDF5E0`, Image destructor `0x3342DB0`.
- Image render `0x3342FB0`, Image command 생성 `0x3345060`, command consumer `0x43EF740`.
- 록온 컨트롤러 갱신 `0x26939D0`.

생성·등록·삭제를 위해 호출하는 별도 native 경로에는 Object `0x26181C0`, Canvas `0xBDF3A0`, Image `0x3340DC0`, attach `0x2618DA0`, Image 초기 bind `0x3345D60`, owning map insert `0x2520180`, 삭제 요청 `0x261CD70`이 있다. 이 주소 목록만으로 올바른 인수·소유권·실행 문맥이 갖춰지는 것은 아니다.

전용 아틀라스 요청 entry hook은 `0x25374A0`이며 준비 상태 확인 호출은 `0x2539460`이다. 사용 지점의 정확한 바이트 접두부와 delegate는 [NativeBindings](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeBindings.cs)에, 실제 hook 집합은 [GameHooks](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/GameHooks.cs)에 있다. [NativeServiceBridge](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeServiceBridge.cs)는 중간 명령 bridge의 제한을 보여준다.

**정적 확인:** 타이유타르 공용 복구 함수에서 네 번째 다리의 현재값 쓰기 직후 명령은 RVA `0x2D35B84`에 있다. 이 지점에서 RSI는 개체를, EAX는 게임이 계산해 다리에 기록한 값을 담는다.

**구현 정책:** 선택적 내부 명령 후크는 이 값을 읽고 지원 빌드·명령 바이트 및 개체·주기 관계를 검사한다. 관찰값을 신뢰할 수 없을 때 다리 백분율은 표시하지 않는다. 이 위치의 레지스터 상태를 다른 명령 위치에 일반화해서는 안 된다. [배포 구현](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeServiceBridge.cs)을 참고한다.

## 렌더 명령 참조와 파괴

**정적 확인:** 검사한 command producer에서 `command+0x248`의 material 참조와 `command+0x290`의 effect-cache payload는 소유 방식이 다르다. material retain은 effect payload까지 retain했다는 증거가 아니다. 검사한 `+0x290` 경로는 cache payload를 빌리는 경로를 포함한다.

**구현 정책:** 모드의 plain Image command는 `+0x290 == 0`인 경로로 제한한다. command 생성 이후 해당 command의 consumer 반환까지 별도로 추적한다. effect가 있는 임의의 Image까지 같은 정리 방식으로 확장하지 않는다.

정리는 다음 순서를 따른다.

1. 자신이 소유한 Object를 비활성화하여 새 표시를 멈춘다.
2. 최소 세 번의 UI/render 반환과 최근 Image 사용 이후의 간격을 기다린다. 이 대기는 구현 정책이며 단독 fence가 아니다.
3. 추적한 command와 실행 중 consumer가 남지 않았는지 확인한다. owner의 Canvas 목록 `+0x2F8..+0x300`, graphics의 두 cache `+0x298` 및 `+0x478`에서도 해당 Object가 빠졌는지 확인한다.
4. 그 뒤 native deferred delete를 요청한다. Object/Canvas/Image의 destructor 완료와 live map 부재를 확인한 후 보조 wrapper 메모리를 해제한다.

전체 관리자 개수가 이전 값과 같다는 사실보다 자신의 key와 component가 소멸했다는 사실이 중요하다. 그사이에 다른 게임 UI가 만들어질 수 있기 때문이다. 위 절차에서 관찰한 마지막 소비는 CPU command consumer의 반환이다. 이를 GPU completion fence의 검증이라고 부르지 않는다.

구현상 상태 전이는 [IndicatorRuntime](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/IndicatorRuntime.cs)과 [NativeHud](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeHud.cs)의 `BeginRelease`, `PollRelease`, `Enter/Leave`에서 확인할 수 있다. 숨김·retirement·파괴 완료는 서로 다른 상태다.

## 록온 링 좌표와 화면 변환

RTTI 이름 `ui::component::ControllerCursorLockOn`인 컨트롤러가 resolve한 root의 `+0x150/+0x154` 화면 좌표를 사용한다. UI owner 기준 scale과 logical 크기로 다음 변환을 적용한다.

```text
x = pixelX / sx - width / 2
y = pixelY / sy + height / 2
```

**구현 정책:** 같은 완료 UI frame·resource epoch의 source인지, 관련 Object가 live map에 남아 있고 활성 상태인지 먼저 확인한다. 유효한 scale과 크기 및 유한한 좌표만 허용한다. 카메라의 추종 동작을 화면 중앙 고정 좌표의 근거로 삼지 않는다.

**실행 관찰:** native quad와 화면 좌표의 관계를 비교한 제한된 표본 및 실제 표시의 록온 추적·해제·재연결·장면 전환 검증이 있다. 초기 native 시제품의 정점 오차·프레임 표본 수를 다른 C# 실행의 계측 결과로 옮겨 쓰지 않는다. 이 관찰은 GPU 표시 지연을 측정한 것도 아니다.

Object의 크기만으로 최종 sprite quad 크기가 정해지지는 않는다. 아틀라스의 trim/padding과 UV 변환도 영향을 준다. 현재 구현은 자신이 소유한 Image의 padding만 정규화하고 원본 게임 Image의 필드를 수정하지 않는다. [HudCoordinates](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/HudCoordinates.cs)에서 변환식을 확인할 수 있다.

## 리소스 요청 문맥과 준비 상태

UI owner 전역 `RVA 0x7C01858`과 아틀라스 요청 context 전역 `RVA 0x7C17858`은 다른 문맥이다. 후자를 전자의 대용으로 사용하면 안 된다.

**구현 정책:** 전용 아틀라스는 원래 로더 콜백의 context·flags 및 현재 스레드의 리소스 mutex 소유를 검사한 문맥에서 한 번 요청한다. nonnull material 하나만으로 준비 완료로 판정하지 않고 metadata·UV·material 식별·native readiness를 대조한다. resource epoch나 material이 바뀌면 준비 상태를 무효화한다.

**구현 정책:** 리소스 세대가 바뀌어도 이전에 검증한 동일 material은 첫 안전한 UI 프레임에서 즉시 재검증한다. material이 교체됐거나 준비가 완료되지 않은 경우에는 검증될 때까지 표시를 숨긴다. [배포 구현](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeHeadAssets.cs)을 참고한다.

**실행 관찰과 한계:** 배포 ZIP 설치 후 앙그라 머리에 록온한 상태에서 메뉴를 열고 닫는 시험에서는 표시 깜빡임이 관찰되지 않았다. 이는 해당 메뉴 전환 시나리오에 한정된 관찰이다.

아틀라스 요청과 hook/delegate의 수명은 프로세스 종료까지 유지한다. 이 모듈의 suspend는 UI 정리를 요청하는 동작이며, 모듈의 완전한 unload와 같지 않다. 전용 아틀라스의 hot unregister를 구현한 것으로 해석하지 않는다. 관련 검사는 [NativeHeadAssets](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeHeadAssets.cs)에 있다.

## 미검증 및 업데이트 후 재확인

현재 plain Image 경로의 제한된 소유권·생성·소비·정리 관찰과 실제 UI 전환 결과가 있다. 이는 모든 effect/cache의 수명, GPU 완료, 동일 sort의 상대 순서, 3D depth occlusion, 임의 주소 재사용, 장시간 실행, 온라인 플레이 또는 실제 모듈 unload를 보증하지 않는다.

업데이트 뒤에는 RVA와 명령 경계뿐 아니라 wrapper move semantics, map node 이동/중복 처리, component attach 소유권, 실제 thread 문맥과 잠금 순서, command payload의 참조 방식, consumer 이후의 cache 제거, destructor 순서, 아틀라스 요청/ready 규약을 다시 확인해야 한다. 세 프레임 대기나 접두부 검사만 유지한 채 나머지 계약도 같다고 가정하면 안 된다.
