# 브라우저 CDP 크롤링 흐름 (엣지/크롬)

프로그램은 HTML 획득을 WebFetch.exe(Playwright 기반)에 위임한다.
WebFetch는 사용자가 `--remote-debugging-port` 옵션으로 실행해 둔 엣지 또는 크롬에
CDP(Chrome DevTools Protocol)로 접속해 페이지를 열고 HTML을 반환한다.

직접 HTTP 요청을 보내지 않는 이유: FMKorea/DCInside는 로그인 세션 쿠키와 자바스크립트
렌더링을 요구하므로, 로그인된 실브라우저 세션을 그대로 사용한다.

## 전체 흐름

1. WindowsApp에서 시작 버튼 클릭
2. 사용할 CDP 포트 결정 (ResolveCdpPort)
   - CDP 포트 자동 감지가 켜져 있으면 CdpPortFinder로 실행 중인 브라우저 프로세스에서 획득
   - 감지 실패 시 수동 입력한 포트 사용 (엣지/크롬 각각 저장)
3. 해당 포트 접속 검사 (IsCdpRunning, 127.0.0.1 TCP 1초)
   - 실패 시 브라우저별 실행 명령을 안내하고 시작을 중단
4. 확정된 포트를 CrawlTaskManager.CdpPort에 저장하고 크롤링 시작
   - 로그: `{브라우저} CDP 포트 {포트} 사용 (자동 감지|수동 설정)`
5. 사이트별 CrawlTaskWorker 스레드가 CrawlTask를 크롤링 주기마다 실행
6. CrawlTask.Do() → AbstractCrawl.TryCrawl(창 모드, CDP 포트)
7. TryDownloadSource()가 WebFetch.exe 실행
   - 인자: `"{url} {tab|window|hidden|headless} {port}"`
8. WebFetch: Playwright `ConnectOverCDP("http://127.0.0.1:{port}")` 접속
   - tab/window/hidden: 라이브 브라우저에 페이지를 열고 URL 이동(30초) → HTML 표준출력 → 페이지 닫기
   - headless: 라이브 브라우저에서는 쿠키와 User-Agent만 읽고, 같은 브라우저 실행 파일을
     헤드리스로 별도 실행해 URL 이동(30초) → HTML 표준출력 → 헤드리스 브라우저만 종료
9. AbstractCrawl: 로그인 벽 검사(FMKorea) → HtmlAgilityPack 파싱 → CrawlResult 목록 생성
10. CrawlTask: 매칭 판정 (닉네임/제목, 포함/일치, 최근 N분, 중복 제외)

## WebFetch.exe 사용법

```
WebFetch.exe <url> [tab|window|hidden|headless] [port]
```

- stdout: HTML / stderr: 실패 사유 / 종료코드: 0 성공, 1 실패
- port 기본값은 9222이며, 1~65535 범위를 벗어나면 종료코드 1과 함께 거부한다.
- CDP 접속 실패 메시지:
  `CDP 포트 {port}에 접속하지 못했습니다. 엣지/크롬을 --remote-debugging-port={port} --user-data-dir=<전용폴더> 옵션으로 실행하세요.`
- 60초 응답 타임아웃은 호출측(AbstractCrawl)이 관리한다. 초과 시 프로세스를 강제 종료한다.
  stdout/stderr는 비동기로 동시에 읽은 뒤 `WaitForExit(60000)`을 적용하므로,
  출력 파이프가 가득 차서 대기하는 상황에서도 타임아웃이 동작한다.

## 창 모드 4종

| 모드 | 동작 | 구현 |
|---|---|---|
| tab (새 탭) | 기존 브라우저 창에 새 탭으로 | `context.NewPageAsync()` |
| window (새 창) | 새 브라우저 창으로 | `Target.createTarget` (newWindow: true) |
| hidden (최소화) | 최소화 상태의 새 창 | createTarget + `Browser.setWindowBounds`(minimized) 이중 설정 |
| headless (완전 무창) | 창/탭을 만들지 않고 별도 헤드리스 브라우저로 | `chromium.LaunchAsync(headless: true)` + 쿠키 이식 |

- 새 창은 고유 fragment가 붙은 `about:blank#GUID`로 생성해, 동시에 실행 중인 다른
  WebFetch 인스턴스가 서로의 페이지를 잘못 잡지 않도록 자기 페이지를 식별한다.
- 작업이 끝나면 WebFetch가 finally에서 `page.CloseAsync()`로 페이지를 닫는다.
  마지막 탭이 닫히면 창도 함께 닫힌다.
- 예외: 호출측의 60초 타임아웃으로 프로세스가 강제 종료되면 숨김 창이 남을 수 있다.
- tab/window/hidden은 라이브 브라우저에 페이지를 만들므로 창, 탭, 포커스, 작업표시줄에
  변화가 생길 수 있다. 이러한 변화를 없애야 하면 headless를 사용한다.

## headless 모드 (완전 무창)

라이브 브라우저에는 어떤 페이지도 만들지 않고, 쿠키만 복사해 별도 브라우저로 수집한다.

1. `ConnectOverCDP`로 라이브 브라우저에 접속한다.
2. `context.CookiesAsync()`로 쿠키를 읽고 `Cookie` 목록으로 변환한다.
3. `Browser.getVersion`으로 라이브 브라우저의 `product`(예: `Edg/155.0.4283.62`)와
   `userAgent`를 읽는다.
