# DumpInspector

DumpInspector는 윈도우 크래시 덤프(.dmp)를 업로드하면 서버에서 WinDbg(cdb)를 호출해 `!analyze -v` 결과를 실시간으로 제공하고, 관리자 페이지에서 사용자·옵션·분석 로그를 관리할 수 있는 웹 애플리케이션입니다.

- **Backend**: ASP.NET Core (.NET 8), MariaDB (Pomelo)
- **Frontend**: React + Vite
- **실시간 분석**: cdb.exe 실행 출력이 WebSocket을 통해 스트리밍됨
- **관리 기능**: 사용자 등록(이메일로 임시 비밀번호 전송), 옵션/심볼/SMTP 설정, 업로드 로그 확인

---

## 주요 기능

- **덤프 업로드 & 즉시 분석**
  - `.dmp` 파일 업로드 시 자동으로 WinDbg cdb를 실행 (`!analyze -v`)
  - 업로드 응답으로 세션 ID를 반환, WebSocket을 통해 실시간 로그 수신
  - 분석 완료 후 요약 및 상세 리포트 저장/표시

- **실시간 스트리밍**
  - `/ws/analysis?id=...` WebSocket으로 cdb 출력이 흐름
  - 완료 시 상세 리포트가 클립보드 복사 버튼과 함께 표시됨

- **PDB 업로드 & SymStore 연동**
  - 상단 탭의 **Upload PDB** 화면에서 `.pdb` 파일을 업로드하면 `symstore.exe`가 자동으로 실행되어 지정한 심볼 스토어 경로로 저장
  - 심볼 스토어 경로/제품명/`symstore.exe` 경로는 CrashDumpSettings 옵션에서 관리하며, `Symbol Path`에 포함하면 cdb가 즉시 활용

- **관리자 대시보드**
  - 사용자 생성 (아이디/이메일, 임시 비밀번호 자동 발급 & 이메일 전송)
  - 사용자 목록/삭제(관리자는 삭제 불가), 임시 비밀번호 재발급
  - 플랫폼 옵션: cdb 경로·심볼 경로·SMTP·NAS·분석 타임아웃 등 설정
  - 업로드 로그 확인 (파일/사용자/IP/요약/리포트)
  - 보안상 관리자 계정은 덤프/PDB 업로드 및 분석 기능을 사용할 수 없으며, 관리 작업과 비밀번호 변경에만 집중합니다.

---

## 환경 요구 사항

| 항목 | 설명 |
| --- | --- |
| .NET 8 SDK | 백엔드 빌드/실행 |
| Node.js 18+ | 프런트 빌드/실행 (Vite) |
| MariaDB 10.x | 사용자/옵션/로그 저장 (기본 연결 문자열 제공) |
| WinDbg (cdb.exe) | `!analyze -v` 실행; Windows Kits 디버거 설치 필요 |
| SMTP 서버 (옵션) | 사용자 임시 비밀번호 전달 메일 송신용 (예: 네이버 SMTP) |

> ⚠️ WinDbg는 `C:\Program Files (x86)\Windows Kits\10\Debuggers\{x86\|x64}\cdb.exe`에 설치되어 있다고 가정합니다. 잡히지 않으면 관리자 옵션에서 직접 경로를 지정하세요.

---

## 빠른 시작

1. **MariaDB 준비**
   ```sql
   CREATE DATABASE dumpinspector CHARACTER SET utf8mb4;
   ```
   `DumpInspector.Server/appsettings.json`의 `ConnectionStrings:DefaultConnection`을 환경에 맞게 수정합니다.

2. **백엔드 실행**
   ```bash
   cd DumpInspector.Server
   dotnet restore
   dotnet run
   ```
   최초 실행 시 DB를 생성하고 기본 관리자 계정(`admin`/config에 지정된 초기 비밀번호)을 만듭니다.

3. **프런트 실행**
  ```bash
   cd dumpinspector.client
   npm install
   npm run dev
   ```
   개발 환경에서는 Vite가 `/api`와 `/ws`를 `http://localhost:5000`으로 프록시합니다.

---

## 관리자 계정 & 옵션

- 최초 로그인: `admin` / `CrashDumpSettings:InitialAdminPassword` (기본 `Admin@123`)
- 관리자 패널에서 다음 항목을 설정하세요.
- 관리자 계정은 옵션/사용자 관리와 본인 비밀번호 변경만 수행하며, 덤프·PDB 업로드 및 분석 기능은 일반 사용자 전용입니다.

| 항목 | 설명 |
| --- | --- |
| SMTP 설정 | 호스트/포트/SSL/계정/From 주소 (임시 비밀번호 전송) |
| CDB Path | WinDbg `cdb.exe` 경로 (미지정 시 기본 설치 경로 탐색) |
| Symbol Path | `_NT_SYMBOL_PATH` (예: `srv*C:\symbols*https://msdl.microsoft.com/download/symbols`) |
| SymStore Path | `symstore.exe` 절대 경로 (미지정 시 Windows Kits 기본 경로 탐색) |
| Symbol Store Root | SymStore 저장 루트 (예: `C:\symbols`) |
| Symbol Store Product | SymStore `/t` 옵션에 사용할 제품명 |
| Analysis Timeout | 분석 최대 시간(초) |
| NAS 설정 | PDB를 NAS에서 제공할 경우 사용 |

옵션은 DB(`Options` 테이블)에 저장되며, SMTP·심볼 경로 등의 런타임 서비스가 매 요청마다 이 값을 읽기 때문에 서버를 재시작하지 않아도 즉시 반영됩니다. `appsettings.json`은 초기 기본값을 공급하는 용도로만 사용됩니다.

---

## 사용 시나리오

