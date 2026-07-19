# RUN — Cách chạy PollBuilder

Các lệnh để chạy dự án (backend, frontend, Docker, test). Mọi lệnh chạy từ **thư mục gốc** `poll-service/` trừ khi ghi rõ khác.

Kiến trúc: 4 backend service (.NET) + 1 gateway + frontend (React) + SQL Server. Người dùng chỉ vào qua **Gateway** (cổng 5000) và **Frontend** (cổng 5173).

---

## 0. Chuẩn bị (làm 1 lần)

Tạo 2 file cấu hình từ file mẫu (cả 2 đều bị gitignore):

```bash
# secrets cho docker-compose (SA_PASSWORD, JWT_SECRET, Google, SMTP...)
cp .env.example .env

# config cho frontend dev (trỏ về gateway ở cổng 5000)
cp frontend/.env.example frontend/.env
```

> PowerShell dùng `Copy-Item .env.example .env` nếu `cp` không có.

Mở `.env` và điền giá trị thật: `SA_PASSWORD` (mật khẩu mạnh: hoa, thường, số, ký tự đặc biệt, 8+), `JWT_SECRET` (≥ 32 ký tự). `GOOGLE_CLIENT_ID` và `SMTP_*` chỉ cần nếu muốn test login Google / gửi email OTP thật (không có thì OTP in ra console).

---

## 1. Cách khuyến nghị — chạy TẤT CẢ bằng Docker

Chạy toàn bộ hệ thống (DB + 4 service + gateway + frontend) bằng một lệnh:

```bash
docker compose up --build
```

- Frontend: <http://localhost:5173>
- Gateway / API: <http://localhost:5000>
- Dừng: `Ctrl + C`, hoặc mở terminal khác chạy `docker compose down`

Chạy nền (detached) để terminal rảnh:

```bash
docker compose up --build -d
docker compose logs -f            # xem log
docker compose down               # dừng và xoá container
```

### Về `--build` (quan trọng)
- **Sửa code xong → phải `docker compose up --build`** để image chứa code mới. `up` thường sẽ dùng lại image cũ.
- Chỉ đổi `.env` / `docker-compose.yml` (không đụng code): `docker compose up -d --force-recreate` là đủ.
- Build lại đúng 1 service: `docker compose up --build -d vote-api`
- Xoá luôn database (reset sạch dữ liệu): `docker compose down -v` ⚠️ mất hết data.

---

## 2. Chạy phát triển (hybrid: backend Docker + frontend local)

Cách hay dùng khi code frontend: backend chạy Docker, frontend chạy Vite (hot-reload nhanh).

**Terminal 1 — backend qua Docker (không cần build lại frontend image):**
```bash
docker compose up --build -d db poll-api vote-api identity-api gateway
```

**Terminal 2 — frontend chạy local:**
```bash
cd frontend
npm install        # lần đầu, hoặc khi đổi dependency
npm run dev        # Vite ở http://localhost:5173, gọi API qua gateway :5000
```

`frontend/.env` đã trỏ `VITE_API_URL=http://localhost:5000/api` nên frontend local nói chuyện với gateway trong Docker.

---

## 3. Chạy một backend service riêng (debug trong Visual Studio / dotnet)

Dùng khi muốn debug đặt breakpoint một service. Cần SQL Server đang chạy (bật riêng bằng Docker):

```bash
docker compose up -d db     # chỉ bật database
```

Rồi chạy service muốn debug:

```bash
# ví dụ Identity API
dotnet run --project services/identity-api/IdentityApi/IdentityApi.csproj
```

Cổng dev (từ launchSettings) khi chạy trực tiếp:

| Service | Cổng dev |
|---|---|
| gateway | 5158 |
| poll-api | 5156 |
| vote-api | 5116 |
| identity-api | 5026 |

> ⚠️ **Lưu ý:** `gateway/appsettings.json` trỏ tới các service bằng **tên Docker** (`http://poll-api:8080`). Nên chạy **toàn bộ** backend ngoài Docker sẽ không tự thông nhau — muốn vậy phải override địa chỉ cluster (biến `ReverseProxy__Clusters__*__Destinations__default__Address`). Vì thế để chạy full stack, **ưu tiên Docker (mục 1)**; chạy `dotnet run` chỉ nên dùng để debug 1 service lẻ.

### Trong Visual Studio
Mở solution của service cần debug (mỗi service có `.sln` riêng, ví dụ `services/identity-api/IdentityApi.sln`) rồi bấm **F5**. Không có solution gộp cả 5 service — full stack chạy bằng Docker.

---

## 4. Test

```bash
# chạy test từng service
dotnet test services/poll-api/PollApi.sln
dotnet test services/vote-api/VoteApi.sln
dotnet test services/identity-api/IdentityApi.sln

# lint + build frontend (giống bước CI)
cd frontend
npm run lint
npm run build
```

---

## 5. Build kiểm tra (không chạy)

```bash
# build 1 service
dotnet build services/gateway/Gateway/Gateway.csproj -c Release

# build image Docker của 1 service mà không chạy
docker compose build vote-api
```

---

## 6. Lệnh Docker hay dùng

| Việc | Lệnh |
|---|---|
| Chạy tất cả (build mới) | `docker compose up --build` |
| Chạy nền | `docker compose up --build -d` |
| Xem log realtime | `docker compose logs -f` |
| Xem log 1 service | `docker compose logs -f gateway` |
| Dừng | `docker compose down` |
| Dừng + xoá DB | `docker compose down -v` |
| Xem container đang chạy | `docker compose ps` |
| Rebuild 1 service | `docker compose up --build -d poll-api` |
| Vào shell container | `docker compose exec gateway sh` |

---

## Ghi chú
- Schema database **tự tạo** khi service khởi động (EF Core migrations), không cần chạy migrate thủ công.
- Cần **Docker Desktop** đang chạy cho mọi lệnh `docker compose`.
- Muốn tạo Admin đầu tiên: đăng ký tài khoản như thường → đặt `Admin__Emails__0=email@cua.ban` cho identity-api → restart. Xem thêm [DEPLOYMENT.md](DEPLOYMENT.md).

## Demo live trigger action
```
git commit --allow-empty -m "ci: trigger deploy demo"
git push
```