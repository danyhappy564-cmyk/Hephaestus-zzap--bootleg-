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
   E:\SPT 4.1\SPT_Runtime\user\mods\Hephaestus\
       Hephaestus.dll
       config\config.json
       db\base.json
       res\img.jpg
   ```
2. 서버를 켜면 로그에 `[Hephaestus] trader added (currency EUR, discount 90%)` 가 나오면 정상입니다.
3. 게임에서 무기 수정 화면(무기 빌더)으로 프리셋을 저장한 뒤 Hephaestus 상인을 열면 그 프리셋이 팔립니다.

> 3.7용 옛 모드 폴더(`user/mods/Hephaestus`, `src/mod.js`가 있는 것)는 4.1에서 동작하지 않습니다. 이 저장소의 `user/` 폴더는 **원본 참고용**으로만 남겨 둔 것이니 설치하지 마세요.

## 설정 (`config/config.json`)

| 키 | 기본값 | 뜻 |
|---|---|---|
| `currency` | `569668774bdc2da2298b4568` (유로) | 가격 화폐. 루블 `5449016a4bdc2d6f028b456f`, 달러 `5696686a4bdc2da3298b456a` |
| `discount` | `90` | 할인율 0~100 (0 = 시세 그대로, 100 = 사실상 공짜 — 최소 1) |
| `cost` | `123` | 프리셋 시세를 하나도 못 구했을 때 쓰는 대체 가격(위 화폐 기준, 할인도 적용됨) |
| `debugLogging` | `false` | `true`면 상인 목록을 만들 때마다 서버 로그에 "프리셋 몇 개 판매 / 몇 개 건너뜀" 한 줄 |

`HELP_`로 시작하는 키는 설명용이라 무시됩니다. 값을 따옴표로 써도(`"90"`) 읽습니다.

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
- 자체 테스트 62개 통과(`tests/Hephaestus.Tests`): `base.json`을 SPT 4.1.6 JSON 규칙으로 읽기, 설정 읽기, 원본이 남긴 실제 무기 데이터 40정으로 상품 만들기
  (프로필 프리셋 안 바뀜·ID 겹침 없음·부품 연결·재고·가격), 프로필별 분리, 패치 연결(대상 함수·매개변수 이름).
- **실제 SPT 서버 기동과 인게임 동작은 아직 확인 안 됨**(코드상 그래야 함, 미검증). 상인이 목록에 보이는지, 프리셋 구매가 되는지는 게임에서 확인이 필요합니다.

## 빌드

```
cd Hephaestus
dotnet build -c Release
# 테스트
dotnet run --project ../tests/Hephaestus.Tests -- ../Hephaestus
```

## 라이선스

원본 `package.json`에 MIT로 표기되어 있어 이 포팅도 MIT를 따릅니다. 원작 권리는 원작자(alexkarpen)에게 있습니다.