1. **덤프 업로드**
   - 업로드 화면에서 `.dmp` 파일 선택 → `Upload`
   - 업로드 응답으로 받은 `sessionId`로 WebSocket(`/ws/analysis`)에 자동 연결 → 실시간 로그 확인
   - 분석 종료 후 상세 리포트를 확인하고 복사 버튼으로 클립보드 저장
   - 관리자 패널의 업로드 기록에서 결과 재확인 가능

2. **PDB 업로드 & 심볼 스토어 등록**
   - 상단 탭에서 `Upload PDB`를 선택해 `.pdb` 파일을 올립니다.
   - 필요 시 제품명/버전/코멘트를 입력하고 업로드 버튼을 누르면 서버가 `symstore.exe add`를 실행
   - 완료 후 저장 경로·명령·symstore 출력 로그를 즉시 확인하고, `Symbol Path`에 해당 루트를 포함시켜 WinDbg에서 활용

3. **사용자 관리**
   - 관리자 패널 → 사용자 생성: 아이디 & 이메일 입력 → 생성
   - 임시 비밀번호가 이메일로 전송됨 (SMTP 정상 구성 필요)
   - 사용자 삭제/임시 비밀번호 재발급 가능 (관리자 계정은 삭제 불가)

4. **SMTP 구성 예시 (네이버)**
   - `smtp.naver.com`, 포트 `587`, SSL 사용
   - `Username` 및 `FromAddress`: 전체 이메일 주소
   - 앱 비밀번호 또는 계정 비밀번호 사용, 네이버 메일에서 SMTP 사용 활성화 필수

---

## 심볼 서버 호스팅 (Nginx 예시)

`symstore.exe`가 만든 폴더(`CrashDumpSettings:SymbolStoreRoot`)를 HTTP/파일 서비스로 그대로 노출하면 WinDbg가 `srv*https://your-host/symbols` 형식으로 심볼을 다운로드할 수 있습니다. 별도 API가 필요하지 않으며, 정적 파일을 서빙할 수 있는 Nginx/IIS/CDN이면 충분합니다.

```
server {
    listen 80;
    server_name symbols.example.com;

    location /symbols/ {
        alias /mnt/symstore/;   # SymbolStoreRoot와 동일한 경로
        autoindex off;
        add_header Cache-Control "public, max-age=31536000";
    }
}
```

위 구성을 적용하고 `_NT_SYMBOL_PATH`에 `srv*https://symbols.example.com/symbols*https://msdl.microsoft.com/download/symbols`를 추가하면 됩니다. Windows에서도 nginx 공식 배포판을 그대로 사용할 수 있습니다.

---

## 중요 참고 사항

- WinDbg 실행 권한이 없거나 심볼 경로가 잘못되면 분석이 실패합니다. WebSocket 스트림과 서버 로그에서 오류 메시지를 확인하세요.
- 분석이 타임아웃(기본 120초)을 초과하면 작업이 취소됩니다. 타임아웃을 늘리거나 심볼 다운로드 속도를 확인하세요.
- SMTP 설정이 잘못되면 사용자 생성 시 이메일 발송이 실패하며 계정도 롤백됩니다.
- WebSocket 스트림이 연결되지 않으면 결과가 출력되지 않습니다. 브라우저 개발자 도구에서 WebSocket 상태를 확인하세요.

---

## 폴더 구조 요약

```
DumpInspector.Server/
 ├─ Controllers/        # API Controllers (Auth, Admin, Dump, Options)
 ├─ Services/
 │   ├─ Analysis/       # WebSocket session manager, session storage
 │   ├─ Implementations # AuthService, CdbAnalysisService, SMTP sender 등
 │   └─ Interfaces      # 서비스 인터페이스 정의
 ├─ Models/             # 데이터 모델 (User, AppSettings, DumpAnalysisResult 등)
 ├─ Data/               # EF Core DbContext
 └─ appsettings*.json

dumpinspector.client/
 ├─ src/
 │   ├─ components/     # React 컴포넌트 (Upload, AdminPanel, OptionsEditor 등)
 │   ├─ api.js          # 프런트 API 래퍼
 │   └─ styles.css      # 전역 스타일
 ├─ vite.config.js      # 개발 서버 프록시 설정
 └─ package.json        # 프런트 종속성
```

---

## 자주 묻는 질문

| 질문 | 답변 |
| --- | --- |
| WinDbg 설치 경로를 찾지 못합니다. | 관리자 옵션에서 `CDB Path`를 절대 경로로 지정하거나 PATH에 추가하세요. 기본 설치 위치(`Windows Kits\10\Debuggers`)도 자동으로 탐색합니다. |
| 스트림이 비어 있습니다. | 업로드 응답의 `sessionId`를 이용해 WebSocket에 연결되고 있는지 확인하세요. Vite 개발 서버는 `/ws` 프록시 설정 후 재시작해야 합니다. |
| SMTP 오류(STARTTLS 등) | `Use SSL`을 켜고, 포트 587/STARTTLS 여부와 계정·비밀번호(앱 비밀번호 포함)를 다시 확인하세요. 메일 설정에서 SMTP 사용이 활성화되어 있는지 확인해야 합니다. |
| 관리자 계정이 삭제됩니다. | 서버에서 관리자 계정 삭제를 막아두었으므로 일반 사용자만 삭제됩니다. |

---

## 라이선스

프로젝트에 특정 라이선스가 정의되어 있지 않습니다. 필요 시 리포지토리에 LICENSE 파일을 추가하세요.

---

## 기타

- WinDbg 출력이나 SMTP 오류 등은 스트림/콘솔 로그에 그대로 기록됩니다. 문제가 발생하면 해당 로그 메시지와 함께 알려주세요.
- 개선 아이디어(예: 분석 취소, 다중 파일 처리 등)는 Issue/PR로 제안해 주세요.

Happy debugging!
