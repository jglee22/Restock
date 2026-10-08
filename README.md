# RESTOCK

Unity 6로 제작한 **3D 편의점 경영 시뮬레이션**입니다.

상품을 주문하고 선반에 진열한 뒤 영업을 시작하면 고객이 상품을 구매하고 계산대를 이용합니다.  
하루의 매출 목표를 달성하면 새로운 상품과 시설이 해금되고, 작은 편의점이 단계적으로 확장됩니다.

5단계 캠페인을 완료한 뒤에는 모든 콘텐츠가 열린 상태에서 샌드박스 형태로 계속 운영할 수 있습니다.

![RESTOCK Expanded Store](docs/hero-store.png)

---

## 🎬 Gameplay Video

[![RESTOCK Gameplay Video](https://img.youtube.com/vi/zXNlVgnIxnA/maxresdefault.jpg)](https://youtu.be/zXNlVgnIxnA)

---

## 🏪 Project Overview

| 항목 | 내용 |
| --- | --- |
| Engine | Unity 6000.4.2f1 |
| Language | C# |
| Render Pipeline | URP 17.4.0 |
| Input | Unity Input System 1.19.0 |
| Navigation | AI Navigation 2.0.12 |
| UI | uGUI 2.0.0 |
| Target Platform | Windows PC |
| Genre | Convenience Store Management Simulation |
| Development Type | 개인 포트폴리오 |

---

## Third-Party Assets

일부 3D 모델과 텍스처는 외부 에셋을 사용했으며,
재배포 라이선스 제한으로 GitHub 저장소에는 포함하지 않았습니다.

따라서 저장소만 clone한 경우 일부 비주얼 리소스가 표시되지 않을 수 있습니다.
완성된 게임 화면과 실제 플레이는 상단의 Gameplay Video와 스크린샷에서 확인할 수 있습니다.

---

## 🔄 Core Gameplay Loop

```text
Preparation
    ↓
Order
    ↓
Product Assignment
    ↓
Restock / Build / Upgrade
    ↓
Open
    ↓
Customer Shopping
    ↓
Checkout
    ↓
Closing
    ↓
Result
    ↓
Progression / Unlock
    ↓
Next Day
```

준비 단계에서는 상품 주문, 진열, 시설 건설과 업그레이드를 진행합니다.

영업을 시작하면 하루 이벤트가 결정되고 고객이 매장에 입장합니다. 고객은 상품을 구매한 뒤 계산대 Queue에 등록되며, 결제가 완료된 시점에 매출이 기록됩니다.

결산에서는 그날의 매출 목표를 평가합니다.

목표에 실패하더라도 **날짜는 다음 날로 진행**되지만,  
Progression Stage는 유지되어 같은 목표에 다시 도전합니다.

---

# ✨ Key Features

## Inventory & Ordering

상품의 고정 데이터와 실제 플레이 상태를 분리했습니다.

`ProductDefinition`은 상품의 정의를 담당하고,  
`StoreInventory`는 창고의 실제 보유 수량을 관리합니다.

```text
ProductDefinition
        ↓
StoreInventory
        ↓
Shelf
        ↓
Product Visual
        ↓
Customer Purchase
        ↓
Revenue
```

상품 주문으로 창고 수량이 증가하고, Restock 시 창고에서 선반으로 재고가 이동합니다.

고객이 실제로 선반에서 상품을 가져가는 데 성공한 경우에만 진열 수량이 감소합니다.

---

## Product Assignment & Restock

각 선반은 런타임에 판매 상품을 지정할 수 있습니다.

상품의 보관 타입과 시설 타입이 맞는 경우에만 지정할 수 있으며, 선반의 실제 수량에 따라 진열된 상품 비주얼도 함께 변경됩니다.

이를 통해 다음 상태를 분리했습니다.

```text
Warehouse Quantity
Shelf Assigned Product
Shelf Quantity
Shelf Product Visual
```

---

## Customer Shopping AI

고객은 `NavMeshAgent`를 이용해 매장 내부를 이동합니다.

```text
Spawn
 ↓
Enter
 ↓
Find Shelf
 ↓
Move
 ↓
Browse
 ↓
Purchase
 ↓
Checkout Queue
 ↓
Payment
 ↓
Exit
```

목표 선반에 도달하지 못하면 다른 선반을 한 번 더 탐색합니다.

재탐색에도 실패하면 현재 구매 상태에 따라 계산대로 이동하거나 매장을 퇴장하도록 처리했습니다.

![Customer Shopping](docs/customer-shopping.png)

고객의 상품 구매와 FIFO Checkout Queue.

---

## FIFO Checkout Queue

계산대가 고객 대기열을 직접 소유합니다.

고객은 Queue 뒤에 등록되고, 가장 앞에 있는 고객만 결제를 시작할 수 있습니다.

```text
Customer
   ↓
Queue Back
   ↓
Queue[0]
   ↓
Checkout
   ↓
Release
   ↓
Next Customer
```

결제 중인 고객이 Queue에서 제거되는 경우에는 진행 중인 Checkout을 무효화하고 다음 고객이 정상적으로 진행할 수 있도록 처리했습니다.

---

## Runtime Facility Build

시설은 Grid Cell과 Footprint를 기준으로 배치합니다.

배치 시 다음 항목을 검사합니다.

- 시설 크기
- 90도 회전
- Grid 점유 상태
- 기존 정적 시설 위치
- 벽 부착 여부
- 건설 가능한 영역

Scene에 처음부터 존재하는 시설과 플레이어가 직접 건설하는 시설이 **같은 Grid 규칙**을 사용합니다.

![Runtime Facility Build](docs/runtime-build.png)

Grid Cell과 Footprint를 기준으로 한 Runtime 시설 배치.

---

# 🛠 Technical Highlights

## 1. Calendar Day와 Progression Stage 분리

날짜와 매장 성장 단계를 같은 값으로 관리하지 않았습니다.

예를 들어 목표에 실패하면:

```text
Day 1 → Day 2
Stage 0 → Stage 0
```

날짜는 진행되지만 매장은 확장되지 않습니다.

목표를 달성한 경우에만:

```text
Day 1 → Day 2
Stage 0 → Stage 1
```

Progression Stage가 증가합니다.

이를 통해 **하루가 흐르는 것**과 **게임 진행도가 상승하는 것**을 서로 독립적으로 관리했습니다.

---

## 2. completedStage 기반 콘텐츠 해금

상품, 시설, 업그레이드, 이벤트마다 별도의 Unlock Flag를 저장하지 않습니다.

대신:

```text
completedStage
        ↓
Product Unlock
Facility Unlock
Upgrade Limit
Available Event
Static Store Expansion
```

형태로 현재 사용할 수 있는 콘텐츠를 계산합니다.

Progression State를 여러 곳에 중복 저장하지 않기 때문에 Save Data와 실제 게임 상태가 어긋나는 가능성을 줄였습니다.

---

## 3. Static / Dynamic Facility Grid

매장에는 두 종류의 시설이 존재합니다.

### Static Facility

Scene에 미리 존재하는 시설입니다.

Stage Progression에 따라 활성화될 수 있으며, 비활성 상태에서도 해당 Grid Cell은 예약됩니다.

따라서 플레이어가 미래의 확장 위치에 미리 시설을 건설할 수 없습니다.

### Dynamic Facility

플레이어가 런타임에 직접 배치하는 시설입니다.

다음 정보를 저장합니다.

```text
Facility ID
Grid Cell
Rotation
Assigned Product
Shelf Quantity
```

Save를 불러올 때 해당 정보를 검증한 뒤 시설을 복원합니다.

---

## 4. Runtime NavMesh Refresh

시설이 추가되거나 Stage Progression으로 새로운 선반이 활성화되면 고객이 이동할 수 있는 공간도 변경됩니다.

시설 변경 후 `NavMeshSurface.BuildNavMesh()`를 이용해 Navigation Mesh를 다시 생성합니다.

다만 여러 시설이 같은 프레임에 변경될 때마다 NavMesh를 반복 생성하지 않습니다.

```text
RequestNavMeshRefresh()
        ↓
Pending Request
        ↓
Next Frame
        ↓
BuildNavMesh()
```

이미 Refresh가 예약되어 있다면 추가 요청은 하나로 합칩니다.

이를 통해 Stage 전환처럼 여러 시설이 동시에 활성화되는 경우에도 NavMesh 갱신은 한 번만 실행됩니다.

---

## 5. Versioned Save / Load

Save Data는 JSON 기반이며 현재 버전은 **v5**입니다.

저장 파일:

```text
restock_save.json
```

저장 시점은 **Preparation**입니다.

저장하는 주요 상태:

```text
Day
Money
Revenue / Expense
Warehouse Inventory
Product Price
Static Shelf State
Dynamic Facilities
Upgrade Level
Completed Progression Stage
```

반면 다음과 같은 일시적인 Simulation State는 저장하지 않습니다.

```text
Customers
Checkout Queue
Current Customer Destination
Daily Runtime Statistics
Current Event Roll
```

영업 도중의 고객 상태까지 복원하기보다, 다음 영업을 시작할 수 있는 **매장 상태를 저장하는 것**으로 범위를 제한했습니다.

---

### Save Migration

현재 Save Version은 5이며 이전 버전도 읽을 수 있습니다.

```text
v1
→ v2 : Dynamic Facility Placement

v2
→ v3 : Facility Product State

v3
→ v4 : Upgrade State

v4
→ v5 : Progression Stage
```

Progression 정보가 존재하지 않는 이전 Save는 기존 플레이어의 콘텐츠가 다시 잠기지 않도록 Campaign Complete 상태로 해석합니다.

잘못된 Version이나 유효하지 않은 데이터는 적용하지 않습니다.

---

# 📈 Progression

캠페인은 총 5개의 매출 목표로 구성되어 있습니다.

| Stage | Revenue Goal | 주요 해금 |
| --- | ---: | --- |
| Stage 0 | ₩38,000 | 라면 상품군 / 통로 진열 공간 |
| Stage 1 | ₩79,000 | 과자 상품군 / 북쪽 벽 진열 공간 / 진열대 |
| Stage 2 | ₩81,000 | 추가 벽 공간 / 벽 진열대 / 냉장고 / 업그레이드 |
| Stage 3 | ₩85,000 | 계산대 / 업그레이드 상한 확장 |
| Stage 4 | ₩90,000 | Campaign Complete |

Stage 5에 도달하면 모든 콘텐츠가 열린 상태에서 샌드박스 플레이를 계속할 수 있습니다.

![Campaign Complete](docs/campaign-complete.png)

5단계 목표 완료 후 Sandbox로 이어지는 Campaign Complete 상태.

---

## Store Expansion

Progression은 단순히 메뉴의 상품만 늘리는 것이 아니라 실제 매장 공간에도 반영됩니다.

| Starter Store | Expanded Store |
| --- | --- |
| ![Starter Store](docs/starter-store.png) | ![Expanded Store](docs/expanded-store.png) |

### Stage 0

작은 Starter Store에서 시작합니다.

```text
Refrigerator × 2
Aisle Shelf × 1
Checkout × 1
```

### Stage 1

매장 중앙에 새로운 통로 진열 공간이 추가됩니다.

### Stage 2

벽 진열 공간과 과자 상품군이 추가됩니다.

### Stage 3+

추가 벽면 시설과 건설/업그레이드 콘텐츠가 열리면서 전체 매장을 활용할 수 있습니다.

---

# 🧱 Architecture

주요 Runtime 책임은 다음과 같이 분리했습니다.

| Class | Responsibility |
| --- | --- |
| `StoreSession` | Day Lifecycle / Game Time |
| `StoreProgression` | Goal / Stage / Unlock / Campaign |
| `StoreInventory` | Warehouse Inventory |
| `Shelf` | Product Assignment / Shelf Stock |
| `CustomerMover` | Customer Movement / Shopping |
| `CheckoutCounter` | FIFO Queue / Checkout |
| `BuildModeController` | Grid Placement / Facility / NavMesh Refresh |
| `StorePersistence` | Save / Load / Migration |
| `StoreHud` | Gameplay UI |

상품의 고정 데이터는 `ProductDefinition` ScriptableObject로 관리합니다.

범용 DI Container나 Event Bus 같은 추가 프레임워크를 만들기보다 현재 프로젝트 규모에서 필요한 책임만 분리하는 방향으로 구성했습니다.

---

# 🏷 Upgrades & Events

## Upgrades

매장 성장에 따라 다음 업그레이드를 사용할 수 있습니다.

- 빠른 계산
- 광고
- 입소문

완료 단계에 따라 구매 가능한 최대 레벨이 증가합니다.

---

## Store Events

영업을 시작할 때 하루에 한 번 이벤트가 결정됩니다.

현재 이벤트:

- 없음
- 붐비는 시간대
- 결제 지연
- 인기 상품

Progression Stage에 따라 등장 가능한 이벤트가 달라집니다.

---

# ⚠️ Known Technical Debt

현재 프로젝트 범위에서는 유지했지만, 기능을 확장한다면 다음 항목을 우선 개선할 수 있습니다.

### Static Facility Progression

일부 고정 시설의 활성 단계가 Scene Object Name에 의존합니다.

현재 매장 규모에서는 관리 가능하지만 시설 종류가 증가한다면 명시적인 Stage Data로 이전하는 것이 적합합니다.

### BuildModeController Responsibility

`BuildModeController`가 Grid Placement뿐 아니라 상품 지정, 시설 복원, NavMesh 갱신 요청까지 담당합니다.

현재 프로젝트에서는 관련 시설 규칙을 한곳에 유지했지만, 건설 기능이 확장된다면 책임 분리가 우선 개선 대상입니다.

### Automated Tests

Unity Test Framework 패키지는 존재하지만 자동화된 테스트 코드는 작성하지 않았습니다.

현재 프로젝트에서는 Play Mode 기반의 회귀 테스트와 런타임 상태 검증을 사용했습니다.

---

# 🎮 Controls

| Input | Action |
| --- | --- |
| WASD / Arrow Keys | Camera Move |
| Mouse Wheel | Camera Zoom |
| Mouse / Pointer | UI / Facility Interaction |
| R | Build Preview 90° Rotate |
| Esc | Close Panel / Cancel Build Mode |

영업 중 시간 제어는 HUD 버튼을 사용합니다.

```text
Pause
x1
x2
x3
Skip Time
```

---

# ▶️ Run

현재 저장소에는 배포 실행 파일을 포함하지 않습니다.

Unity에서 실행하려면:

```text
Unity Version
6000.4.2f1

Scene
Assets/Scenes/Main.unity
```

프로젝트를 연 뒤 `Main.unity` Scene을 실행하면 됩니다.

Save Data는 Unity의 `persistentDataPath` 아래에 다음 이름으로 생성됩니다.

```text
restock_save.json
```

---

# 📌 Development Focus

Restock에서는 기능 수를 늘리는 것보다 다음 문제를 해결하는 데 초점을 맞췄습니다.

```text
Day와 Progression State 분리

단일 Progression State에서
상품 / 시설 / 이벤트 / 업그레이드 파생

Static / Dynamic Facility의
Grid State 통합

시설 변경에 대응하는
Runtime NavMesh 갱신

기존 Save를 유지하면서
게임 진행 구조 확장
```

작은 편의점에서 시작해 상품을 판매하고, 목표를 달성하고, 매장이 실제로 확장되는 흐름을 하나의 게임 루프로 연결하는 것을 목표로 했습니다.