# Part Break Indicator

개발 중인 Granblue Fantasy: Relink용 Reloaded-II 모드입니다. 지원하는 록온 부위에 미파괴/파괴 상태를 표시하고, 초기 내구도를 확인한 경우 남은 내구도를 정수 백분율로 표시합니다. 앙그라 마이뉴의 두 뿔은 하나의 머리 그림에서 독립적으로 표시합니다.

아직 공개 릴리즈가 아니며 전체 보스 지원이 완료되지 않았습니다. 오프라인 플레이를 대상으로 합니다. 기본 체력바가 곧 파괴 진행도인 거골 리액터의 추가 표시는 보류했습니다.

## 소스 상태

현재 소스는 코드 검토와 테스트를 통과한 미설치 머리 지원 후보 `82572c0ddc8474895bd61ef895383cc6d3e64b33f24a2f573ccd36fc6bad349d`에 대응합니다. 게임 설치본은 `98b7618d61cec66b2816e5058ce44d4951fe7cdf021b32da482a7dd3a37a7dad`이며, [설치본 체크포인트](../../docs/checkpoints/2026-11-08-part-break-installed.md)를 별도로 보존했습니다. 새 머리 지원의 실제 피해·파괴·재생과 UI는 아직 게임에서 검증하지 않았습니다.

이후 단계와 설치 여부는 [체크포인트 기록](../../docs/checkpoints/)을 확인하세요. `ModConfig.json`은 승인된 개발용 0.1.0 설정을 유지하므로, 설명에 이전 막대 게이지 표현이 남아 있습니다.

## 개발 환경과 확인

- Windows x64, .NET SDK 9.
- Reloaded-II와 `reloaded.sharedlib.hooks`, `gbfrelink.utility.manager`.
- 프로젝트 전용 앙그라 UI 리소스는 `assets/GBFR/`에 포함합니다. 게임에서 추출한 리소스나 외부 도구 DLL은 포함하지 않습니다.
- `PartIndicator.csproj`의 `ReloadedDirectory` 기본값은 사용자 프로필 아래 `Desktop/Tools/Reloaded-II`입니다. 필요하면 MSBuild 속성으로 다른 설치 경로를 지정하세요. 테스트 프로젝트의 외부 DLL 경로는 현재 개발 환경 기준입니다.

저장소 루트에서 실행합니다.

```powershell
dotnet build work/part_indicator_mod/PartIndicator.csproj -c Release
dotnet run --project work/part_indicator_mod/Tests/PartIndicator.Tests.csproj -c Release
```

빌드는 게임 실행 검증을 대신하지 않습니다. 개발용 Frida 관찰기는 모드에 자동 포함하거나 실행하지 않습니다. 게임에서 모듈을 완전히 제거하려면 재시작이 필요합니다.

개발 과정에는 Codex와 REA를 사용했으며, 게임 화면과 입력·자연 파괴·복구 확인은 사용자가 수행했습니다.
