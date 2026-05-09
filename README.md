# CBT Exam System

A fully standalone, offline Computer-Based Testing system built with .NET 8 WPF + ASP.NET Core + SQLite.

---

## Solution Structure

```
CbtExam.sln
├── src/
│   ├── CbtExam.Shared/          # Shared models & DTOs
│   │   ├── Models/Entities.cs
│   │   └── DTOs/Dtos.cs
│   ├── CbtExam.Data/            # EF Core + SQLite
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── CbtExam.Api/             # ASP.NET Core Web API
│   │   ├── ApiBootstrap.cs
│   │   ├── Controllers/
│   │   │   ├── ExamsController.cs
│   │   │   ├── SessionsController.cs
│   │   │   └── StudentController.cs
│   │   ├── Hubs/ExamHub.cs      # SignalR real-time
│   │   └── Services/
│   │       ├── QuestionShuffler.cs
│   │       └── DataSeeder.cs
│   └── CbtExam.Desktop/         # WPF Admin App (entry point)
│       ├── Views/               # XAML pages
│       ├── ViewModels/          # MVVM
│       ├── Services/            # Server host + API client
│       ├── Converters/
│       └── wwwroot/             # Student web app (embedded)
│           ├── index.html
│           ├── css/style.css
│           └── js/app.js
└── build.bat                    # One-click publish script
```

---

## Prerequisites (Development Only)

- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
- Visual Studio 2022 or VS Code

---

## Development Setup

```bash
# 1. Restore packages
dotnet restore CbtExam.sln

# 2. Run the desktop app
dotnet run --project src/CbtExam.Desktop/CbtExam.Desktop.csproj
```

---

## Building for Production (Single EXE)

```bash
# Option 1: Use the build script
build.bat

# Option 2: Manual command
dotnet publish src/CbtExam.Desktop/CbtExam.Desktop.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output publish/CbtExam
```

Output: `publish/CbtExam/CbtExam.exe`

- No .NET runtime required on target machine
- No installation required
- Double-click to launch

---

## How to Use

### Admin (Exam Server Machine)

1. Double-click `CbtExam.exe`
2. Click **▶ Start Server** in the sidebar
3. Go to **Exams** → Create an exam → Add questions
4. Go to **Session** → Select exam → Click **▶ Start Session**
5. Note the **Session Code** and **Join URL** displayed
6. Share the URL with students (e.g., `http://192.168.1.100:5000?code=A1B2C3`)
7. Monitor students in real-time via **Monitor** tab
8. View results via **Results** tab after exam ends
9. Use **End All** to force-stop all active sessions (auto-submits pending students)

### Students

1. Open the Join URL in any browser on the same LAN
2. Enter the Session Code, Full Name, and Student ID
3. Click **Join Exam**
4. Answer all questions using the navigator panel
5. Click **Submit Exam** when done (or it auto-submits when time expires)
6. Use **Save & Exit** to continue later from Resume

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  WPF Desktop App                     │
│  ┌─────────────┐    ┌──────────────────────────────┐ │
│  │  Admin UI   │    │  Embedded ASP.NET Core Server │ │
│  │  (MVVM/WPF) │◄──►│  Kestrel on :5000            │ │
│  └─────────────┘    │  ┌──────────┐ ┌───────────┐  │ │
│                     │  │ REST API │ │ SignalR   │  │ │
│                     │  └──────────┘ └───────────┘  │ │
│                     │  ┌──────────────────────────┐ │ │
│                     │  │  EF Core + SQLite DB     │ │ │
│                     │  └──────────────────────────┘ │ │
│                     │  ┌──────────────────────────┐ │ │
│                     │  │  Static Files (wwwroot)  │ │ │
│                     │  └──────────────────────────┘ │ │
│                     └──────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
                           │ LAN (HTTP)
              ┌────────────┼────────────┐
              ▼            ▼            ▼
         [Browser]    [Browser]    [Browser]
         Student 1    Student 2    Student N
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/exams | List all exams |
| POST | /api/exams | Create exam |
| DELETE | /api/exams/{id} | Delete exam |
| POST | /api/exams/{id}/questions | Add question |
| DELETE | /api/exams/questions/{id} | Delete question |
| GET | /api/sessions | List sessions |
| POST | /api/sessions/start | Start session |
| POST | /api/sessions/{id}/stop | Stop session |
| POST | /api/sessions/end-all | End all active sessions |
| POST | /api/sessions/export | Export JSON snapshots/log bundles |
| GET | /api/sessions/{id}/students | Get student statuses |
| GET | /api/sessions/{id}/results | Get results |
| POST | /api/student/join | Student joins exam |
| GET | /api/student/{id}/questions | Get shuffled questions |
| POST | /api/student/submit | Submit answers |
| POST | /api/student/progress | Save per-question progress |
| GET | /api/student/{id}/progress | Load saved progress |
| POST | /api/student/tabswitch | Report tab switch |
| POST | /api/student/heartbeat | Device state heartbeat |
| POST | /api/student/snapshot | Camera snapshot upload |

---

## Question Format (JSON Import)

```json
{
  "questionNumber": 1,
  "text": "What is the capital of France?",
  "options": ["London", "Berlin", "Paris", "Madrid"],
  "correctAnswer": "Paris"
}
```

The shuffling engine:
- Shuffles options using Fisher-Yates algorithm
- Tracks correct answer by **value** (not index)
- Returns `correctIndex` pointing to the correct option in the shuffled array
- Never exposes `correctAnswer` to the student frontend

---

## Anti-Cheat Features

- Tab switch detection via Visibility API → logged to DB
- Fullscreen enforcement → re-requested on exit
- Right-click disabled during exam
- Keyboard shortcuts (Ctrl+C, F12, etc.) blocked
- Refresh warning via `beforeunload` event
- All violations visible in Monitor tab
- Optional camera snapshot capture from student exam page
- Device heartbeat includes connection state and battery telemetry

---

## Security Notes

- Admin endpoints (`/api/exams`, `/api/sessions`) require header `X-Admin-Key`.
- Default admin key is `admin123`; set `CBT_ADMIN_KEY` in environment for production.
- Student-facing endpoints remain accessible for browser exam clients.

---

## Smoke Checklist

1. Launch desktop app and confirm server URL appears in Settings.
2. Create an exam, import questions JSON, start a session.
3. Join from student browser, answer a few questions, use Save & Exit.
4. Rejoin and resume; submit and verify score/result page.
5. Confirm monitor shows live answered count/current question/connection/battery.
6. Click End All and verify unsubmitted students become submitted.
7. Run Export and verify files under `database/` plus dated artifacts in `database/results` and `database/logs`.

---

## Database

SQLite file: `cbt_exam.db` (created automatically on first run)

Tables: `Exams`, `Questions`, `ExamSessions`, `Students`, `StudentExams`, `Answers`

---

## Troubleshooting

**Server won't start**: Check if port 5000 is in use. Change port in Settings tab.

**Students can't connect**: Ensure Windows Firewall allows port 5000.
Run: `netsh advfirewall firewall add rule name="CBT Exam" dir=in action=allow protocol=TCP localport=5000`

**Database errors**: Delete `cbt_exam.db` to reset (loses all data).
