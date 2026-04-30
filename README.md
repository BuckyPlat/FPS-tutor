# 🎯 FPS Tutor

![Unity Version](https://img.shields.io/badge/Unity-6000.3.10f1-000000.svg?style=for-the-badge&logo=unity)
![Photon](https://img.shields.io/badge/Photon-PUN_2-004480.svg?style=for-the-badge)
![PlayFab](https://img.shields.io/badge/PlayFab-Cloud_Backend-2E8B57.svg?style=for-the-badge)
![UI System](https://img.shields.io/badge/UI_Toolkit-Modern_UI-6A5ACD.svg?style=for-the-badge)

**FPS Tutor** là một dự án game bắn súng góc nhìn thứ nhất (First-Person Shooter) nhiều người chơi được xây dựng trên nền tảng Unity 6. Game mang đến trải nghiệm đấu trường online với hệ thống phòng chơi Photon, đăng nhập tài khoản PlayFab, chiến đấu bằng nhiều loại vũ khí, hồi sinh, bảng xếp hạng, chat trong trận và giao diện hiện đại bằng UI Toolkit.

---

## ✨ Tính Năng Nổi Bật (Features)

### 🔫 Hệ Thống Bắn Súng FPS (Core FPS Mechanics)
*   **Điều khiển góc nhìn thứ nhất:** Hỗ trợ di chuyển, xoay camera, sprint, jump, recoil và cảm giác bắn trực tiếp thông qua các component `Movement`, `FPSCameraController` và `Weapon`.
*   **Vũ khí đa dạng:** Có cả hitscan weapon và projectile weapon, hỗ trợ damage RPC, reload, ammo UI, recoil, VFX pooling và hiệu ứng nổ.
*   **Máu và hồi sinh:** `Health` quản lý sát thương, death state, hiệu ứng vignette, hồi máu, respawn delay và pickup hồi phục trong map.

### 🌐 Kết Nối Nhiều Người Chơi (Photon Multiplayer)
*   **Tạo và tham gia phòng:** `RoomList` và Lobby UI cho phép người chơi tạo room, chọn map và tham gia các phòng đang mở.
*   **Đồng bộ trận đấu:** `RoomManager` quản lý các phase `Waiting`, `Live`, `Finished`, đồng bộ trạng thái bằng Photon Room Custom Properties.
*   **Kill feed và leaderboard:** Kill/death được xác nhận bằng Photon RaiseEvent, cập nhật điểm, bảng xếp hạng, kết quả trận và phần thưởng.

### ☁️ Tài Khoản & Lưu Trữ Đám Mây (Cloud & Account System)
*   **Tích hợp PlayFab:** Hỗ trợ đăng nhập, đăng ký, custom ID fallback và lưu display name cho người chơi.
*   **Cập nhật thống kê:** Kills và Deaths được gửi lên PlayFab Player Statistics để phục vụ theo dõi tiến trình tài khoản.
*   **Reward cục bộ:** Coin reward sau trận được lưu bằng `PlayerPrefs`, hỗ trợ vòng lặp thưởng thắng/thua trong menu.

### 🎒 Quản Lý & Giao Diện (UI & Management)
*   **UI Toolkit hiện đại:** Main Menu, Lobby và Gameplay HUD được xây dựng bằng UXML/USS với controller riêng biệt.
*   **HUD trong trận:** Hiển thị máu, đạn, timer, chat, kill feed, leaderboard, pause menu, respawn countdown và result screen.
*   **Tùy chỉnh cấu hình:** Lưu các thiết lập HUD, tooltip, âm lượng, mouse sensitivity, smoothing và invert mouse bằng `PlayerPrefs`.

---

## 📂 Kiến Trúc & Cấu Trúc Mã Nguồn

Dự án được tổ chức theo kiến trúc component-based quen thuộc của Unity, tách rõ gameplay, networking, UI và prefab runtime cho Photon:
*   `RoomManager` & `RoomList`: Điều phối room flow, match state, spawn player, respawn, score, reward và kết nối Photon Lobby.
*   `PlayerSetup` / `Movement` / `Weapon` / `Health`: Nhóm component cốt lõi của player, chịu trách nhiệm local setup, di chuyển, chiến đấu và máu.
*   `UIToolkitMenuController` / `UIToolkitLobbyController` / `UIToolkitGameplayUIController`: Điều khiển toàn bộ UI Toolkit cho menu, lobby và gameplay HUD.
*   `PlayFabLogin` / `PlayFabRegister`: Xử lý account flow, display name, custom ID fallback và kết nối PlayFab SDK.
*   `Assets/Resources`: Chứa các prefab được spawn qua `PhotonNetwork.Instantiate`, bao gồm `Player`, projectile, hit VFX và health pickup.

---

## 🛠️ Yêu Cầu Cài Đặt (Requirements)

1.  **Unity Editor:** Phiên bản chính xác `6000.3.10f1`.
2.  **Package phụ thuộc chính:**
    *   Photon PUN / Photon Realtime / Photon Chat
    *   PlayFab SDK & PlayFab Editor Extensions
    *   UI Toolkit & UGUI
    *   Cinemachine, Post Processing, Input System
3.  **Dịch vụ online:** Cần cấu hình Photon AppId và PlayFab TitleId hợp lệ để test đầy đủ login, lobby và multiplayer.

## 📌 Hướng Dẫn Nhanh (Quick Start)

1. Clone dự án về máy:
```bash
git clone https://github.com/BuckyPlat/FPS-tutor.git
```
2. Mở Unity Hub, chọn **Add Project from disk** và trỏ tới thư mục root `FPS-tutor`.
3. Chờ Unity Package Manager tự động restore toàn bộ package trong `Packages/manifest.json`.
4. Cấu hình Photon AppId và PlayFab TitleId nếu cần test online/backend.
5. Trong Unity, mở scene `Assets/_Game/Scenes/Core/Menu.unity` và nhấn **Play** để bắt đầu từ Main Menu.

---
*Dự án thuộc bản quyền phát triển bởi Team ([BuckyPlat](https://github.com/BuckyPlat), [Bapnuong](https://github.com/Bapnuong), [Reider25](https://github.com/Reider25) và [Yutaka ReiRoku](https://github.com/Yutaka-ReiRoku)).*