4. `product`가 `Edg/`면 엣지, `Chrome/`이면 크롬 실행 파일을 선택한다.
   설정의 '크롤링 브라우저' 값이 아니라 라이브 브라우저의 실제 제품을 기준으로 한다.
5. 선택한 실행 파일을 `headless: true`로 실행하고, 라이브 브라우저와 같은
   User-Agent, `ko-KR`, `Asia/Seoul`로 새 컨텍스트를 만든 뒤 쿠키를 추가한다.
6. URL을 열고 HTML을 반환한 뒤 헤드리스 브라우저만 닫는다.

- 쿠키 이식은 전체 추가가 실패하면 쿠키 1개씩 재시도한다. 일부 쿠키가 Chromium
  정책에 맞지 않아도 나머지 쿠키는 유지된다.
- 실행 파일을 찾지 못하면 Playwright 번들 Chromium을 사용한다.
- **라이브 브라우저에 `CloseAsync()`를 호출하면 사용자의 브라우저가 종료된다.**
  WebFetch는 headless 모드에서 라이브 브라우저를 닫지 않으며, 프로세스 종료 시
  CDP 연결만 정리된다.
- 같은 실행 파일과 같은 User-Agent를 사용하는 이유는 `cf_clearance` 등
  브라우저 지문에 묶인 쿠키의 호환성을 높이기 위해서다.
- 검증 결과(엣지 155, 9223): 3~5초 내 200 응답, 로그인 벽 없음, FMKorea 검색 결과
  20건 파싱. 라이브 브라우저의 창 수, 포그라운드, CDP 대상 수는 변하지 않았다.

## CDP 포트 자동 감지 (CdpPortFinder)

- WMI(Win32_Process)로 `msedge.exe`/`chrome.exe` 프로세스의 CommandLine을 읽어
  `--remote-debugging-port=(\d+)` 값을 파싱한다.
- 같은 브라우저 인스턴스가 여러 개면 CreationDate가 가장 최근인 것을 사용한다.
- `--remote-debugging-port=0`(랜덤 포트)이면 `--user-data-dir` 폴더의
  `DevToolsActivePort` 파일 첫 줄에서 실제 포트를 읽는다.
- 감지 실패 시 0을 반환하고, 호출측이 수동 포트로 대체한다.
- Chromium은 자식 프로세스에 같은 옵션을 전파하므로 동일 포트가 여러 번 감지되어도 결과는 같다.

## 브라우저 사전 준비

엣지:

```
msedge.exe --user-data-dir="%LOCALAPPDATA%\Edge-CDP" --profile-directory=Default --remote-debugging-port=9222
```

크롬:

```
chrome.exe --user-data-dir="%LOCALAPPDATA%\Chrome-CDP" --profile-directory=Default --remote-debugging-port=9223
```

- Chromium 136 이후 버전은 기본 프로필에서 원격 디버깅을 차단하므로
  `--user-data-dir`로 전용 프로필을 지정해야 한다.
- 엣지와 크롬을 동시에 띄울 때는 포트를 다르게 지정한다(기본 엣지 9222 / 크롬 9223).
- FMKorea 크롤링은 로그인이 필요하다. 사용할 브라우저 프로필에서 FMKorea에 로그인해 둔다.
  프로그램은 계정 비밀번호를 저장하지 않으며, 로그인된 브라우저 세션(쿠키)을 그대로 사용한다.
- 포트 자동 감지가 켜져 있으면 설정의 포트 값과 무관하게 실행 중인 브라우저의 실제 포트로 접속한다.

## 로그인 벽 판정

- 응답 HTML에 로그인 폼(`form id="fo_member_login"` + `procMemberLogin`)이 있으면 로그인 벽으로 판정한다.
- 주의: `dispMemberLoginForm`/`procMemberLogin` 문자열은 정상 게시판 페이지의 헤더 로그인
  위젯에도 들어 있어 판별자로 쓰면 오탐이 발생한다. 반드시 로그인 폼 id 존재 여부로 판정한다.
- 로그인 벽이면 `[FM] 크롤링 브라우저(엣지 또는 크롬)에서 에펨코리아에 로그인되어 있지 않습니다.`
  오류로 해당 사이클이 실패 처리된다.

## 실패 시나리오

| 증상 | 원인 | 대처 |
|---|---|---|
| CDP 포트 접속 실패 | 브라우저 미실행 또는 포트 불일치 | 안내된 실행 명령으로 브라우저 실행 |
| WebFetch 종료코드 1 | CDP 접속 실패, 페이지 로드 30초 초과 등 | stderr 메시지 확인 |
| HTML 다운로드 실패 | WebFetch.exe 없음/빈 결과 | 빌드로 WebFetch.exe가 출력 폴더에 복사됐는지 확인 |
| 60초 타임아웃 | 페이지 로드 지연 | 프로세스 강제 종료 후 다음 주기에 재시도 |
| 로그인 벽 오류 | 사용 중인 브라우저 프로필에 FMKorea 로그인 없음 | 해당 브라우저에서 로그인 후 재시도 |
| 헤드리스 실행 실패 | 엣지/크롬 실행 파일을 찾지 못함, 실행 권한 없음 | stderr의 실행 파일 경로 확인. 실행 파일을 찾지 못하면 Playwright 번들 Chromium을 사용한다 |
| headless에서만 로그인 벽 | 라이브 브라우저와 다른 지문으로 `cf_clearance` 거부 | 엣지/크롬 설치 경로를 확인하고, 필요하면 tab/hidden 모드로 전환 |
