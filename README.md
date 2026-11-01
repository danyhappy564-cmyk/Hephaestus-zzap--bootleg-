# Hephaestus (SPT 4.1.6 포팅)

내가 저장해 둔 **무기 프리셋(무기 빌드)** 을 그대로 파는 상인 "Hephaestus"를 추가하는 서버 모드입니다.
프리셋 부품들의 플리마켓 시세를 합친 값에서 크게 할인된 가격(기본 90% 할인, 유로)으로 완성된 총을 바로 살 수 있습니다.

---

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

- **Original Author:** alexkarpen (Alexandros)
- **Original Repository:** Hephaestus
- **Original Link:** [https://github.com/alexkarpen/Hephaestus](https://github.com/alexkarpen/Hephaestus)
- **License:** MIT (원본 `package.json` 표기)

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

## 변경 이력

- 2026-11-01 12:02 — 1.2.0 첫 릴리즈 준비: 최종 코드로 다시 빌드(오류·경고 0, 자체 검사 94개 통과)하고 릴리즈 zip을 새로 묶음. 기능 변화 없음.
- 2026-11-01 11:46 — 실제 SPT 4.1 서버를 띄워 끝까지 시험(테스트 32개 통과)하다 찾은 문제 수정: 모드가 "SPT 4.1.6 전용"으로 표시돼 있어서, 서버가 자기 버전을 4.1.5로 알리면 **이 모드뿐 아니라 서버의 모든 모드가 로드되지 않는**(`NO MODS WILL BE LOADED`) 상태가 됐음 → "4.1.5 이상 4.2 미만"으로 넓힘.
- 2026-11-01 11:35 — 한국어/영어 자동 전환과 F12 설정 화면 추가.
  ① 상인 이름·설명·위치를 게임 언어에 맞춰 표시(게임 언어가 한국어면 "헤파이스토스"와 한국어 설명, 그 밖의 언어는 원래 영어 문구),
  ② 새 클라이언트 플러그인(`HephaestusClient.dll`)으로 F12에서 할인율·화폐·대체 가격·디버그 로그를 바꾸면 서버 `config.json`에 바로 저장되고 상인 화면을 다시 열면 새 가격 적용(서버 재시작 필요 없음),
  ③ F12 화면 언어는 "자동"이면 게임 언어를 따르고, 게임이 아직 언어를 정하기 전에는 윈도우 표시 언어를 씀(수동으로 한국어/English 고정 가능),
  ④ 서버 로그도 서버 PC의 윈도우 언어(한국어면 한국어)로 출력,
  ⑤ 서버·클라이언트·테스트를 한 번에 여는 `Hephaestus.sln` 추가.
- 2026-11-01 11:25 — SPT 3.7(TypeScript) 모드를 SPT 4.1.6 서버 모드(C#)로 새로 옮김(1.2.0). 원본에서 문제가 되던 부분도 같이 고침:
  ① 상인 목록을 만들 때 내 프로필에 저장된 프리셋 자체를 덮어쓰던 문제(프리셋 첫 부품 정보가 상인용 값으로 바뀌어 저장됨) → 복사본으로만 작업,
  ② 여러 프로필(Fika 등)에서 마지막에 접속한 사람의 프리셋으로 상인 물건이 통째로 바뀌던 문제 → 사람마다 자기 프리셋만 보이고 살 수 있게,
  ③ 가방 정리·아이템 이동 같은 행동을 할 때마다 상인 목록을 다시 만들고 가격이 매번 무작위로 흔들리던 것 → 상인 화면을 열거나 살 때만 만들고, 가격은 무작위 없는 시세 합으로 고정,
  ④ 지운 모드의 부품이 들어간 프리셋 때문에 상인 화면이 깨질 수 있던 것 → 그런 프리셋은 건너뜀,
  ⑤ 가격이 소수점·0원으로 나오던 경우 → 정수, 최소 1,
  ⑥ 4.1에서 쓸 수 없는 상인 ID(`hephaestus_alxk`)와 다른 모드와 겹치기 쉬운 초상화 경로(`img`)를 새 값으로 교체, 4.1 상인에게 필요한 항목(`isAvailableInPVE` 등) 추가.

## 설치

1. 릴리즈 zip(`release/Hephaestus-1.2.0.zip`)을 SPT 설치 폴더(`E:\SPT 4.1`)에 풀면 아래처럼 들어갑니다.
   ```
   E:\SPT 4.1\SPT_Runtime\user\mods\Hephaestus\     ← 서버 모드 (필수)
       Hephaestus.dll
       config\config.json
       db\base.json
       res\img.jpg
   E:\SPT 4.1\BepInEx\plugins\HephaestusClient.dll   ← F12 설정 화면 (선택)
   ```
2. 서버를 켜면 로그에 `[Hephaestus] 상인 추가 완료 (화폐 EUR, 할인 90%)`(영어 윈도우면 `trader added ...`)가 나오면 정상입니다.
3. 게임에서 무기 수정 화면(무기 빌더)으로 프리셋을 저장한 뒤 Hephaestus 상인을 열면 그 프리셋이 팔립니다.
4. F12 설정 화면이 필요 없으면 `HephaestusClient.dll`은 빼도 됩니다. 서버 모드만으로 상인은 그대로 동작하고, 설정은 `config.json`을 메모장으로 고치면 됩니다.

> 3.7용 옛 모드 폴더(`user/mods/Hephaestus`, `src/mod.js`가 있는 것)는 4.1에서 동작하지 않습니다. 이 저장소의 `user/` 폴더는 **원본 참고용**으로만 남겨 둔 것이니 설치하지 마세요.

## 설정 (`config/config.json`)

| 키 | 기본값 | 뜻 |
|---|---|---|
| `currency` | `569668774bdc2da2298b4568` (유로) | 가격 화폐. 루블 `5449016a4bdc2d6f028b456f`, 달러 `5696686a4bdc2da3298b456a` |
| `discount` | `90` | 할인율 0~100 (0 = 시세 그대로, 100 = 사실상 공짜 — 최소 1) |
| `cost` | `123` | 프리셋 시세를 하나도 못 구했을 때 쓰는 대체 가격(위 화폐 기준, 할인도 적용됨) |
| `debugLogging` | `false` | `true`면 상인 목록을 만들 때마다 서버 로그에 "프리셋 몇 개 판매 / 몇 개 건너뜀" 한 줄 |

`HELP_`로 시작하는 키는 설명용이라 무시됩니다. 값을 따옴표로 써도(`"90"`) 읽습니다.
F12에서 값을 바꾸면 이 파일의 값 4개만 바뀌고 `HELP_` 설명은 그대로 남습니다.

## F12 설정 화면 (`HephaestusClient.dll`)

| 분류 | 항목 | 뜻 |
|---|---|---|
| 0. 언어 | 언어 | `자동 · Auto`(기본) / `한국어 · Korean` / `English` |
| 1. 상인 가격 | 할인율 (%) | 0~100, 위 `discount` |
| 1. 상인 가격 | 가격 화폐 | 유로 / 루블 / 달러, 위 `currency` |
| 1. 상인 가격 | 대체 가격 | 1~10,000,000, 위 `cost` |
| 1. 상인 가격 | 서버 디버그 로그 | 위 `debugLogging` |
| 2. 상태 | 서버 연결 상태 | 마지막 저장 결과(읽기 전용) |

- 값을 바꾸면 0.8초 뒤 서버로 보내 `config.json`에 저장합니다(슬라이더를 끄는 동안은 한 번만 보냄). **상인 화면을 다시 열면** 새 가격이 나옵니다.
- 게임을 켤 때 서버의 현재 값을 받아 F12에 채웁니다(서버 `config.json`이 기준). 연결이 안 되면 10초 간격으로 6번까지 다시 시도하고, "서버 연결 상태"에 이유를 보여 줍니다.
- 설정은 서버 하나에 하나입니다. Fika처럼 여러 명이 한 서버를 쓰면 누가 바꾸든 모두에게 같이 적용됩니다.

## 언어 자동 선택

| 무엇 | 어떻게 정해지나 |
|---|---|
| 상인 이름·설명·위치 | 게임 설정 언어. 게임이 서버에 그 언어의 문구를 요청하므로 한국어(`kr`)면 한국어, 그 밖의 언어는 영어 |
| F12 화면 | "자동"이면 게임이 실제로 적용한 언어(한국어 → 한국어, 그 밖 → 영어). 게임이 아직 언어를 정하기 전(시작 직후)에는 윈도우 표시 언어. 1초마다 다시 확인하고, 바뀌면 **F12 창을 닫았다 다시 열면** 적용 |
| 서버 로그 | 서버를 켠 PC의 윈도우 표시 언어 |

게임 언어는 게임 내부 값(문구 관리자)을 직접 읽고, 게임 업데이트로 그 값을 못 읽게 되면 오류 없이 윈도우 언어로 대신합니다.

## 동작 방식 (원본과 같은 점 / 달라진 점)

- **같은 점**: 저장한 무기 프리셋마다 상품 1개, 재고 2000개(사실상 무제한), 신뢰도 1단계부터 구매 가능, 가격 = 부품 시세 합 × (1 - 할인율), 상인 새로고침 1시간, 상인 설명 문구·초상화.
- **달라진 점**
  - 상인 목록은 **상인 화면을 열 때와 살 때** 그 사람의 프리셋으로 새로 만듭니다(원본은 접속할 때와 아이템을 움직일 때마다).
    방금 저장한 프리셋이 안 보이면 상인 화면을 한 번 나갔다 다시 들어가 보세요.
  - 가격은 "플리 시세"(무작위 흔들림 없는 기준값)의 합입니다. 원본은 플리 판매 글처럼 매번 ±값이 섞여 화면을 열 때마다 가격이 달랐습니다.
  - 장비 프리셋·탄창 프리셋은 원본처럼 팔지 않습니다(무기 프리셋만).
  - 상인 ID가 바뀌었습니다(`a18160fc82b4f2be55205c89`). 4.1은 24자리 16진수 ID만 받기 때문입니다.

## 확인 상태

- `dotnet build -c Release` 성공(경고 0, 오류 0) — SPT 4.1.6 서버 패키지 기준.
- `Hephaestus.sln` 전체 빌드(서버·클라이언트·테스트) 경고 0, 오류 0.
- 자체 테스트 94개 통과(`tests/Hephaestus.Tests`): `base.json`을 SPT 4.1.6 JSON 규칙으로 읽기, 설정 읽기·저장(설명 키 보존·잘못된 값 보정), 원본이 남긴 실제 무기 데이터 40정으로 상품 만들기
  (프로필 프리셋 안 바뀜·ID 겹침 없음·부품 연결·재고·가격), 프로필별 분리, 패치 연결(대상 함수·매개변수 이름),
  언어 선택 규칙(수동/자동·게임 언어/윈도우 언어), 언어별 상인 문구, F12 요청 → 서버 → 응답(한국어 문구 포함) 왕복.
- **실제 SPT 4.1 서버에서 확인함**(컨테이너에서 서버를 띄우고 게임 대신 스크립트로 요청, `tests/e2e/e2e.py` 32개 통과):
  모드 로드·상인 등록, 게임이 받는 상인 정보(`isAvailableInPVE`·초상화 경로), 초상화 파일 응답, 언어별 문구(`kr` 헤파이스토스 / `en`·`ru` 영어),
  프리셋 저장 → 상인 목록(부품 전부·재고 2000·정수 가격·다시 열어도 같은 가격), 프로필 프리셋 안 바뀜, 다른 프로필엔 안 보임,
  F12 설정 라우트(불러오기·루블로 바꾸기·`config.json` 저장·설명 키 보존), 구매(총이 창고에 들어옴·표시 가격만큼 정확히 차감·부품 전부·ID 중복 없음, 두 번째 구매도 정상),
  서버 로그 한국어(한국어 환경으로 서버 실행 시).
- **아직 확인 안 된 것**(코드상 그래야 함, 미검증): 실제 게임 화면 — 상인 목록·한국어 이름 표시, F12 화면 표시·언어 자동 전환·저장 버튼 동작(F12 플러그인 자체는 게임 없이 실행할 수 없음).
- 컨테이너에서 만든 `HephaestusClient.dll`은 Unity 대신 빈 껍데기 DLL로 컴파일했습니다(결과 DLL은 BepInEx·netstandard만 참조함을 확인). 배포 전에는 아래처럼 PC에서 다시 빌드하는 것을 권장합니다.

## 빌드

`Hephaestus.sln`을 Visual Studio / Rider로 열거나:

```
dotnet build Hephaestus.sln -c Release
# 게임이 E:\SPT 4.1이 아니면: dotnet build Hephaestus.sln -c Release -p:SptRoot="D:\다른\경로"
# 테스트
dotnet run --project tests/Hephaestus.Tests -c Release -- Hephaestus
```

- 서버 모드: `Hephaestus/bin/Release/net10.0/`
- F12 플러그인: `HephaestusClient/bin/Release/HephaestusClient.dll` (게임 폴더의 `BepInEx\core\BepInEx.dll`, `EscapeFromTarkov_Data\Managed\UnityEngine.dll`을 참조)

## 라이선스

원본 `package.json`에 MIT로 표기되어 있어 이 포팅도 MIT를 따릅니다. 원작 권리는 원작자(alexkarpen)에게 있습니다.
